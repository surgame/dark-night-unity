using System;
using AnyRules.Next;
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
        public const string ScenePath = "Assets/DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity";
        private SessionNetwork network;
        private RandomLevelTemplate template;
        private PinewatchStage stage;
        private TerrainPreview view;
        private WorldIdentity world;
        private ulong presentedCommit;
        private bool presenting;
        public static void Install(SessionNetwork network, RandomLevelTemplate template, PinewatchStage stage)
        {
            var entry = network.gameObject.AddComponent<RandomLevelEntry>();
            entry.network = network; entry.template = template; entry.stage = stage; stage.RandomTerrain = true;
            network.Terrain = new SessionTerrainNetwork(InstanceFinder.NetworkManager, network, template.Definition);
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
                    view = root.AddComponent<TerrainPreview>(); view.ViewCamera = stage.SceneCamera;
                    view.ShowReplica(template.Definition, new TerrainReplicaSource(replica), replica.World);
                }
                else if (replica.CommitId != presentedCommit)
                {
                    presentedCommit = replica.CommitId;
                    view?.NotifyReplicaChanged();
                }
                if (view.LastError != null) throw view.LastError;
                network.Terrain.PresentationReady = view.Ready;
            }
            catch (Exception error) { network.Fail(error); }
        }
        private void Clear()
        {
            if (view != null) Destroy(view.gameObject);
            view = null; presenting = false; network.Terrain.PresentationReady = false;
            presentedCommit = 0;
        }
        private void OnDestroy() { Clear(); network.Terrain?.Dispose(); }
    }
}
