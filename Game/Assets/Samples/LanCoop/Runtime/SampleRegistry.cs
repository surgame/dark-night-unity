using GameCore.NetworkCommands;
using GameCore.Objects.Behaviours;
using GameCore.Objects.NetworkStates;

namespace DarkNights.Samples.LanCoop.Runtime
{
    /// <summary>样板显式注册入口；只在独立场景启动时调用，不在导入时改全局表，不重置宿主现有注册。</summary>
    public static class SampleRegistry
    {
        public static void Register()
        {
            BehaviourTypeResolver.Factories[typeof(CampBehaviour)] = () => new CampBehaviour();
            BehaviourTypeResolver.TypeStringMap[typeof(CampBehaviour).FullName] = typeof(CampBehaviour);
            GenericTypeRegistry<INetworkCommand>.Register<CampCommand>(29101);
            GenericTypeRegistry<IStateData>.Register<CampState>(29102);
            GenericTypeRegistry<INetworkCommand>.MarkInitialized();
            GenericTypeRegistry<IStateData>.MarkInitialized();
        }
    }
}
