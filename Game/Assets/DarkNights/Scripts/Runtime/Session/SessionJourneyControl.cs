using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Session
{
    /// <summary>航程命令的可信连接边界；先校验本人角色租约与目标船，导航能力只在现有同步事务中改变状态。</summary>
    internal static class SessionJourneyControl
    {
        internal static int Apply(ObjectSession world, SessionConnection connection, SessionRequest request)
        {
            if (!world.IsExpedition || world.Flow?.Enabled != true || world.Paused ||
                world.Expedition.Ship?.Id != request.TargetId || request.ActorIds.Count != 1) return 0;
            if (world.Journey.Capture().Revision != request.Value) return 0;
            var actor = world.Index.Find<ActorBehaviour>(request.ActorIds[0]);
            var state = actor?.Read();
            if (state == null || state.ControllerSlot != connection.PlayerSlot ||
                state.ControllerGeneration != connection.Generation || state.ControlLease != request.ControlLease) return -1;
            return world.Mutations.Run(() => request.Operation == SessionOperation.SelectDestination
                ? world.Flow.SelectDestination(actor, request.Kind, request.Value)
                : world.Flow.Cancel(actor));
        }
    }
}
