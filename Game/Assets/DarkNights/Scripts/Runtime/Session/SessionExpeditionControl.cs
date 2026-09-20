using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Session
{
    /// <summary>远征命令的可信连接授权；个人请求校验角色租约，起飞与购买只允许房主，状态由现有事务更新。</summary>
    internal static class SessionExpeditionControl
    {
        internal static int Apply(ObjectSession world, SessionConnection connection, SessionRequest request)
        {
            bool personal = request.Kind is "unload" or "board" or "relay" or "mine";
            if (!world.IsExpedition || !personal && !connection.IsHost) return -1;
            var hero = request.ActorIds.Count == 1 ? world.Index.Find<ActorBehaviour>(request.ActorIds[0]) : null;
            if (personal && (hero == null || hero.Read().ControllerSlot != connection.PlayerSlot ||
                hero.Read().ControllerGeneration != connection.Generation || hero.Read().ControlLease != request.ControlLease)) return -1;
            return world.Mutations.Run(() => world.Expedition.Command(request.Kind, request.TargetId, hero));
        }
    }
}
