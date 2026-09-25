using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
                new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false, maximumInitializationCells: 131072, chunkSource: source,
                    sourceDrivenInputs: true, layers: new GridLayerConfiguration(GridEditability.ReadOnly, GridRenderPolicy.LiveRules, GridBusinessCapability.TileTypeOnly)));
            while (!creation.IsCompleted) yield return null;
            Assert.That(creation.Exception, Is.Null);
            var map = creation.Result;
            try
            {
                var loading = map.LoadRegionAsync(map.Descriptor.Bounds);
                while (!loading.IsCompleted) yield return null;
                Assert.That(loading.Exception, Is.Null);
                MapInputBatch baseline = null;
                source.InputChanged += batch => baseline = batch; source.PublishInitialBaseline();
                map.InstallSourceInput(baseline);
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
                var fill = new MapInputBatch(map.World, 1, 0, 0, 1, MapInputBatchKind.Delta,
                    new[] { new MapInputCell(target, new GridCell(map.Tiles.ByKey("slate"))) });
                map.InstallSourceInput(fill);
                map.Tick();
                for (int i = 0; i < 1500 && (map.Renderer.QueueCount > 0 || map.Renderer.InFlightCount > 0); i++) { map.Tick(); yield return null; }
                before = map.Renderer.CommittedBuilds;
                var dig = new MapInputBatch(map.World, 1, 0, 0, 2, MapInputBatchKind.Delta,
                    new[] { new MapInputCell(target, default) });
                var receipt = map.InstallSourceInput(dig).Receipt;
                var changed = map.WhenPresentedAsync(receipt, PresentationScope.VisibleNow());
                for (int i = 0; i < 1500 && !changed.IsCompleted; i++) { map.Tick(); yield return null; }
                Assert.That(changed.IsCompleted, Is.True); Assert.That(changed.Exception, Is.Null);
                changed.Result.RequirePresentedOrSuperseded();
                Assert.That(map.Renderer.CommittedBuilds - before, Is.InRange(1, 4));
                Capture(map.Root);
            }
            finally { map.Dispose(); }
        }

        [UnityTest]
        public IEnumerator PreviewKeepsCameraPagesVisibleAcrossDirectionChanges()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(TerrainTestAssets.Root + "/Maps/GreypineTest.asset");
            var source = new TerrainBlueprintSource(asset.ReadBlueprint(), asset.Definition.LoadGameplayCatalog().Tiles);
            var creation = ARDMapController.CreateAsync(asset.Definition,
                new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false, maximumInitializationCells: 131072, chunkSource: source));
            while (!creation.IsCompleted) yield return null;
            Assert.That(creation.Exception, Is.Null);
            var map = creation.Result;
            var cameraObject = new GameObject("Terrain pan regression");
            cameraObject.SetActive(false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 8; camera.aspect = 1;
            var terrainObject = new GameObject("Terrain static host regression");
            terrainObject.SetActive(false);
            var preview = terrainObject.AddComponent<TerrainPreview>();
            preview.ViewCamera = camera;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var controllerField = typeof(TerrainPreview).GetField("controller", flags);
            var update = typeof(TerrainPreview).GetMethod("Update", flags);
            controllerField.SetValue(preview, map);
            try
            {
                var loading = map.LoadRegionAsync(map.Descriptor.Bounds);
                while (!loading.IsCompleted) yield return null;
                Assert.That(loading.Exception, Is.Null);
                // Left/down exposed the missing shared page. Include reverse, diagonal and map edges.
                var positions = new[] { new Vector2(120, -104), new Vector2(104, -104),
                    new Vector2(120, -104), new Vector2(136, -104), new Vector2(120, -104),
                    new Vector2(120, -120), new Vector2(120, -104), new Vector2(120, -88),
                    new Vector2(104, -120), new Vector2(0, 0), new Vector2(319, -191), new Vector2(120, -104) };
                foreach (var position in positions)
                {
                    camera.transform.position = new Vector3(position.x, position.y, -10);
                    update.Invoke(preview, null);
                    for (int i = 0; i < 1500 && (map.Renderer.QueueCount > 0 || map.Renderer.InFlightCount > 0); i++)
                    { update.Invoke(preview, null); yield return null; }
                    Assert.That(map.Renderer.QueueCount + map.Renderer.InFlightCount, Is.Zero);
                    Assert.That(preview.LastError, Is.Null);
                    // Check the real frustum, independently of the preview's padded logical strips.
                    int size = map.Descriptor.PageSize;
                    var bounds = map.Descriptor.Bounds;
                    int left = Mathf.FloorToInt(Mathf.Max(bounds.MinU - 1, position.x - 8) / size);
                    int right = Mathf.FloorToInt(Mathf.Min((float)bounds.MaxUExclusive - .01f, position.x + 7.99f) / size);
                    int bottom = Mathf.FloorToInt(Mathf.Max(bounds.MinV - 1, position.y - 8) / size);
                    int top = Mathf.FloorToInt(Mathf.Min((float)bounds.MaxVExclusive - .01f, position.y + 7.99f) / size);
                    for (int v = bottom; v <= top; v++) for (int u = left; u <= right; u++)
                    {
                        var page = new PageCoord(u, v);
                        Assert.That(map.Renderer.TryGetPageInfo(page, out var info), Is.True, position + " missing " + page);
                        Assert.That(info.HasVisibleOutput, Is.True, position + " hidden " + page);
                    }
                    long built = preview.BuiltPages;
                    for (int i = 0; i < 120; i++) update.Invoke(preview, null);
                    Assert.That(preview.BuiltPages, Is.EqualTo(built), "Static preview rebuilt after " + position);
                }
            }
            finally
            {
                controllerField.SetValue(preview, null);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(terrainObject); map.Dispose();
            }
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
