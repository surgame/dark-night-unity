using System;
using System.IO;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>松散土石堆的定向原生资源首建；80×48连续像素土纹配100PPU、Point和透明坡形网格。仅新增空路径，保留既有事件及人工资源，编辑态网格为可追溯派生预览。</summary>
    public static class GeologySoilAssets
    {
        private const string Root = GeologyCollapseAssets.Root;

        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("土堆装配只能在编辑态执行。");
            var texture = Texture();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Soil.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("锁定URP缺少2D土堆shader。");
                material = new Material(shader) { mainTexture = texture };
                AssetDatabase.CreateAsset(material, Root + "/Soil.mat");
            }
            string pilePath = Root + "/RubblePile.prefab";
            if (!File.Exists(pilePath))
            {
                var root = new GameObject("RubblePile");
                try
                {
                    var pile = root.AddComponent<GeologyPileView>();
                    pile.Geometry = root.AddComponent<MeshFilter>(); pile.Surface = root.AddComponent<MeshRenderer>();
                    pile.Surface.sharedMaterial = material; pile.Surface.sortingOrder = -16;
                    pile.Surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    pile.Surface.receiveShadows = false;
                    var config = new GeologyEventConfig { SourceV = -31, SourceRows = 3, RubbleWidth = 5 }.Freeze();
                    pile.Bind(Enumerable.Range(0, config.RubbleCount).Select(config.Motion).ToArray(),
                        Enumerable.Range(0, config.RubbleCount).Select(config.PileShape).ToArray(), config.Collapse);
                    pile.Present((1u << config.RubbleCount) - 1);
                    string previewPath = Root + "/SoilPreview.asset";
                    if (File.Exists(previewPath)) throw new InvalidOperationException("土堆预览路径已有作者资源，拒绝覆盖。");
                    var preview = UnityEngine.Object.Instantiate(pile.Geometry.sharedMesh);
                    preview.name = "Legacy soil material preview"; AssetDatabase.CreateAsset(preview, previewPath);
                    pile.Geometry.sharedMesh = preview; root.transform.position = Vector3.zero;
                    PrefabUtility.SaveAsPrefabAsset(root, pilePath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var prefab = PrefabUtility.LoadPrefabContents(GeologyLabAssets.PrefabPath);
            try
            {
                var view = prefab.GetComponent<GeologyEventView>();
                if (view.Pile != null) return;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(pilePath), prefab.transform);
                view.Pile = instance.GetComponent<GeologyPileView>(); view.Pile.Surface.enabled = false;
                view.ForceRefreshAllReferences();
                PrefabUtility.SaveAsPrefabAsset(prefab, GeologyLabAssets.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static Texture2D Texture()
        {
            string path = Root + "/Soil.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(80, 48, TextureFormat.RGBA32, false);
                try
                {
                    var pixels = new Color32[80 * 48];
                    for (int y = 0; y < 48; y++) for (int x = 0; x < 80; x++)
                    {
                        uint hash = Hash(x, y); int clod = (int)(Hash(x / 4, y / 3) % 5);
                        int grain = (int)(hash % 9) - 4;
                        pixels[y * 80 + x] = new Color32((byte)(112 + clod * 5 + grain),
                            (byte)(78 + clod * 4 + grain), (byte)(47 + clod * 3 + grain), 255);
                    }
                    // 固定碎石斑点只占少量面积；内部不描格线，土纹跨越全部落地格。
                    for (int i = 0; i < 38; i++)
                    {
                        int x = 2 + (int)(Hash(i, 37) % 75), y = 2 + (int)(Hash(i, 83) % 43);
                        int size = i % 3 == 0 ? 4 : 2;
                        for (int dy = 0; dy < size; dy++) for (int dx = 0; dx < size + 1; dx++)
                        {
                            if (dx + dy == 0 || dx == size && dy == size - 1) continue;
                            bool top = dy == size - 1;
                            pixels[(y + dy) * 80 + x + dx] = top ? new Color32(167, 145, 108, 255) : new Color32(77, 66, 49, 255);
                        }
                    }
                    texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Repeat; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static uint Hash(int x, int y)
        {
            unchecked { uint value = (uint)(x * 374761393 + y * 668265263 + 1015); value = (value ^ value >> 13) * 1274126177; return value ^ value >> 16; }
        }
    }
}
