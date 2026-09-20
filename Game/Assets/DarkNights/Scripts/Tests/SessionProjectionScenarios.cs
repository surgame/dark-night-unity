using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Runtime.Objects;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using Newtonsoft.Json;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 以实际会话指令验证完整展示、嵌套冻结、身份与载入边界；轨迹夹具由测试创建，不修改冻结旧档。
    /// JSON 仅用于比较值及检查私有状态未暴露，不作为 MemoryPack 或真实网络验收。
    /// </summary>
    public static class SessionProjectionScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            var first = session.CaptureProjection();
            var initial = first.World;
            var direct = World(catalog, layout);
            check(initial.Actors.Count == 7 && initial.Buildings.Count == 4 && initial.Worksites.Count == 6 &&
                initial.Projectiles.Count == 0, "Projection includes initial actors, buildings and generated farm worksite");
            check(initial.Camp.Stock.Wood == 100 && initial.Camp.Population == 7 && initial.Camp.Capacity == 9 &&
                initial.Camp.Mode == "Playing" && initial.Camp.WavePhase == "Day" && initial.Camp.DayRemaining == 90,
                "Projection carries real initial economy and wave HUD data");
            check(first.Epoch == 1 && first.PlayerCount == 2 && first.ReadyCount == 2 && first.Publication == 1,
                "Projection captures session metadata at the same boundary");
            string frozen = JsonConvert.SerializeObject(first);
            check(!frozen.Contains("Rng") && !frozen.Contains("Damage") && !frozen.Contains("AiClock") &&
                !frozen.Contains("NextEntityId") && !frozen.Contains("SelectedIds"), "Projection omits RNG, damage and private recovery or local selection state");
            var workers = initial.Actors.Where(a => a.Kind == "worker").Select(a => a.Id).ToArray();
            var wood = initial.Worksites.First(w => w.Kind == "wood");
            Execute(session, guest, Request(session, SessionOperation.IssueOrders, 1, new[] { workers[0] }, wood.Id, wood.X));
            direct.IssueOrders(new[] { workers[0] }, wood.Id, wood.X);
            for (int i = 1; i < 720; i++) session.Tick();
            for (int i = 0; i < 720; i++) direct.Advance(1.0 / 60);
            var gathered = session.CaptureProjection();
            var actor = gathered.World.Actors.First(a => a.Id == workers[0]);
            var actual = direct.Index.Find<ActorBehaviour>(workers[0]).CaptureState();
            check(gathered.World.Camp.Stock.Wood == 103 && actor.X == actual.X && actor.Hp == actual.Hp &&
                actor.Activity == actual.Activity.ToString() && actor.ActionTime == actual.ActionTime && actor.Walking == actual.Walking,
                "Projection sampling preserves actual gathering, movement and animation values");
            check(JsonConvert.SerializeObject(first) == frozen, "Projection remains frozen after twelve seconds of world mutation");
            Execute(session, host, Request(session, SessionOperation.SetPaused, 1, value: 1));
            Execute(session, guest, Request(session, SessionOperation.TrainActors, 2, new[] { workers[4], workers[2] }, kind: "archer"));
            var training = session.CaptureProjection();
            var barracks = training.World.Buildings.First(b => b.Kind == "barracks");
            check(barracks.Training.Select(t => t.ActorId).SequenceEqual(new[] { workers[4], workers[2] }),
                "Projection preserves paid training order and stable actor IDs");
            string frozenTraining = JsonConvert.SerializeObject(training);
            Execute(session, host, Request(session, SessionOperation.SetPaused, 2, value: 0));
            for (int i = 0; i < 60; i++) session.Tick();
            check(JsonConvert.SerializeObject(training) == frozenTraining, "Projection nested training queue stays frozen after simulation");
            Bounds(check, initial);
            Arrows(check, catalog, layout);
        }

        private static void Bounds(Action<bool, string> check, WorldViewData initial)
        {
            var actors = initial.Actors.ToArray();
            var copied = new WorldViewData(initial.Camp, actors, initial.Buildings, initial.Worksites, initial.Projectiles);
            actors[0] = null;
            check(copied.Actors[0] != null && Throws<NotSupportedException>(() =>
                ((IList<ActorViewData>)copied.Actors).Clear()), "Projection copies caller arrays and exposes read-only lists");
            var source = new[] { new TrainingViewData(initial.Actors[0].Id, "archer", 2) };
            var building = new BuildingViewData(1000, "barracks", 0, 1, 1, 0, 0, 0, source);
            source[0] = null;
            check(building.Training[0].Remaining == 2 && Throws<NotSupportedException>(() =>
                ((IList<TrainingViewData>)building.Training).Clear()), "Projection copies nested caller queue and prevents mutation");
            check(Throws<ArgumentException>(() => new WorldViewData(initial.Camp, initial.Actors,
                new[] { new BuildingViewData(initial.Actors[0].Id, "house", 0, 1, 1, 0, 0, 0, Array.Empty<TrainingViewData>()) },
                initial.Worksites, initial.Projectiles)), "Projection rejects cross-category duplicate entity IDs");
            var sites = Enumerable.Range(1, 256).Select(id => new WorksiteViewData(id, "wood", 0, 0, 120, 0, 0, 0)).ToArray();
            var arrows = Enumerable.Range(1, 1024).Select(id => new ProjectileViewData(id, 0, 0, 1, 1, 0, 1)).ToArray();
            var maximum = new WorldViewData(initial.Camp, Array.Empty<ActorViewData>(), Array.Empty<BuildingViewData>(), sites, arrows);
            check(maximum.Worksites.Count == 256 && maximum.Projectiles.Count == 1024, "Projection accepts complete documented count limits without truncation");
            check(Throws<ArgumentOutOfRangeException>(() => new WorldViewData(initial.Camp, initial.Actors,
                initial.Buildings, sites, arrows)), "Projection rejects excessive total entities before copying");
            check(Throws<ArgumentOutOfRangeException>(() => new WorldViewData(initial.Camp, initial.Actors,
                initial.Buildings, initial.Worksites, arrows.Concat(new[] { arrows[0] }).ToArray())), "Projection rejects excessive projectile count");
            check(Throws<ArgumentException>(() => new WorldViewData(initial.Camp, initial.Actors,
                initial.Buildings, initial.Worksites, new[] { arrows[0], arrows[0] })), "Projection rejects duplicate projectile identity");
        }

        private static void Arrows(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out _);
            var source = World(catalog, layout);
            var snapshot = source.CaptureWorld();
            int target = snapshot.Actors[0].Id;
            var codec = Codec(catalog, layout);
            var document = Newtonsoft.Json.Linq.JObject.Parse(codec.Serialize(snapshot));
            document["world"]["projectiles"] = new Newtonsoft.Json.Linq.JArray(
                new Newtonsoft.Json.Linq.JObject { ["from"] = new Newtonsoft.Json.Linq.JArray(10, 20), ["to"] = new Newtonsoft.Json.Linq.JArray(30, 40),
                    ["target_id"] = target, ["damage"] = 1, ["age"] = 0, ["duration"] = 0.01,
                    ["kind"] = 0, ["velocity_x"] = 0, ["velocity_y"] = 0, ["gravity"] = 0,
                    ["radius"] = 0, ["blast_radius"] = 0, ["stuck"] = false },
                new Newtonsoft.Json.Linq.JObject { ["from"] = new Newtonsoft.Json.Linq.JArray(50, 60), ["to"] = new Newtonsoft.Json.Linq.JArray(70, 80),
                    ["target_id"] = target, ["damage"] = 1, ["age"] = 0, ["duration"] = 10,
                    ["kind"] = 0, ["velocity_x"] = 0, ["velocity_y"] = 0, ["gravity"] = 0,
                    ["radius"] = 0, ["blast_radius"] = 0, ["stuck"] = false });
            string saved = codec.Serialize(codec.Parse(document.ToString()));
            var ticket = Execute(session, host, Request(session, SessionOperation.BeginLoad, 1));
            session.CompleteLoad(ticket, saved);
            var before = session.CaptureProjection();
            check(before.Epoch == 2 && before.Revision == 0 && before.ReadyCount == 0 && before.World.Projectiles.Count == 2,
                "Projection load publishes complete flying arrows while awaiting Ready");
            check(codec.Serialize(session.CaptureWorld()) == saved, "Projection capture does not advance RNG or mutate the loaded world");
            session.AcknowledgeReady(host, session.Epoch, session.Revision);
            session.Tick();
            var after = session.CaptureProjection();
            check(after.World.Projectiles.Count == 1 && after.World.Projectiles[0].ViewId == before.World.Projectiles[1].ViewId &&
                after.World.Projectiles[0].Age > before.World.Projectiles[1].Age, "Projection arrow identity survives earlier list element removal");
            check(before.World.Projectiles.Count == 2 && before.World.Projectiles[0].Age == 0,
                "Projection old arrow snapshot survives authoritative impact and removal");
            session.Dispose();
            check(Throws<ObjectDisposedException>(() => session.CaptureProjection()), "Projection cannot capture a disposed authority");
        }
    }
}
