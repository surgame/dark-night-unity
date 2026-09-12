using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using FishNet;
using Runtime.AppStartup;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace DarkNights.Entry
{
    /// <summary>
    /// 配置就绪后加载正式可编辑关卡，导出唯一布局并把网络会话服务登记到既有 AppStartup 上下文。
    /// 不自动开局或生成第二份布局；菜单／自动验收入口后续调用同一会话连接方法。
    /// </summary>
    [Serializable, Preserve, AppStartupModule]
    public sealed class GameSessionStartupModule : IAppStartupModule
    {
        public string Name => "Dark Nights 会话装配";
        public string Description => "加载灰松谷与正式会话入口。";
        public string Category => "游戏内容";
        public int Order => 9100;
        public bool Required => true;
        public IReadOnlyList<Type> Dependencies => new[] { typeof(GameContentStartupModule), typeof(UGUIRuntimeStartupModule), typeof(ObjectDefinitionLoaderStartupModule) };

        public async UniTask InitializeAsync(AppStartupContext context, CancellationToken cancellationToken)
        {
            const string defaultScene = "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity";
            string scenePath = defaultScene;
#if UNITY_EDITOR
            scenePath = UnityEditor.SessionState.GetString("DarkNights.PlayScene", defaultScene);
#endif
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
            {
#if UNITY_EDITOR
                await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive)).ToUniTask(cancellationToken: cancellationToken);
#else
                await SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive).ToUniTask(cancellationToken: cancellationToken);
#endif
                scene = SceneManager.GetSceneByPath(scenePath);
            }
            GameCatalog catalog = context.Resolve<GameCatalog>();
            var authoring = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelLayoutAuthoring>(true)).Single();
            LevelLayout layout = authoring.CreateLayout(catalog, DefinitionRuleIndex.Kind);
            var network = context.GetOrCreateChild("Dark Nights Session").gameObject.AddComponent<SessionNetwork>();
            context.Register(network);
            context.Register(layout);
            network.Initialize(InstanceFinder.NetworkManager, catalog, layout);
            PinewatchStage stage = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PinewatchStage>(true)).Single();
            stage.Initialize(layout);
            var entities = network.gameObject.AddComponent<SessionEntityViews>();
            entities.Initialize(network.Client, catalog, stage, authoring);
            var ui = network.gameObject.AddComponent<SessionUiController>();
            await ui.Initialize(network, catalog, stage, entities);
            network.gameObject.AddComponent<SessionPlacementView>().Initialize(network.Client, entities, ui.Input, stage, catalog, layout);
            await network.gameObject.AddComponent<SessionEffects>().Initialize(network.Client, entities, ui, stage, layout.GroundY);
            SessionAutomation.Install(network);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Debug.Log("DARK_NIGHTS_SESSION_AVAILABLE protocol=" + DarkNights.Runtime.Session.SessionAuthority.ProtocolVersion + " level=" + catalog.Level.Id);
        }
    }
}
