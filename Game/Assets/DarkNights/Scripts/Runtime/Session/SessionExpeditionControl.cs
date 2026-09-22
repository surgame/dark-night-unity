using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Session
{
    /// <summary>远征命令的可信连接授权；驾驶等个人请求校验本人角色租约，购买与返航结算只允许房主，状态由现有事务更新。</summary>
    internal static class SessionExpeditionControl
    {
        internal static int Apply(ObjectSession world, SessionConnection connection, SessionRequest request)
        {
            bool personal = request.Kind is "unload" or "board" or "relay" or "mine" or "pilot" or "takeoff" or "land" or "cancel-flight" or "deploy";
            if (!world.IsExpedition || !personal && !connection.IsHost) return -1;
            var hero = request.ActorIds.Count == 1 ? world.Index.Find<ActorBehaviour>(request.ActorIds[0]) : null;
            if (personal && (hero == null || hero.Read().ControllerSlot != connection.PlayerSlot ||
                hero.Read().ControllerGeneration != connection.Generation || hero.Read().ControlLease != request.ControlLease)) return -1;
            if (world.Paused) return 0;
            return world.Mutations.Run(() => world.Expedition.Command(request.Kind, request.TargetId, hero));
        }
    }
}
