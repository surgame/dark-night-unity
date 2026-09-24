using System;
using AnyRules.Next;
using AnyRules.Next.Networking;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
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
        private ChunkReplicaStateMachine subscribedReplica;
        private WorldIdentity world;
        private ulong presentedCommit;
        private bool presenting;
        private AnyRules.Next.Authoring.ARDMapDefinition definition;
        private CaveTerrainStyle style;
        public static void Install(SessionNetwork network, RandomLevelTemplate template, PinewatchStage stage)
        {
            var entry = network.gameObject.AddComponent<RandomLevelEntry>();
            entry.network = network; entry.template = template; entry.stage = stage; stage.RandomTerrain = true;
            bool contour = Array.IndexOf(System.Environment.GetCommandLineArgs(), "--dn-contour-static") >= 0;
            entry.definition = contour ? template.ContourDefinition : template.Definition;
            entry.style = contour ? template.StaticBackgroundStyle : template.CaveStyle;
            stage.ActorPresentationScale = entry.style != null && entry.style.ProceduralRock ? 2 : 1;
            if (entry.definition == null || (contour && entry.style == null)) throw new InvalidOperationException("地图缺少指定风格的独立配置。");
            network.Terrain = new SessionTerrainNetwork(InstanceFinder.NetworkManager, network, entry.definition, template.Expedition, entry.style?.VisualIdentity ?? "");
        }
        private void Update()
        {
            try
            {
                network.Terrain.Pump();
                var replica = network.Terrain.Replica;
                if (!network.Terrain.DataReady) { Clear(); return; }
                if (!presenting || !world.Equals(replica.World))
                {
                    Clear(); world = replica.World; presentedCommit = replica.CommitId; presenting = true;
                    var root = new GameObject("Pinewatch random terrain");
                    root.transform.SetParent(transform, false);
                    root.transform.localPosition = new Vector3(0, PlayableTerrain.OriginY / 100, 0);
                    root.transform.localScale = Vector3.one * (PlayableTerrain.CellPixels / 100f);
                    view = root.AddComponent<TerrainPreview>(); view.ViewCamera = stage.SceneCamera; view.CaveStyle = style;
                    view.ShowReplica(definition, new TerrainReplicaSource(replica), replica.World, network.Terrain.Background);
                    subscribedReplica = replica;
                    subscribedReplica.Applied += OnReplicaApplied;
                }
                var frame = network.Client.Replica.Current;
                if (frame != null) { view.SetMinerals(frame.World.Worksites); view.SetDevices(frame.World); }
                if (view.LastError != null) throw view.LastError;
                network.Terrain.PresentationReady = view.Ready;
            }
            catch (Exception error) { network.Fail(error); }
        }
        private void OnReplicaApplied(MapReplicaChange transition)
        {
            if (!presenting || !world.Equals(transition.World) || transition.Commit <= presentedCommit) return;
            presentedCommit = transition.Commit;
            view?.NotifyReplicaChanged(transition);
        }
        private void Clear()
        {
            if (subscribedReplica != null) subscribedReplica.Applied -= OnReplicaApplied;
            subscribedReplica = null;
            if (view != null) Destroy(view.gameObject);
            view = null; presenting = false; network.Terrain.PresentationReady = false;
            presentedCommit = 0;
        }
        private void OnDestroy() { Clear(); network.Terrain?.Dispose(); }
    }
}
