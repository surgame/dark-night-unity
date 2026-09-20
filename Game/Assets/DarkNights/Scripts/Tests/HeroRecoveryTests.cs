using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证新主角状态的严格存档、冻结投影与 epoch 边界；旧连接占用不进入恢复数据。
    /// 全部使用真实世界恢复事务、MemoryPack 编解码和原战斗死亡路径。
    /// </summary>
    public sealed class HeroRecoveryTests
    {
        [UnityTest]
        public IEnumerator AirborneRecoveryKeepsMotionAndEquipmentButReleasesOwnership() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.SelectHeroItem, value: 3);
            f.Command(SessionOperation.UseHeroItem, kind: "jetpack", value: f.State.SelectionRevision);
            f.Input(jumpHeld: true, jumpPressed: true); f.Step(1); f.Step(12, jumpHeld: true, keepAlive: true);
            var state = f.State; var stale = f.Packet(horizontal: 1);
            string save = f.World.SaveCodec.Serialize(f.Authority.CaptureWorld());
            var ticket = f.Command(SessionOperation.BeginLoad);
            f.Authority.CompleteLoad(ticket, save);
            Assert.That(f.State.Height, Is.EqualTo(state.Height)); Assert.That(f.State.VerticalSpeed, Is.EqualTo(state.VerticalSpeed));
            Assert.That(f.State.JetpackFuel, Is.EqualTo(state.JetpackFuel)); Assert.That(f.State.JetpackEquipped, Is.True);
            Assert.That(f.State.SelectedItem, Is.EqualTo(3)); Assert.That(f.State.ControllerSlot, Is.EqualTo(-1));
            Assert.That(f.State.LastInputSequence, Is.Zero); Assert.That(f.State.JumpHeld, Is.False);
            Assert.That(f.Authority.SubmitInput(f.Host, stale), Is.False);
            f.Ready(); Assert.That(f.Command(SessionOperation.ClaimHero).Code, Is.EqualTo(SessionResultCode.Applied));
        });

        [UnityTest]
        public IEnumerator LoadDoesNotTreatRememberedIdsAsExistingIdleHeroes() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(true);
            int[] previous = f.World.Index.Actors.Where(actor => actor.CaptureState().ControllerSlot >= 0)
                .Select(actor => actor.Id).ToArray();
            var save = JObject.Parse(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()));
            foreach (var actor in save["world"]["actors"]) actor["manual_control"] = false;
            var ticket = f.Command(SessionOperation.BeginLoad);
            f.Authority.CompleteLoad(ticket, save.ToString());
            int before = f.World.Index.Actors.Count;
            f.Ready();
            Assert.That(f.World.Index.Actors.Count, Is.EqualTo(before + 2));
            Assert.That(previous.All(id => f.World.Index.Find<ActorBehaviour>(id)
                .CaptureState().ControllerSlot < 0), Is.True);
        });

        [UnityTest]
        public IEnumerator InvalidMotionOrFuelCannotReplaceCurrentWorld() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(); f.Command(SessionOperation.ClaimHero);
            string before = f.World.SaveCodec.Serialize(f.Authority.CaptureWorld());
            var bad = JObject.Parse(before); bad["world"]["actors"][0]["height"] = 9999;
            Assert.Throws<FormatException>(() => f.World.Restore(bad.ToString()));
            Assert.That(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()), Is.EqualTo(before));
            bad = JObject.Parse(before); bad["world"]["actors"][0]["jetpack_fuel"] = 900;
            Assert.Throws<FormatException>(() => f.World.Restore(bad.ToString()));
            Assert.That(f.State.ControllerSlot, Is.Zero);
        });

        [UnityTest]
        public IEnumerator NativeProjectionCarriesFrozenVerticalAndInventoryState() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(); f.Command(SessionOperation.ClaimHero);
            f.Input(jumpPressed: true); f.Step(8);
            var frame = f.Authority.CaptureProjection();
            var codec = new ProjectionCodec(f.World.Catalog, f.World.Layout);
            var frozen = codec.Decode(codec.Encode(frame)).World.Actors.Single(a => a.Id == f.ActorId);
            Assert.That(frozen.Height, Is.GreaterThan(0)); Assert.That(frozen.ControlLease, Is.EqualTo(f.State.ControlLease));
            Assert.That(frozen.ControllerSlot, Is.Zero); Assert.That(frozen.SelectedItem, Is.Zero);
            float height = frozen.Height; f.Step(40); Assert.That(frozen.Height, Is.EqualTo(height));
            var invalid = new HeroInputRequest(7, f.Authority.Epoch, f.Authority.PolicyRevision, f.ActorId,
                f.State.ControlLease, 100, f.Authority.ServerTick, 1, false, false, false, false);
            Assert.That(f.Authority.SubmitInput(f.Host, invalid), Is.False);
        });

        [UnityTest]
        public IEnumerator DeathRemovesPossessionWithoutAnOrphanedPlayerIndex() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            var save = JObject.Parse(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()));
            var actor = save["world"]["actors"][0]; actor["x"] = actor["move_x"] = actor["rally_x"] = f.World.Layout.SpawnX - 1;
            actor["hp"] = 1; f.World.Restore(save.ToString());
            f.Command(SessionOperation.ClaimHero); f.Command(SessionOperation.StartNight);
            for (int i = 0; i < 900 && f.Actor != null; i++) f.Authority.Tick();
            Assert.That(f.Actor, Is.Null);
            Assert.That(f.Authority.CaptureProjection().World.Actors.Any(a => a.ControllerSlot == 0), Is.False);
        });
    }
}
