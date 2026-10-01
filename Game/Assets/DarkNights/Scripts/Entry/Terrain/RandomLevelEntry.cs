using System;
using AnyRules.Next;
using AnyRules.Next.Networking;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using DarkNights.View.Expedition;
using DarkNights.View.Terrain;
using FishNet;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>随机灰松谷场景的装配及只读页面生命周期；网络、对象与渲染代次必须一致，退房和换世界销毁旧页面。</summary>
    public sealed class RandomLevelEntry : MonoBehaviour
    {
        public const string ExpeditionScenePath = "Assets/DarkNights/Res/Scenes/Expedition/Expedition.unity";
        public const string ScenePath = "Assets/DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity";
        private SessionNetwork network;
        private RandomLevelTemplate template;
        private PinewatchStage stage;
        private TerrainPreview view;
        private JourneyEnvironment environment;
        private ChunkReplicaStateMachine subscribedReplica;
        private WorldIdentity world;
        private ulong receivedSession, receivedStreamGeneration, receivedCommit;
        private bool receivedIdentityKnown;
        private bool presenting;
        private AnyRules.Next.Authoring.ARDMapDefinition definition;
        private CaveTerrainStyle style;
        public static void Install(SessionNetwork network, RandomLevelTemplate template, PinewatchStage stage)
        {
            var entry = network.gameObject.AddComponent<RandomLevelEntry>();
            entry.network = network; entry.template = template; entry.stage = stage; stage.RandomTerrain = true;
            entry.environment = network.gameObject.AddComponent<JourneyEnvironment>();
            entry.environment.Initialize(stage.SceneCamera);
            bool contour = Array.IndexOf(System.Environment.GetCommandLineArgs(), "--dn-contour-static") >= 0;
            var profile = TerrainProfileConfig.Resolve();
            entry.definition = contour ? profile.ContourDefinition : profile.Definition;
            entry.style = (contour ? profile.BackgroundStyle : profile.CaveStyle) as CaveTerrainStyle;
            stage.MiningSelector = entry.style?.MiningSelector ?? stage.MiningSelector;
            stage.ActorPresentationScale = entry.style != null && entry.style.ProceduralRock ? 2 : 1;
            if (entry.definition == null || (template.Expedition || contour) && entry.style == null)
                throw new InvalidOperationException("地图缺少指定风格的独立配置。");
            if (template.Expedition && entry.style.Background == null)
                throw new InvalidOperationException("正式远征缺少洞穴背景层配置。");
            network.Terrain = new SessionTerrainNetwork(InstanceFinder.NetworkManager, network, entry.definition, template.Expedition, entry.style?.VisualIdentity ?? "");
        }
        private void Update()
        {
            try
            {
                network.Terrain.Pump();
                var frame = network.Client.Replica.Current;
                environment.Present(frame);
                var replica = network.Terrain.Replica;
                if (!network.Terrain.DataReady || frame == null || network.Terrain.Epoch != frame.Epoch)
                { Clear(); return; }
                var journey = frame.World.Expedition?.Journey;
                string mapId = journey?.MapId ?? "";
                if (!JourneyPresentationRules.InSpace(journey) && mapId.Length != 0 &&
                    mapId.Replace("-", "") != replica.World.WorldId.ToString().Replace("-", ""))
                { Clear(); return; }
                if (JourneyPresentationRules.InSpace(frame?.World.Expedition?.Journey))
                {
                    if (presenting) Clear();
                    network.Terrain.PresentationReady = environment.SpaceReady;
                    return;
                }
                if (!presenting || !world.Equals(replica.World))
                {
                    Clear(); world = replica.World; receivedSession = replica.Session;
                    receivedStreamGeneration = replica.Generation; receivedCommit = replica.CommitId; receivedIdentityKnown = true; presenting = true;
                    var root = new GameObject("Pinewatch random terrain");
                    root.transform.SetParent(transform, false);
                    root.transform.localPosition = new Vector3(0, PlayableTerrain.OriginY / 100, 0);
                    root.transform.localScale = Vector3.one * (PlayableTerrain.CellPixels / 100f);
                    view = root.AddComponent<TerrainPreview>(); view.ViewCamera = stage.SceneCamera;
                    var source = new TerrainReplicaSource(replica);
                    if (style != null) view.ShowCaveReplica(definition, style, source, replica.World, network.Terrain.Background);
                    else view.ShowReplica(definition, source, replica.World, network.Terrain.Background);
                    subscribedReplica = replica;
                    subscribedReplica.Applied += OnReplicaApplied;
                }
                if (frame != null) { view.SetMinerals(frame.World.Worksites); view.SetDevices(frame.World); }
                if (view.LastError != null) throw view.LastError;
                network.Terrain.PresentationReady = view.Ready;
            }
            catch (Exception error) { network.Fail(error); }
        }
        private void OnReplicaApplied(MapReplicaChange transition)
        {
            if (!presenting || !world.Equals(transition.World)) return;
            bool lifecycle = transition.Kind == MapReplicaChangeKind.WorldReset ||
                transition.Kind == MapReplicaChangeKind.VisibilityRevoked || transition.Kind == MapReplicaChangeKind.Disconnected;
            bool streamChanged = receivedIdentityKnown && (receivedSession != transition.Session ||
                receivedStreamGeneration != transition.StreamGeneration);
            if (!lifecycle && !streamChanged && receivedIdentityKnown && transition.Commit <= receivedCommit) return;
            receivedSession = transition.Session; receivedStreamGeneration = transition.StreamGeneration;
            receivedIdentityKnown = true;
            receivedCommit = lifecycle ? 0 : transition.Commit;
            view?.NotifyReplicaChanged(transition);
        }
        private void Clear()
        {
            if (subscribedReplica != null) subscribedReplica.Applied -= OnReplicaApplied;
            subscribedReplica = null;
            if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            view = null; presenting = false; network.Terrain.PresentationReady = false;
            receivedSession = receivedStreamGeneration = receivedCommit = 0; receivedIdentityKnown = false;
        }
        private void OnDestroy() { Clear(); if (environment != null) Destroy(environment); network.Terrain?.Dispose(); }
    }
}
