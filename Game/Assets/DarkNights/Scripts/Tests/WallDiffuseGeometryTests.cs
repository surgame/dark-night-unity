using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>外露边的独立几何 oracle；覆盖全坡形、未知边界、门洞与跨页连续性，不依赖作者资源或业务地图。</summary>
    public sealed class WallDiffuseGeometryTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        public void EveryShapeHasSolidInsideAndKnownAirOutsideItsEdges(int shape)
        {
            var cells = new byte[9]; Array.Fill(cells, (byte)128); cells[4] = (byte)(192 | shape);
            var result = LightBoundaryBuilder.Build(new LightGeometrySnapshot(3, 3, cells), null);
            Assert.That(result.Count, Is.InRange(3, 5));
            for (int i = 0; i < result.Count; i++)
            {
                var edge = result[i];
                for (int j = 1; j < 10; j++)
                {
                    float x = edge.Ax + (edge.Bx - edge.Ax) * j / 10, y = edge.Ay + (edge.By - edge.Ay) * j / 10;
                    Assert.That(Solid(x - edge.Nx * .001f, y - edge.Ny * .001f), Is.True);
                    Assert.That(Solid(x + edge.Nx * .001f, y + edge.Ny * .001f), Is.False);
                }
            }
            bool Solid(float x, float y)
            {
                int u = (int)Math.Floor(x), row = (int)Math.Floor(y);
                byte cell = cells[row * 3 + u];
                return (cell & 64) != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)(cell & 15), x - u, 1 - (y - row));
            }
        }

        [Test]
        public void LongWallMergesAcrossPagesAndDoorKeepsPhysicalSegments()
        {
            var cells = new byte[64 * 3]; Array.Fill(cells, (byte)128);
            for (int x = 0; x < 64; x++) cells[64 + x] = 192;
            var whole = LightBoundaryBuilder.Build(new LightGeometrySnapshot(64, 3, cells), null);
            Assert.That(whole.Count, Is.EqualTo(2));
            Assert.That(Math.Abs(whole[0].Bx - whole[0].Ax), Is.EqualTo(64));
            cells[64 + 32] = 128;
            var door = LightBoundaryBuilder.Build(new LightGeometrySnapshot(64, 3, cells), null);
            Assert.That(door.Count, Is.EqualTo(6));
            var repeated = LightBoundaryBuilder.Build(new LightGeometrySnapshot(64, 3, cells), null);
            for (int i = 0; i < door.Count; i++) Assert.That(repeated[i].Id, Is.EqualTo(door[i].Id));
        }

        [Test]
        public void UnknownAndUnloadedCellsNeverBecomeAirAndCancellationKeepsNoResult()
        {
            var cells = new byte[] { 0, 0, 0, 0, 192, 0, 0, 0, 0 };
            Assert.That(LightBoundaryBuilder.Build(new LightGeometrySnapshot(3, 3, cells), null).Count, Is.Zero);
            cells[3] = 128;
            Assert.That(LightBoundaryBuilder.Build(new LightGeometrySnapshot(3, 3, cells), null).Count, Is.EqualTo(1));
            Assert.Throws<OperationCanceledException>(() => LightBoundaryBuilder.Build(new LightGeometrySnapshot(3, 3, cells), () => true));
        }
    }
}
