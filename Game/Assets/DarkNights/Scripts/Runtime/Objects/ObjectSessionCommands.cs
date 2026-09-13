using System;
using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 将经过权限与去重门禁的命令传给本局 YYGC 能力；明确使用请求参数，不读取全局选择。
    /// U2 切片只开放已经迁移的操作，未迁移业务明确拒绝，不调用旧世界。
    /// </summary>
    internal static class ObjectSessionCommands
    {
        internal static bool Valid(ObjectSession session, SessionRequest request)
        {
            foreach (int id in request.ActorIds)
            {
                ActorBehaviour actor = session.Index.Find<ActorBehaviour>(id);
                if (actor == null || actor.Enemy || actor.Hp <= 0) return false;
            }
            switch (request.Operation)
            {
                case SessionOperation.IssueOrders:
                    return request.ActorIds.Count > 0 && request.X >= 0 && request.X <= session.Layout.WorldWidth &&
                        (request.TargetId == 0 || session.Index.Find(request.TargetId) != null);
                case SessionOperation.PlaceBuilding:
                    return request.Kind == "house" && request.X >= 0 && request.X <= session.Layout.WorldWidth;
                case SessionOperation.SetPaused:
                case SessionOperation.SetSpeed:
                case SessionOperation.SetControlMode:
                case SessionOperation.Save:
                case SessionOperation.BeginLoad:
                case SessionOperation.Restart: return true;
                default: return false;
            }
        }

        internal static int Apply(ObjectSession session, SessionRequest request, out int id)
        {
            id = 0;
            switch (request.Operation)
            {
                case SessionOperation.IssueOrders:
                    return session.IssueOrders(request.ActorIds, request.TargetId, request.X);
                case SessionOperation.PlaceBuilding:
                    id = session.PlaceBuilding(request.Kind, request.X, request.ActorIds);
                    return id == 0 ? 0 : 1;
                case SessionOperation.SetPaused:
                    session.SetTime(request.Value == 1, session.Speed);
                    return 1;
                case SessionOperation.SetSpeed:
                    session.SetTime(session.Paused, request.Value);
                    return 1;
                default: throw new InvalidOperationException("Operation is outside the migrated slice.");
            }
        }
    }
}
