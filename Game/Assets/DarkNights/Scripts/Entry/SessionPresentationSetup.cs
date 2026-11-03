using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Network;
using DarkNights.View;
using ED.SC.Components;
using GameCore.Interactions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Entry
{
    /// <summary>
    /// Bootstrap正式关卡与MapModule共用的表现装配入口；服务随传入的会话宿主释放。
    /// 只连接实体副本、输入、UI、效果及控制台，不选择地图、不建立权威状态或自动连接网络。
    /// </summary>
    internal static class SessionPresentationSetup
    {
        internal static async UniTask<SessionUiController> Initialize(SessionNetwork network, GameCatalog catalog,
            PinewatchStage stage, LevelLayout layout, Scene scene,
            HeroLightPresentation lighting = null, Terrain.RandomLevelEntry terrainEntry = null,
            Runtime.Physics.PhysicsProfile physicsProfile = null)
        {
            var entities = network.gameObject.AddComponent<SessionEntityViews>();
            entities.Initialize(network.Client, catalog, stage, network);
            if (physicsProfile != null) network.gameObject.AddComponent<PhysicsSessionViews>()
                .Initialize(network, physicsProfile, layout.Physics, stage, entities);
            var ui = network.gameObject.AddComponent<SessionUiController>();
            Runtime.Diagnostics.BootstrapStartupTrace.Mark("UiStarted");
            await ui.Initialize(network, catalog, stage, entities, layout.Expedition, layout.Physics);
            Runtime.Diagnostics.BootstrapStartupTrace.Mark("UiReady");
            if (terrainEntry != null) lighting.Initialize(network, entities, ui.Actions, terrainEntry);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugObjectHub.Install(network);
            if (!layout.MapModuleStudy) QuickTestHub.Install(network, ui, layout.Expedition);
#endif
            ConsoleSystem console = Object.FindAnyObjectByType<ConsoleSystem>();
            if (console != null)
            {
                console.gameObject.AddComponent<SmartConsoleLogBridge>().Initialize(console);
                YYInteractionSessionHandle consoleModal = null;
                console.OnActivate += () => consoleModal = YYInteractionSessionService.Instance.Begin(new YYInteractionSessionDescriptor
                {
                    Kind = "dark_nights.console", Owner = "SmartConsole", Priority = 200,
                    Blocks = YYInteractionBlockFlags.All, ConflictPolicy = YYInteractionConflictPolicy.SuspendLowerPriority
                });
                console.OnDeactivate += () => { consoleModal?.Dispose(); consoleModal = null; };
            }
            network.gameObject.AddComponent<SessionPlacementView>().Initialize(network.Client, ui.Input, stage, catalog, layout);
            await network.gameObject.AddComponent<SessionEffects>().Initialize(network.Client, ui, stage, layout.GroundY);
            var panel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExpeditionPanel>(true)).SingleOrDefault();
            if (panel != null) network.gameObject.AddComponent<ExpeditionHud>().Initialize(network, panel);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            return ui;
        }
    }
}
