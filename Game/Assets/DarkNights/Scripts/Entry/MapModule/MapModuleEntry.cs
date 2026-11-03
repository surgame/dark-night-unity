using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using DarkNights.View.MapModule;
using FishNet;
using GameCore.Objects.Definition;
using Runtime.AppStartup;
using UnityEngine;

namespace DarkNights.Entry.MapModule
{
    /// <summary>
    /// 新版模块研究Scene的唯一运行入口；从已检查的固定Bake启动既有FishNet Host及YYGC Stateful对象。
    /// 复用正式输入、角色运动、UI和双层地图Ready；不调用旧生成器、旧WhiteboxSession或独立调试台。
    /// </summary>
    public sealed class MapModuleEntry : MonoBehaviour
    {
        public const string SelectionKey = "DarkNights.MapModule.Case";
        public const string VersionKey = "DarkNights.MapModule.Bake";
        public const string StudyKey = "DarkNights.MapModule.Study";
        public const string PhysicsStudyScene = "Assets/DarkNights/Res/Scenes/Workbenches/Physics/CooperativePhysicsStudy.unity";
        public MapModuleStudyAuthoring Study;
        public string DefaultCaseId = "gentle";
        public PinewatchStage Stage;
        public ScenePlacement HeroPlacement;
        public Runtime.Physics.PhysicsProfile PhysicsProfile;
        public Runtime.Physics.HangingRopePlacement[] HangingRopes = Array.Empty<Runtime.Physics.HangingRopePlacement>();
        public Runtime.Physics.PhysicsPropPlacement[] PhysicsProps = Array.Empty<Runtime.Physics.PhysicsPropPlacement>();
        public bool StartAsHost = true;
        public string ServerAddress = "127.0.0.1";
        public ushort ServerPort = 27779;
        public SessionNetwork Network { get; private set; }
        public LevelLayout Layout { get; private set; }
        private bool startRequested;

        public async UniTask Initialize(AppStartupContext context, GameCatalog catalog, CancellationToken token)
        {
            string selected = DefaultCaseId, expected = "";
#if UNITY_EDITOR
            if (Study != null && UnityEditor.SessionState.GetString(StudyKey, "") == Study.StudyId)
            {
                selected = UnityEditor.SessionState.GetString(SelectionKey, selected);
                expected = UnityEditor.SessionState.GetString(VersionKey, "");
            }
#endif
            if (Study == null || Stage == null || HeroPlacement == null || HeroPlacement.Loader == null ||
                HeroPlacement.View == null || Network != null)
                throw new InvalidOperationException("模块研究缺少显式Rig绑定或已启动。");
            var sample = Study.RequiredCase(selected);
            var bake = sample.Bake ?? throw new InvalidOperationException("先从地图工坊准备当前样例的制作版本。");
            if (bake.StudyId != Study.StudyId || bake.CaseId != sample.CaseId ||
                bake.SourceHash != MapModuleSource.Hash(Study, sample) ||
                expected.Length != 0 && expected != bake.ContentHash)
                throw new InvalidOperationException("选中的制作版本与当前作者场景不一致，请重新检查并准备版本。");
            var definition = HeroPlacement.Loader.ResolveDefinition();
            if (ObjectSessionResources.Rule(definition) != "worker") throw new InvalidOperationException("研究主角需复用正式worker能力。");
            bool previewEnabled = sample.PreviewRenderer.enabled;
            foreach (var candidate in Study.Cases)
            {
                candidate.gameObject.SetActive(candidate == sample);
                candidate.PreviewRenderer.enabled = false;
                foreach (var module in candidate.Modules)
                    if (module.PreviewRenderer != null) module.PreviewRenderer.enabled = false;
            }
            HeroPlacement.gameObject.SetActive(false);
            float width = Core.Config.Terrain.TerrainGenerationSettings.Width * 16;
            Layout = new LevelLayout(width, 0, 0, width, bake.SpawnX, bake.SpawnX,
                Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                new[] { new PlacementDefinition("worker", bake.SpawnX, name: HeroPlacement.InitialName,
                    placementKey: HeroPlacement.PlacementKey) }, randomTerrain: true, expedition: true,
                mapModuleIdentity: bake.ContentHash, mapModuleSpawnHeight: bake.SpawnHeight,
                physics: FreezePhysics());
            foreach (var prop in PhysicsProps) prop.gameObject.SetActive(false);
            foreach (var rope in HangingRopes) rope.gameObject.SetActive(false);
            var definitions = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance);
            var resources = await ObjectSessionResources.Prepare(new[] { definitions.GetRequired("worker"), definitions.GetRequired("ship") }, token);
            try
            {
                token.ThrowIfCancellationRequested();
                var owner = context.GetOrCreateChild("Dark Nights Session").gameObject;
                Network = owner.AddComponent<SessionNetwork>();
                Network.Initialize(InstanceFinder.NetworkManager, catalog, Layout, resources,
                    new[] { new ObjectPlacement(HeroPlacement.PlacementKey, definition, bake.SpawnX, 0,
                        HeroPlacement.InitialName, HeroPlacement.Loader) }, Stage.RuntimeGroup("Unbound Entities"),
                    GameSessionStartupModule.DebugHeroSpeedMultiplier());
                Stage.RandomTerrain = true; Stage.ActorPresentationScale = 1; Stage.Initialize(Layout);
                var profile = TerrainProfileConfig.Resolve();
                Network.Terrain = new SessionTerrainNetwork(InstanceFinder.NetworkManager, Network, profile.Definition, true,
                    "mapmodule-whitebox-v1:" + bake.ContentHash);
                Network.Terrain.InstallPrepared(() => bake.Freeze(Guid.NewGuid().ToString("N")));
                owner.AddComponent<MapModuleTerrainView>().Initialize(Network, sample, previewEnabled);
                await SessionPresentationSetup.Initialize(Network, catalog, Stage, Layout, gameObject.scene, physicsProfile: PhysicsProfile);
                token.ThrowIfCancellationRequested();
            }
            catch
            {
                if (Network != null) { Destroy(Network.gameObject); Network = null; }
                else resources.Dispose();
                throw;
            }
        }

