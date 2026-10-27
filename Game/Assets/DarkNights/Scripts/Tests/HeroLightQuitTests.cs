using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.Entry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 照明桥退出门控的有限回归；只用自建空宿主和显式退休任务。
    /// 验证已完成清理直接放行、待完成清理及重复退出均暂缓，实际 Player 退出由跨进程复验覆盖。
    /// </summary>
    public sealed class HeroLightQuitTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private HeroLightPresentation presentation;

        [SetUp]
        public void Create()
        {
            host = new GameObject("Owned light quit probe");
            presentation = host.AddComponent<HeroLightPresentation>();
        }

        [Test]
        public void CompletedRetirementAllowsQuitImmediately()
        {
            Assert.That(RequestQuit(), Is.True);
            Assert.That(presentation.enabled, Is.False);
            Assert.That(presentation.Retirement.IsCompletedSuccessfully, Is.True);
        }

        [UnityTest]
        public IEnumerator PendingRetirementVetoesRepeatedQuitUntilCompletion()
        {
            var worker = new TaskCompletionSource<bool>();
            var retiring = (List<Task>)typeof(HeroLightPresentation).GetField("retiring", Private)
                .GetValue(presentation);
            retiring.Add(worker.Task);
            host.SetActive(false);
            typeof(HeroLightPresentation).GetMethod("OnDisable", Private).Invoke(presentation, null);
            Assert.That(presentation.Retirement.IsCompleted, Is.False);
            Assert.That(RequestQuit(), Is.False);
            Assert.That(RequestQuit(), Is.False);
            worker.SetResult(true);
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (!presentation.Retirement.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(presentation.Retirement.IsCompletedSuccessfully, Is.True);
            yield return null;
        }

        [TearDown]
        public void DestroyOwnedHost()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        private bool RequestQuit() => (bool)typeof(HeroLightPresentation).GetMethod("WantsToQuit", Private)
            .Invoke(presentation, null);
    }
}
