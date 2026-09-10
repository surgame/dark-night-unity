using GameCore.Objects.Singletons;

namespace YYGC.IdentityValidation
{
    /// <summary>真实启动模块的受控单例；用于核对禁用记录和启用记录只启动一次。</summary>
    public sealed partial class IdentityProbeSingleton : SingletonBehaviour<IdentityProbeSingleton>
    {
        public static int Initializations;
        protected override void OnSingletonInitialize() { Initializations++; }
    }
}
