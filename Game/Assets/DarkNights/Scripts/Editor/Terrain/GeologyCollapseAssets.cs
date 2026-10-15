using System;
using System.IO;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>塌方专用原生碎岩和粒子资源的定向升级；确定性原生像素纹理只在新目录首建，既有Prefab保留GUID及警示绑定，不改作者岩壁素材。</summary>
    public static class GeologyCollapseAssets
    {
        public const string Root = "Assets/DarkNights/Res/Effects/Geology";

        public static void Upgrade()
        {
            string oldDirectory = GeologyLabAssets.Root + "/Collapse";
            if (AssetDatabase.IsValidFolder(oldDirectory) && !AssetDatabase.IsValidFolder(Root))
            {
                string error = AssetDatabase.MoveAsset(oldDirectory, Root);
                if (error.Length != 0) throw new InvalidOperationException(error);
            }
            GeologyLabAssets.Folder(Root);
            var stones = new Sprite[4];
            for (int i = 0; i < stones.Length; i++) stones[i] = Texture("Rock" + i, 16, i);
            var dust = Texture("Dust", 12, 4); var chip = Texture("Chip", 2, 5);
            var prefab = PrefabUtility.LoadPrefabContents(GeologyLabAssets.PrefabPath);
            try
            {
                var view = prefab.GetComponent<GeologyEventView>();
                var old = view.Falling.GetComponent<SpriteRenderer>();
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                view.Falling.localScale = Vector3.one; view.Falling.localPosition = Vector3.zero;
                view.Falling.gameObject.SetActive(true);
                if (view.Fragments.Length == 0)
                {
                    view.Fragments = new SpriteRenderer[24];
                    for (int i = 0; i < view.Fragments.Length; i++)
                    {
                        var rock = GeologyLabAssets.Marker("Fragment" + i, view.Falling, stones[i % 4], Color.white);
                        rock.sortingOrder = -15; rock.enabled = false; view.Fragments[i] = rock;
                    }
                }
                if (view.Debris == null)
                {
                    view.Debris = new GameObject("Debris").AddComponent<GeologyDebrisView>();
                    view.Debris.transform.SetParent(prefab.transform, false);
                    view.Debris.Dust = Particles("Dust", view.Debris.transform, dust, false);
                    view.Debris.Chips = Particles("Chips", view.Debris.transform, chip, true);
                }
                string debrisPath = Root + "/Debris.prefab";
                if (!File.Exists(debrisPath))
                {
                    PrefabUtility.SaveAsPrefabAsset(view.Debris.gameObject, debrisPath);
                    UnityEngine.Object.DestroyImmediate(view.Debris.gameObject);
                    var debris = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(debrisPath), prefab.transform);
                    view.Debris = debris.GetComponent<GeologyDebrisView>();
                }
                view.ForceRefreshAllReferences();
                PrefabUtility.SaveAsPrefabAsset(prefab, GeologyLabAssets.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            GeologySoilAssets.Upgrade();
            GeologyMixedAssets.Upgrade();
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
                        float dx = x - (size - 1) * .5f, dy = y - (size - 1) * .5f;
                        bool inside = variant >= 4 ? dx * dx + dy * dy < size * size * .24f :
                            Math.Abs(dx) + Math.Abs(dy) * .6f < 8 - variant * .25f && Math.Abs(dy) < 6.5f &&
                            x + y > 3 + variant && x - y < 12 - variant;
                        if (!inside) continue;
                        if (variant == 4) pixels[y * size + x] = new Color32(168, 156, 137, (byte)(75 + (x + y) % 3 * 30));
                        else
                        {
                            int shade = 74 + variant * 8 + (y > size / 2 ? 24 : 0) + (x * 3 + y * 7) % 4 * 4;
                            if (x + y == 10 + variant || x - y == 3) shade -= 25;
                            pixels[y * size + x] = new Color32((byte)(shade + 12), (byte)(shade + 8), (byte)shade, 255);
                        }
                    }
                    texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static ParticleSystem Particles(string name, Transform parent, Sprite sprite, bool chips)
        {
            string materialPath = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) throw new InvalidOperationException("锁定URP缺少原生粒子Unlit shader。");
                material = new Material(shader); material.SetTexture("_BaseMap", sprite.texture);
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var system = root.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false; system.randomSeed = chips ? 21015u : 11015u;
            var main = system.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = chips ? .326f : .018f;
            main.startSpeed = 0; main.startColor = chips ? new Color(.7f, .65f, .56f) : new Color(.8f, .75f, .66f, .65f);
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.6f, .55f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = root.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -10;
            return system;
        }
    }
}
