using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 首版四格道具栏能力：职业武器、工作工具、喷气背包、炸药；选择、装备和燃料只写 ActorState。
    /// 使用请求绑定当前选择版本，工具只操作附近合法目标，伤害与生产仍由原能力结算。
    /// </summary>
    public sealed partial class HeroInventoryBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
        public static string ItemKey(int slot) => slot == 0 ? "weapon" : slot == 1 ? "tool" : slot == 2 ? "jetpack" : slot == 3 ? "explosive" : "";

        internal bool Select(int slot)
        {
            if (slot < 0 || slot > 3) return false;
            ActorState state = actor.Edit();
            if (state.SelectedItem == slot) return true;
            actor.World.Work.Clear(actor);
            state.SelectedItem = slot;
            state.SelectionRevision = checked(state.SelectionRevision + 1);
            return true;
        }

        internal bool Use(string item, int revision, int targetId)
        {
            ActorState state = actor.Edit();
            if (revision != state.SelectionRevision || item != ItemKey(state.SelectedItem)) return false;
            if (item == "jetpack")
            {
                if (targetId != 0) return false;
                state.JetpackEquipped = !state.JetpackEquipped;
                return true;
            }
            IEntityBehaviour target = actor.World.Index.Find(targetId);
            if (target is BuildingBehaviour farm && farm.RuleKey == "farm" && farm.IsComplete)
                target = actor.World.Index.Find(farm.FarmSiteId);
            if (target == null) return false;
            if (item == "weapon")
            {
                if (!(target is ActorBehaviour enemy) || !enemy.Enemy || enemy.Hp <= 0 ||
                    state.HitPending || state.AttackClock > 0 || !actor.World.Combat.InRange(actor, enemy)) return false;
                actor.World.Work.Clear(actor);
                state.TargetId = target.Id; state.Activity = ActorActivity.Attack;
                state.Face = target.X < actor.X ? -1 : 1;
                state.AttackClock = actor.Definition.AttackSeconds;
                state.Windup = actor.Definition.Windup; state.HitPending = true; state.ActionTime = 0;
                return true;
            }
            if (item == "tool" && target is MineralDepositBehaviour deposit)
            {
                float targetHeight = actor.World.Terrain == null ? 0 :
                    PlayableTerrain.OriginY - (deposit.Y + .5f) * PlayableTerrain.CellPixels;
                if (actor.RuleKey != "worker" ||
                    Math.Abs(actor.X - deposit.X) > actor.World.Catalog.Balance.HeroControl.WorkReach ||
                    Math.Abs(state.Height - targetHeight) > actor.World.Catalog.Balance.HeroControl.WorkReach ||
                    !deposit.ExtractByHand()) return false;
                actor.World.Economy.AddResource(deposit.ResourceId, 1);
                actor.World.Work.Clear(actor);
                return true;
            }
            if (state.Height != 0 || actor.RuleKey != "worker" ||
                Math.Abs(actor.X - target.X) > actor.World.Catalog.Balance.HeroControl.WorkReach) return false;
            if (!actor.World.Work.Assign(actor, target, allowManual: true)) return false;
            state.Activity = target is WorksiteBehaviour ? ActorActivity.Work : ActorActivity.Build;
            state.Face = target.X < actor.X ? -1 : 1;
            return true;
        }
    }
}
