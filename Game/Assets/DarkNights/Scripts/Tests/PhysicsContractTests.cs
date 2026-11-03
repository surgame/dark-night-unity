using System;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Physics;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 物理检查点闭包、传输冻结与内容错配的边界测试；不运行Editor物理或宣称原生求解确定性。
    /// 用于后续获准测试批次，本次开发默认只编译，失败不得交换活动会话。
    /// </summary>
    public sealed class PhysicsContractTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private static PhysicsBodySnapshot Actor(int id = 1) => new PhysicsBodySnapshot(id, 80, 40, 12, -30);

        [Test]
        public void SnapshotRejectsDanglingAndDuplicateConstraints()
        {
            var bodies = new[] { Actor() };
            Assert.Throws<ArgumentException>(() => new PhysicsWorldSnapshot(Hash, 1, "", 0, 0, bodies,
                new[] { new PhysicsRopeSnapshot(1, 2, 40) }));
            Assert.Throws<ArgumentException>(() => new PhysicsWorldSnapshot(Hash, 1, "", 0, 0,
                new[] { Actor(), Actor(2) }, new[]
                { new PhysicsRopeSnapshot(1, 2, 40), new PhysicsRopeSnapshot(1, 2, 60) }));
        }

        [Test]
        public void FrozenFrameNeverBorrowsWireArraysOrObjects()
        {
            var original = new PhysicsWorldSnapshot(Hash, 5, "", 0, 0,
                new[] { Actor(), new PhysicsBodySnapshot(-1, 20, 10, 0, 0) },
                new[] { new PhysicsRopeSnapshot(1, -1, 50) });
            var wire = PhysicsWorldWire.From(original);
            var frozen = wire.Freeze();
            wire.Bodies[0].X = 700;
            wire.Ropes[0].Length = 500;
            wire.Bodies = Array.Empty<PhysicsBodyWire>();
            Assert.AreEqual(20, frozen.Bodies[0].X);
            Assert.AreEqual(50, frozen.Ropes[0].Length);
            Assert.AreEqual(2, frozen.Bodies.Count);
        }

        [Test]
        public void ReconcileRequiresWholeInteractionClosureAndMatchingBackend()
        {
            var definition = new PhysicsWorldDefinition(Hash, 2400, 3600, 1, .6f, 112, 192,
                new[] { new PhysicsPropDefinition(-1, 20, 0, 32, 32, 2) });
            var missingBox = new PhysicsWorldSnapshot(Hash, 2, "", 0, 0,
                new[] { Actor() }, Array.Empty<PhysicsRopeSnapshot>());
            Assert.Throws<FormatException>(() => PhysicsSnapshotRules.Validate(missingBox, definition, new[] { 1 }));
            Assert.Throws<FormatException>(() => PhysicsSnapshotRules.Validate(missingBox, null, new[] { 1 }));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void BodyRejectsNonFiniteMotion(float value) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsBodySnapshot(1, 0, 0, value, 0));

        [Test]
        public void RotatedBoxUsesActualJointAnchorForReach()
        {
            var rules = new PhysicsWorldDefinition(Hash, 2400, 3600, 1, .6f, 112, 192,
                new[] { new PhysicsPropDefinition(-1, 50, 0, 32, 64, 2) });
            var actor = new PhysicsBodySnapshot(1, 18, -22, 0, 0);
            var box = new PhysicsBodySnapshot(-1, 50, 0, 0, 0, 90);
            Assert.AreEqual(0, PhysicsRopeGeometry.Distance(actor, box, rules), .0001);
        }

        [Test]
        public void PresentationSnapsDiscontinuityAndInterpolatesShortAngle()
        {
            var old = new PhysicsBodySnapshot(-1, 0, 0, 0, 0, 179);
            var current = new PhysicsBodySnapshot(-1, 10, 0, 0, 0, -179);
            var half = new PhysicsBodyPresentation(old, current, .5);
            Assert.AreEqual(5, half.X);
            Assert.AreEqual(180, half.Angle);
            var teleported = new PhysicsBodySnapshot(-1, 100, 0, 0, 0);
            Assert.AreEqual(100, new PhysicsBodyPresentation(old, teleported, .1).X);
        }
    }
}
