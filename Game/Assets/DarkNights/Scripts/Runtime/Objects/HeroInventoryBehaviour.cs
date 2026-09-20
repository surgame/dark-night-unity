using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 四格主角装备选择和背包装备入口；所有实例状态归 ActorState，切换会取消尚未投出的炸弹。
    /// 手枪、矿镐、炸弹使用同一主角输入流，离散 Use 请求只负责喷气背包开关。
    /// </summary>
    public sealed partial class HeroInventoryBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
        public static string ItemKey(int slot) => slot == 0 ? "pistol" : slot == 1 ? "pickaxe" : slot == 2 ? "bomb" : slot == 3 ? "jetpack" : "";

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
            if (revision != state.SelectionRevision || item != ItemKey(state.SelectedItem) || item != "jetpack" || targetId != 0) return false;
            state.JetpackEquipped = !state.JetpackEquipped;
            return true;
        }
    }
}
