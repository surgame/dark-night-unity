using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnyRules.Next;
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
        public IEnumerator ExposedMineralChangesActualCompositePixels()
        {
            var flow = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            var preset = QuickTestPreset.Create(QuickTestPreset.EmbeddedMineralsId, flow);
            var map = PlanetTerrainGenerator.GenerateCandidate(preset.Planet, preset.Seed,
                "4d7c4817f85a4a4ca48a41ba1ece3bb8", flow.FreezeCaveMap(), pipeline: flow.FreezeModifiers());
            var profile = TerrainProfileConfig.Resolve();
            var style = profile.CaveStyle as CaveTerrainStyle;
            var definition = profile.Definition;
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
            var view = root.AddComponent<TerrainPreview>(); view.ViewCamera = camera; view.SurfaceSky = true;
            var minerals = map.Deposits.Select((deposit, index) => new MineralDepositViewData(index + 1,
                (deposit.X + .5f) * 16, deposit.Y, deposit.RoomKind, deposit.Rarity,
                deposit.MineralKind, 40, 1, 1, deposit.Cells.Select(cell =>
                    new MineralCellViewData(cell.U, cell.V, cell.Capacity, cell.Capacity, 40, 1)).ToArray())).ToArray();
            var source = new TerrainBlueprintSource(map.Blueprint(), definition.LoadGameplayCatalog().Tiles);
            string evidence = Path.GetFullPath("../artifacts/embedded-ore-development-20261004/p0/composite-r3");
            Directory.CreateDirectory(evidence);
            try
            {
                view.ShowCaveReplica(definition, style, source,
                    new WorldIdentity(StableGuid.Parse(map.WorldId), 1), map.Background);
                double deadline = EditorApplication.timeSinceStartup + 90;
                while ((!view.Ready || view.MineralInputBatches < 2) && view.LastError == null && EditorApplication.timeSinceStartup < deadline)
                { view.SetMinerals(minerals); view.TickFromEditor(); camera.Render(); yield return null; }
                Assert.That(view.LastError, Is.Null); Assert.That(view.Ready, Is.True);
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
                view.SetMinerals(Array.Empty<MineralDepositViewData>());
                while ((!view.Ready || view.MineralInputBatches < 3) && view.LastError == null && EditorApplication.timeSinceStartup < deadline)
                { view.TickFromEditor(); camera.Render(); yield return null; }
                Assert.That(view.LastError, Is.Null); Assert.That(view.Ready, Is.True);
                Capture(camera, target, image); var after = image.GetPixels32();
                File.WriteAllBytes(Path.Combine(evidence, "after.png"), image.EncodeToPNG());
                int changed = before.Zip(after, (a, b) => !a.Equals(b)).Count(value => value);
                int Difference(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
                int visible = before.Zip(after, Difference).Count(value => value > 20);
                var samples = string.Join(",", sweep.Select(pair => "\"" + pair.Key + "\":" + pair.Value.Zip(after, Difference).Count(value => value > 20)));
                File.WriteAllText(Path.Combine(evidence, "pixels.json"), "{\"changedPixels\":" + changed + ",\"visiblePixels\":" + visible + ",\"orderSweep\":{" + samples + "}}");
                Assert.That(visible, Is.GreaterThan(10), "矿层必须在组合画面中清晰可辨，不能把1级颜色变化记为画面通过");
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraRoot);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D image)
        { camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 512, 320), 0, 0); image.Apply(); }
    }
}
