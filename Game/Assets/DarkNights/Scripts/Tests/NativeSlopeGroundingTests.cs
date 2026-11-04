using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 使用真实YYGC角色、MovementBehaviour和单步原生物理验证坡面接地。
    /// 测试只在独立会话的派生碰撞缓存中安装长坡，作者地图、资源和玩家存档不参与写入。
    /// </summary>
    public sealed class NativeSlopeGroundingTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private UnifiedSessionScope scope;
        private ObjectSession world;
        private WhiteboxSession session;
        private ActorBehaviour actor;
        private Rigidbody2D body;

        [UnityTest]
        public IEnumerator SlopesKeepGroundControlAndAllowRealTakeoff()
        {
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(async () => scope = await UnifiedSessionScope.Create());
            var failures = new List<string>();
            foreach (float slope in new[] { 0, .5f, -.5f, 1, -1 })
            {
                RunCase(failures, "站立及停步 " + slope, () => StandingAndBrakingKeepSupport(slope));
                yield return null;
                if (slope == 0) continue;
                RunCase(failures, "向右行走 " + slope, () => WalkingAlongSlopeUsesGroundControl(slope, 1));
                yield return null;
                RunCase(failures, "向左行走 " + slope, () => WalkingAlongSlopeUsesGroundControl(slope, -1));
                yield return null;
                RunCase(failures, "坡上起跳 " + slope, () => UphillJumpLeavesSupportAndLandsAgain(slope));
                yield return null;
                RunCase(failures, "脱离坡面 " + slope, () => AscendingAwayFromNearbySlopeRemainsAirborne(slope));
                yield return null;
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private void PrepareWorld()
        {
            var layout = new LevelLayout(4000, 0, 0, 4000, 2000, 2000,
                Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                new[] { new PlacementDefinition("worker", 2000) }, localWhitebox: true,
                physics: new PhysicsWorldDefinition(new string('a', 64), 2400, 3600, 1, .6f, 112, 192,
                    Array.Empty<PhysicsPropDefinition>()));
            // 既有Editor装配夹具未向FishNet注册Prefab；只接收其固定Awake诊断，其他错误仍使测试失败。
            LogAssert.Expect(LogType.Error, new Regex(@"NetworkObject Name \[WorldSession\].*is expected to be initialized but was not\."));
            world = scope.NewWorld(RuleScenario.Catalog(), layout);
            actor = world.Index.Actors.Single();
            session = new WhiteboxSession(world, actor.PlacementKey);
            session.Step(0, false, false, false);
            object backend = typeof(PhysicsSessionBehaviour).GetField("backend", Hidden).GetValue(world.Physics);
            Scene scene = (Scene)backend.GetType().GetField("scene", Hidden).GetValue(backend);
            body = scene.GetRootGameObjects().Single(root => root.name == "Physics body " + actor.Id)
                .GetComponent<Rigidbody2D>();
        }

        private void RunCase(List<string> failures, string name, Action action)
        {
            try { PrepareWorld(); action(); }
            catch (Exception error) { failures.Add(name + "\n" + error); }
            finally { session?.Dispose(); session = null; }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try { scope?.Dispose(); }
            finally { scope = null; }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private void StandingAndBrakingKeepSupport(float slope)
        {
            InstallSlope(slope);
            for (int tick = 0; tick < 60; tick++) session.Step(0, false, false, false);
            AssertGrounded("静止");
            int direction = slope < 0 ? -1 : 1;
            for (int tick = 0; tick < 30; tick++) session.Step(direction, false, false, false);
            for (int tick = 0; tick < 60; tick++)
            {
                session.Step(0, false, false, false);
                AssertGrounded("停步 tick=" + tick);
            }
        }

        private void WalkingAlongSlopeUsesGroundControl(float slope, int direction)
        {
            InstallSlope(slope);
            for (int tick = 0; tick < 20; tick++) session.Step(0, false, false, false);
            float start = actor.CaptureState().X;
            for (int tick = 0; tick < 60; tick++)
            {
                session.Step(direction, false, false, false);
                AssertGrounded("行走 tick=" + tick);
            }
            Assert.That((actor.CaptureState().X - start) * direction, Is.GreaterThan(30));
        }

        private void UphillJumpLeavesSupportAndLandsAgain(float slope)
        {
            InstallSlope(slope);
            for (int tick = 0; tick < 20; tick++) session.Step(0, false, false, false);
            int direction = slope < 0 ? -1 : 1;
            for (int tick = 0; tick < 12; tick++) session.Step(direction, false, false, false);
            session.Step(direction, true, true, false);
            Assert.That(actor.CaptureState().SupportPlatform, Is.EqualTo(-1), "起跳必须离地");
            Assert.That(actor.CaptureState().JumpBufferRemaining, Is.Zero, "坡上起跳必须消费输入");
            Assert.That(actor.CaptureState().VerticalSpeed, Is.GreaterThan(100));
            for (int tick = 0; tick < 150; tick++) session.Step(0, false, false, false);
            AssertGrounded("跳跃后落坡");
        }

        private void AscendingAwayFromNearbySlopeRemainsAirborne(float slope)
        {
            InstallSlope(slope);
            for (int tick = 0; tick < 20; tick++) session.Step(0, false, false, false);
            SetMotion(actor.CaptureState().X, actor.CaptureState().Height, 0, 160);
            session.Step(0, false, false, false);
            Assert.That(actor.CaptureState().SupportPlatform, Is.EqualTo(-1));
            Assert.That(actor.CaptureState().VerticalSpeed, Is.GreaterThan(100));
        }

        private void InstallSlope(float slope)
        {
            Scene scene = body.gameObject.scene;
            var polygon = scene.GetRootGameObjects().Single(root => root.name == "Authority terrain collision")
                .GetComponent<PolygonCollider2D>();
            polygon.pathCount = 1;
            polygon.SetPath(0, new[]
            {
                new Vector2(10, -20), new Vector2(30, -20),
                new Vector2(30, 10 * slope), new Vector2(10, -10 * slope)
            });
            polygon.GetComponent<CompositeCollider2D>().GenerateGeometry();
            Physics2D.SyncTransforms();
            SetMotion(2000, Math.Abs(slope) * 8 + 2, 0, 0);
        }

        private void SetMotion(float x, float height, float vx, float vy)
        {
            world.Mutations.Run(() =>
            {
                var state = (ActorState)typeof(ActorBehaviour).GetMethod("Edit", Hidden).Invoke(actor, null);
                TerrainMotionTestMap.Set(state, nameof(ActorState.X), x);
                TerrainMotionTestMap.Set(state, nameof(ActorState.Height), height);
                TerrainMotionTestMap.Set(state, nameof(ActorState.HorizontalSpeed), vx);
                TerrainMotionTestMap.Set(state, nameof(ActorState.VerticalSpeed), vy);
                return true;
            });
        }

        private void AssertGrounded(string context)
        {
            var state = actor.CaptureState();
            Assert.That(state.SupportPlatform, Is.Zero, context + " v=" + state.HorizontalSpeed + "," +
                state.VerticalSpeed + " n=" + state.PhysicsGroundNormalX + "," + state.PhysicsGroundNormalY);
            Assert.That(world.CaptureView().Actors.Single().SupportPlatform, Is.Zero, context + " 展示投影");
        }
    }
}
