using System.Collections;
using System.IO;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Unity;
using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>实际页面构建和静态缓存回归；捕获真实相机输出，局部破坏不允许引发整图页面重建。</summary>
    public sealed class TerrainPresentationTests
    {
        [Test]
        public void AuthoredAtlasAndNativeCatalogReload()
        {
            const string root = TerrainTestAssets.Root;
            var importer = (TextureImporter)AssetImporter.GetAtPath(root + "/Art/terrain-8x8-all.png");
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(root + "/Art/terrain-8x8-all.png").OfType<Sprite>().Count(), Is.EqualTo(512));
            var map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(root + "/Maps/GreypineTest.asset");
            Assert.That(map.ReadBlueprint().MaterialAt(62, 72), Is.EqualTo(7));
            Assert.That(map.Definition.LoadGameplayCatalog().Definitions.Count, Is.EqualTo(8));
            Assert.That(map.Definition.LoadRuntimeCatalog(), Is.Not.Null);
        }
        [UnityTest]
        public IEnumerator ActualPagesStayStaticAndDestructionIsLocal()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(TerrainTestAssets.Root + "/Maps/GreypineTest.asset");
            var source = new TerrainBlueprintSource(asset.ReadBlueprint(), asset.Definition.LoadGameplayCatalog().Tiles);
            var creation = ARDMapController.CreateAsync(asset.Definition,
                new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false, maximumInitializationCells: 131072, chunkSource: source));
            while (!creation.IsCompleted) yield return null;
            Assert.That(creation.Exception, Is.Null);
            var map = creation.Result;
            try
            {
                var loading = map.LoadRegionAsync(map.Descriptor.Bounds);
                while (!loading.IsCompleted) yield return null;
                Assert.That(loading.Exception, Is.Null);
                var shown = map.ShowRegion(map.Descriptor.Bounds);
                var waiting = map.WhenPresentedAsync(shown);
                for (int i = 0; i < 1500 && !waiting.IsCompleted; i++) { map.Tick(); yield return null; }
                Assert.That(waiting.IsCompleted, Is.True);
                Assert.That(waiting.Exception, Is.Null);
                waiting.Result.RequirePresentedOrSuperseded();
                long before = map.Renderer.CommittedBuilds;
                for (int i = 0; i < 120; i++) map.Tick();
                Assert.That(map.Renderer.CommittedBuilds, Is.EqualTo(before));
                var target = new CellCoord(120, -110);
                map.SetTileType(target, map.Tiles.ByKey("slate"));
                map.Tick();
                for (int i = 0; i < 1500 && (map.Renderer.QueueCount > 0 || map.Renderer.InFlightCount > 0); i++) { map.Tick(); yield return null; }
                before = map.Renderer.CommittedBuilds;
                var receipt = map.ClearTile(target); var changed = map.WhenPresentedAsync(receipt, PresentationScope.VisibleNow());
                for (int i = 0; i < 1500 && !changed.IsCompleted; i++) { map.Tick(); yield return null; }
                Assert.That(changed.IsCompleted, Is.True); Assert.That(changed.Exception, Is.Null);
                changed.Result.RequirePresentedOrSuperseded();
                Assert.That(map.Renderer.CommittedBuilds - before, Is.InRange(1, 4));
                Capture(map.Root);
            }
            finally { map.Dispose(); }
        }

        private static void Capture(GameObject root)
        {
            var cameraObject = new GameObject("Terrain QA Camera"); var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(1280, 768, 24); var image = new Texture2D(1280, 768, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographic = true; camera.orthographicSize = 96; camera.aspect = 1280f / 768;
                camera.transform.position = new Vector3(159.5f, -95.5f, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.094f, .19f, .15f);
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 768), 0, 0); image.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/terrain"));
                Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, "unity-terrain.png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
        }
    }
}
