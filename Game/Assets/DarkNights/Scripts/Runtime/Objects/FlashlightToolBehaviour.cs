using DarkNights.Core.Config;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>原生手电对象的静态能力；每次装配重新冻结配置，装备与开关唯一归角色 ActorState。</summary>
    [RequireConfig(typeof(FlashlightToolConfig))]
    public sealed partial class FlashlightToolBehaviour : PooledBehaviour
    {
        [Inject] private FlashlightToolConfig config;
        public FlashlightRules Rules { get; private set; }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Rules = config.Freeze();
        }

        public override void OnDespawn()
        {
            Rules = null;
            base.OnDespawn();
        }
    }
}
