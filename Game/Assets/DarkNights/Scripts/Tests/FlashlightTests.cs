using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>手电首版的有限合同检查；覆盖真实权威入口、保存投影和后台几何取消，不代替多人或人工美术验收。</summary>
    public sealed class FlashlightTests
    {
        [Test]
        public void InvalidLightRulesRejectNonfiniteAndOutOfRange()
        {
            Assert.Throws<ArgumentException>(()=>new FlashlightRules(float.NaN,90,1,2,.5f,1,1,1));
            Assert.Throws<ArgumentException>(()=>new FlashlightRules(14,180,1,2,.5f,1,1,1));
        }

        [Test]
        public void UnknownAndThickWallsStayOpaqueInDistanceCache()
        {
            var snapshot=new LightGeometrySnapshot(3,1,new byte[] {128,192,0});
            var result=LightWallDistance.Build(snapshot,()=>false);
            Assert.That(result[0],Is.Zero);
            Assert.That(result[4],Is.GreaterThan(0));
            Assert.That(result[8],Is.GreaterThan(result[4]));
        }

        [Test]
        public void RetiredGeometryCancelsBeforePublishing()
        {
            using var cancellation=new CancellationTokenSource(); cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(()=>LightWallDistance.Build(
                new LightGeometrySnapshot(1,1,new byte[] {192}),()=>cancellation.IsCancellationRequested));
        }

        [Test]
        public void NativeFlashlightAssetAndGeneratedBindingsAreComplete() => Editor.FlashlightContentSetup.Validate();

        [UnityTest]
        public IEnumerator AuthorityToggleSaveAndWireUseTheSameActorState() => UniTask.ToCoroutine(async ()=>
        {
            using var fixture=await HeroTestSession.Create();
            fixture.Command(SessionOperation.ClaimHero);
            var actor=fixture.World.CaptureView().Actors.Single(a=>a.Id==fixture.ActorId);
            Assert.That(actor.LightDefinition,Is.Not.Empty); Assert.That(actor.LightEnabled,Is.True);
            var request=new SessionRequest(SessionOperation.SetHeroLight,SessionAuthority.ProtocolVersion,
                fixture.Authority.Epoch,fixture.Authority.PolicyRevision,100,new[] {fixture.ActorId},
                value:0,controlLease:fixture.State.ControlLease);
            Assert.That(fixture.Authority.Submit(fixture.Host,request).Code,Is.EqualTo(SessionResultCode.Pending));
            fixture.Authority.Tick(); Assert.That(fixture.State.LightEnabled,Is.False);
            var view=fixture.World.CaptureView().Actors.Single(a=>a.Id==fixture.ActorId);
            var wire=ActorWire.From(view).Freeze();
            Assert.That(wire.LightDefinition,Is.EqualTo(view.LightDefinition)); Assert.That(wire.LightEnabled,Is.False);
            string saved=fixture.World.SaveCodec.Serialize(fixture.World.CaptureWorld());
            fixture.World.Restore(saved);
            Assert.That(fixture.State.LightDefinition,Is.EqualTo(view.LightDefinition)); Assert.That(fixture.State.LightEnabled,Is.False);
        });

        [UnityTest]
        public IEnumerator UnownedToggleCannotChangeLight() => UniTask.ToCoroutine(async ()=>
        {
            using var fixture=await HeroTestSession.Create(); fixture.Command(SessionOperation.ClaimHero);
            var request=new SessionRequest(SessionOperation.SetHeroLight,SessionAuthority.ProtocolVersion,
                fixture.Authority.Epoch,fixture.Authority.PolicyRevision,100,new[] {fixture.ActorId},
                value:0,controlLease:fixture.State.ControlLease);
            fixture.Authority.Submit(fixture.Guest,request); fixture.Authority.Tick();
            Assert.That(fixture.State.LightEnabled,Is.True);
        });
    }
}
