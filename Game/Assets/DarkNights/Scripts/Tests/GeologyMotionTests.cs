using System;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>塌方重力和堆积几何的纯数据回归；验证加速、落点钳制、可清障区域数和逐层支撑，不用截图或历史编译替代实际场景验收。</summary>
    public sealed class GeologyMotionTests
    {
        [Test]
        public void GravityStartsAtRestAndEqualIntervalsTravelFurther()
        {
            var rock = new FallingRockViewData(0, 180, 32, 20, 320, .1);
            rock.Sample(.1, out float startX, out float startY);
            Assert.That(startX, Is.Zero); Assert.That(startY, Is.EqualTo(180));
            rock.Sample(.3, out _, out float y1); rock.Sample(.5, out _, out float y2);
            Assert.That(180 - y1, Is.EqualTo(6.4f).Within(.001f));
            Assert.That(y1 - y2, Is.GreaterThan((180 - y1) * 2.9f));
            rock.Sample(10, out float endX, out float endY);
            Assert.That(endX, Is.EqualTo(32)); Assert.That(endY, Is.EqualTo(20));
        }

        [Test]
        public void ThreeByThreeCeilingUsesAsymmetricLowPileAndMixedFragments()
        {
            var config = new GeologyEventConfig { SourceV = -31, SourceRows = 3, RubbleWidth = 5 }.Freeze();
            var cells = Enumerable.Range(0, config.RubbleCount).Select(config.Landing).ToArray();
            Assert.That(cells.Distinct().Count(), Is.EqualTo(config.RubbleCount));
            Assert.That(config.RubbleCount, Is.InRange(1, 31));
            foreach (var cell in cells.Where(c => c.V > config.LandingV))
                Assert.That(cells.Any(c => c.U == cell.U && c.V == cell.V - 1), Is.True);
            var fragments = Enumerable.Range(0, config.Collapse.Count).Select(config.Collapse.Fragment).ToArray();
            Assert.That(fragments.Length, Is.EqualTo(26));
            Assert.That(fragments.Count(f => f.Width >= 18), Is.EqualTo(4));
            Assert.That(fragments.Count(f => f.Width >= 9 && f.Width < 18), Is.EqualTo(12));
            Assert.That(fragments.Count(f => f.Width < 9), Is.EqualTo(10));
            Assert.That(config.Collapse.Duration, Is.EqualTo(config.FallSeconds));
            for (int i = 0; i < config.Collapse.Count; i++)
            {
                config.Collapse.Sample(i, config.FallSeconds, out _, out float y, out _);
                Assert.That(y, Is.GreaterThanOrEqualTo(config.Collapse.FloorY));
            }
        }

        [Test]
        public void InvalidGravityOrUnsupportedTallPileFailsBeforeActivation()
        {
            Assert.Throws<ArgumentException>(() => new GeologyEventConfig { Gravity = 0 }.Freeze());
            Assert.Throws<ArgumentException>(() => new GeologyEventConfig { SourceRows = 3, RubbleWidth = 3 }.Freeze());
        }
    }
}
