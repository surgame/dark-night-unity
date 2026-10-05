using DarkNights.Core.ViewData;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// Host本地主角的即时冻结展示副本，复用正式投影映射；每次只复制一个角色，不发布全世界或持有可写状态。
    /// 观察帧、权威世界、玩家槽位和控制租约必须一致；加载、换世界或失去控制时停止使用，客户端继续原展示路径。
    /// </summary>
    public static class HostHeroProjection
    {
        public static ActorViewData Capture(ObjectSession world, SessionAuthority authority,
            SessionViewData frame, ActorViewData observed, int playerSlot)
        {
            if (world == null || authority == null || frame == null || observed == null || !world.Context.IsActive ||
                authority.Closed || authority.Loading || frame.Loading || authority.Epoch != frame.Epoch ||
                !observed.ManualControl || observed.ControllerSlot != playerSlot || observed.ControlLease <= 0)
                return null;
            authority.CheckThread();
            var actor = world.Index.Find<ActorBehaviour>(observed.Id);
            if (actor == null || actor.RuleKey != observed.Kind) return null;
            var state = actor.Read();
            if (!state.ManualControl || state.ControllerSlot != playerSlot || state.ControlLease != observed.ControlLease)
                return null;
            return ObjectProjection.CaptureActor(world, actor);
        }
    }
}
