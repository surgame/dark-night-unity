using System;
using DarkNights.Core.Physics;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 吊绳物体的YYGC装配能力，注入并冻结定义参数，绑定所属会话的固定拓扑。
    /// 对象激活和退休沿框架生命周期，绳节姿态与抓附仍唯一归PhysicsSessionBehaviour。
    /// </summary>
    [RequireConfig(typeof(HangingRopeConfig))]
    public sealed partial class HangingRopeBehaviour : PooledBehaviour
    {
        [Inject] private HangingRopeConfig config;
        private HangingRopeConfig frozen;
        public HangingRopeDefinition Installation { get; private set; }
        public HangingRopeConfig Configuration => frozen?.Copy() ??
            throw new InvalidOperationException("吊绳配置尚未装配。");

        protected override void OnSpawn()
        {
            base.OnSpawn();
            frozen = config?.Copy() ?? throw new InvalidOperationException("吊绳缺少共享配置。");
            Installation = null;
        }

        public void Bind(HangingRopeDefinition installation)
        {
            if (installation == null || Installation != null ||
                _context.Owner.Definition.Guid.ToString() != installation.DefinitionGuid ||
                HangingRopeConfig.DefinitionHash(_context.Owner.Definition) != installation.DefinitionHash)
                throw new InvalidOperationException("吊绳实例的定义、配置或固定拓扑不匹配。");
            Installation = installation;
        }

        public override void OnDespawn()
        {
            Installation = null;
            frozen = null;
            base.OnDespawn();
        }
    }
}
