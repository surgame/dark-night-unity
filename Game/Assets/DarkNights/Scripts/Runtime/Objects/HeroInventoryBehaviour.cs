using System;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using DarkNights.Core.Config;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 四格装备的稳定 Definition 身份与当前工具装配入口；背包状态归 ActorState，工具缓存仅持有 YYGC 对象引用。
    /// 手枪、矿镐、炸弹使用同一主角输入流；喷气背包作为独立的已购买能力。
    /// </summary>
    public sealed partial class HeroInventoryBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;
        private ObjectInstance toolObject;
        private string toolGuid = "";
        private ObjectSession toolSession;
        public static string Slot(ActorState state, int slot) => slot switch
        {
            0 => state.Slot0, 1 => state.Slot1, 2 => state.Slot2, 3 => state.Slot3, _ => ""
        };

        internal static bool Give(ActorState state, string item)
        {
            if (string.IsNullOrEmpty(item) || !Guid.TryParseExact(item, "N", out var guid) || guid == Guid.Empty ||
                Slot(state, 0) == item || Slot(state, 1) == item || Slot(state, 2) == item || Slot(state, 3) == item) return false;
            if (state.Slot0 == "") state.Slot0 = item;
            else if (state.Slot1 == "") state.Slot1 = item;
            else if (state.Slot2 == "") state.Slot2 = item;
            else if (state.Slot3 == "") state.Slot3 = item;
            else return false;
            state.InventoryRevision = checked(state.InventoryRevision + 1);
            return true;
        }

        internal MiningToolBehaviour MiningTool()
        {
            var state = actor.Read();
            string guid = Slot(state, state.SelectedItem);
            if (actor.World.Resources.Equipment.Mining(guid) == null) { ReleaseTool(); return null; }
            if (toolObject != null && toolGuid == guid) return toolObject.GetBehaviour<MiningToolBehaviour>();
            var definition = actor.World.Resources.Equipment.Resolve(guid);
            var next = actor.World.Resources.Create(definition, actor.Object.SessionContext, actor.Object.transform).Owner;
            try
            {
                var capability = next.GetBehaviour<MiningToolBehaviour>() ?? throw new InvalidOperationException("工具缺少采集能力。");
                next.Activate(); next.gameObject.SetActive(false);
                ReleaseTool(); toolObject = next; toolGuid = guid; toolSession = actor.World;
                return capability;
            }
            catch { actor.World.ReleaseEntity(next); throw; }
        }

        private void ReleaseTool()
        {
            if (toolObject != null) toolSession.ReleaseEntity(toolObject);
            toolObject = null; toolGuid = ""; toolSession = null;
        }

        public override void OnDespawn()
        {
            ReleaseTool();
            base.OnDespawn();
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

        /// <summary>旧的离散矿床命令不再执行；矿镐统一由带目标的主角输入和服务端装备步骤推进。</summary>
        internal bool Use(string item, int revision, int targetId)
        {
            return false;
        }
    }
}
