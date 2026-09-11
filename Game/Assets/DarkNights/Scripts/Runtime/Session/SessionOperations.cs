using System;
using System.Linq;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 将已授权请求映射到现有 Core 显式操作；不持有第二套经济、HP 或单位选择。
    /// 先检查整个参数集合，再沿用原规则的支付、自动选工人与训练部分成功顺序。
    /// </summary>
    internal static class SessionOperations
    {
        internal static bool HostRequired(SessionOperation operation) => operation >= SessionOperation.SetPaused;

        internal static bool ValidShape(SessionRequest r)
        {
            if (r == null || r.Sequence <= 0 || r.PolicyRevision < 0 ||
                !Enum.IsDefined(typeof(SessionOperation), r.Operation) || float.IsNaN(r.X) || float.IsInfinity(r.X) ||
                r.TargetId < 0 || r.ActorIds.Any(id => id <= 0)) return false;
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

        internal static bool ValidWorld(GameSession world, SessionRequest r)
        {
            foreach (int id in r.ActorIds)
            {
                var actor = world.World.Find<Actor>(id);
                if (actor == null || actor.Enemy || actor.Hp <= 0) return false;
            }
            switch (r.Operation)
            {
                case SessionOperation.IssueOrders:
                    return r.ActorIds.Count > 0 && r.X >= 0 && r.X <= world.Layout.WorldWidth &&
                        (r.TargetId == 0 || world.World.Find(r.TargetId) != null);
                case SessionOperation.PlaceBuilding:
                    return r.X >= 0 && r.X <= world.Layout.WorldWidth && r.Kind != "tavern" &&
                        world.Catalog.Balance.Buildings.ContainsKey(r.Kind);
                case SessionOperation.TrainActors:
                    return r.ActorIds.Count > 0 && (r.Kind == "spearman" || r.Kind == "archer");
                case SessionOperation.Repair: return world.World.Find<Building>(r.TargetId) != null;
                default: return true;
            }
        }

        internal static int Apply(GameSession world, SessionRequest r, out int entityId)
        {
            entityId = 0;
            int nextId = world.World.NextId;
            switch (r.Operation)
            {
                case SessionOperation.IssueOrders: return world.Orders.Issue(r.ActorIds, r.TargetId, r.X);
                case SessionOperation.TrainActors: return world.Training.Start(r.Kind, r.ActorIds);
                case SessionOperation.PlaceBuilding:
                    if (!world.Construction.Place(r.Kind, r.X, r.ActorIds)) return 0;
                    entityId = nextId;
                    return 1;
                case SessionOperation.Recruit:
                    if (!world.Camp.Recruit()) return 0;
                    entityId = nextId;
                    return 1;
                case SessionOperation.Repair:
                    if (!world.Camp.Repair(r.TargetId)) return 0;
                    entityId = r.TargetId;
                    return 1;
                case SessionOperation.SetPaused: world.Paused = r.Value == 1; return 1;
                case SessionOperation.SetSpeed: world.Speed = r.Value; return 1;
                case SessionOperation.StartNight:
                    if (world.Waves.Phase != WavePhase.Day || world.Mode != SessionMode.Playing) return 0;
                    world.Waves.StartNight();
                    return 1;
                default: throw new InvalidOperationException("Operation belongs to session lifecycle.");
            }
        }
    }
}
