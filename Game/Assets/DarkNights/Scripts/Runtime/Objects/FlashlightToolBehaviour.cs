using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>原生照明道具的装配能力标记；持有与开关唯一归角色 ActorState，光效由可复用 View 组件拥有。</summary>
    [RequireConfig(typeof(FlashlightToolConfig))]
    public sealed partial class FlashlightToolBehaviour : PooledBehaviour
    {
        [Inject] private FlashlightToolConfig config;
        public bool Ready { get; private set; }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Ready = config != null;
        }

        public override void OnDespawn()
        {
            Ready = false;
            base.OnDespawn();
        }
    }
}
