using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>角色照明的权威能力，仅在已验证的会话事务中修改所属 ActorState，不读取设备或保存副本。</summary>
    public sealed partial class HeroLightBehaviour : PooledBehaviour
    {
        [Inject] private ActorBehaviour actor;

        internal void EnsureEquipped()
        {
            var state = actor.Edit();
            if (state.LightDefinition != "") return;
            string starter = actor.World.Resources.Equipment.StarterLight;
            if (starter == "") return;
            state.LightDefinition = starter;
            state.LightEnabled = true;
            state.LightAimAngle = state.Face < 0 ? 180 : 0;
        }

        internal bool SetEnabled(bool enabled)
        {
            var state = actor.Read();
            if (actor.World.Resources.Equipment.Light(state.LightDefinition) == null) return false;
            if (state.LightEnabled != enabled) actor.Edit().LightEnabled = enabled;
            return true;
        }
    }
}
