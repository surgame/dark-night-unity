using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 用正式会话 Prefab 和真实 YYGC 上下文验证角色、配置、失败清理与重新装配。
    /// 服务端时钟仍由显式回调推进；真实 transport 和重连由独立 Player 验收。
    /// </summary>
    [Category("UnifiedSession")]
    public sealed class WorldSessionLifecycleTests
    {
        private UnifiedSessionScope scope;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return UniTask.ToCoroutine(async () => scope = await UnifiedSessionScope.Create());
        }

        [TearDown]
        public void Cleanup() => scope?.Dispose();

        private ObjectSession Prepare() => scope.NewWorld(RuleScenario.Catalog(), RuleScenario.Layout(), false);
        private static CampSessionBehaviour Behaviour(ObjectSession world) => world.Camp.Object.GetBehaviour<CampSessionBehaviour>();

        [TestCase(true)]
        [TestCase(false)]
        public void ConfigurationAndRoleStartOnceInEitherOrder(bool roleFirst)
        {
            var world = Prepare();
            var behaviour = Behaviour(world);
            if (roleFirst) behaviour.OnStartServer();
            Assert.That(behaviour.Server, Is.Null);
            Configure(world);
            if (!roleFirst)
            {
                Assert.That(behaviour.Server, Is.Null);
                behaviour.OnStartServer();
            }
            var server = behaviour.Server;
            Assert.That(server, Is.Not.Null);
            behaviour.OnStartServer();
            Assert.That(behaviour.Server, Is.SameAs(server));
            Assert.That(behaviour.StartCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => Configure(world));
        }

        [Test]
        public void StopRevokesBeforeDisposalAndLateStartCannotResurrect()
        {
            var world = Prepare();
            var behaviour = Behaviour(world);
            SessionServer previous = null;
            int detached = 0;
            Configure(world, value =>
            {
                detached++;
                Assert.That(value.Server, Is.Null);
                Assert.That(previous.Authority.Closed, Is.False);
            });
            behaviour.OnStartServer();
            previous = behaviour.Server;
            behaviour.StopServer();
            long tick = previous.Authority.ServerTick;
            behaviour.Update(10);
            previous.Advance(10);
            behaviour.OnStartServer();
            behaviour.StopServer();
            Assert.That(detached, Is.EqualTo(1));
            Assert.That(previous.Authority.Closed, Is.True);
            Assert.That(previous.Authority.ServerTick, Is.EqualTo(tick));
            Assert.That(world.Context.IsAlive, Is.False);
            Assert.That(behaviour.Server, Is.Null);
        }

        [Test]
        public void ClientConfigurationAndDespawnNeverCreateAuthority()
        {
            var world = Prepare();
            var behaviour = Behaviour(world);
            Configure(world);
            behaviour.Update(10);
            Assert.That(behaviour.Server, Is.Null);
            Assert.That(world.Elapsed, Is.Zero);
            behaviour.OnDespawn();
            behaviour.OnStartServer();
            Assert.That(behaviour.Server, Is.Null);
            Assert.That(behaviour.Configured, Is.False);
            Assert.That(world.Context.IsAlive, Is.False);
        }

        [Test]
        public void FailedCreationReleasesContextAndFreshAssemblyStarts()
        {
            var failed = Prepare();
            var failedBehaviour = Behaviour(failed);
            int detached = 0;
            Configure(failed, value => detached++, "");
            Assert.Throws<ArgumentException>(() => failedBehaviour.OnStartServer());
            Assert.That(detached, Is.EqualTo(1));
            Assert.That(failedBehaviour.Server, Is.Null);
            Assert.That(failedBehaviour.StartCount, Is.Zero);
            Assert.That(failed.Context.IsAlive, Is.False);
            var fresh = Prepare();
            Configure(fresh);
            Behaviour(fresh).OnStartServer();
            Assert.That(Behaviour(fresh).Server.Authority.Closed, Is.False);
            Assert.That(Behaviour(fresh).StartCount, Is.EqualTo(1));
        }

        private static void Configure(ObjectSession world, Action<CampSessionBehaviour> detached = null,
            string directory = "../artifacts/yygc-unified/u5/lifecycle-saves/v2")
        {
            Behaviour(world).Configure(world.Catalog, world.Layout, world.Camp.Object.GetBehaviour<WorldSessionBehaviour>(),
                directory, world, detached, error => Assert.Fail(error.ToString()));
        }
    }
}
