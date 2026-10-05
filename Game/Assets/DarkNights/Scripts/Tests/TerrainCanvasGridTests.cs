using System.Reflection;
using DarkNights.Editor.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>工作台逻辑／渲染叠图与拆填拾取回归；覆盖半格边界、缩放平移和工具切换，不创建地图或写作者资产。</summary>
    public sealed class TerrainCanvasGridTests
    {
        [Test]
        public void SelectingEditorBrushDefaultsToLogicButManualRenderChoicePersists()
        {
            var canvas = new TerrainStylePreviewCanvas { ShowGrid = false };
            canvas.ActiveTool = TerrainStylePreviewTool.Dig;
            Assert.That(canvas.ShowGrid, Is.True); Assert.That(canvas.GridMode, Is.EqualTo(TerrainGridMode.Logical));
            canvas.GridMode = TerrainGridMode.Render;
            canvas.ActiveTool = TerrainStylePreviewTool.Dig;
            Assert.That(canvas.GridMode, Is.EqualTo(TerrainGridMode.Render));
            canvas.ActiveTool = TerrainStylePreviewTool.Fill;
            Assert.That(canvas.GridMode, Is.EqualTo(TerrainGridMode.Logical));
            canvas.GridMode = TerrainGridMode.Render; canvas.ActiveTool = TerrainStylePreviewTool.Pan;
            Assert.That(canvas.GridMode, Is.EqualTo(TerrainGridMode.Render));
        }

        [Test]
        public void RuntimeBrushDefaultsToLogicWithoutOverridingManualChoiceEachFrame()
        {
            var root = new GameObject("Runtime grid choice regression"); root.SetActive(false);
            try
            {
                var panel = root.AddComponent<TerrainDebugPanel>(); panel.ShowGrid = false;
                panel.MapTool = 2;
                Assert.That(panel.ShowGrid, Is.True); Assert.That(panel.GridMode, Is.EqualTo(TerrainGridMode.Logical));
                panel.GridMode = TerrainGridMode.Render; panel.MapTool = 2;
                Assert.That(panel.GridMode, Is.EqualTo(TerrainGridMode.Render));
                panel.MapTool = 3; Assert.That(panel.GridMode, Is.EqualTo(TerrainGridMode.Logical));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(.5f)]
        [TestCase(1f)]
        [TestCase(4f)]
        public void BrushPicksNearestLogicalCenterAcrossHalfCellBoundaryAndGridModes(float zoom)
        {
            var image = new Texture2D(80, 80);
            try
            {
                var canvas = new TerrainStylePreviewCanvas(); canvas.SetZoom(zoom, new Vector2(7, -11), true);
                var area = new Rect(20, 30, 100, 100);
                var bounds = (Rect)typeof(TerrainStylePreviewCanvas).GetMethod("ImageBounds", BindingFlags.Instance |
                    BindingFlags.NonPublic).Invoke(canvas, new object[] { area, image });
                var pick = typeof(TerrainStylePreviewCanvas).GetMethod("CellAt", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var mode in new[] { TerrainGridMode.Logical, TerrainGridMode.Render })
                {
                    canvas.GridMode = mode;
                    Vector2Int? At(float x, float y) => (Vector2Int?)pick.Invoke(canvas, new object[] {
                        new Vector2(area.x + bounds.x + x * bounds.width / 10, area.y + bounds.y + y * bounds.height / 10), area, image });
                    Assert.That(At(2.49f, 4.49f), Is.EqualTo(new Vector2Int(2, 4)));
                    Assert.That(At(2.51f, 4.51f), Is.EqualTo(new Vector2Int(3, 5)));
                    Assert.That(At(0, 0), Is.EqualTo(Vector2Int.zero));
                    Assert.That(At(9.51f, 4), Is.Null, "不能把画面外缘选为越界逻辑格。");
                }
            }
            finally { Object.DestroyImmediate(image); }
        }
    }
}
