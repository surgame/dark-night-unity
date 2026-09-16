using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证唯一时钟、暂停时业务以及受控加载的完整生命周期；加载使用真实 v3 存档解析与对象替换。
    /// 失败或取消不得替换世界，成功必须保留房间策略、轮换 epoch、清除 Ready 并拒绝旧票据。
    /// </summary>
    public static class SessionLifecycleScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            Clock(check, catalog, layout);
            Loading(check, catalog, layout);
        }

        private static void Clock(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Create(catalog, layout);
            var host = session.Connect(0);
            var guest = session.Connect(1);
            session.Tick();
            session.AcknowledgeReady(guest, session.Epoch, session.Revision);
            session.Tick();
            check(session.CaptureWorld().Elapsed == 0 && session.ServerTick == 2, "Session clock waits for initial Host Ready while service ticks continue");
            session.AcknowledgeReady(host, session.Epoch, session.Revision);
            Execute(session, host, Request(session, SessionOperation.SetSpeed, 1, value: 2));
            for (int i = 1; i < 60; i++) session.Tick();
            check(RuleScenario.Approx(session.CaptureWorld().Elapsed, 2), "Session 60 ticks at 2x advance exactly two simulation seconds");
            Execute(session, host, Request(session, SessionOperation.SetPaused, 2, value: 1));
            long pausedTick = session.ServerTick;
            double elapsed = session.CaptureWorld().Elapsed;
            Execute(session, guest, Request(session, SessionOperation.PlaceBuilding, 1, x: 184, kind: "house"));
            for (int i = 0; i < 60; i++) session.Tick();
            check(session.ServerTick == pausedTick + 61 && session.CaptureWorld().Elapsed == elapsed &&
                session.CaptureWorld().Buildings.Last().Progress == 0 && session.CaptureWorld().Economy.Resources.Wood == 75,
                "Session paused ticks process authorized payment without advancing construction or simulation");
            Execute(session, host, Request(session, SessionOperation.SetSpeed, 3, value: 1));
            Execute(session, host, Request(session, SessionOperation.SetPaused, 4, value: 0));
            check(RuleScenario.Approx(session.CaptureWorld().Elapsed, elapsed + 1.0 / 60), "Session resume applies the current speed once");
        }

        private static void Loading(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            Execute(session, host, Request(session, SessionOperation.SetPaused, 1, value: 1));
            Execute(session, host, Request(session, SessionOperation.SetSpeed, 2, value: 2));
            Execute(session, host, Request(session, SessionOperation.SetControlMode, 3, value: (int)CampControlMode.HostOnly));
            var codec = Codec(catalog, layout);
            string before = codec.Serialize(session.CaptureWorld());
            var begin = Request(session, SessionOperation.BeginLoad, 4, value: 2);
            session.Submit(host, begin);
            session.Submit(host, Request(session, SessionOperation.Recruit, 5));
            var results = session.Tick();
            var ticket = results[0];
            check(ticket.Code == SessionResultCode.Applied && results[1].Code == SessionResultCode.Loading && session.Loading,
                "Session BeginLoad locks out later queued business in the same tick");
            check(ReferenceEquals(session.Submit(host, begin), ticket), "Session BeginLoad resend returns same ticket without starting another load");
            check(session.Submit(host, Request(session, SessionOperation.Recruit, 6)).Code == SessionResultCode.Loading &&
                !session.AcknowledgeReady(guest, session.Epoch, session.Revision), "Session loading blocks new business and Ready");
            long tick = session.ServerTick;
            session.Tick();
            check(session.ServerTick == tick + 1 && codec.Serialize(session.CaptureWorld()) == before,
                "Session loading keeps service ticks alive and world frozen");
            check(Throws<FormatException>(() => session.CompleteLoad(ticket, "{}")) && !session.Loading && session.Epoch == 1 &&
                codec.Serialize(session.CaptureWorld()) == before && host.Ready && guest.Ready,
                "Session failed parsing preserves whole world, epoch, Ready, pause and speed");
            check(ReferenceEquals(session.Submit(host, begin), ticket) && !session.Loading,
                "Session failed load cannot be restarted by retransmitting the same request");
            var cancelTicket = Execute(session, host, Request(session, SessionOperation.BeginLoad, 6));
            session.CancelLoad(cancelTicket);
            check(!session.Loading && codec.Serialize(session.CaptureWorld()) == before &&
                Throws<InvalidOperationException>(() => session.CancelLoad(cancelTicket)), "Session cancellation releases loading once without changing world");
            using var source = World(catalog, layout);
            source.IssueOrders(new[] { 11 }, 6, 402);
            for (int i = 0; i < 360; i++) source.Advance(1.0 / 60);
            string saved = codec.Serialize(source.CaptureWorld());
            var load = Request(session, SessionOperation.BeginLoad, 7);
            var success = Execute(session, host, load);
            check(Throws<InvalidOperationException>(() => session.CompleteLoad(ticket, saved)) && session.Loading,
                "Session stale completion ticket cannot complete or cancel a newer load");
            session.CompleteLoad(success, saved);
            check(session.Epoch == 2 && session.Revision == 0 && session.PolicyRevision == 1 &&
                session.ControlMode == CampControlMode.HostOnly && !session.Loading && session.ReadyCount == 0,
                "Session successful load rotates epoch and Ready while preserving current room policy");
            check(codec.Serialize(session.CaptureWorld()) == saved, "Session successful load replaces entire world including RNG, projectiles and stored time controls");
            check(session.Submit(host, load).Code == SessionResultCode.EpochChanged &&
                !session.AcknowledgeReady(guest, 1, 0), "Session rejects old epoch business and Ready after load");
            check(Throws<InvalidOperationException>(() => session.CompleteLoad(success, saved)), "Session successful load ticket cannot be replayed");
            session.Tick();
            check(codec.Serialize(session.CaptureWorld()) == saved, "Session restored simulation waits for new Host baseline Ready");
            check(session.AcknowledgeReady(host, 2, session.Revision) && session.AcknowledgeReady(guest, 2, session.Revision),
                "Session active connections can acknowledge the new world");
            check(Execute(session, guest, Request(session, SessionOperation.Recruit, 1)).Code == SessionResultCode.PermissionDenied,
                "Session restored save cannot expand guest permissions");
            // 上面的拒绝已推进一次；两局分别运行同样的 1200 个固定步检查恢复确定性。
            for (int i = 1; i < 1200; i++) session.Tick();
            for (int i = 0; i < 1200; i++) source.Advance(1.0 / 60);
            check(codec.Serialize(session.CaptureWorld()) == codec.Serialize(source.CaptureWorld()),
                "Loaded object world continues twenty seconds deterministically");
            var closing = Execute(session, host, Request(session, SessionOperation.BeginLoad, 1));
            session.Disconnect(host);
            check(Throws<InvalidOperationException>(() => session.CompleteLoad(closing, saved)), "Session Host exit invalidates an outstanding asynchronous load ticket");
        }
    }
}
