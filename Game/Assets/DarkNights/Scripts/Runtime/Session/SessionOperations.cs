using System;
using System.Linq;
using DarkNights.Core.Logic.State;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 检查请求的参数形状与房主权限分类；不持有或访问玩法状态。
    /// 具体业务合法性与执行交给本局 YYGC 对象能力，队列入口保持有界。
    /// </summary>
    internal static class SessionOperations
    {
        internal static bool HostRequired(SessionOperation operation) => operation >= SessionOperation.SetPaused && operation <= SessionOperation.Restart;

        internal static bool ValidShape(SessionRequest r)
        {
            if (r == null || r.Sequence <= 0 || r.PolicyRevision < 0 ||
                !Enum.IsDefined(typeof(SessionOperation), r.Operation) || float.IsNaN(r.X) || float.IsInfinity(r.X) ||
                r.TargetId < 0 || r.ActorIds.Any(id => id <= 0)) return false;
            if (SessionHeroControl.IsOperation(r.Operation))
                return r.ActorIds.Count == 1 && r.X == 0 &&
                    (r.Operation == SessionOperation.ClaimHero ? r.ControlLease == 0 : r.ControlLease > 0) &&
                    (r.Operation == SessionOperation.UseHeroItem ? r.Value >= 0 &&
                        (r.Kind == "weapon" || r.Kind == "tool" || r.Kind == "jetpack" || r.Kind == "explosive") :
                        r.Operation == SessionOperation.DeployMineralDrill ? r.TargetId > 0 && r.Kind.Length == 0 && r.Value == 0 :
                        r.TargetId == 0 && r.Kind.Length == 0 &&
                        (r.Operation == SessionOperation.SelectHeroItem ? r.Value >= 0 && r.Value <= 3 : r.Value == 0));
            if (r.ControlLease != 0) return false;
            bool actors = r.Operation == SessionOperation.IssueOrders || r.Operation == SessionOperation.PlaceBuilding ||
                r.Operation == SessionOperation.TrainActors;
            bool position = r.Operation == SessionOperation.IssueOrders || r.Operation == SessionOperation.PlaceBuilding;
            bool target = r.Operation == SessionOperation.IssueOrders || r.Operation == SessionOperation.Repair;
            bool kind = r.Operation == SessionOperation.PlaceBuilding || r.Operation == SessionOperation.TrainActors;
            if ((!actors && r.ActorIds.Count != 0) || (!position && r.X != 0) || (!target && r.TargetId != 0) ||
                (!kind && r.Kind.Length != 0) || (kind && r.Kind.Length == 0)) return false;
            switch (r.Operation)
            {
                case SessionOperation.SetPaused: return r.Value == 0 || r.Value == 1;
                case SessionOperation.SetSpeed: return r.Value == 1 || r.Value == 2;
                case SessionOperation.SetControlMode: return r.Value == (int)CampControlMode.SharedCamp || r.Value == (int)CampControlMode.HostOnly;
                case SessionOperation.BeginLoad:
                case SessionOperation.Save: return r.Value >= 0 && r.Value <= 9;
                default: return r.Value == 0;
            }
        }

    }
}
