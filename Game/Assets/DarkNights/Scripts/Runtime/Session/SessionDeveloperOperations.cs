using System;

namespace DarkNights.Runtime.Session
{
    /// <summary>开发者请求的参数合同；编号使用独立保留区，正式非开发构建在入口拒绝所有调试操作。</summary>
    internal static class SessionDeveloperOperations
    {
        internal static bool IsOperation(SessionOperation operation) => operation >= SessionOperation.DebugSetEnabled &&
            operation <= SessionOperation.DebugSpawnProjectile;
        internal static bool ValidShape(SessionRequest request)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (request.ControlLease != 0) return false;
            if (request.Operation == SessionOperation.DebugSetEnabled)
                return request.ActorIds.Count == 0 && request.TargetId == 0 && request.Kind.Length == 0 && request.X == 0 &&
                    (request.Value == 0 || request.Value == 1);
            if (!Guid.TryParseExact(request.Kind, "N", out var guid) || guid == Guid.Empty) return false;
            if (request.Operation == SessionOperation.DebugRemoveObject)
                return request.ActorIds.Count == 0 && request.TargetId > 0 && request.Value == 0 && request.X == 0;
            if (request.ActorIds.Count != 1 || request.TargetId != 0) return false;
            if (request.Operation == SessionOperation.DebugSpawnProjectile)
                return request.X == 0 && request.Value >= 1 && request.Value <= 100;
            if (request.Value < 0) return false;
            return request.Operation == SessionOperation.DebugGiveEquipment
                ? request.X >= 1 && request.X <= 100 && request.X == (int)request.X
                : request.Operation == SessionOperation.DebugRemoveEquipment && request.X == 0;
#else
            return false;
#endif
        }
    }
}
