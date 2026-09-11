using System;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Save;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 用真实权威世界比较不同渲染帧率与固定 60 Hz 的完整结果，验证追帧余量、暂停、倍速和线程约束。
    /// 本测试只验证时钟适配算法；Unity Update 装配和 Player 帧时须另行实测。
    /// </summary>
    public static class SessionClockScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var codec = new GameSaveJson(catalog, layout);
            foreach (int fps in new[] { 30, 60, 144 })
            {
                using var session = Open(catalog, layout, out _, out _);
                var clock = new SessionClock(session);
                var direct = new GameSession(catalog, layout);
                for (int i = 0; i < fps * 10; i++) clock.Advance(1.0 / fps);
                for (int i = 0; i < 600; i++) direct.Advance(1.0 / 60);
                check(session.ServerTick == 600 && clock.PendingSeconds < 1e-8 &&
                    codec.Serialize(session.CaptureWorld()) == codec.Serialize(SnapshotMapper.Capture(direct)),
                    "Clock " + fps + " FPS matches ten seconds of complete 60 Hz Core state");
            }
            using var game = Open(catalog, layout, out var host, out _);
            var driver = new SessionClock(game, 8);
            var speed = Request(game, SessionOperation.SetSpeed, 1, value: 2);
            game.Submit(host, speed);
            var results = driver.Advance(1);
            check(driver.LastSteps == 8 && game.ServerTick == 8 && driver.PendingSeconds > 0.8 &&
                results.Count == 1 && results[0].Code == SessionResultCode.Applied, "Clock limits catch-up and returns ordered business receipts without losing backlog");
            while (driver.PendingSeconds > 1e-8) driver.Advance(0);
            check(game.ServerTick == 60 && RuleScenario.Approx(game.CaptureProjection().Elapsed, 2),
                "Clock drains backlog exactly and applies speed only once");
            game.Submit(host, Request(game, SessionOperation.SetPaused, 2, value: 1));
            driver.Advance(1.0 / 60);
            double elapsed = game.CaptureProjection().Elapsed;
            long tick = game.ServerTick;
            for (int i = 0; i < 60; i++) driver.Advance(1.0 / 60);
            check(game.ServerTick == tick + 60 && game.CaptureProjection().Elapsed == elapsed,
                "Clock keeps session ticks running while paused without advancing simulation");
            var load = Request(game, SessionOperation.BeginLoad, 3);
            game.Submit(host, load);
            var ticket = driver.Advance(1.0 / 60)[0];
            tick = game.ServerTick;
            driver.Advance(0.1);
            check(game.ServerTick == tick + 6 && game.CaptureProjection().Elapsed == elapsed && game.Loading,
                "Clock keeps service alive while loading freezes world");
            game.CancelLoad(ticket);
            tick = game.ServerTick;
            double backlog = driver.PendingSeconds;
            foreach (double invalid in new[] { -1.0, double.NaN, double.PositiveInfinity, double.MaxValue })
                check(Throws<ArgumentOutOfRangeException>(() => driver.Advance(invalid)), "Clock rejects invalid elapsed input " + invalid);
            check(game.ServerTick == tick && driver.PendingSeconds == backlog, "Clock invalid input preserves world and accumulator");
            check(Throws<ArgumentOutOfRangeException>(() => driver.Advance(SessionClock.MaximumBacklogSeconds + 1)) &&
                driver.PendingSeconds == backlog, "Clock excessive stall fails explicitly without silently dropping elapsed time");
            check(Task.Run(() => Throws<InvalidOperationException>(() => driver.Advance(0))).Result &&
                Task.Run(() => Throws<InvalidOperationException>(() => new SessionClock(game))).Result,
                "Clock cannot run or bind authority on a foreign thread");
            check(Throws<ArgumentOutOfRangeException>(() => new SessionClock(game, 0)), "Clock requires a positive bounded step budget");
            game.Dispose();
            check(driver.Advance(1).Count == 0 && driver.LastSteps == 0 && driver.PendingSeconds == 0,
                "Clock stops and releases backlog after Host session disposal");
        }
    }
}
