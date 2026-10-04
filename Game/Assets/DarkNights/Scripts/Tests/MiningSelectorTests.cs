using System.Reflection;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using GameCore.Objects.NetworkStates;
using MemoryPack;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Tests
{
    /// <summary>采矿选择器的逻辑格与原生 UGUI 几何回归；只创建隔离控件，不修改人工资源或游戏状态。</summary>
    public sealed class MiningSelectorTests
    {
        [Test]
        public void ReachIsFrozenAndChangesToolFingerprint()
        {
            var config = new MiningToolConfig();
            Assert.That(config.Reach, Is.EqualTo(64));
            var frozen = config.Freeze();
            string fingerprint = config.Fingerprint();
            config.Reach = 32;
            Assert.That(frozen.Reach, Is.EqualTo(64));
            Assert.That(config.Freeze().Reach, Is.EqualTo(32));
            Assert.That(config.Fingerprint(), Is.Not.EqualTo(fingerprint));
        }

        [Test]
        public void ToolTargetsAndMaterialsAreFrozenAndFingerprintChanges()
        {
            var config = new MiningToolConfig();
            var frozen = config.Freeze();
            string identity = config.Fingerprint();
            config.Targets |= DarkNights.Core.Config.MiningTargetKinds.MineralDeposit;
            Assert.That(config.Fingerprint(), Is.Not.EqualTo(identity));
            Assert.That(frozen.BlockReason(HeroMiningTargetKind.MineralDeposit, "iron"), Is.Not.Empty);
            config.AllMaterials = false; config.Materials = new[] { "iron" };
            frozen = config.Freeze(); config.Materials[0] = "gold";
            Assert.That(frozen.BlockReason(HeroMiningTargetKind.MineralDeposit, "iron"), Is.Empty);
            Assert.That(frozen.BlockReason(HeroMiningTargetKind.MineralDeposit, "gold"), Is.Not.Empty);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DepositMiningRequirementsRoundTripAndFreeze(int level)
        {
            var source = new MineralDepositViewData(4, 2200, 72, "shelf", "common", "iron", 40, 1, level,
                new[] { new MineralCellViewData(138, -72, 80, 79, 40, 1) });
            var decoded = MemoryPackSerializer.Deserialize<MineralDepositWire>(MemoryPackSerializer.Serialize(MineralDepositWire.From(source)));
            var frozen = decoded.Freeze(); decoded.RequiredMiningLevel = level + 1;
            Assert.That(frozen.RequiredMiningLevel, Is.EqualTo(level));
            Assert.That(frozen.Remaining, Is.EqualTo(79));
        }

        [TestCase(-17, -42)]
        [TestCase(0, 0)]
        [TestCase(18, -80)]
        public void CentersRoundTripToAuthorityCells(int cellU, int cellV)
        {
            Assert.That(TerrainMiningGeometry.CellU(TerrainMiningGeometry.CenterX(cellU)), Is.EqualTo(cellU));
            Assert.That(TerrainMiningGeometry.CellV(TerrainMiningGeometry.CenterHeight(cellV)), Is.EqualTo(cellV));
        }

        [TestCase(-8.01f, -1)]
        [TestCase(-8f, 0)]
        [TestCase(7.99f, 0)]
        [TestCase(8f, 1)]
        public void HorizontalEdgesUseCenteredCells(float position, int expected) =>
            Assert.That(TerrainMiningGeometry.CellU(position), Is.EqualTo(expected));

        [Test]
        public void ReachIncludesBoundaryButNotNextCell()
        {
            Assert.That(TerrainMiningGeometry.WithinReach(0, PlayableTerrain.OriginY, 4, 0, 64), Is.True);
            Assert.That(TerrainMiningGeometry.WithinReach(0, PlayableTerrain.OriginY, 5, 0, 64), Is.False);
        }

        [Test]
        public void StandingBesideOrAboveCellUsesSurfaceReachInsteadOfCenter()
        {
            float center = TerrainMiningGeometry.CenterHeight(-39);
            Assert.That(TerrainMiningGeometry.WithinReach(1296, center + 1, 82, -39, 16), Is.True);
            Assert.That(TerrainMiningGeometry.WithinReach(1312, center + 17, 82, -39, 16), Is.True);
            Assert.That(TerrainMiningGeometry.WithinReach(1287.9f, center, 82, -39, 16), Is.False);
        }

        [TestCase(1, 1f)]
        [TestCase(1, 2f)]
        [TestCase(2, 1f)]
        [TestCase(2, 2f)]
        public void GridHasBrightCenterAndFadedNonBlockingSurround(int radius, float scale)
        {
            var root = new GameObject("Isolated mining canvas", typeof(Canvas));
            var cameraObject = new GameObject("Isolated mining camera", typeof(Camera));
            var overlay = new GameObject("Isolated mining selector", typeof(RectTransform));
            Mesh mesh = null;
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.scaleFactor = scale;
                overlay.transform.SetParent(root.transform, false);
                var graphic = overlay.AddComponent<TerrainMiningSelectorGraphic>();
                graphic.rectTransform.anchorMin = Vector2.zero; graphic.rectTransform.anchorMax = Vector2.one;
                graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 2;
                Canvas.ForceUpdateCanvases();
                graphic.Present(camera, Vector3.zero, new TerrainMiningSelectorSettings { Radius = radius }, true, true);
                mesh = Populate(graphic);
                Assert.That(mesh.vertexCount, Is.GreaterThan(200));
                Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(System.Array.Exists(mesh.colors, tint => tint.a > .99f), Is.True);
                Assert.That(System.Array.Exists(mesh.colors, tint => tint.a > 0 && tint.a < .1f), Is.True);
                Vector2 expected = camera.WorldToScreenPoint(Vector3.zero);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform, expected, null, out var local);
                Assert.That(mesh.bounds.center.x, Is.EqualTo(local.x).Within(.01f));
                Assert.That(mesh.bounds.center.y, Is.EqualTo(local.y).Within(.01f));
                graphic.Present(camera, Vector3.zero, new TerrainMiningSelectorSettings(), false, true);
                Object.DestroyImmediate(mesh); mesh = Populate(graphic);
                Assert.That(mesh.vertexCount, Is.Zero);
                graphic.Present(camera, Vector3.zero, new TerrainMiningSelectorSettings(), true, false);
                Object.DestroyImmediate(mesh); mesh = Populate(graphic);
                Assert.That(System.Array.TrueForAll(mesh.colors, tint => tint.a <= .3f), Is.True);
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
            }
        }

        private static Mesh Populate(TerrainMiningSelectorGraphic graphic)
        {
            using var helper = new VertexHelper();
            typeof(TerrainMiningSelectorGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null)
                .Invoke(graphic, new object[] { helper });
            var mesh = new Mesh(); helper.FillMesh(mesh); return mesh;
        }
    }
}
