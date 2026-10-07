using System.Reflection;
using DarkNights.Entry;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>在不生成真实日志风暴的条件下验证 F10 接入边界；测试队列和自反馈过滤，不运行旧玩法。</summary>
    public sealed class SmartConsoleLogBridgeTests
    {
        [Test]
        public void ApplicationLogQueueDropsOldestAndRemainsBounded()
        {
            var owner = new GameObject("bounded log queue test");
            try
            {
                var bridge = owner.AddComponent<SmartConsoleLogBridge>();
                var capture = typeof(SmartConsoleLogBridge).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < SmartConsoleLogBridge.MaximumQueued + 7; i++)
                    capture.Invoke(bridge, new object[] { new string('中', 4096), "unused stack", LogType.Log });
                Assert.That(bridge.Queued, Is.EqualTo(SmartConsoleLogBridge.MaximumQueued));
                Assert.That(bridge.Dropped, Is.EqualTo(7));
                typeof(SmartConsoleLogBridge).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(bridge, null);
                Assert.That(bridge.Queued, Is.Zero, "退出生命周期必须释放等待日志");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase("The character with Unicode value \\u5168 was not found in [LogText]", true)]
        [TestCase("The character with Unicode value \\u5168 was not found in [AutocompleteText]", true)]
        [TestCase("The character with Unicode value \\u5168 was not found in [ShopTitle]", false)]
        [TestCase("Ordinary warning [LogText]", false)]
        [TestCase(null, false)]
        public void OnlyConsoleMissingGlyphWarningsAreFiltered(string message, bool expected) =>
            Assert.That(SmartConsoleLogBridge.IsConsoleFontWarning(message), Is.EqualTo(expected));
    }
}
