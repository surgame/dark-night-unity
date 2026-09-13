using System.IO;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine.TestTools;
using System.Reflection;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证客户端 Ready 回执在重试、迟到和断开边界的处理，不模拟可信服务端授权。
    /// 测试仅设置已发送握手序号；实际重试发送及四端容量由独立 Player 回归覆盖。
    /// </summary>
    [Category("UnifiedSession")]
    public sealed class SessionClientReadyTests
    {
        private GameObject owner;
        private PlayerEndpoint endpoint;
        private SessionClient client;
        private SessionAuthority authority;
        private UnifiedSessionScope scope;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return UniTask.ToCoroutine(async () => scope = await UnifiedSessionScope.Create());
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            authority = SessionScenario.Create(RuleScenario.Catalog(), RuleScenario.Layout());
            owner = new GameObject("Ready reply test");
            endpoint = owner.AddComponent<PlayerEndpoint>();
            client = new SessionClient(null);
            client.Begin();
            client.Attach(endpoint);
            client.Replica.Apply(client.ConnectionGeneration, authority.CaptureProjection());
            typeof(SessionClient).GetField("readySequence", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(client, 7L);
        }

        [TearDown]
        public void Cleanup()
        {
            client?.Dispose();
            authority?.Dispose();
            Object.DestroyImmediate(owner);
            scope?.Dispose();
        }

        [Test]
        public void SuccessfulRetryCannotBeRevokedByLateRejection()
        {
            client.Receive(endpoint, Reply(7, 1, "NotReady"));
            Assert.That(client.Ready, Is.False);
            client.Receive(endpoint, Reply(7, 1, "Ready"));
            Assert.That(client.Ready, Is.True);
            client.Receive(endpoint, Reply(7, 1, "NotReady"));
            Assert.That(client.Ready, Is.True);
            Assert.That(client.HadReady, Is.True);
        }

        [TestCase(6, 1)]
        [TestCase(7, 0)]
        public void OldHandshakeOrEpochCannotConfirm(long sequence, int epoch)
        {
            client.Receive(endpoint, Reply(sequence, epoch, "Ready"));
            Assert.That(client.Ready, Is.False);
        }

        [Test]
        public void DetachedEndpointAndDisposedReplicaCannotConfirm()
        {
            client.Detach(endpoint);
            client.Receive(endpoint, Reply(7, 1, "Ready"));
            Assert.That(client.Ready, Is.False);
            client.Attach(endpoint);
            client.Dispose();
            client.Receive(endpoint, Reply(7, 1, "Ready"));
            Assert.That(client.Ready, Is.False);
        }

        private static CommandFeedback Reply(long sequence, int epoch, string code) =>
            new CommandFeedback(sequence, epoch, 0, code, 0, 0, true, 1, 1);
    }
}
