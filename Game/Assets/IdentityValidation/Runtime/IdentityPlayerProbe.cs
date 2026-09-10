using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Managing;
using GameCore.NetworkCommands;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using GameCore.Objects.Singletons;
using GameCore.UI.UGUI;
using MemoryPack;
using Runtime.AppStartup;
using UnityEngine;

namespace YYGC.IdentityValidation
{
    /// <summary>独立验证 Player 的入口；先运行真实启动模块，再选择本地或网络验收，异常以非零退出码报告。</summary>
    public sealed class IdentityPlayerProbe : MonoBehaviour
    {
        public ObjectDefinitionDatabase Database;
        public ObjectSingletonDatabase Singletons;
        public ObjectDefinition LocalA, LocalB, Ui, Network;
        public NetworkManager Manager;
        public StateSynchronizer SceneObject;
        public IdentityProbeReport Report { get; private set; }

        private async void Start()
        {
            string scenario = IdentityProbeReport.Argument("-identityCase", "local");
            string output = IdentityProbeReport.Argument("-identityResult", "identity-player-result.json");
            Report = new IdentityProbeReport { scenario = scenario, unityVersion = Application.unityVersion, developmentBuild = Debug.isDebugBuild };
#if YYGC_IDENTITY_BASELINE
            Report.wireVersion = 1;
#else
            Report.wireVersion = DefinitionNetworkProfile.WireVersion;
#endif
            try
            {
                Application.runInBackground = true;
                Application.targetFrameRate = 60;
                Report.Check(!Debug.isDebugBuild && !Application.isEditor, "正式非 Development Player");
                Database = Instantiate(Database);
                Singletons = Instantiate(Singletons);
                DIContainer.InitializeRoot();
                GenericTypeRegistry<IStateData>.Reset();
                GenericTypeRegistry<INetworkCommand>.Reset();
                GenericTypeRegistry<IStateData>.Register(typeof(IdentityProbeState), scenario == "mismatch-types" ? 8 : 7);
                GenericTypeRegistry<IStateData>.MarkInitialized();
                GenericTypeRegistry<INetworkCommand>.MarkInitialized();
                IdentityProbeFormatterInitializer.RegisterFormatter();
#if YYGC_IDENTITY_BASELINE
                // 冻结旧包的 Editor 作用域生成器不参与 Player；旧对照宿主显式注册自己的两个测试行为。
                GameCore.Objects.Behaviours.BehaviourTypeResolver.Factories[typeof(IdentityStateBehaviour)] = () => new IdentityStateBehaviour();
                GameCore.Objects.Behaviours.BehaviourTypeResolver.Factories[typeof(IdentityProbeSingleton)] = () => new IdentityProbeSingleton();
#endif
                var context = new AppStartupContext(null, null, transform);
                var startup = new ObjectV2RuntimeStartupModule();
                startup.InitializeOnAwake(context);
                await startup.InitializeAsync(context, CancellationToken.None);
                Report.Check(context.RootSingletonHosts.Count == 1 && IdentityProbeSingleton.Initializations == 1,
                    "旧记录顺序 / 启用状态保留，根单例只启动一次");
                ObjectDefinitionLoaderGate.Open();
                if (scenario == "local") await RunLocal();
                else await IdentityNetworkProbe.Run(this, scenario);
                Report.result = "passed";
                Report.Save(output);
                Debug.Log("IDENTITY_PLAYER_PASSED " + scenario);
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Report.result = "failed";
                Report.error = error.ToString();
                Report.Save(output);
                Debug.LogException(error);
                Application.Quit(1);
            }
        }

        private async UniTask RunLocal()
        {
#if YYGC_IDENTITY_BASELINE
            throw new InvalidOperationException("旧基线只执行 LegacyV1 网络对照。");
#else
            Report.Check(LocalA.Id == 0 && LocalB.Id == 0 && Ui.Id == 0, "GUID-only 内容保持旧编号为零");
            var first = await ObjectInstanceFactory.CreateByKeyAsync(LocalA.Key, Vector3.zero, Quaternion.identity);
            var second = await ObjectInstanceFactory.CreateByGuidAsync(LocalB.Guid, Vector3.one, Quaternion.identity);
            Report.Check(first != null && second != null, "正式 Key / GUID 工厂从打包 Addressables 创建对象");
            var instance = first.GetComponent<ObjectInstance>();
            Report.Check(instance.GetBehaviour<IdentityLocalBehaviour>().Marker == 11, "第一个定义注入配置 11");
            instance.Initialize("reuse", LocalB);
            Report.Check(instance.GetBehaviour<IdentityLocalBehaviour>().Marker == 22, "同 Prefab 实例切换到定义 B 后注入配置 22");
            Report.Check(second.GetComponent<ObjectInstance>().Definition == LocalB, "GUID 创建解析到同一目录资产");
            var ui = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(UGUIManager)).GetComponent<UGUIManager>();
            var panelA = await ui.CreatePanelByKeyAsync(Ui.Key);
            var panelB = await ui.CreatePanelByGuidAsync(Ui.Guid);
            Report.Check(panelA != null && panelB != null && panelA != panelB && panelA.transform.parent == ui.transform,
                "正式 Key / GUID 创建两个独立 UGUI 面板并绑定父级");
            Report.Check(panelA.GetComponent<ObjectInstance>().Definition == Ui, "UGUI 通过 GUID-only 定义完成初始化");
            Report.Check(DefinitionGuid.FromNetworkBytes(LocalA.Guid.ToNetworkBytes()) == LocalA.Guid, "Player 中 16 字节 GUID 编解码一致");
            Report.Check(DefinitionLookup.ResolveUnique(Database, "guid:" + LocalA.GuidString) == LocalA, "正式 Player 支持唯一 GUID 搜索");
#endif
        }
    }
}
