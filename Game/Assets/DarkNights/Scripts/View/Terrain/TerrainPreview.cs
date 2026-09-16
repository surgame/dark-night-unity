using System;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Unity;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>独立地图测试场景的离线预览；只在启用时加载，静止后停止地图 Tick，退出释放页和资源。</summary>
    public sealed class TerrainPreview : MonoBehaviour
    {
        public TerrainMapAsset Map;
        public Camera ViewCamera;
        private ARDMapController controller;
        private CancellationTokenSource lifetime;
        private GridBounds visible;
        public long BuiltPages => controller?.Renderer.CommittedBuilds ?? 0;
        public Exception LastError { get; private set; }

        private async void OnEnable()
        {
            if (Map == null || ViewCamera == null) return;
            var own = lifetime = new CancellationTokenSource();
            try
            {
                var catalog = Map.Definition.LoadGameplayCatalog();
                var source = new TerrainBlueprintSource(Map.ReadBlueprint(), catalog.Tiles);
                // Definition reloads its catalog; TileIds remain mapped through stable terrain keys.
                var result = await ARDMapController.CreateAsync(Map.Definition,
                    new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false,
                        maximumInitializationCells: 131072, chunkSource: source, parent: transform), own.Token);
                if (own.IsCancellationRequested) { await result.DisposeAsync(); return; }
                controller = result;
                await controller.LoadRegionAsync(controller.Descriptor.Bounds, own.Token);
                UpdateVisible();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { LastError = e; Debug.LogException(e, this); }
        }

        private void Update()
        {
            if (controller == null || LastError != null) return;
            UpdateVisible();
            var renderer = controller.Renderer;
            if (renderer.QueueCount != 0 || renderer.InFlightCount != 0 || renderer.CommittedBuilds == 0) controller.Tick();
        }

        private void UpdateVisible()
        {
            var bounds = controller.Descriptor.Bounds;
            float halfH = ViewCamera.orthographicSize, halfW = halfH * ViewCamera.aspect;
            Vector3 p = ViewCamera.transform.position;
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

        private async void OnDisable()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            var old = controller; controller = null; visible = default;
            if (old != null) await old.DisposeAsync();
        }
    }
}
