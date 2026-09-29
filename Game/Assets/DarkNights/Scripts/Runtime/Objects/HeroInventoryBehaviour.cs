using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;
using DarkNights.Core.Config;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 四格主角装备选择和背包装备入口；所有实例状态归 ActorState，切换会取消尚未投出的炸弹。
    /// 手枪、矿镐、炸弹使用同一主角输入流；喷气背包作为独立的已购买能力。
    /// </summary>
    public sealed partial class HeroInventoryBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
        public static HeroEquipmentKind Slot(ActorState state, int slot) => (HeroEquipmentKind)(slot switch
        {
            0 => state.Slot0, 1 => state.Slot1, 2 => state.Slot2, 3 => state.Slot3, _ => 0
        });

        public static string ItemKey(HeroEquipmentKind item) => item switch
        {
            HeroEquipmentKind.Pistol => "pistol", HeroEquipmentKind.Pickaxe => "pickaxe",
            HeroEquipmentKind.Bomb => "bomb", _ => ""
        };

        internal static bool Give(ActorState state, HeroEquipmentKind item)
        {
            if (item <= HeroEquipmentKind.Empty || item > HeroEquipmentKind.Bomb ||
                Slot(state, 0) == item || Slot(state, 1) == item || Slot(state, 2) == item || Slot(state, 3) == item) return false;
            if (state.Slot0 == 0) state.Slot0 = (int)item;
            else if (state.Slot1 == 0) state.Slot1 = (int)item;
            else if (state.Slot2 == 0) state.Slot2 = (int)item;
            else if (state.Slot3 == 0) state.Slot3 = (int)item;
            else return false;
            state.InventoryRevision = checked(state.InventoryRevision + 1);
            return true;
        }

        internal bool Select(int slot)
        {
            if (slot < 0 || slot > 3) return false;
            ActorState state = actor.Edit();
            if (state.SelectedItem == slot) return true;
            actor.World.Work.Clear(actor);
            HeroEquipment.Cancel(state);
            state.EquipmentAction = 0;
            state.SelectedItem = slot;
            state.SelectionRevision = checked(state.SelectionRevision + 1);
            return true;
        }

        internal bool Use(string item, int revision, int targetId)
        {
            ActorState state = actor.Edit();
            if (revision != state.SelectionRevision || item != ItemKey(Slot(state, state.SelectedItem)) || item != "pickaxe") return false;
            return HeroMining.TryDeposit(actor, targetId);
        }
    }
}
