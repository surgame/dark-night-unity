using System;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Networking;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>局部矿副本到原生页面的装配；区域换代先退休旧宿主，只装载已订阅区块，避免 Unknown 被当成空格或保留过期页面。</summary>
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
        private Task retirement = Task.CompletedTask;
        public bool Ready => view?.Ready == true;
        public Exception LastError => view?.LastError;
        public long BuiltPages => view?.BuiltPages ?? 0;
        public long InputBatches => view?.InputBatches ?? 0;
        public string WaitReason => view?.WaitReason ?? "等待矿层数据／页面退休";
        public MineralReplicaPresentation(Transform parent, ARDMapDefinition definition, Camera camera, Texture lights, float ambient)
        { this.parent = parent; this.definition = definition; this.camera = camera; this.lights = lights; this.ambient = ambient; }
        public void Present(ChunkReplicaStateMachine next, GridBounds region, bool dataReady)
        {
            if (next != replica || next != null && (session != next.Session || generation != next.Generation)) Retire();
            if (!dataReady || next?.Descriptor == null || next.CommitId == 0 || !retirement.IsCompleted) return;
            if (view == null)
            {
                replica = next; session = next.Session; generation = next.Generation;
                source = new TerrainReplicaSource(next); replica.Applied += Changed;
                root = new GameObject("Embedded mineral map pages"); root.transform.SetParent(parent, false);
                view = root.AddComponent<MineralLayerView>();
                _ = view.OpenAsync(definition, source, camera, next.World, lights: lights, ambient: ambient, region: region);
            }
            view.Tick();
        }
        private void Changed(MapReplicaChange change)
        {
            if (change.Session != session || change.StreamGeneration != generation) { Retire(); return; }
            if (view?.Loading == false) source?.NotifyChanged(change);
        }
        private void Retire()
        {
            if (replica != null) replica.Applied -= Changed;
            replica = null; source = null;
            var old = view; view = null;
            retirement = old?.RetireAsync() ?? retirement;
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); root = null; }
        }
        public Task RetireAsync() { Retire(); return retirement; }
    }
}
