using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>原生美术、坡道端点及导入密度合同；防止重复缩放和可见坡道与权威通路脱节。</summary>
    public sealed class ShipGeometryTests
    {
        [TestCase(-160, 0)] [TestCase(-80, 40)] [TestCase(16, 40)] [TestCase(80, 80)] [TestCase(128, 80)]
        public void WalkwayEndpointsMatchNativeArt(float x, float height) => Assert.That(ShipGeometry.Floor(x), Is.EqualTo(height));
        [Test]
        public void WalkwayIsContinuousAndFitsTheExistingDock()
        {
            for (float x = -159; x <= 128; x++) Assert.That(ShipGeometry.Floor(x) - ShipGeometry.Floor(x - 1), Is.InRange(0, .625f));
            Assert.That((ExpeditionTerrainGenerator.DockRight - ExpeditionTerrainGenerator.DockLeft) * 16 - ShipGeometry.HalfWidth * 2, Is.EqualTo(32));
            Assert.That(ShipGeometry.Inside(-100, 30), Is.False, "坡道上的角色不能使收舱门槛通过。");
            Assert.That(ShipGeometry.AtPilot(96, 80), Is.True);
            Assert.That(ShipGeometry.AtPilot(60, 80), Is.False);
        }
        [Test]
        public void FlightRulesRejectUnboundedOrNonFiniteValues()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ShipFlightDefinition(maximumLift: 193));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ShipFlightDefinition(horizontalSpeed: float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ShipFlightDefinition(doorSeconds: double.PositiveInfinity));
        }
        [Test]
        public void NativeShipAndRobotUseOneDensityConversion()
        {
            var ship = (TextureImporter)AssetImporter.GetAtPath(Editor.ShipAssetSetup.Art + "/hull_back.png");
            var robot = (TextureImporter)AssetImporter.GetAtPath(Editor.ShipAssetSetup.Art + "/robot_walk_0.png");
            Assert.That(ship.spritePixelsPerUnit, Is.EqualTo(50)); Assert.That(robot.spritePixelsPerUnit, Is.EqualTo(100));
            Assert.That(ship.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(ship.mipmapEnabled, Is.False);
            Assert.That(ship.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }
    }
}
