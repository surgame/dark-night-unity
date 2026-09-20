using System;
using System.Collections.Generic;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Unity;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>独立预览与正式会话共用的只读地形表现；加载蓝图或网络副本，静止后停止地图 Tick，退出释放页和资源。</summary>
    public sealed class TerrainPreview : MonoBehaviour
    {
        public TerrainMapAsset Map;
        public CaveTerrainStyle CaveStyle;
        private CaveVisualSource caveSource;
        public Camera ViewCamera;
        private ARDMapController controller;
        private CancellationTokenSource lifetime;
        private TerrainReplicaSource replicaSource;
        private GridBounds visible;
        private bool localCoordinates;
        private bool refreshingReplica;
        private readonly HashSet<ChunkCoord> pendingReplicaChunks = new HashSet<ChunkCoord>();
        public long BuiltPages => controller?.Renderer.CommittedBuilds ?? 0;
        public int LastChangedChunkCount { get; private set; }
        public int LastRefreshRegionCount { get; private set; }
        public long RefreshBatchCount { get; private set; }
        public bool RefreshingReplica => refreshingReplica;
        public Exception LastError { get; private set; }
        public bool Ready => controller != null && controller.Renderer.CommittedBuilds > 0 && controller.Renderer.QueueCount == 0 && controller.Renderer.InFlightCount == 0;
        public void NotifyReplicaChanged() => replicaSource?.NotifyChanged();
        public async void ShowReplica(AnyRules.Next.Authoring.ARDMapDefinition definition, IMapChunkSource source, WorldIdentity world)
        {
            replicaSource = source as TerrainReplicaSource;
            if (CaveStyle != null)
            { caveSource = new CaveVisualSource(source, CaveStyle, definition.LoadGameplayCatalog().Tiles, transform); source = caveSource; }
            localCoordinates = true;
            var own = lifetime = new CancellationTokenSource();
            try
            {
                var result = await ARDMapController.CreateAsync(definition, new MapOptions(initialize: false, showOnCreate: false,
                    autoUpdate: false, maximumInitializationCells: 131072, chunkSource: source, parent: transform, world: world, profile: CaveProfile()), own.Token);
                if (own.IsCancellationRequested) { await result.DisposeAsync(); return; }
                controller = result;
                await result.LoadRegionAsync(result.Descriptor.Bounds, own.Token);
                if (own.IsCancellationRequested) return;
                if (replicaSource != null) replicaSource.Changed += OnReplicaChanged;
                caveSource?.Flush();
                UpdateVisible();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { LastError = error; Debug.LogException(error, this); }
        }

        private void OnEnable()
        {
            if (Map == null || ViewCamera == null) return;
            if (CaveStyle == null) CaveStyle = Map.CaveStyle;
            ShowBlueprint(Map.Definition, Map.ReadBlueprint());
        }

        public async void ShowBlueprint(AnyRules.Next.Authoring.ARDMapDefinition definition,
            DarkNights.Core.Config.Terrain.TerrainBlueprint blueprint)
        {
            if (lifetime != null) throw new InvalidOperationException("每个预览只接收一份蓝图；重新生成须替换预览实例。");
            localCoordinates = true;
            var own = lifetime = new CancellationTokenSource();
            try
            {
                var catalog = definition.LoadGameplayCatalog();
                IMapChunkSource source = new TerrainBlueprintSource(blueprint, catalog.Tiles);
                if (CaveStyle != null) { caveSource = new CaveVisualSource(source, CaveStyle, catalog.Tiles, transform); source = caveSource; }
                // Definition reloads its catalog; TileIds remain mapped through stable terrain keys.
                var result = await ARDMapController.CreateAsync(definition,
                    new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false,
                        maximumInitializationCells: 131072, chunkSource: source, parent: transform, profile: CaveProfile()), own.Token);
                if (own.IsCancellationRequested) { await result.DisposeAsync(); return; }
                controller = result;
                await result.LoadRegionAsync(result.Descriptor.Bounds, own.Token);
                if (own.IsCancellationRequested) return;
                caveSource?.Flush();
                UpdateVisible();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { LastError = e; Debug.LogException(e, this); }
        }

        private RenderProfile CaveProfile() => caveSource == null ? null : new RenderProfile(defaultMaterial: caveSource.Material);

        private void Update()
        {
            if (controller == null || LastError != null || refreshingReplica) return;
            UpdateVisible();
            if (pendingReplicaChunks.Count != 0) StartReplicaRefresh();
            var renderer = controller.Renderer;
            if (renderer.QueueCount != 0 || renderer.InFlightCount != 0 || renderer.CommittedBuilds == 0) controller.Tick();
        }

        private void UpdateVisible()
        {
            var bounds = controller.Descriptor.Bounds;
            float halfH = ViewCamera.orthographicSize / (localCoordinates ? transform.lossyScale.y : 1), halfW = halfH * ViewCamera.aspect;
            Vector3 p = localCoordinates ? transform.InverseTransformPoint(ViewCamera.transform.position) : ViewCamera.transform.position;
            int page = controller.Descriptor.PageSize;
            int minU = Math.Max(bounds.MinU, Mathf.FloorToInt((p.x - halfW - 2) / page) * page);
            int minV = Math.Max(bounds.MinV, Mathf.FloorToInt((p.y - halfH - 2) / page) * page);
            int maxU = Math.Min((int)bounds.MaxUExclusive, Mathf.CeilToInt((p.x + halfW + 2) / page) * page);
            int maxV = Math.Min((int)bounds.MaxVExclusive, Mathf.CeilToInt((p.y + halfH + 2) / page) * page);
            if (maxU <= minU || maxV <= minV) return;
            var next = new GridBounds(minU, minV, maxU - minU, maxV - minV);
            if (visible.Equals(next)) return;
            if (visible.IsValid) HideDifference(visible, next);
            // HideRegion expands logical strips by the DualGrid halo. Restore the complete
            // target so shared boundary pages cannot stay hidden; unchanged shown pages stay cached.
            controller.ShowRegion(next); visible = next;
        }

        // Only departing logical strips are hidden; ShowRegion restores any shared halo pages.
        private void HideDifference(GridBounds area, GridBounds overlap)
        {
            int left = Math.Max(area.MinU, overlap.MinU), bottom = Math.Max(area.MinV, overlap.MinV);
            int right = (int)Math.Min(area.MaxUExclusive, overlap.MaxUExclusive);
            int top = (int)Math.Min(area.MaxVExclusive, overlap.MaxVExclusive);
            if (!overlap.IsValid || left >= right || bottom >= top) { controller.HideRegion(area); return; }
            HideStrip(area.MinU, area.MinV, left - area.MinU, area.Height);
            HideStrip(right, area.MinV, (int)area.MaxUExclusive - right, area.Height);
            HideStrip(left, area.MinV, right - left, bottom - area.MinV);
            HideStrip(left, top, right - left, (int)area.MaxVExclusive - top);
        }

        private void HideStrip(int u, int v, int width, int height)
        {
            if (width > 0 && height > 0) controller.HideRegion(new GridBounds(u, v, width, height));
        }

        private void OnReplicaChanged(IReadOnlyList<ChunkCoord> changedChunks)
        {
            if (changedChunks == null || changedChunks.Count == 0) return;
            LastChangedChunkCount = changedChunks.Count;
            foreach (var chunk in changedChunks) pendingReplicaChunks.Add(chunk);
            StartReplicaRefresh();
        }

        private async void StartReplicaRefresh()
        {
            if (controller == null || !visible.IsValid || lifetime == null || refreshingReplica) return;
            refreshingReplica = true;
            var own = lifetime;
            try
            {
                while (pendingReplicaChunks.Count != 0 && !own.IsCancellationRequested)
                {
                    ChunkCoord[] batch = new ChunkCoord[pendingReplicaChunks.Count];
                    pendingReplicaChunks.CopyTo(batch); pendingReplicaChunks.Clear();
                    IReadOnlyList<GridBounds> regions = MergeChunkRegions(batch, controller.Descriptor.ChunkSize);
                    LastRefreshRegionCount = regions.Count;
                    RefreshBatchCount++;
                    foreach (GridBounds region in regions)
                    {
                        await controller.UnloadRegionAsync(region, MapUnloadPolicy.DiscardUnsaved, own.Token);
                        await controller.LoadRegionAsync(region, own.Token);
                    }
                    if (!own.IsCancellationRequested) { caveSource?.Flush(); controller.ShowRegion(visible); }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { LastError = error; Debug.LogException(error, this); }
            finally
            {
                refreshingReplica = false;
                if (pendingReplicaChunks.Count != 0 && lifetime == own && !own.IsCancellationRequested)
                    StartReplicaRefresh();
            }
        }

        private static IReadOnlyList<GridBounds> MergeChunkRegions(IReadOnlyList<ChunkCoord> chunks, int size)
        {
            var remaining = new HashSet<ChunkCoord>(chunks);
            var regions = new List<GridBounds>();
            while (remaining.Count != 0)
            {
                ChunkCoord start = default;
                foreach (var candidate in remaining) { start = candidate; break; }
                var queue = new Queue<ChunkCoord>(); queue.Enqueue(start); remaining.Remove(start);
                int minU = start.U, maxU = start.U, minV = start.V, maxV = start.V;
                while (queue.Count != 0)
                {
                    ChunkCoord current = queue.Dequeue();
                    minU = Math.Min(minU, current.U); maxU = Math.Max(maxU, current.U);
                    minV = Math.Min(minV, current.V); maxV = Math.Max(maxV, current.V);
                    foreach (var neighbor in new[]
                    {
                        new ChunkCoord(current.U - 1, current.V), new ChunkCoord(current.U + 1, current.V),
                        new ChunkCoord(current.U, current.V - 1), new ChunkCoord(current.U, current.V + 1)
                    }) if (remaining.Remove(neighbor)) queue.Enqueue(neighbor);
                }
                regions.Add(new GridBounds(checked(minU * size), checked(minV * size),
                    checked((maxU - minU + 1) * size), checked((maxV - minV + 1) * size)));
            }
            return regions;
        }

        private async void OnDisable()
        {
            if (replicaSource != null) { replicaSource.Changed -= OnReplicaChanged; replicaSource = null; }
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; refreshingReplica = false; pendingReplicaChunks.Clear();
            var old = controller; controller = null; visible = default;
            if (old != null) await old.DisposeAsync();
            caveSource?.Dispose(); caveSource = null;
        }
    }
}
