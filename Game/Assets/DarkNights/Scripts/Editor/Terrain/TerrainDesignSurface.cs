using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 单窗口的轻量白模表面；小属性纹理加共享坡形表直接绘制原结构，不启动地形规则、岩壁或背景烘焙。
    /// 最多缓存三张只读属性页，绘制目标仅一份；临时笔触覆盖独立工作页，退休释放全部GPU资源。
    /// </summary>
    internal sealed class TerrainDesignSurface : IDisposable
    {
        private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private readonly LinkedList<string> recent = new LinkedList<string>();
        private Texture2D properties, working, shapeTable;
        private byte[] activePixels;
        private Material material;
        private bool dirty = true, minerals = true;
        private int version, drawnVersion = -1;
        internal RenderTexture Image { get; private set; }
        internal bool Ready => Image != null && !dirty && drawnVersion == version;
        internal int CachedMaps => cache.Count;
        internal long TextureBytes => (long)cache.Count * TerrainDesignCache.ByteCount +
            (working == null ? 0 : TerrainDesignCache.ByteCount) + (Image == null ? 0 : 2560L * 1536 * 4);
        internal string CacheStatus { get; private set; }
        internal string SelectionDetail { get; set; } = "点击查格；拖拽平移；滚轮缩放。";

        internal void Open(PlayableTerrain map, string key, TerrainMapDocument document)
        {
            EnsureResources();
            activePixels = TerrainDesignCache.Build(map);
            using (var hash = SHA256.Create())
                key += "|" + BitConverter.ToString(hash.ComputeHash(activePixels));
            if (working != null) { UnityEngine.Object.DestroyImmediate(working); working = null; }
            if (cache.TryGetValue(key, out var found))
            { properties = found; recent.Remove(key); CacheStatus = "回切缓存"; }
            else
            {
                byte[] pixels = null;
                if (document != null && document.Map != null && document.Map.ContentSha256 == document.DesignPreviewSource &&
                    document.DesignPreview != null)
                    pixels = TerrainDesignCache.Read(document.DesignPreview, document.DesignPreviewSource);
                if (pixels != null && !pixels.SequenceEqual(activePixels)) pixels = null;
                CacheStatus = pixels == null ? "同源结构投影" : "已保存设计预览";
                properties = Create(pixels ?? activePixels);
                cache.Add(key, properties);
            }
            recent.AddLast(key);
            while (cache.Count > 3)
            {
                string oldest = recent.First.Value; recent.RemoveFirst();
                UnityEngine.Object.DestroyImmediate(cache[oldest]); cache.Remove(oldest);
            }
            SelectionDetail = "点击查格；拖拽平移；滚轮缩放。";
            Invalidate();
        }

        internal void Apply(IReadOnlyList<TerrainBlueprintCellChange> changes)
        {
            if (activePixels == null || changes.Count == 0) return;
            foreach (var cell in changes)
            {
                int i = (cell.Row * TerrainDesignCache.Width + cell.X) * 4;
                activePixels[i] = cell.Material; activePixels[i + 1] = cell.Shape;
                if (working != null) working.SetPixel(cell.X, TerrainDesignCache.Height - 1 - cell.Row,
                    new Color32(activePixels[i], activePixels[i + 1], activePixels[i + 2], activePixels[i + 3]));
            }
            if (working == null) working = Create(activePixels, true);
            else working.Apply(false, false);
            Invalidate();
        }

        internal void Minerals(bool value)
        { if (minerals != value) { minerals = value; Invalidate(); } }

        private void Invalidate() { dirty = true; version++; }

        internal void Prepare()
        {
            if (!dirty || properties == null) return;
            material.SetTexture("_ShapeTable", shapeTable);
            material.SetFloat("_ShowMinerals", minerals ? 1 : 0);
            var prior = RenderTexture.active; bool write = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(working != null ? working : properties, Image, material);
            }
            finally { RenderTexture.active = prior; GL.sRGBWrite = write; }
            dirty = false;
        }

        internal void ConfirmDraw()
        { if (!dirty && Image != null) drawnVersion = version; }

        private static Texture2D Create(byte[] bytes, bool readable = false)
        {
            var pixels = new Color32[TerrainDesignCache.Width * TerrainDesignCache.Height];
            for (int row = 0; row < TerrainDesignCache.Height; row++)
                for (int x = 0; x < TerrainDesignCache.Width; x++)
                {
                    int i = (row * TerrainDesignCache.Width + x) * 4;
                    pixels[(TerrainDesignCache.Height - 1 - row) * TerrainDesignCache.Width + x] =
                        new Color32(bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3]);
                }
            var texture = new Texture2D(TerrainDesignCache.Width, TerrainDesignCache.Height, TextureFormat.RGBA32, false, true)
            { name = "Map design attributes", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply(false, !readable);
            return texture;
        }

        private void EnsureResources()
        {
            if (material != null) return;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/DarkNights/Res/Editor/Terrain/TerrainDesign.shader");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("设计模式Shader尚未就绪。");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            shapeTable = new Texture2D(13, 1, TextureFormat.RGBAFloat, false, true)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var shapes = new Color[13];
            for (int i = 0; i < shapes.Length; i++)
            {
                var shape = (TerrainCellShape)i;
                shapes[i] = new Color(TerrainShapeGeometry.Edge(shape, 0), TerrainShapeGeometry.Edge(shape, 1),
                    TerrainShapeGeometry.Ceiling(shape) ? 1 : 0, i == 0 ? 1 : 0);
            }
            shapeTable.SetPixels(shapes); shapeTable.Apply(false, true);
            Image = new RenderTexture(2560, 1536, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { name = "Map structure whitebox", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            Image.Create();
        }

        public void Dispose()
        {
            foreach (var texture in cache.Values) UnityEngine.Object.DestroyImmediate(texture);
            cache.Clear(); recent.Clear(); properties = null;
            activePixels = null;
            if (working != null) UnityEngine.Object.DestroyImmediate(working);
            if (shapeTable != null) UnityEngine.Object.DestroyImmediate(shapeTable);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (Image != null) { Image.Release(); UnityEngine.Object.DestroyImmediate(Image); }
            working = shapeTable = null; material = null; Image = null; dirty = true; drawnVersion = -1;
        }
    }
}
