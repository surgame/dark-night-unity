using System;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证请求尺寸、非法目标、去重窗口和连接代次的拒绝路径，不以可靠传输代替业务去重。
    /// 并发测试只证明会话拒绝跨线程写入；连接模拟不替代 FishNet 独立进程认证及流量验收。
    /// </summary>
    public static class SessionBoundaryScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            InvalidInputs(check, catalog, layout);
            WindowAndConnections(check, catalog, layout);
            GlobalBound(check, catalog, layout);
            HostObservation(check, catalog, layout);
        }

        private static void HostObservation(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            var pending = Request(session, SessionOperation.Recruit, 1);
            session.Submit(host, pending);
            long tick = session.ServerTick;
            session.Disconnect(host, closeHostedSession: false);
            check(!session.Closed && !host.Ready && session.PlayerCount == 1,
                "Session Host observation can detach without closing the hosted world");
            check(session.Tick().Single().Code == SessionResultCode.InvalidConnection && session.ServerTick > tick,
                "Session detached Host intent is rejected while authoritative ticks continue");
            check(session.Submit(host, pending).Code == SessionResultCode.InvalidConnection &&
                !session.AcknowledgeReady(host, session.Epoch, session.Revision),
                "Session detached Host capability cannot submit or become Ready");
            check(Execute(session, guest, Request(session, SessionOperation.Recruit, 1)).Code == SessionResultCode.Applied,
                "Session remaining guest can operate the shared camp after local observation stops");
            var replacement = session.Connect(0);
            check(replacement.Generation > host.Generation &&
                session.AcknowledgeReady(replacement, session.Epoch, session.Revision),
                "Session local Host observation can obtain a fresh capability and snapshot");
            session.Disconnect(replacement);
            check(session.Closed, "Session explicit Host departure still closes authority");
        }

        private static void InvalidInputs(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            Execute(session, host, Request(session, SessionOperation.SetPaused, 1, value: 1));
            var codec = Codec(catalog, layout);
            var frozen = session.CaptureWorld();
            string before = codec.Serialize(frozen);
            int worker = frozen.Actors[0].Id;
            var invalid = new[]
            {
                Request(session, (SessionOperation)999, 1),
                Request(session, SessionOperation.Recruit, 0),
                Request(session, SessionOperation.IssueOrders, 2, new[] { worker }, x: float.NaN),
                Request(session, SessionOperation.IssueOrders, 3, new[] { worker }, x: float.PositiveInfinity),
                Request(session, SessionOperation.IssueOrders, 4, new[] { worker }, x: -1),
                Request(session, SessionOperation.IssueOrders, 5, new[] { worker }, x: layout.WorldWidth + 1),
                Request(session, SessionOperation.IssueOrders, 6, new[] { worker, int.MaxValue }, x: 200),
                Request(session, SessionOperation.IssueOrders, 7, new[] { worker }, target: int.MaxValue, x: 200),
                Request(session, SessionOperation.IssueOrders, 8, new[] { -1 }, x: 200),
                Request(session, SessionOperation.PlaceBuilding, 9, x: 184, kind: "unknown"),
                Request(session, SessionOperation.PlaceBuilding, 10, x: 184, kind: "tavern"),
                Request(session, SessionOperation.TrainActors, 11, new[] { worker }, kind: "zombie"),
                Request(session, SessionOperation.Repair, 12, target: worker),
                Request(session, SessionOperation.Recruit, 13, new[] { worker }),
                Request(session, SessionOperation.Recruit, 14, kind: "house"),
                Request(session, SessionOperation.SetSpeed, 15, value: 3),
                Request(session, SessionOperation.SetPaused, 16, value: 2),
                Request(session, SessionOperation.SetControlMode, 17, value: -1),
                Request(session, SessionOperation.BeginLoad, 18, value: 10)
            };
            foreach (var request in invalid)
                check(Execute(session, host, request).Code == SessionResultCode.InvalidRequest,
                    "Session rejects invalid parameter set " + request.Sequence + "/" + request.Operation);
            check(codec.Serialize(session.CaptureWorld()) == before, "Session malformed requests never partially modify paused world");
            check(Throws<ArgumentException>(() => Request(session, SessionOperation.IssueOrders, 19, new int[257])) &&
                Throws<ArgumentException>(() => Request(session, SessionOperation.PlaceBuilding, 19, kind: new string('a', 65))),
                "Session rejects oversized arrays and strings before copying them into queue");
            check(session.Submit(guest, null).Code == SessionResultCode.InvalidRequest, "Session null request rejected");
            check(session.Submit(guest, new SessionRequest(SessionOperation.Recruit, 99, session.Epoch, 0, 20)).Code == SessionResultCode.ProtocolMismatch,
                "Session mismatched protocol rejected before queuing");
            check(session.Submit(guest, new SessionRequest(SessionOperation.Recruit, 6, session.Epoch, 0, 20)).Code == SessionResultCode.ProtocolMismatch,
                "Session rejects protocol 6 before compressed-projection readiness");
            check(session.Submit(guest, new SessionRequest(SessionOperation.Recruit, SessionAuthority.ProtocolVersion, 999, 0, 20)).Code == SessionResultCode.EpochChanged,
                "Session mismatched epoch rejected before queuing");
            var ordered = Request(session, SessionOperation.IssueOrders, 20, new[] { frozen.Actors[0].Id, frozen.Actors[1].Id }, x: 220);
            Execute(session, guest, ordered);
            var altered = Request(session, SessionOperation.IssueOrders, 20, ordered.ActorIds.Reverse().ToArray(), x: 220);
            check(session.Submit(guest, altered).Code == SessionResultCode.SequenceConflict, "Session same sequence with changed ordered actors is rejected");
            check(session.Submit(guest, Request(session, SessionOperation.IssueOrders, 20, ordered.ActorIds, x: 221)).Code == SessionResultCode.SequenceConflict,
                "Session same sequence with changed coordinate is rejected");
            var task = Task.Run(() => Throws<InvalidOperationException>(() => session.Tick()));
            check(task.GetAwaiter().GetResult(), "Session foreign-thread mutation is rejected before work begins");
            Execute(session, host, Request(session, SessionOperation.StartNight, 21));
            Execute(session, host, Request(session, SessionOperation.SetPaused, 22, value: 0));
            Execute(session, host, Request(session, SessionOperation.SetPaused, 23, value: 1));
            int enemy = session.CaptureWorld().Actors.First(a => a.Enemy).Id;
            check(Execute(session, guest, Request(session, SessionOperation.IssueOrders, 21, new[] { worker, enemy }, x: 220)).Code == SessionResultCode.InvalidRequest,
                "Session mixed friendly and enemy actor list is rejected as a whole");
        }

        private static void WindowAndConnections(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            Execute(session, host, Request(session, SessionOperation.SetPaused, 1, value: 1));
            var first = Request(session, SessionOperation.Recruit, 1);
            for (int i = 1; i <= SessionAuthority.MaximumPendingPerPlayer; i++)
                session.Submit(guest, Request(session, SessionOperation.Recruit, i));
            check(session.PendingCount == 16 && session.Submit(guest, Request(session, SessionOperation.Recruit, 17)).Code == SessionResultCode.QueueFull,
                "Session per-connection backlog is bounded and overflow is explicit");
            var results = session.Tick();
            check(results.Count(r => r.Code == SessionResultCode.Applied) == 1 && session.CaptureWorld().Economy.Resources.Food == 48,
                "Session queued recruitment observes latest cooldown and only pays once");
            check(session.Submit(guest, first).Code == SessionResultCode.Applied, "Session old cached request retains original result after later failures");
            for (int i = 17; i <= 81; i++) Execute(session, guest, Request(session, SessionOperation.Recruit, i));
            check(session.Submit(guest, first).Code == SessionResultCode.SequenceExpired && session.PendingCount == 0,
                "Session evicted sequence never executes again");
            check(session.Submit(guest, Request(session, SessionOperation.Recruit, 80)).Code == SessionResultCode.NoEffect,
                "Session recent cached sequence retains failed business result");
            Execute(session, guest, Request(session, SessionOperation.Recruit, 100));
            check(session.Submit(guest, Request(session, SessionOperation.Recruit, 99)).Code == SessionResultCode.SequenceExpired,
                "Session unseen out-of-order old sequence is explicitly expired");
            session.Submit(guest, Request(session, SessionOperation.PlaceBuilding, 101, x: 184, kind: "house"));
            var replacement = session.Connect(1);
            check(replacement.Generation == guest.Generation + 1 && !replacement.Ready && !guest.Ready,
                "Session reconnect rotates capability and Ready without inheriting pending intent");
            check(session.Tick().Single().Code == SessionResultCode.InvalidConnection && session.CaptureWorld().Economy.Resources.Wood == 100,
                "Session queued old connection cannot pay after reconnect");
            check(session.Submit(guest, first).Code == SessionResultCode.InvalidConnection &&
                !session.AcknowledgeReady(guest, session.Epoch, session.Revision), "Session old capability cannot submit or acknowledge Ready");
            check(session.Submit(replacement, Request(session, SessionOperation.Recruit, 1)).Code == SessionResultCode.NotReady,
                "Session new connection must apply a current baseline first");
            check(!session.AcknowledgeReady(replacement, session.Epoch, 0) &&
                !session.AcknowledgeReady(replacement, session.Epoch, session.Revision + 1), "Session Ready rejects stale and future baseline revisions");
            check(session.AcknowledgeReady(replacement, session.Epoch, session.Revision) &&
                Execute(session, replacement, Request(session, SessionOperation.PlaceBuilding, 1, x: 184, kind: "house")).Code == SessionResultCode.Applied,
                "Session reconnected player starts a fresh sequence namespace after Ready");
            using var other = Open(catalog, layout, out var otherHost, out var otherGuest);
            check(session.Submit(otherGuest, Request(session, SessionOperation.Recruit, 2)).Code == SessionResultCode.InvalidConnection,
                "Session rejects capability issued by another authority");
            session.Disconnect(replacement);
            check(session.PlayerCount == 1 && session.CaptureWorld().Buildings.Last().WorkerId != 0,
                "Session guest disconnect retains accepted construction and residents");
            session.Disconnect(host);
            check(session.Closed && session.PendingCount == 0 && session.Tick().Count == 0 &&
                session.Submit(host, first).Code == SessionResultCode.SessionClosed, "Session Host disconnect closes authority and clears pending work");
        }

        private static void GlobalBound(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Create(catalog, layout);
            for (int slot = 0; slot < 4; slot++)
            {
                var connection = session.Connect(slot);
                session.AcknowledgeReady(connection, session.Epoch, session.Revision);
                for (int sequence = 1; sequence <= 16; sequence++)
                    session.Submit(connection, Request(session, SessionOperation.Recruit, sequence));
            }
            var replacement = session.Connect(1);
            session.AcknowledgeReady(replacement, session.Epoch, session.Revision);
            check(session.PlayerCount == 4 && session.PendingCount == 64 &&
                session.Submit(replacement, Request(session, SessionOperation.Recruit, 1)).Code == SessionResultCode.QueueFull,
                "Session reconnect cannot bypass the global four-player backlog bound");
            var results = session.Tick();
            check(results.Count == 64 && results.Count(r => r.Code == SessionResultCode.InvalidConnection) == 16 && session.PendingCount == 0,
                "Session drains stale connection backlog once with explicit rejection receipts");
        }
    }
}
