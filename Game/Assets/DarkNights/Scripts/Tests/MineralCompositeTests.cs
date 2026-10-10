using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Networking;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>正式背景、前景和矿层的组合实绘检查；固定同一地图、相机和矿格，只比较矿占用变化，排除人物动画造成的假通过。</summary>
    public sealed class MineralCompositeTests
    {
        [UnityTest]
        public IEnumerator ExposedMineralChangesActualCompositePixels() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var flow = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            var preset = QuickTestPreset.Create(QuickTestPreset.EmbeddedMineralsId, flow);
            var map = PlanetTerrainGenerator.GenerateCandidate(preset.Planet, preset.Seed,
                "4d7c4817f85a4a4ca48a41ba1ece3bb8", flow.FreezeCaveMap(), pipeline: flow.FreezeModifiers());
            var profile = TerrainProfileConfig.Resolve();
            var style = profile.CaveStyle as CaveTerrainStyle;
            var definition = profile.Definition;
            var layout = new LevelLayout(5120, RuleScenario.Layout().GroundY, 380, 770, 900, 568,
                new[] { new PlacementDefinition("ship", 568) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                randomTerrain: true, expedition: true);
            var session = scope.NewWorld(RuleScenario.Catalog(), layout, false,
                terrain: value => new SessionTerrain(value.Context, definition.LoadGameplayCatalog(), map, definition: definition), quickTest: preset);
            using var authority = new DarkNights.Runtime.Session.SessionAuthority(session);
            var mineralMap = session.Terrain.Minerals; var rules = mineralMap.Rules;
            var foregroundMap = session.Terrain.Map; var foregroundRules = foregroundMap.Rules;
            var foregroundReplica = TerrainMapNetworking.CreateReplica(foregroundRules.Business.Gameplay,
                definition.AuthoringSourceDigest, foregroundRules.Business);
            using var foregroundStream = TerrainMapNetworking.OpenStream(foregroundMap,
                TerrainMapNetworking.Handshake(foregroundMap, foregroundRules.Business.Gameplay, definition.AuthoringSourceDigest),
                2, _ => true, () => 1);
            void FlushForeground()
            { byte[] packet; while ((packet = foregroundStream.Dequeue()) != null) foregroundReplica.ReceivePacket(packet); foregroundStream.Acknowledge(foregroundReplica.CommitId); }
            var replica = TerrainMapNetworking.CreateReplica(rules.Business.Gameplay, rules.Definition.AuthoringSourceDigest, rules.Business);
            using var stream = new MapInterestService(mineralMap, mineralMap.Tiles,
                new MapHandshake(mineralMap.Descriptor, rules.Business.ContentDigest, rules.Definition.AuthoringSourceDigest,
                    rules.Business.Gameplay.Definitions.Select(value => value.Identity.Guid).ToArray()),
                1, _ => true, () => mineralMap.CommitId, () => 1);
            void Flush() { byte[] packet; while ((packet = stream.Dequeue()) != null) replica.ReceivePacket(packet); stream.Acknowledge(replica.CommitId); }
            var region = SessionMineralNetwork.Around(88 * 16, TerrainMiningGeometry.CenterHeight(-71));
            stream.Subscribe(MineralRegionReadiness.Subscription(region, mineralMap.Descriptor.Bounds)); Flush();
            foregroundStream.Subscribe(MineralRegionReadiness.Subscription(region, foregroundMap.Descriptor.Bounds)); FlushForeground();
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Composite mineral regression"); SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.localScale = Vector3.one * .16f;
            var cameraRoot = new GameObject("Composite camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = .96f; camera.allowHDR = false; camera.allowMSAA = false;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.position = root.transform.TransformPoint(new Vector3(88, -71, -60));
            var target = new RenderTexture(512, 320, 16, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            var image = new Texture2D(512, 320, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            var view = root.AddComponent<TerrainPreview>(); view.ViewCamera = camera; view.SurfaceSky = true; view.UseMineralReplica = true;
            view.LocalRegion = region;
            var source = new TerrainReplicaSource(foregroundReplica);
            foregroundReplica.Applied += view.NotifyReplicaChanged;
            string evidence = Path.GetFullPath("../artifacts/mineral-composite/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") +
                "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(evidence);
            try
            {
                view.ShowCaveReplica(definition, style, source,
                    new WorldIdentity(StableGuid.Parse(map.WorldId), 1), map.Background);
                double deadline = EditorApplication.timeSinceStartup + 90;
                while ((!view.Ready || view.MineralInputBatches < 1) && view.LastError == null && EditorApplication.timeSinceStartup < deadline)
                { view.SetMineralReplica(replica, region, true); view.TickFromEditor(); camera.Render(); await UniTask.Yield(); }
                Assert.That(view.LastError, Is.Null); Assert.That(view.Ready, Is.True);
                AssertRockCacheMatches(view);
                Assert.That(view.LoadedTerrainChunks, Is.EqualTo(25));
                int backgroundBuilds = view.BackgroundBuildCount;
                var staticBackground = TerrainVisualTestScope.Read<object>(TerrainVisualTestScope.Read<CaveVisualSource>(view, "caveSource"), "staticBackground");
                var backgroundPages = TerrainVisualTestScope.Read<IDictionary>(staticBackground, "pages");
                var initialBackgroundPages = new Dictionary<object, object>();
                foreach (DictionaryEntry page in backgroundPages) initialBackgroundPages.Add(page.Key, page.Value);
                var mineralHost = root.GetComponentInChildren<MineralLayerView>();
                foreach (var destination in new[] { SessionMapRegion.Around(4000, -1200), region })
                {
                    stream.Subscribe(MineralRegionReadiness.Subscription(destination, mineralMap.Descriptor.Bounds)); Flush();
                    view.SetMineralReplica(replica, destination, false);
                    Assert.That(view.Ready, Is.False, "新矿层区域数据未确认时不能沿用旧Ready。");
                    Assert.That(root.GetComponentInChildren<MineralLayerView>(), Is.SameAs(mineralHost),
                        "同世界矿层换代必须保留原宿主，不能出现整层销毁空窗。");
                    Assert.That(mineralHost.gameObject.activeInHierarchy, Is.True);
                    foregroundStream.Subscribe(MineralRegionReadiness.Subscription(destination, foregroundMap.Descriptor.Bounds)); FlushForeground();
                    view.TickFromEditor();
                    Assert.That(view.RefreshingReplica, Is.True, "保留旧画面时仍须等待新流的完整基线，不能提前确认 Ready。");
                    Assert.That(view.Ready, Is.False);
                    Assert.That(root.transform.Find("Cave distant wall").gameObject.activeInHierarchy, Is.True,
                        "同世界换区不能隐藏背景并露出相机底色。");
                    // 与正式角色兴趣范围一致：目标基线到达后镜头随目标区移动，返回时恢复原矿格取景。
                    camera.transform.position = root.transform.TransformPoint(new Vector3(
                        destination.Equals(region) ? 88 : destination.MinU + destination.Width * .5f,
                        destination.Equals(region) ? -71 : destination.MinV + destination.Height * .5f, -60));
                    view.SetReplicaRegion(foregroundReplica, destination, true);
                    view.SetMineralReplica(replica, destination, true);
                    while (!view.Ready && view.LastError == null && EditorApplication.timeSinceStartup < deadline)
                    { view.SetMineralReplica(replica, destination, true); view.TickFromEditor(); camera.Render(); await UniTask.Yield(); }
                    Assert.That(view.LastError, Is.Null); Assert.That(view.Ready, Is.True);
                    Assert.That(root.GetComponentInChildren<MineralLayerView>(), Is.SameAs(mineralHost));
                    AssertRockCacheMatches(view);
                    Assert.That(view.LoadedTerrainChunks, Is.LessThanOrEqualTo(25));
                    Assert.That(TerrainVisualTestScope.Read<object>(TerrainVisualTestScope.Read<CaveVisualSource>(view, "caveSource"), "staticBackground"),
                        Is.SameAs(staticBackground), "局部换区不得重建静态背景缓存。");
                    foreach (var page in initialBackgroundPages)
                        Assert.That(backgroundPages[page.Key], Is.SameAs(page.Value), "已有背景页必须复用，不能因流换代重新烘焙。");
                    // 镜头进入远区允许首次构建新页；构建增量只能等于新增页数，返回原区不得重烘旧页。
                    Assert.That(view.BackgroundBuildCount, Is.EqualTo(backgroundBuilds + backgroundPages.Count - initialBackgroundPages.Count));
                }
                Assert.That(foregroundReplica.Read(new CellCoord(280, -150)).State, Is.EqualTo(GridSampleState.Unknown));
                Capture(camera, target, image); var before = image.GetPixels32();
                File.WriteAllBytes(Path.Combine(evidence, "before.png"), image.EncodeToPNG());
                var renderers = root.GetComponentInChildren<MineralLayerView>().GetComponentsInChildren<Renderer>(true);
                var orders = renderers.Select(value => value.sortingOrder).ToArray();
                var sweep = new Dictionary<int, Color32[]>();
                foreach (int offset in new[] { 10, 20, 30 })
                {
                    for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = orders[i] + offset;
                    Capture(camera, target, image); sweep.Add(offset, image.GetPixels32());
                    File.WriteAllBytes(Path.Combine(evidence, "order-plus-" + offset + ".png"), image.EncodeToPNG());
                }
                for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = orders[i];
                var hit = typeof(MineralMapAuthority).GetMethod("StageHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                session.Mutations.Run(() =>
                {
                    foreach (var deposit in map.Deposits) foreach (var cell in deposit.Cells)
                        for (int i = 0; i < cell.Capacity; i++) hit.Invoke(mineralMap, new object[] { new CellCoord(cell.U, cell.V), 40, 0 });
                    return true;
                });
                stream.Publish(); Flush();
                while ((!view.Ready || view.MineralInputBatches < 2) && view.LastError == null && EditorApplication.timeSinceStartup < deadline)
                { view.SetMineralReplica(replica, region, true); view.TickFromEditor(); camera.Render(); await UniTask.Yield(); }
                Assert.That(view.LastError, Is.Null); Assert.That(view.Ready, Is.True);
                Capture(camera, target, image); var after = image.GetPixels32();
                File.WriteAllBytes(Path.Combine(evidence, "after.png"), image.EncodeToPNG());
                int changed = before.Zip(after, (a, b) => !a.Equals(b)).Count(value => value);
                int Difference(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
                int visible = before.Zip(after, Difference).Count(value => value > 20);
                var samples = string.Join(",", sweep.Select(pair => "\"" + pair.Key + "\":" + pair.Value.Zip(after, Difference).Count(value => value > 20)));
                File.WriteAllText(Path.Combine(evidence, "pixels.json"), "{\"changedPixels\":" + changed + ",\"visiblePixels\":" + visible + ",\"orderSweep\":{" + samples + "}}");
                Assert.That(visible, Is.GreaterThan(10), "矿层必须在组合画面中清晰可辨，不能把1级颜色变化记为画面通过");
                stream.Revoke(); Flush();
                Assert.That(mineralHost == null || !mineralHost.gameObject.activeInHierarchy, Is.True,
                    "撤权仍须立即撤除矿层，不能保留旧授权画面。");
                Assert.That(view.Ready, Is.False, "撤权后不能沿用矿层已绘制的Ready。");
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraRoot);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        });

        private static void Capture(Camera camera, RenderTexture target, Texture2D image)
        { camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 512, 320), 0, 0); image.Apply(); }

        private static void AssertRockCacheMatches(TerrainPreview view)
        {
            var visual = TerrainVisualTestScope.Read<CaveVisualSource>(view, "caveSource");
            var rock = TerrainVisualTestScope.Read<object>(visual, "localRockSurface");
            var geometry = TerrainVisualTestScope.Read<object>(rock, "geometry");
            var cells = TerrainVisualTestScope.Read<Color32[]>(visual, "cells");
            CollectionAssert.AreEqual(cells.Select(c => c.r).ToArray(), TerrainVisualTestScope.Read<byte[]>(geometry, "materials"),
                "后加载的前景格也必须进入岩壁烘焙缓存，不能保留洞穴下方的空白条带。");
            CollectionAssert.AreEqual(cells.Select(c => c.g).ToArray(), TerrainVisualTestScope.Read<byte[]>(geometry, "shapes"));
        }
    }
}
