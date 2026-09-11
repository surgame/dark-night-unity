using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using Newtonsoft.Json;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 从真实权威命令验证反馈窗口、冻结、业务重发和切 epoch 清理；与独立 .NET 和 Editor 共用。
    /// 不重写原规则夹具，也不把表现通知当成新的经济状态或网络可靠性结论。
    /// </summary>
    public static class SessionEventScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            var first = session.CaptureProjection();
            check(first.Events.Count == 2 && first.Events[0].Type == "banner" && first.Events[1].Type == "message",
                "Journal captures original new-game feedback before constructor completes");
            string frozen = JsonConvert.SerializeObject(first.Events);
            Execute(session, host, Request(session, SessionOperation.SetPaused, 1, value: 1));
            int actor = first.World.Actors.First(a => a.Kind == "worker").Id;
            SessionRequest last = null;
            for (int i = 1; i <= 150; i++)
            {
                last = Request(session, SessionOperation.IssueOrders, i, new[] { actor }, x: 600 + i % 4);
                Execute(session, guest, last);
            }
            var current = session.CaptureProjection();
            check(current.Events.Count == SessionViewData.MaximumEvents && current.Events[0].Sequence > 1,
                "Presentation window is bounded and discards only old event records");
            check(current.Events.All(e => e.Tick <= current.ServerTick) && current.Elapsed == 0,
                "Event lifetime clock advances while simulation is paused");
            check(JsonConvert.SerializeObject(first.Events) == frozen, "Event snapshots remain frozen after journal changes");
            check(Throws<NotSupportedException>(() => ((IList<PresentationEvent>)current.Events).Clear()), "Event list is not writable");
            long sequence = current.Events.Last().Sequence;
            session.Submit(guest, last);
            session.Tick();
            check(session.CaptureProjection().Events.Last().Sequence == sequence, "Duplicate business request cannot emit another command ring");
            var codec = new GameSaveJson(catalog, layout);
            string saved = codec.Serialize(session.CaptureWorld());
            var ticket = Execute(session, host, Request(session, SessionOperation.BeginLoad, 2));
            session.CompleteLoad(ticket, saved);
            var restored = session.CaptureProjection();
            check(restored.Epoch == first.Epoch + 1 && restored.Events.Count == 0, "Loading clears old-world transient events");
            check(!saved.Contains("PresentationEvent") && !saved.Contains("Events"), "Presentation journal is not a persistent gameplay state");
            var cursor = new PresentationCursor();
            check(cursor.Consume(first).Count == 2 && cursor.Consume(first).Count == 0,
                "Repeated projection consumes initial feedback only once");
            cursor.Reset();
            check(cursor.Consume(first).Count == 2, "A new connection explicitly resets the local event cursor");
            var recent = new PresentationEvent(3, 400, "sound", "snd_training_start");
            var old = new PresentationEvent(4, 0, "effect", cue: new VisualCue("corpse", 100, 320, ContentId: "worker"));
            var rubble = new PresentationEvent(5, 0, "effect", cue: new VisualCue("rubble", 100, 320, ContentId: "house"));
            var late = new SessionViewData(4, first.Epoch, 0, 600, 0, false, 2, 2, false, true, 1, 0,
                first.World, new[] { recent, old, rubble });
            check(cursor.Consume(late).Select(e => e.Sequence).SequenceEqual(new long[] { 5 }),
                "Late join skips expired sounds and corpses but restores an unexpired rubble effect");
            check(cursor.Consume(first).Count == 0, "An older publication cannot replay already consumed events");
            var newWorld = new SessionViewData(5, first.Epoch + 1, 0, 0, 0, false, 2, 2, false, true, 1, 0,
                first.World, first.Events);
            check(cursor.Consume(newWorld).Count == 2, "A new epoch accepts fresh event sequence numbers");
            var retained = new SessionViewData(6, newWorld.Epoch, 0, 600, 0, false, 2, 2, false, true, 1, 0,
                first.World, new[] { new PresentationEvent(500, 600, "message", "recent") }, new[] { rubble });
            check(cursor.Consume(retained).Select(e => e.Sequence).OrderBy(s => s).SequenceEqual(new long[] { 5, 500 }),
                "An unexpired remnant survives eviction from the recent event window");
            check(cursor.Consume(retained).Count == 0, "Active remnant snapshots do not repeatedly create views");
            var feedback = new SessionFeedback();
            long tick = 0;
            using (var journal = new SessionEventJournal(feedback, () => tick))
            {
                feedback.Emit(new VisualCue("rubble", 100, 320, ContentId: "house"));
                var original = journal.FreezeRemnants();
                for (int i = 0; i < 200; i++) feedback.Notify("dense notification");
                check(journal.Freeze().All(e => e.Cue == null) && journal.FreezeRemnants().Count == 1,
                    "Actual journal preserves live rubble after two hundred later notifications");
                tick = 180 * 60;
                check(journal.FreezeRemnants().Count == 0 && original.Count == 1,
                    "Actual remnant retention expires at its source lifetime without modifying old snapshots");
            }
            Timeline(check, first);
        }

        private static void Timeline(Action<bool, string> check, SessionViewData first)
        {
            var before = first.World.Actors[0];
            var after = new ActorViewData(before.Id, before.Kind, before.Name, before.Enemy, before.X + 10, before.Hp,
                before.Activity, before.TargetId, before.Face, before.Walking, before.ActionTime + 1, before.Windup, before.HitFlash);
            var world = new WorldViewData(first.World.Camp, new[] { after }, first.World.Buildings, first.World.Worksites, first.World.Projectiles);
            var second = new SessionViewData(2, first.Epoch, 1, first.ServerTick + 6, 0, false, 2, 2, false, false, 1, 1, world);
            var timeline = new PresentationTimeline(); timeline.Push(first, 0); timeline.Push(second, 1);
            check(Math.Abs(timeline.X(after, 1.05) - (before.X + 5)) < 0.001, "Actor display interpolates between two authoritative positions");
            check(timeline.X(after, 10) == after.X, "Missing packets never extrapolate actor movement");
            check(Math.Abs(timeline.ActionTime(after, 1.05) - (before.ActionTime + 0.5)) < 0.001, "Continuous animation time interpolates without model writes");
            var paused = new SessionViewData(3, first.Epoch, 2, first.ServerTick + 12, 0, false, 2, 2, false, true, 1, 1, world);
            timeline.Push(paused, 2);
            check(timeline.X(after, 2) == after.X, "Pausing displays the final authorized position immediately");
            timeline.Reset(); timeline.Push(first, 3);
            check(timeline.X(before, 3) == before.X, "Reconnect clears the old interpolation history");
        }
    }
}
