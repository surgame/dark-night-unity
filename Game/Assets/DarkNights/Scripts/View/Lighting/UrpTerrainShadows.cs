using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkNights.View.Lighting
{
    /// <summary>URP 回退的局部区块遮挡所有者；每 16 格区块一个模板实例，只更新内容变化的区块，隐藏时停止所有遮挡。</summary>
    public sealed class UrpTerrainShadows
    {
        private const int Size = 16;
        private readonly ShadowCaster2D template;
        private readonly Dictionary<Vector2Int, (ShadowCaster2D Caster, ulong Hash)> chunks =
            new Dictionary<Vector2Int, (ShadowCaster2D, ulong)>();
        private readonly HashSet<Vector2Int> wanted = new HashSet<Vector2Int>();
        private long revision = -1;
        private RectInt installed;
        private ShadowCaster2D border;
        public UrpTerrainShadows(ShadowCaster2D source) { template = source; }

        public void Update(Terrain.TerrainPreview preview)
        {
            var source = preview.LightingSource;
            var loaded = preview.LightingLoadedBounds;
            var bounds = new RectInt(loaded.MinU, 1 - (int)loaded.MaxVExclusive, loaded.Width, loaded.Height);
            if (revision == source.GeometryRevision && installed == bounds)
            { foreach (var chunk in chunks.Values) chunk.Caster.gameObject.SetActive(true); border?.gameObject.SetActive(true); return; }
            if (installed != bounds || border == null) UpdateBorder(preview, bounds);
            wanted.Clear();
            for (int row = Mathf.FloorToInt(bounds.yMin / (float)Size); row <= Mathf.FloorToInt((bounds.yMax - 1) / (float)Size); row++)
                for (int u = Mathf.FloorToInt(bounds.xMin / (float)Size); u <= Mathf.FloorToInt((bounds.xMax - 1) / (float)Size); u++)
                {
                    var key = new Vector2Int(u, row); wanted.Add(key);
                    var region = new RectInt(Mathf.Max(u * Size, bounds.xMin), Mathf.Max(row * Size, bounds.yMin),
                        Mathf.Min((u + 1) * Size, bounds.xMax) - Mathf.Max(u * Size, bounds.xMin),
                        Mathf.Min((row + 1) * Size, bounds.yMax) - Mathf.Max(row * Size, bounds.yMin));
                    ulong hash = 14695981039346656037UL;
                    for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
                        hash = unchecked((hash ^ source.LightingCell(x, y)) * 1099511628211UL);
                    hash = unchecked((hash ^ (uint)region.xMin ^ ((ulong)(uint)region.yMin << 32)) * 1099511628211UL);
                    hash = unchecked((hash ^ (uint)region.width ^ ((ulong)(uint)region.height << 32)) * 1099511628211UL);
                    if (!chunks.TryGetValue(key, out var chunk))
                    {
                        var caster = Object.Instantiate(template, preview.transform, false);
                        caster.name = "URP terrain shadow " + key; caster.gameObject.hideFlags = HideFlags.DontSave;
                        chunk = (caster, 0);
                    }
                    if (chunk.Hash != hash)
                    {
                        var collider = chunk.Caster.GetComponent<PolygonCollider2D>();
                        var paths = UrpTerrainShadowPaths.Build(region, source.LightingCell);
                        collider.pathCount = paths.Count;
                        for (int n = 0; n < paths.Count; n++) collider.SetPath(n, paths[n]);
                        chunk = (chunk.Caster, hash);
                    }
                    chunk.Caster.gameObject.SetActive(true); chunks[key] = chunk;
                }
            var removed = new List<Vector2Int>();
            foreach (var chunk in chunks) if (!wanted.Contains(chunk.Key))
            { Destroy(chunk.Value.Caster.gameObject); removed.Add(chunk.Key); }
            foreach (var key in removed) chunks.Remove(key);
            revision = source.GeometryRevision; installed = bounds;
        }

        private void UpdateBorder(Terrain.TerrainPreview preview, RectInt bounds)
        {
            if (border == null)
            {
                border = Object.Instantiate(template, preview.transform, false);
                border.name = "URP unknown-region boundary"; border.gameObject.hideFlags = HideFlags.DontSave;
            }
            float left = bounds.xMin - .5f, right = bounds.xMax - .5f;
            float bottom = .5f - bounds.yMax, top = .5f - bounds.yMin;
            var collider = border.GetComponent<PolygonCollider2D>(); collider.pathCount = 4;
            Vector2[] Rect(float x0, float y0, float x1, float y1) => new[]
            { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
            collider.SetPath(0, Rect(left - 1, bottom - 1, left, top + 1));
            collider.SetPath(1, Rect(right, bottom - 1, right + 1, top + 1));
            collider.SetPath(2, Rect(left, bottom - 1, right, bottom));
            collider.SetPath(3, Rect(left, top, right, top + 1));
            border.gameObject.SetActive(true);
        }

        public void Suspend()
        { foreach (var chunk in chunks.Values) chunk.Caster.gameObject.SetActive(false); border?.gameObject.SetActive(false); }
        public void Dispose()
        { foreach (var chunk in chunks.Values) Destroy(chunk.Caster.gameObject); chunks.Clear(); if (border != null) Destroy(border.gameObject); border = null; }
        private static void Destroy(GameObject value)
        { value.SetActive(false); if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