        public Core.Physics.PhysicsWorldDefinition FreezePhysics()
        {
            if (PhysicsProfile == null)
            {
                if (PhysicsProps.Length != 0 || HangingRopes.Length != 0) throw new InvalidOperationException("箱子场景缺少物理Profile。");
                return null;
            }
            var ropes = HangingRopes.Select(p => p == null
                ? throw new InvalidOperationException("环境吊绳场景引用缺失。") : p.Freeze()).ToArray();
            return PhysicsProfile.Freeze(PhysicsProps.Select(p => p == null
                ? throw new InvalidOperationException("物理道具场景引用缺失。") : p.Freeze(PhysicsProfile))
                .Concat(ropes.SelectMany(r => r.InitialBodies())).ToArray(), ropes);
        }

        /// <summary>AppStartup只负责装配；完成注册并达到Ready后才提交一次研究Host连接，退出取消未完成请求。</summary>
        public async UniTask StartWhenReady(CancellationToken startupToken)
        {
            if (startRequested) return;
            startRequested = true;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(startupToken, this.GetCancellationTokenOnDestroy());
            try
            {
                await AppStartup.WaitUntilReady(lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                if (Network == null || !Network.CanStartSession)
                    throw new InvalidOperationException("研究会话装配未完成或已有连接，不能重复启动Host。");
                string[] args = System.Environment.GetCommandLineArgs();
                string Read(string key, string fallback)
                {
                    int i = Array.IndexOf(args, key);
                    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
                }
                bool host = Read("--dn-role", StartAsHost ? "host" : "client") == "host";
                string address = Read("--dn-address", ServerAddress);
                if (!ushort.TryParse(Read("--dn-port", ServerPort.ToString()), out ushort port) || port == 0)
                    throw new InvalidOperationException("研究端口必须在1至65535之间。");
                await Network.Connect(host, address, port);
                lifetime.Token.ThrowIfCancellationRequested();
                // Connect通过Failed统一上报并回收失败；不能将返回误报为Host成功或客户端Ready。
                if (Network != null && Network.Hosting && Network.ActiveSession?.Server != null)
                    Debug.Log("MAPMODULE_HOST_START study=" + Study.StudyId + " bake=" + Layout.MapModuleIdentity);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (Network != null) Network.Fail(error);
                else Debug.LogException(error);
            }
        }
    }
}
