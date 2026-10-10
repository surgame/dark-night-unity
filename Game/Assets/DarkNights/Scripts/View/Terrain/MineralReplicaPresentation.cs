using System;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Networking;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>局部矿副本到原生页面的装配；同世界换区保留重叠页面并等待新基线，撤权、断线或换世界立即退休，当前代次实绘后才就绪。</summary>
    public sealed class MineralReplicaPresentation
    {
        private readonly Transform parent;
        private readonly ARDMapDefinition definition;
        private readonly Camera camera;
        private readonly Texture lights;
        private readonly float ambient;
        private ChunkReplicaStateMachine replica;
        private TerrainReplicaSource source;
        private MineralLayerView view;
        private GameObject root;
        private ulong session, generation;
        private WorldIdentity world;
        private bool currentDataReady;
        private Task retirement = Task.CompletedTask;
        public bool Ready => currentDataReady && replica != null && generation == replica.Generation && view?.Ready == true;
        public Exception LastError => view?.LastError;
        public long BuiltPages => view?.BuiltPages ?? 0;
        public long InputBatches => view?.InputBatches ?? 0;
        public string WaitReason => view?.WaitReason ?? "等待矿层数据／页面退休";
        public MineralReplicaPresentation(Transform parent, ARDMapDefinition definition, Camera camera, Texture lights, float ambient)
        { this.parent = parent; this.definition = definition; this.camera = camera; this.lights = lights; this.ambient = ambient; }
        public void Present(ChunkReplicaStateMachine next, GridBounds region, bool dataReady)
        {
            if (next != replica || next != null && (session != next.Session || !world.Equals(next.World) || next.Closed)) Retire();
            currentDataReady = dataReady;
            if (!dataReady || next?.Descriptor == null || next.Closed || next.CommitId == 0 || !retirement.IsCompleted) return;
            if (view == null)
            {
                replica = next; session = next.Session; world = next.World;
                source = new TerrainReplicaSource(next); replica.Applied += Changed;
                root = new GameObject("Embedded mineral map pages"); root.transform.SetParent(parent, false);
                view = root.AddComponent<MineralLayerView>();
                _ = view.OpenAsync(definition, source, camera, next.World, lights: lights, ambient: ambient, region: region);
            }
            generation = next.Generation;
            view.SetReplicaRegion(next, region, dataReady);
            view.Tick();
        }
        private void Changed(MapReplicaChange change)
        {
            if (change.Session != session || !world.Equals(change.World) ||
                change.Kind == MapReplicaChangeKind.VisibilityRevoked || change.Kind == MapReplicaChangeKind.Disconnected)
            { Retire(); return; }
            if (change.StreamGeneration != generation) currentDataReady = false;
            if (view?.Loading == false) source?.NotifyChanged(change);
        }
        private void Retire()
        {
            if (replica != null) replica.Applied -= Changed;
            currentDataReady = false;
            replica = null; source = null;
            var old = view; view = null;
            retirement = old?.RetireAsync() ?? retirement;
            if (root != null)
            {
                root.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }
        }
        public Task RetireAsync() { Retire(); return retirement; }
    }
}
