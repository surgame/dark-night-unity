using System;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>
    /// 实际 Player 内的无状态装配探针，用生成注入检查必需配置。
    /// 只由显式验收入口创建，不拥有经济、实体状态或更新循环。
    /// </summary>
    [RequireConfig(typeof(AssemblyProbeConfig))]
    public sealed partial class AssemblyProbeBehaviour : PooledBehaviour
    {
        [Inject] private AssemblyProbeConfig config;
        public int Observed { get; private set; }

        protected override void OnSpawn()
        {
            if (config == null) throw new InvalidOperationException("Probe config was not injected.");
            Observed = config.Value;
        }
    }
}
