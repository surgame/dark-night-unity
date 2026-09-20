using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
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
        public const string HeroSpeedPreference = "DarkNights.Debug.HeroSpeed8x";
        public const float FastHeroMultiplier = 8;
        public string Name => "Dark Nights 会话装配";
        public string Description => "加载灰松谷与正式会话入口。";
        public string Category => "游戏内容";
        public int Order => 9100;
        public bool Required => true;
        public IReadOnlyList<Type> Dependencies => new[] { typeof(GameContentStartupModule), typeof(UGUIRuntimeStartupModule), typeof(ObjectDefinitionLoaderStartupModule) };

        public async UniTask InitializeAsync(AppStartupContext context, CancellationToken cancellationToken)
        {
            const string defaultScene = "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity";
            string scenePath = Array.IndexOf(System.Environment.GetCommandLineArgs(), "--dn-camp-mode") >= 0 ? defaultScene : Terrain.RandomLevelEntry.ExpeditionScenePath;
#if UNITY_EDITOR
            scenePath = UnityEditor.SessionState.GetString("DarkNights.PlayScene", scenePath);
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
            LevelLayout layout = authoring.CreateLayout(catalog, DefinitionRuleIndex.RuleKey);
            var randomLevel = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DarkNights.View.Terrain.RandomLevelTemplate>(true)).SingleOrDefault();
            if (randomLevel != null && !randomLevel.Expedition) layout = DarkNights.Core.Logic.Terrain.PlayableTerrainGenerator.Layout(layout);
            var network = context.GetOrCreateChild("Dark Nights Session").gameObject.AddComponent<SessionNetwork>();
            context.Register(network);
            context.Register(layout);
            PinewatchStage stage = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PinewatchStage>(true)).Single();
            var entries = layout.Buildings.Concat(layout.Worksites).Concat(layout.Actors).ToArray();
            var placementsByKey = authoring.GetComponentsInChildren<ScenePlacement>(true).ToDictionary(p => p.PlacementKey);
            var definitions = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance);
            ObjectPlacement[] placements = entries.Select(p => new ObjectPlacement(p.PlacementKey, definitions.GetRequired(p.Kind),
                p.X, p.Variant, p.Name, placementsByKey[p.PlacementKey].Loader)).ToArray();
            foreach (ScenePlacement placement in placementsByKey.Values) placement.gameObject.SetActive(false);
            var required = catalog.Balance.Buildings.Keys.Concat(catalog.Balance.Worksites.Keys).Concat(catalog.Balance.Units.Keys);
            if (definitions.FindOptional(MineralDepositRuleConfig.Rule) != null)
                required = required.Concat(new[] { MineralDepositRuleConfig.Rule });
            ObjectSessionResources resources = await ObjectSessionResources.Prepare(required.Select(definitions.GetRequired).ToArray(), cancellationToken);
            float debugHeroSpeed = DebugHeroSpeedMultiplier();
            network.Initialize(InstanceFinder.NetworkManager, catalog, layout, resources, placements, stage.Entities, debugHeroSpeed);
            if (debugHeroSpeed > 1) Debug.Log("DARK_NIGHTS_DEBUG_HERO_SPEED multiplier=" + debugHeroSpeed);
            stage.Initialize(layout);
            if (randomLevel != null) Terrain.RandomLevelEntry.Install(network, randomLevel, stage);
            var entities = network.gameObject.AddComponent<SessionEntityViews>();
            entities.Initialize(network.Client, catalog, stage, network);
            var ui = network.gameObject.AddComponent<SessionUiController>();
            await ui.Initialize(network, catalog, stage, entities, layout.Expedition);
            network.gameObject.AddComponent<SessionPlacementView>().Initialize(network.Client, ui.Input, stage, catalog, layout);
            await network.gameObject.AddComponent<SessionEffects>().Initialize(network.Client, ui, stage, layout.GroundY);
            var expeditionPanel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExpeditionPanel>(true)).SingleOrDefault();
            if (expeditionPanel != null) network.gameObject.AddComponent<ExpeditionHud>().Initialize(network, expeditionPanel);
            SessionAutomation.Install(network);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Debug.Log("DARK_NIGHTS_SESSION_AVAILABLE protocol=" + DarkNights.Runtime.Session.SessionAuthority.ProtocolVersion + " level=" + catalog.Level.Id);
        }

        public static float DebugHeroSpeedMultiplier()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float result = 1;
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(HeroSpeedPreference, false)) result = FastHeroMultiplier;
#endif
            string[] arguments = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "--dn-debug-hero-speed");
            if (index >= 0 && (index + 1 >= arguments.Length || !float.TryParse(arguments[index + 1],
                NumberStyles.Float, CultureInfo.InvariantCulture, out result) || result < 1 || result > 16))
                throw new ArgumentException("--dn-debug-hero-speed 必须指定 1–16 的倍率。");
            return result;
#else
            return 1;
#endif
        }
    }
}
