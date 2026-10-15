using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>选定大小混合版的定向资源制作；在统一Geology目录首建棕褐色方岩和尘粒，原生Prefab沿用GUID。旧土堆资源保留，不重建场景、不保存用户未保存的布局。</summary>
    public static class GeologyMixedAssets
    {
        private const string Root = GeologyCollapseAssets.Root;

        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("混合碎岩资源只能在编辑态制作。");
            var stones = new Sprite[4];
            for (int i = 0; i < 4; i++) stones[i] = Texture("MixedRock" + i, 16, i);
            var dust = Texture("MixedDust", 12, 4); var chips = Texture("MixedChip", 2, 5);
            UpgradeDebris(dust, chips);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/MixedPile.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("缺少已锁定URP的Sprite shader。");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, Root + "/MixedPile.mat");
            }
            UpgradePile(material);
            var prefab = PrefabUtility.LoadPrefabContents(GeologyLabAssets.PrefabPath);
            try
            {
                var view = prefab.GetComponent<GeologyEventView>();
                var fragments = new SpriteRenderer[64];
                Array.Copy(view.Fragments, fragments, Math.Min(view.Fragments.Length, fragments.Length));
                for (int i = 0; i < fragments.Length; i++)
                {
                    if (fragments[i] == null) fragments[i] = GeologyLabAssets.Marker("Fragment" + i, view.Falling, stones[i % 4], Color.white);
                    fragments[i].sprite = stones[i % 4]; fragments[i].color = Color.white;
                    fragments[i].sortingOrder = -15; fragments[i].enabled = false;
                }
                view.Fragments = fragments; view.Pile.Surface.sharedMaterial = material;
                view.Pile.Surface.enabled = false; view.ForceRefreshAllReferences();
                PrefabUtility.SaveAsPrefabAsset(prefab, GeologyLabAssets.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static void UpgradePile(Material material)
        {
            string path = Root + "/RubblePile.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pile = prefab.GetComponent<GeologyPileView>(); pile.Surface.sharedMaterial = material;
                var config = new GeologyEventConfig { SourceV = -31, SourceRows = 3, RubbleWidth = 5 }.Freeze();
                pile.Bind(Enumerable.Range(0, config.RubbleCount).Select(config.Motion).ToArray(),
                    Enumerable.Range(0, config.RubbleCount).Select(config.PileShape).ToArray(), config.Collapse);
                pile.Present((1u << config.RubbleCount) - 1);
                string meshPath = Root + "/MixedPreview.asset";
                var preview = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (preview == null)
                {
                    preview = UnityEngine.Object.Instantiate(pile.Geometry.sharedMesh);
                    preview.name = "Mixed asymmetric rubble preview"; AssetDatabase.CreateAsset(preview, meshPath);
                }
                else
                {
                    var source = pile.Geometry.sharedMesh; preview.Clear(); preview.vertices = source.vertices;
                    preview.uv = source.uv; preview.colors = source.colors; preview.triangles = source.triangles;
                    preview.RecalculateBounds(); EditorUtility.SetDirty(preview); AssetDatabase.SaveAssetIfDirty(preview);
                }
                pile.Geometry.sharedMesh = preview; prefab.transform.position = Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static void UpgradeDebris(Sprite dust, Sprite chips)
        {
            string path = Root + "/Debris.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var debris = prefab.GetComponent<GeologyDebrisView>();
                Configure(debris.Dust, dust, .62f); Configure(debris.Chips, chips, 1);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static void Configure(ParticleSystem system, Sprite sprite, float opacity)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.startColor = new Color(1, 1, 1, opacity); main.maxParticles = 256;
            var renderer = system.GetComponent<ParticleSystemRenderer>(); var material = renderer.sharedMaterial;
            if (material == null) throw new InvalidOperationException("现有尘粒Prefab缺少明确材质。");
            material.SetTexture("_BaseMap", sprite.texture); material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        private static Sprite Texture(string name, int size, int variant)
        {
            string path = Root + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                try
                {
                    var pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        if (variant < 4) pixels[y * size + x] = GeologyRockPixels.Sample(variant, x, y);
                        else if (variant == 5) pixels[y * size + x] = new Color32(98, 72, 45, 255);
                        else
                        {
                            float dx = x - 5.5f, dy = y - 5.5f;
                            if (dx * dx + dy * dy > 34) continue;
                            pixels[y * size + x] = new Color32(114, 85, 54, (byte)(45 + (x * 7 + y * 3) % 4 * 22));
                        }
                    }
                    texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.sRGBTexture = true;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
