using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>原生矿层固定样板的真实资产、四角规则与相机检查；只操作预览场景，验证混矿和空格而不启动业务会话。</summary>
    public sealed class MineralLayerTests
    {
        private static readonly string Evidence = Path.GetFullPath("../artifacts/mineral-map-migration-20261005/mineral-assets");

        [Test]
        public void ActualCatalogHasFourVariantsAndNoMixedTransitionOwnership()
        {
            var definition = MineralLayerAssets.Ensure();
            var catalog = definition.LoadRuntimeCatalog();
            Assert.That(catalog.VisualSets.Count(set => set.Variants.Variants.Count == 4), Is.EqualTo(75));
            var fill = AssetDatabase.LoadAssetAtPath<TileVisualSet>(MineralLayerAssets.Root + "/TransparentFill.asset");
            Assert.That(fill.Variants[0].Sprite.texture.GetPixels32().All(pixel => pixel.a == 0), Is.True);
            var world = new WorldIdentity(StableGuid.Parse("4d7c4817f85a4a4ca48a41ba1ece3bb8"), 1);
            var context = new RuleQueryContext(42, world.WorldId, 1, new VisualCoord(15, -8));
            var solver = new MultiTerrainSolver(catalog);
            var tiles = new uint[] { 0 }.Concat(MineralLayerAssets.Keys.Select(key => catalog.Gameplay.Tiles.ByKey(key))).ToArray();
            for (int code = 0; code < 1296; code++)
            {
                int value = code; var samples = new GridSample[4]; var ids = new uint[4];
                for (int corner = 0; corner < 4; corner++)
                { ids[corner] = tiles[value % 6]; samples[corner] = GridSample.FromCell(new GridCell(ids[corner])); value /= 6; }
                var recipe = solver.Solve(world, context, new CornerSamples(samples[0], samples[1], samples[2], samples[3]), 1);
                Assert.That(recipe.Status, Is.EqualTo(code == 0 ? RecipeStatus.Empty : RecipeStatus.Ready), "四角组合 " + code);
                Assert.That(recipe.Parts.All(part => part.Kind == RecipePartKind.Fill || part.Kind == RecipePartKind.TerrainSurface), Is.True);
                Assert.That(recipe.Parts.Count(part => part.Kind == RecipePartKind.TerrainSurface), Is.EqualTo(ids.Where(id => id != 0).Distinct().Count()));
                foreach (var part in recipe.Parts.Where(part => part.Kind == RecipePartKind.TerrainSurface))
                {
                    uint owner = catalog.Gameplay.Tiles.ByGuid(part.Key.TerrainGuid);
                    int mask = 0;
                    for (int corner = 0; corner < 4; corner++) if (ids[corner] == owner) mask |= 1 << corner;
                    var sprite = definition.VisualAssets.Single(binding => StableGuid.Parse(binding.ResourceId) == part.ResourceId).Sprite;
                    Assert.That(sprite.name, Does.Contain("_m" + mask + "_"), "求解必须选择该矿种的完整占用掩码");
                }
            }
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(definition);
            Assert.That(AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(MineralLayerAssets.DefinitionPath).LoadRuntimeCatalog().SourceDigest,
                Is.EqualTo(catalog.SourceDigest), "保存重开必须保留原生编译目录身份");
        }

        [Test]
        public void FrozenInputRejectsOverlapAndUnchangedOccupancyDoesNotAdvance()
        {
            var tiles = MineralLayerAssets.Ensure().LoadGameplayCatalog().Tiles;
            var source = new MineralLayerInputSource(tiles);
            var value = new MapInputCell(new CellCoord(4, -4), new GridCell(tiles.ByKey("iron")));
            source.Replace(new[] { value }); ulong before = source.SourceCommit;
            source.Replace(new[] { value }); Assert.That(source.SourceCommit, Is.EqualTo(before));
            Assert.Throws<ArgumentException>(() => source.Replace(new[] { value, value }));
            Assert.That(source.SourceCommit, Is.EqualTo(before), "坏候选不能推进当前输入");
            source.Replace(Array.Empty<MapInputCell>()); Assert.That(source.SourceCommit, Is.EqualTo(before + 1));
        }

        [UnityTest]
        public IEnumerator EmptyLocalViewportRequiresCameraDrawButNoNonemptyPage()
        {
            var definition = MineralLayerAssets.Ensure();
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Empty local mineral regression"); SceneManager.MoveGameObjectToScene(root, scene);
            var cameraRoot = new GameObject("Empty local camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 8; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(200, -100, -10);
            var target = new RenderTexture(128, 128, 16); target.Create(); camera.targetTexture = target;
            var layer = root.AddComponent<MineralLayerView>();
            try
            {
                var source = new MineralLayerInputSource(definition.LoadGameplayCatalog().Tiles);
                var opening = layer.OpenAsync(definition, source, camera,
                    new WorldIdentity(StableGuid.Parse("4d7c4817f85a4a4ca48a41ba1ece3bb8"), 1), region: new GridBounds(0, -96, 96, 96));
                double deadline = EditorApplication.timeSinceStartup + 30;
                while (!opening.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
                opening.GetAwaiter().GetResult(); layer.Tick();
                Assert.That(layer.LastError, Is.Null); Assert.That(layer.BuiltPages, Is.Zero);
                Assert.That(layer.NeedsPresentationDraw, Is.True); Assert.That(layer.Ready, Is.False);
                camera.Render(); Assert.That(layer.Ready, Is.True, "已知空视口完成绘制后可就绪，不能要求存在非空矿页。");
                camera.transform.position = new Vector3(88, -70, -10);
                layer.Tick();
                while (!layer.NeedsPresentationDraw && layer.LastError == null && EditorApplication.timeSinceStartup < deadline)
                { layer.Tick(); yield return null; }
                Assert.That(layer.LastError, Is.Null); Assert.That(layer.NeedsPresentationDraw, Is.True, layer.WaitReason);
                camera.Render(); Assert.That(layer.Ready, Is.True, "跨局部区边缘的视口不能让规则等待永久Unknown。");
            }
            finally
            {
                camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraRoot);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        [UnityTest]
        public IEnumerator ActualPagesRenderMixedMineralsAtBothScalesAndEmptyLayerBecomesReady()
        {
            var definition = MineralLayerAssets.Ensure();
            var tiles = definition.LoadGameplayCatalog().Tiles;
            foreach (float scale in new[] { 1f, .16f })
            {
                var scene = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("Native embedded mineral sample"); SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = new Vector3(3, 7, 0); root.transform.localScale = Vector3.one * scale;
                var cameraRoot = new GameObject("Native mineral sample camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
                var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.allowHDR = false; camera.allowMSAA = false;
                camera.orthographic = true; camera.orthographicSize = 8 * scale;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.transform.position = root.transform.TransformPoint(new Vector3(12, -8, -10 / scale));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(40, 31, 23, 255);
                var target = new RenderTexture(512, 320, 16, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
                var image = new Texture2D(512, 320, TextureFormat.RGBA32, false);
                var source = new MineralLayerInputSource(tiles);
                var layer = root.AddComponent<MineralLayerView>();
                var previous = RenderTexture.active;
                try
                {
                    var opening = layer.OpenAsync(definition, source, camera,
                        new WorldIdentity(StableGuid.Parse("4d7c4817f85a4a4ca48a41ba1ece3bb8"), 1), ambient: 1);
                    double deadline = UnityEditor.EditorApplication.timeSinceStartup + 90;
                    while (!opening.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                    Assert.That(opening.IsCompleted, Is.True); opening.GetAwaiter().GetResult();
                    while (!layer.NeedsPresentationDraw && layer.LastError == null && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                    { layer.Tick(); yield return null; }
                    Assert.That(layer.LastError, Is.Null); Assert.That(layer.NeedsPresentationDraw, Is.True);
                    Assert.That(layer.Ready, Is.False, "初始空基线的0游标不代表已经绘制");
                    camera.Render(); Assert.That(layer.Ready, Is.True);
                    source.Replace(Sample(tiles));
                    while (!layer.Ready && layer.LastError == null && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                    { layer.Tick(); camera.Render(); yield return null; }
                    Assert.That(layer.LastError, Is.Null); Assert.That(layer.Ready, Is.True);
                    Capture(camera, target, image);
                    Color32 ground = Pixel(camera, root.transform, image, 2, -2);
                    Color32 iron = Pixel(camera, root.transform, image, 7, -7);
                    Color32 gold = Pixel(camera, root.transform, image, 13, -7);
                    Assert.That(iron, Is.Not.EqualTo(ground), "实际矿页不能是隐藏黑图");
                    Assert.That(gold, Is.Not.EqualTo(ground)); Assert.That(gold, Is.Not.EqualTo(iron));
                    Assert.That(Pixel(camera, root.transform, image, 4.5f, -12.5f), Is.EqualTo(ground), "银矿仅斜对角不能在中心连桥");
                    Assert.That(Pixel(camera, root.transform, image, 9.5f, -12.5f), Is.EqualTo(ground), "钻石仅斜对角不能在中心连桥");
                    Directory.CreateDirectory(Evidence);
                    File.WriteAllBytes(Path.Combine(Evidence, "mixed-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"), image.EncodeToPNG());
                    long batches = layer.InputBatches; ulong before = layer.InstalledCommit;
                    source.Replace(Sample(tiles)); layer.Tick();
                    Assert.That(layer.InputBatches, Is.EqualTo(batches), "相同占用不能反复重建");
                    source.Replace(Array.Empty<MapInputCell>()); layer.Tick();
                    Assert.That(layer.Ready, Is.False, "新输入未经绘制不能提前Ready");
                    deadline = UnityEditor.EditorApplication.timeSinceStartup + 60;
                    while (!layer.Ready && layer.LastError == null && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                    { layer.Tick(); camera.Render(); yield return null; }
                    Assert.That(layer.LastError, Is.Null); Assert.That(layer.Ready, Is.True, "全空矿层也必须Ready");
                    Assert.That(layer.InstalledCommit, Is.GreaterThan(before)); Assert.That(layer.PresentedCommit, Is.EqualTo(layer.InstalledCommit));
                    Capture(camera, target, image);
                    Assert.That(Pixel(camera, root.transform, image, 7, -7), Is.EqualTo(ground));
                    Assert.That(Pixel(camera, root.transform, image, 13, -7), Is.EqualTo(ground));
                    File.WriteAllBytes(Path.Combine(Evidence, "empty-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"), image.EncodeToPNG());
                }
                finally
                {
                    var retirement = layer.RetireAsync();
                    if (retirement.IsCompleted) retirement.GetAwaiter().GetResult();
                    RenderTexture.active = previous; camera.targetTexture = null;
                    EditorSceneManager.ClosePreviewScene(scene); target.Release();
                    UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
                }
            }
        }

        private static MapInputCell[] Sample(TileCatalog catalog)
        {
            var result = new List<MapInputCell>();
            void Add(int u, int v, string kind) => result.Add(new MapInputCell(new CellCoord(u, v), new GridCell(catalog.ByKey(kind))));
            for (int u = 4; u <= 9; u++) for (int v = -9; v <= -5; v++) if (u != 6 || v != -6) Add(u, v, "iron");
            for (int u = 10; u <= 15; u++) for (int v = -9; v <= -5; v++) Add(u, v, "gold");
            for (int u = 16; u <= 20; u++) for (int v = -8; v <= -5; v++) Add(u, v, "copper");
            Add(4, -12, "silver"); Add(5, -13, "silver"); Add(9, -12, "diamond"); Add(10, -13, "diamond");
            return result.ToArray();
        }
        private static void Capture(Camera camera, RenderTexture target, Texture2D image)
        { camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 512, 320), 0, 0); image.Apply(); }
        private static Color32 Pixel(Camera camera, Transform root, Texture2D image, float u, float v)
        {
            var point = camera.WorldToScreenPoint(root.TransformPoint(new Vector3(u, v, 0)));
            return image.GetPixel((int)point.x, (int)point.y);
        }
    }
}
