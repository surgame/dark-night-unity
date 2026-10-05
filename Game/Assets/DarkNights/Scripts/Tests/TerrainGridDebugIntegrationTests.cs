using System;
using System.Collections;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Editor;
using AnyRules.Next.Unity;
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
    /// <summary>真实两层表现宿主的只读调试接线；检查输入变化、四角来源、地图切换与退休，不改作者资产或正式会话。</summary>
    public sealed class TerrainGridDebugIntegrationTests
    {
        [UnityTest]
        public IEnumerator ForegroundAndMineralsShareDebuggerWithoutTransferringMapOwnership()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(TerrainTestAssets.Root + "/Maps/GreypineTest.asset");
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(MineralLayerAssets.DefinitionPath);
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Terrain grid debug regression");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(3, 7, 0); root.transform.localScale = Vector3.one * .16f;
            var camera = root.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = .8f;
            camera.transform.position = new Vector3(3 + 12 * .16f, 7 - 8 * .16f, -10);
            var foreground = root.AddComponent<TerrainPreview>(); foreground.ViewCamera = camera;
            foreground.LocalRegion = new GridBounds(0, -32, 32, 32);
            var world = new WorldIdentity(StableGuid.Parse("fa05ccab4c284b519d8d09e18ed7fc3d"), 1);
            var source = new TerrainBlueprintSource(asset.ReadBlueprint(), asset.Definition.LoadGameplayCatalog().Tiles);
            var mineralRoot = new GameObject("Minerals"); mineralRoot.transform.SetParent(root.transform, false);
            var minerals = mineralRoot.AddComponent<MineralLayerView>();
            var mineralSource = new MineralLayerInputSource(definition.LoadGameplayCatalog().Tiles);
            GridDebugWindow window = null;
            try
            {
                foreground.ShowReplica(asset.Definition, source, world);
                var opening = minerals.OpenAsync(definition, mineralSource, camera, world, region: foreground.LocalRegion);
                double deadline = EditorApplication.timeSinceStartup + 60;
                while ((!opening.IsCompleted || foreground.RefreshingReplica) && EditorApplication.timeSinceStartup < deadline)
                { foreground.TickFromEditor(); yield return null; }
                Assert.That(opening.IsCompleted, Is.True); opening.GetAwaiter().GetResult();
                Assert.That(foreground.LastError, Is.Null); Assert.That(minerals.LastError, Is.Null);
                var bridges = root.GetComponentsInChildren<GridDebugView>();
                Assert.That(bridges.Length, Is.EqualTo(2));
                var front = bridges.Single(view => view.Controller.Descriptor.Layer == 0);
                var ore = bridges.Single(view => view.Controller.Descriptor.Layer == 1);
                Assert.That(front.name, Does.StartWith("前景地形")); Assert.That(ore.name, Does.StartWith("矿层地形"));
                Assert.That(GridDebugWindow.LiveMaps(), Has.Member(front));
                Assert.That(GridDebugWindow.LiveMaps(), Has.Member(ore));
                Assert.That(ore.transform.TransformPoint(Vector3.zero), Is.EqualTo(mineralRoot.transform.position));
                var position = new CellCoord(12, -8);
                uint iron = definition.LoadGameplayCatalog().Tiles.ByKey("iron");
                mineralSource.Replace(new[] { new MapInputCell(position, new GridCell(iron)) }); minerals.Tick();
                Assert.That(ore.Controller.InspectCell(position).Terrain.Key.ToString(), Is.EqualTo("iron"));
                var visual = ore.Controller.InspectVisual(new VisualCoord(12, -8));
                Assert.That(visual.Corners.Select(value => value.Position), Does.Contain(position));
                Assert.That(visual.Recipe, Is.Not.Null);
                ulong commit = ore.Controller.Map.CommitId;
                window = GridDebugWindow.ShowForController(front.Controller, front.transform);
                window = GridDebugWindow.ShowForController(ore.Controller, ore.transform);
                Assert.That(window.Controller, Is.SameAs(ore.Controller));
                window.Close(); window = null;
                Assert.That(ore.Controller.IsDisposed, Is.False); Assert.That(front.Controller.IsDisposed, Is.False);
                Assert.That(ore.Controller.Map.CommitId, Is.EqualTo(commit), "只读窗口不得推进地图提交。");
                mineralSource.Replace(Array.Empty<MapInputCell>()); minerals.Tick();
                Assert.That(ore.Controller.InspectCell(position).Sample.State, Is.EqualTo(GridSampleState.Empty));
                window = GridDebugWindow.ShowForController(ore.Controller, ore.transform);
                var retirement = minerals.RetireAsync();
                while (!retirement.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(retirement.IsCompleted, Is.True); retirement.GetAwaiter().GetResult();
                typeof(GridDebugWindow).GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).Invoke(window, null);
                Assert.That(window.Controller, Is.Null, "退休地图不得继续保留借用绑定。");
                Assert.That(GridDebugWindow.LiveMaps(), Has.No.Member(ore));
                Assert.That(GridDebugWindow.LiveMaps(), Has.Member(front));
            }
            finally
            {
                if (window != null) window.Close();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
