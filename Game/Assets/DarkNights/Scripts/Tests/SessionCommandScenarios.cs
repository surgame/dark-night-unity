using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 在冻结开局规则下验证统一入口、多人接受次序、一次支付与权限切换。
    /// 直接 Core 对照仅存在于测试中，用完整世界快照检查会话包装没有改变采集、施工和训练规则。
    /// </summary>
    public static class SessionCommandScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            Gameplay(check, catalog, layout);
            Permissions(check, catalog, layout);
            Payments(check, catalog, layout);
        }

        private static void Gameplay(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            var direct = new GameSession(catalog, layout);
            var before = session.CaptureWorld();
            int[] workers = before.Actors.Where(a => a.Kind == "worker").Select(a => a.Id).ToArray();
            int wood = before.Worksites.First(s => s.Kind == "wood").Id;
            var input = new[] { workers[0], workers[0] };
            var gather = Request(session, SessionOperation.IssueOrders, 1, input, wood, 402);
            input[0] = int.MaxValue;
            check(gather.ActorIds.SequenceEqual(new[] { workers[0] }), "Session freezes and deduplicates actors in first occurrence order");
            var pending = session.Submit(guest, gather);
            check(pending.Code == SessionResultCode.Pending && ReferenceEquals(session.Submit(guest, gather), pending) &&
                session.PendingCount == 1, "Session retransmission while pending queues one operation");
            var receipt = session.Tick().Single();
            check(receipt.Code == SessionResultCode.Applied && receipt.AffectedCount == 1 && receipt.PlayerSlot == 1 &&
                receipt.ServerTick == 1 && receipt.Revision > 0, "Session receipt identifies origin, tick and resulting revision");
            check(ReferenceEquals(session.Submit(guest, gather), receipt), "Session retransmission returns original completed receipt");
            direct.Orders.Issue(new[] { workers[0] }, wood, 402);
            direct.Advance(1.0 / 60);
            for (int tick = 1; tick < 720; tick++) { session.Tick(); direct.Advance(1.0 / 60); }
            var codec = new GameSaveJson(catalog, layout);
            check(codec.Serialize(session.CaptureWorld()) == codec.Serialize(SnapshotMapper.Capture(direct)) &&
                session.CaptureWorld().Economy.Resources.Wood == 103, "Session 60 Hz gathering matches complete Core world after 12 seconds");
            check(before.Economy.Resources.Wood == 100 && before.Actors[0].State == ActorActivity.Idle,
                "Session capture remains frozen after later simulation");
            var build = Request(session, SessionOperation.PlaceBuilding, 1, new[] { workers[1] }, x: 184, kind: "house");
            check(Execute(session, host, build).EntityId == before.NextEntityId, "Session Host builds through the same entry and receives created entity ID");
            direct.Construction.Place("house", 184, new[] { workers[1] });
            direct.Advance(1.0 / 60);
            for (int tick = 0; tick < 1800; tick++) { session.Tick(); direct.Advance(1.0 / 60); }
            check(codec.Serialize(session.CaptureWorld()) == codec.Serialize(SnapshotMapper.Capture(direct)),
                "Session worker travel and completed house preserve full Core world");
            var pause = Request(session, SessionOperation.SetPaused, 2, value: 1);
            Execute(session, host, pause);
            direct.Paused = true;
            var train = Request(session, SessionOperation.TrainActors, 2, new[] { workers[4], workers[2], workers[4] }, kind: "archer");
            var training = Execute(session, guest, train);
            direct.Training.Start("archer", new[] { workers[4], workers[2] });
            check(training.AffectedCount == 2 && codec.Serialize(session.CaptureWorld()) == codec.Serialize(SnapshotMapper.Capture(direct)),
                "Session paused training preserves ordered unique payment and queue semantics");
        }

        private static void Permissions(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            Execute(session, host, Request(session, SessionOperation.SetPaused, 1, value: 1));
            var initial = session.CaptureWorld();
            int worker = initial.Actors[0].Id;
            int wood = initial.Worksites.First(s => s.Kind == "wood").Id;
            Execute(session, guest, Request(session, SessionOperation.IssueOrders, 1, new[] { worker }, wood, 402));
            var stale = Request(session, SessionOperation.PlaceBuilding, 2, x: 184, kind: "house");
            var mode = Request(session, SessionOperation.SetControlMode, 2, value: (int)CampControlMode.HostOnly);
            session.Submit(host, mode);
            session.Submit(guest, stale);
            var changed = session.Tick();
            check(changed[0].Code == SessionResultCode.Applied && changed[1].Code == SessionResultCode.PolicyChanged &&
                session.PolicyRevision == 1 && session.Epoch == 1, "Session policy switch rejects queued old intent without changing epoch");
            check(session.CaptureWorld().Economy.Resources.Wood == 100 && session.CaptureWorld().Actors[0].TargetId == wood,
                "Session policy switch preserves existing work and rejects old auto-construction payment");
            var denied = new[]
            {
                Request(session, SessionOperation.IssueOrders, 3, new[] { worker }, x: 200),
                Request(session, SessionOperation.PlaceBuilding, 4, x: 184, kind: "house"),
                Request(session, SessionOperation.TrainActors, 5, new[] { worker }, kind: "spearman"),
                Request(session, SessionOperation.Recruit, 6),
                Request(session, SessionOperation.Repair, 7, target: initial.Buildings[0].Id)
            };
            foreach (var request in denied)
                check(Execute(session, guest, request).Code == SessionResultCode.PermissionDenied, "Session HostOnly rejects guest " + request.Operation);
            check(session.CaptureWorld().Economy.Resources == initial.Economy.Resources, "Session denied operations cannot alter any resource");
            check(Execute(session, host, Request(session, SessionOperation.Recruit, 3)).Code == SessionResultCode.Applied,
                "Session HostOnly still permits Host recruitment");
            Execute(session, host, Request(session, SessionOperation.SetControlMode, 4, value: (int)CampControlMode.SharedCamp));
            long sequence = 8;
            foreach (var operation in new[] { SessionOperation.SetPaused, SessionOperation.SetSpeed, SessionOperation.StartNight,
                SessionOperation.SetControlMode, SessionOperation.BeginLoad })
            {
                int value = operation == SessionOperation.SetSpeed ? 2 : 0;
                check(Execute(session, guest, Request(session, operation, sequence++, value: value)).Code == SessionResultCode.PermissionDenied,
                    "Session SharedCamp retains Host privilege for " + operation);
            }
            session.Submit(host, Request(session, SessionOperation.IssueOrders, 5, new[] { worker }, x: 200));
            session.Submit(guest, Request(session, SessionOperation.IssueOrders, sequence, new[] { worker }, x: 300));
            session.Tick();
            check(session.CaptureWorld().Actors[0].MoveX == 300, "Session last accepted legal command wins for shared resident");
            Execute(session, host, Request(session, SessionOperation.SetPaused, 6, value: 0));
            for (int i = 0; i < 60; i++) session.Tick();
            check(session.CaptureWorld().Actors[0].X > initial.Actors[0].X, "Session unpause resumes existing accepted orders");
        }

        private static void Payments(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            Execute(session, host, Request(session, SessionOperation.SetPaused, 1, value: 1));
            var third = session.Connect(2);
            session.AcknowledgeReady(third, session.Epoch, session.Revision);
            var requests = new[]
            {
                Request(session, SessionOperation.PlaceBuilding, 2, x: 184, kind: "tower"),
                Request(session, SessionOperation.PlaceBuilding, 1, x: 708, kind: "tower"),
                Request(session, SessionOperation.PlaceBuilding, 1, x: 756, kind: "tower")
            };
            session.Submit(host, requests[0]);
            session.Submit(guest, requests[1]);
            session.Submit(third, requests[2]);
            var results = session.Tick();
            check(results.Select(r => r.Code).SequenceEqual(new[] { SessionResultCode.Applied, SessionResultCode.Applied, SessionResultCode.NoEffect }),
                "Session three concurrent constructions only spend available resources in accepted order");
            var after = session.CaptureWorld();
            check(after.Economy.Resources.Wood == 10 && after.Economy.Resources.Stone == 10 && after.Buildings.Count == 6,
                "Session insufficient third purchase leaves resources and entity count intact");
            check(ReferenceEquals(session.Submit(host, requests[0]), results[0]) && session.PendingCount == 0,
                "Session Host resend cannot pay a second time");
            int[] workers = after.Actors.Where(a => a.Kind == "worker").Select(a => a.Id).Reverse().ToArray();
            var train = Execute(session, guest, Request(session, SessionOperation.TrainActors, 2, workers, kind: "spearman"));
            var trained = session.CaptureWorld();
            check(train.AffectedCount == 2 && trained.Economy.Resources.Wood == 0 &&
                trained.Buildings.First(b => b.Kind == "barracks").TrainingQueue.Select(t => t.ActorId).SequenceEqual(workers.Take(2)),
                "Session training with insufficient resources preserves ordered partial success");
            int site = trained.Worksites.First(s => s.Kind == "stone").Id;
            session.Submit(host, Request(session, SessionOperation.IssueOrders, 3, new[] { workers[2] }, site, 548));
            session.Submit(guest, Request(session, SessionOperation.IssueOrders, 3, new[] { workers[3] }, site, 548));
            var claims = session.Tick();
            check(claims[0].AffectedCount == 1 && claims[1].Code == SessionResultCode.NoEffect &&
                session.CaptureWorld().Worksites.First(s => s.Id == site).WorkerId == workers[2],
                "Session simultaneous requests cannot steal the only stone worksite");
        }
    }
}
