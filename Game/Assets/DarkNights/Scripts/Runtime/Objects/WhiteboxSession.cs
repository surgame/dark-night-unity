using System;
using System.Linq;
using DarkNights.Core.ViewData;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 最小角色白模的本地权威调度；对象和运动状态仍归原ActorBehaviour及ActorState。
    /// 仅推进一个已装配角色的正式运动，不运行营地、网络、采集或文件存储；退出释放同一会话。
    /// </summary>
    public sealed class WhiteboxSession : IDisposable
    {
        private ActorBehaviour hero;
        public ObjectSession World { get; }
        public ObjectInstance HeroObject => hero?.Object;
        public ActorViewData Hero => hero == null ? null : ObjectProjection.CaptureActor(World, hero);

        public WhiteboxSession(ObjectSession world, string placementKey)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (!world.Layout.LocalWhitebox || !world.Context.IsActive ||
                (world.Terrain != null && world.Terrain.Map == null))
                throw new InvalidOperationException("角色白模必须使用已激活的本地会话及就绪地形。");
            hero = world.Index.Actors.Single(actor => actor.PlacementKey == placementKey);
            world.Mutations.Run(() =>
            {
                var state = hero.Edit();
                state.ManualControl = true;
                state.ControllerSlot = 0;
                state.OwnerSlot = 0;
                state.ControlLease = 1;
                state.Height = 0;
                state.SupportPlatform = 0;
                return true;
            });
        }

        public void Step(int horizontal, bool jumpPressed, bool jumpHeld, bool sprintHeld)
        {
            if (hero == null || World.Paused) return;
            if (horizontal < -1 || horizontal > 1)
                throw new ArgumentOutOfRangeException(nameof(horizontal));
            World.Mutations.Run(() =>
            {
                var state = hero.Edit();
                state.Horizontal = horizontal;
                state.JumpPending |= jumpPressed;
                state.JumpHeld = jumpHeld;
                state.SprintHeld = sprintHeld;
                SessionActorMotion.Advance(World, World.Camp.BeginStep(1d / 60));
                return true;
            });
        }

        public void SetPaused(bool paused) => World.SetTime(paused, 1);

        public void Dispose()
        {
            hero = null;
            World.Dispose();
        }
    }
}
