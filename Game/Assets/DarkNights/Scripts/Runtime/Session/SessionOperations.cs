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
        internal static bool HostRequired(SessionOperation operation) => (operation >= SessionOperation.SetPaused && operation <= SessionOperation.Restart) || SessionDeveloperOperations.IsOperation(operation);

        internal static SessionResultCode ValidateEnvelope(bool closed, bool activeConnection, int epoch, SessionRequest request)
        {
            if (closed) return SessionResultCode.SessionClosed;
            if (!activeConnection) return SessionResultCode.InvalidConnection;
            if (!ValidShape(request)) return SessionResultCode.InvalidRequest;
            if (request.Protocol != SessionAuthority.ProtocolVersion) return SessionResultCode.ProtocolMismatch;
            if (request.Epoch != epoch) return SessionResultCode.EpochChanged;
            return SessionResultCode.Applied;
        }

        internal static bool ValidShape(SessionRequest r)
        {
            if (r == null || r.Sequence <= 0 || r.PolicyRevision < 0 ||
                !Enum.IsDefined(typeof(SessionOperation), r.Operation) || float.IsNaN(r.X) || float.IsInfinity(r.X) ||
                r.TargetId < 0 || r.ActorIds.Any(id => id <= 0)) return false;
            if (SessionDeveloperOperations.IsOperation(r.Operation)) return SessionDeveloperOperations.ValidShape(r);
            if (r.Operation is SessionOperation.SelectDestination or SessionOperation.CancelJourney)
                return r.ActorIds.Count == 1 && r.TargetId > 0 && r.X == 0 && r.ControlLease > 0 && r.Value >= 0 &&
                    (r.Operation == SessionOperation.SelectDestination ? r.Kind.Length > 0 : r.Kind.Length == 0);
            if (r.Operation == SessionOperation.Expedition)
                return r.ActorIds.Count <= 1 && r.X == 0 && r.Value == 0 && r.Kind.Length > 0 &&
                    new[] { "depart", "unload", "board", "recall", "launch", "emergency", "robot", "cargo", "crew", "mine", "resupply", "pilot", "takeoff", "land", "cancel-flight", "deploy" }.Contains(r.Kind);
            if (r.Operation == SessionOperation.SellCarriedOre)
                return r.ActorIds.Count == 1 && r.ControlLease > 0 && r.TargetId > 0 &&
                    r.Kind == "sale" && r.Value >= 0 && r.X >= 0 && r.X <= 100000 && r.X == (int)r.X;
            if (r.Operation == SessionOperation.BuyEquipment)
                return r.ActorIds.Count == 1 && r.ControlLease > 0 && r.TargetId > 0 &&
                    r.X == 0 && r.Value >= 0 &&
                    (r.Kind == "pistol" || r.Kind == "pickaxe" || r.Kind == "jetpack");
            if (r.Operation == SessionOperation.SetHeroLight)
                return r.ActorIds.Count == 1 && r.ControlLease > 0 && r.TargetId == 0 &&
                    r.X == 0 && r.Kind.Length == 0 && (r.Value == 0 || r.Value == 1);
            if (SessionHeroControl.IsOperation(r.Operation))
                return r.ActorIds.Count == 1 && r.X == 0 &&
                    (r.Operation == SessionOperation.ClaimHero ? r.ControlLease == 0 : r.ControlLease > 0) &&
                    (r.Operation == SessionOperation.UseHeroItem ? r.Value >= 0 &&
                        (r.Kind == "pickaxe" && r.TargetId > 0) :
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
