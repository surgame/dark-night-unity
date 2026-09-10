using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace YYGC.IdentityValidation
{
    /// <summary>通过实际生成注入路径读取配置，验证 Key 工厂和定义切换后的 DI 归属。</summary>
    [RequireConfig(typeof(IdentityProbeConfig))]
    public sealed partial class IdentityLocalBehaviour : PooledBehaviour
    {
        [Inject] private IdentityProbeConfig config;
        public int Marker => config.Marker;
    }
}
