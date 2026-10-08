using System.Linq;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>角色照明的权威能力，仅在已验证的会话事务中修改所属 ActorState，不读取设备或保存副本。</summary>
    public sealed partial class HeroLightBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;

        internal void GiveStarter()
        {
            string starter = actor.World.Resources.Equipment.StarterLight;
            if (starter == "") return;
            HeroInventoryTransactions.Give(actor.World, actor, actor.World.Resources.Equipment.Resolve(starter));
        }

        internal bool SetEnabled(bool enabled)
        {
            var state = actor.Read();
            if (!actor.World.Resources.Equipment.HasLight(state.LightDefinition) ||
                !System.Linq.Enumerable.Range(0, 4).Any(slot => HeroInventoryBehaviour.Slot(state, slot) == state.LightDefinition)) return false;
            if (state.LightEnabled != enabled) actor.Edit().LightEnabled = enabled;
            return true;
        }
    }
}
