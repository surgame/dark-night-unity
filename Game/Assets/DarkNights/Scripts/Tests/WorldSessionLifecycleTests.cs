using System;
using System.IO;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using GameCore.Objects.Behaviours;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证实际会话行为的配置／角色门槛、幂等清理与重新装配；使用真实 Core 和服务组合。
    /// 不伪造 FishNet 传输，角色分发的端到端检查由 Play 生命周期探针和独立 Player 承担。
    /// </summary>
    public sealed class WorldSessionLifecycleTests
    {
        [SetUp]
        public void Setup() => RuleScenario.RepositoryRoot = Path.GetFullPath("..");

        [TestCase(true)]
        [TestCase(false)]
        public void ConfigurationAndRoleStartOnceInEitherOrder(bool roleFirst)
        {
            var behaviour = new CampSessionBehaviour();
            behaviour.Initialize(default(BehaviourContext));
            try
            {
                if (roleFirst) behaviour.OnStartServer();
                Assert.That(behaviour.Server, Is.Null);
                if (roleFirst) ExpectTransportGuard();
                Configure(behaviour);
                if (!roleFirst)
                {
                    Assert.That(behaviour.Server, Is.Null);
                    ExpectTransportGuard();
                    behaviour.OnStartServer();
                }
                var server = behaviour.Server;
                Assert.That(server, Is.Not.Null);
                behaviour.OnStartServer();
                Assert.That(behaviour.Server, Is.SameAs(server));
                Assert.That(behaviour.StartCount, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => Configure(behaviour));
            }
            finally { behaviour.Dispose(); }
        }

        [Test]
        public void StopRevokesBeforeDisposalAndLateStartCannotResurrect()
        {
            var behaviour = new CampSessionBehaviour();
            behaviour.Initialize(default(BehaviourContext));
            SessionServer previous = null;
            int detached = 0;
            Configure(behaviour, value =>
            {
                detached++;
                Assert.That(value.Server, Is.Null);
                Assert.That(previous.Authority.Closed, Is.False);
            });
            ExpectTransportGuard();
            behaviour.OnStartServer();
            previous = behaviour.Server;
            behaviour.StopServer();
            long tick = previous.Authority.ServerTick;
            behaviour.Update(10);
            previous.Advance(10);
            behaviour.OnStartServer();
            behaviour.StopServer();
            behaviour.Dispose();
            Assert.That(detached, Is.EqualTo(1));
            Assert.That(previous.Authority.Closed, Is.True);
            Assert.That(previous.Authority.ServerTick, Is.EqualTo(tick));
            Assert.That(behaviour.Server, Is.Null);
        }

        [Test]
        public void ClientConfigurationAndDespawnNeverCreateAuthority()
        {
            var behaviour = new CampSessionBehaviour();
            behaviour.Initialize(default(BehaviourContext));
            Configure(behaviour);
            behaviour.Update(10);
            Assert.That(behaviour.Server, Is.Null);
            behaviour.OnDespawn();
            behaviour.OnStartServer();
            Assert.That(behaviour.Server, Is.Null);
            Assert.That(behaviour.Configured, Is.False);
            behaviour.Dispose();
        }

        [Test]
        public void FailedCreationDetachesAndReassemblyGetsFreshService()
        {
            var behaviour = new CampSessionBehaviour();
            behaviour.Initialize(default(BehaviourContext));
            int detached = 0;
            Configure(behaviour, value => detached++, "");
            Assert.Throws<ArgumentException>(() => behaviour.OnStartServer());
            Assert.That(detached, Is.EqualTo(1));
            Assert.That(behaviour.Server, Is.Null);
            Assert.That(behaviour.StartCount, Is.Zero);
            behaviour.OnDespawn();
            behaviour.Initialize(default(BehaviourContext));
            Configure(behaviour);
            ExpectTransportGuard();
            behaviour.OnStartServer();
            Assert.That(behaviour.Server.Authority.Closed, Is.False);
            Assert.That(behaviour.StartCount, Is.EqualTo(1));
            behaviour.Dispose();
        }

        // EditMode 未启动真实 transport；明确验收投影拒绝越过服务端门槛，不屏蔽其余日志。
        private static void ExpectTransportGuard() => LogAssert.Expect(LogType.Error,
            "MutateState can only be called on the server. Clients must send commands.");

        private static void Configure(CampSessionBehaviour behaviour, Action<CampSessionBehaviour> detached = null,
            string directory = "../artifacts/c-refactor/lifecycle-saves")
        {
            behaviour.Configure(RuleScenario.Catalog(), RuleScenario.Layout(), new WorldSessionBehaviour(),
                directory, detached, error => Assert.Fail(error.ToString()));
        }
    }
}
