using System;
using AnyRules.Next;
using AnyRules.Next.Unity;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>前景与矿层共用的正交相机分页范围和退出页处理；保持原格坐标合同，只改变原生页面可见性，不修改地图。</summary>
    internal static class TerrainViewport
    {
        internal static GridBounds Capture(WorldDescriptor descriptor, Transform root, Camera camera)
        {
            var bounds = descriptor.Bounds;
            float halfH = camera.orthographicSize / Mathf.Abs(root.lossyScale.y), halfW = halfH * camera.aspect;
            Vector3 p = root.InverseTransformPoint(camera.transform.position);
            int page = descriptor.PageSize;
            int left = Math.Max(bounds.MinU, Mathf.FloorToInt((p.x - halfW - 2) / page) * page);
            int bottom = Math.Max(bounds.MinV, Mathf.FloorToInt((p.y - halfH - 2) / page) * page);
            int right = Math.Min((int)bounds.MaxUExclusive, Mathf.CeilToInt((p.x + halfW + 2) / page) * page);
            int top = Math.Min((int)bounds.MaxVExclusive, Mathf.CeilToInt((p.y + halfH + 2) / page) * page);
            return right <= left || top <= bottom ? default : new GridBounds(left, bottom, right - left, top - bottom);
        }

        internal static void HideDifference(ARDMapController controller, GridBounds area, GridBounds overlap)
        {
            if (!area.IsValid) return;
            int left = Math.Max(area.MinU, overlap.MinU), bottom = Math.Max(area.MinV, overlap.MinV);
            int right = (int)Math.Min(area.MaxUExclusive, overlap.MaxUExclusive), top = (int)Math.Min(area.MaxVExclusive, overlap.MaxVExclusive);
            if (!overlap.IsValid || left >= right || bottom >= top) { controller.HideRegion(area); return; }
            void Hide(int u, int v, int width, int height)
            { if (width > 0 && height > 0) controller.HideRegion(new GridBounds(u, v, width, height)); }
            Hide(area.MinU, area.MinV, left - area.MinU, area.Height);
            Hide(right, area.MinV, (int)area.MaxUExclusive - right, area.Height);
            Hide(left, area.MinV, right - left, bottom - area.MinV);
            Hide(left, top, right - left, (int)area.MaxVExclusive - top);
        }
        /// <summary>保留非世界边缘的一整页规则缓冲；显示的页不会读取加载范围外的 Unknown 四角。</summary>
        internal static GridBounds Limit(GridBounds next, GridBounds loaded, WorldDescriptor descriptor)
        {
            var bounds = descriptor.Bounds; int page = descriptor.PageSize;
            int left = Math.Max(next.MinU, loaded.MinU + (loaded.MinU == bounds.MinU ? 0 : page));
            int bottom = Math.Max(next.MinV, loaded.MinV + (loaded.MinV == bounds.MinV ? 0 : page));
            int right = (int)Math.Min(next.MaxUExclusive, loaded.MaxUExclusive - (loaded.MaxUExclusive == bounds.MaxUExclusive ? 0 : page));
            int top = (int)Math.Min(next.MaxVExclusive, loaded.MaxVExclusive - (loaded.MaxVExclusive == bounds.MaxVExclusive ? 0 : page));
            return next.IsValid && right > left && top > bottom ? new GridBounds(left, bottom, right - left, top - bottom) : default;
        }
    }
}
