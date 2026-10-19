using System;
using DarkNights.Core.Config.Expedition;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>星图距离、配置冻结与输入边界的定向检查；不代替独立进程的状态传输及实际航程验收。</summary>
    public sealed class StarNavigationTests
    {
        private static NavigationDefinition Catalog() => new NavigationDefinition(5, 3,
            new[] { new NavigationNodeDefinition("a", "A", 0, 0, 1, "mock", false),
                new NavigationNodeDefinition("b", "B", 90, 0, 2, "mock", true) });

        [Test]
        public void ApproachingOnePlanetIncreasesTimeToTheOther()
        {
            var catalog = Catalog();
            Assert.That(catalog.ArrivalSeconds(45, 0, "a"), Is.EqualTo(12));
            Assert.That(catalog.ArrivalSeconds(45, 0, "b"), Is.EqualTo(12));
            Assert.That(catalog.ArrivalSeconds(15, 0, "a"), Is.EqualTo(6));
            Assert.That(catalog.ArrivalSeconds(15, 0, "b"), Is.EqualTo(18));
            Assert.That(catalog.ArrivalSeconds(0, 0, "a"), Is.EqualTo(3));
        }

        [Test]
        public void InvalidCoordinatesAndDuplicateNodesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Catalog().Distance(float.NaN, 0, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationNodeDefinition("a", "A", float.PositiveInfinity, 0, 1, "", false));
            Assert.Throws<ArgumentException>(() => new NavigationDefinition(5, 3,
                new[] { Catalog().Nodes[0], Catalog().Nodes[0] }));
            Assert.Throws<ArgumentException>(() => Catalog().Distance(0, 0, "missing"));
        }

        [Test]
        public void FrozenDirectoryDoesNotRetainTheInputArray()
        {
            var a = Catalog().Nodes[0]; var b = Catalog().Nodes[1];
            var source = new[] { a, b };
            var frozen = new NavigationDefinition(5, 3, source);
            source[0] = b;
            Assert.That(frozen.Nodes[0].Id, Is.EqualTo("a"));
            Assert.That(frozen.Matches(Catalog()), Is.True);
            Assert.That(frozen.Find("b").PreviewOnly, Is.True);
        }
    }
}
