using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using DarkNights.Core.Config;
using DarkNights.Core.Save;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.Entities;


namespace DarkNights.Core.Save
{
    /// <summary>
    /// 在运行模型与版本化DTO之间进行完整映射，不读取文件也不绕过验证。
    /// 恢复先验证冻结输入，在新会话创建建筑/工位/单位，再补关系、箭矢和随机状态；调用方确认成功后才替换活动世界。
    /// </summary>
    public static class SnapshotMapper
    {
        public static SessionSnapshot Capture(GameSession s, LegacyDisplayState display = null) => new SessionSnapshot(
            schemaVersion: 1,
            levelId: s.Catalog.Level.Id,
            economy: new EconomySnapshot(
            resources: s.Economy.Stock,
            upkeepElapsed: s.Economy.UpkeepElapsed,
            starvationElapsed: s.Economy.StarvationElapsed,
            recruitCooldown: s.Economy.RecruitCooldown),
            wave: new WaveSnapshot(
            index: s.Waves.Index,
            phase: s.Waves.Phase,
            dayRemaining: s.Waves.DayRemaining,
            spawnElapsed: s.Waves.SpawnElapsed,
            nextSpawn: s.Waves.NextSpawn),
            elapsed: s.Elapsed,
            speed: s.Speed,
            paused: s.Paused,
            nextEntityId: s.World.NextId,
            rngSeed: unchecked((long)s.Random.Seed).ToString(CultureInfo.InvariantCulture),
            rngState: unchecked((long)s.Random.State).ToString(CultureInfo.InvariantCulture),
            actors: s.World.Actors.Select(a => new ActorSnapshot(
            id: a.Id,
            kind: a.Kind,
            enemy: a.Enemy,
            name: a.Name,
            x: a.X,
            hp: a.Hp,
            state: a.State,
            targetId: a.TargetId,
            moveX: a.MoveX,
            rallyX: a.RallyX,
            face: a.Face,
            actionTime: a.ActionTime,
            attackClock: a.AttackClock,
            windup: a.Windup,
            hitPending: a.HitPending,
            forcedAttack: a.ForcedAttack,
            aiClock: a.AiClock)).ToList(),
            buildings: s.World.Buildings.Select(b => new BuildingSnapshot(
            id: b.Id,
            kind: b.Kind,
            x: b.X,
            hp: b.Hp,
            progress: b.Progress,
            workerId: b.WorkerId,
            attackClock: b.AttackClock,
            trainingQueue: b.TrainingQueue.Select(t => new TrainingSnapshot(
            actorId: t.ActorId,
            kind: t.Kind,
            remaining: t.Remaining)).ToList())).ToList(),
            worksites: s.World.Worksites.Select(w => new WorksiteSnapshot(
            id: w.Id,
            kind: w.Kind,
            x: w.X,
            workerId: w.WorkerId,
            amount: w.Amount,
            progress: w.Progress,
            variant: w.Variant,
            farmId: w.FarmId)).ToList(),
            projectiles: s.World.Projectiles.Select(p => new ProjectileSnapshot(
            from: new double[] { p.From.X, p.From.Y },
            to: new double[] { p.To.X, p.To.Y },
            targetId: p.TargetId,
            damage: p.Damage,
            age: p.Age,
            duration: p.Duration)).ToList(),
            stats: new StatisticsSnapshot(
            kills: s.Stats.Kills,
            lost: s.Stats.Lost,
            gathered: s.Stats.Gathered),
            cameraX: display?.CameraX ?? s.Layout.CameraX,
            cameraZoom: display?.CameraZoom ?? 2.8f,
            selectedIds: display?.SelectedIds ?? Array.Empty<int>());

        public static GameSession Restore(SessionSnapshot saved, GameCatalog catalog, LevelLayout layout)
        {
            string error = SnapshotValidator.Validate(saved, catalog, layout);
            if (error.Length != 0) throw new ArgumentException(error, nameof(saved));
            var restored = new GameSession(catalog, layout);
            Apply(restored, saved);
            return restored;
        }

        private static void Apply(GameSession s, SessionSnapshot saved)
        {
            s.ClearState();
            s.Stats.Kills = saved.Stats.Kills;
            s.Stats.Lost = saved.Stats.Lost;
            s.Stats.Gathered = saved.Stats.Gathered;
            s.Economy.Stock = saved.Economy.Resources;
            s.Economy.UpkeepElapsed = saved.Economy.UpkeepElapsed;
            s.Economy.StarvationElapsed = saved.Economy.StarvationElapsed;
            s.Economy.RecruitCooldown = saved.Economy.RecruitCooldown;
            s.Waves.Index = saved.Wave.Index;
            s.Waves.Phase = saved.Wave.Phase;
            s.Waves.DayRemaining = saved.Wave.DayRemaining;
            s.Waves.SpawnElapsed = saved.Wave.SpawnElapsed;
            s.Waves.NextSpawn = saved.Wave.NextSpawn;
            foreach (var entry in saved.Buildings)
            {
                var b = s.Lifecycle.SpawnBuilding(entry.Kind, (float)entry.X, false, entry.Id, false);
                b.Hp = entry.Hp;
                b.Progress = entry.Progress;
                b.WorkerId = entry.WorkerId;
                b.AttackClock = entry.AttackClock;
                b.TrainingQueue.AddRange(entry.TrainingQueue.Select(t => new TrainingOrder(t.ActorId, t.Kind, t.Remaining)));
            }
            foreach (var entry in saved.Worksites)
            {
                var site = s.Lifecycle.SpawnSite(entry.Kind, (float)entry.X, entry.Variant, entry.FarmId, entry.Id);
                site.Amount = entry.Amount;
                site.WorkerId = entry.WorkerId;
                site.Progress = entry.Progress;
                if (site.FarmId != 0)
                    s.World.Find<Building>(site.FarmId)!.FarmSiteId = site.Id;
            }
            foreach (var entry in saved.Actors)
            {
                var a = s.Lifecycle.SpawnActor(entry.Kind, (float)entry.X, entry.Enemy, entry.Name, entry.Id);
                a.Hp = entry.Hp;
                a.State = entry.State;
                a.TargetId = entry.TargetId;
                a.MoveX = (float)entry.MoveX;
                a.RallyX = (float)entry.RallyX;
                a.Face = (float)entry.Face;
                a.ActionTime = entry.ActionTime;
                a.AttackClock = entry.AttackClock;
                a.Windup = entry.Windup;
                a.HitPending = entry.HitPending;
                a.ForcedAttack = entry.ForcedAttack;
                a.AiClock = entry.AiClock;
            }
            foreach (var p in saved.Projectiles)
                s.World.Projectiles.Add(new(new((float)p.From[0], (float)p.From[1]), new((float)p.To[0], (float)p.To[1]),
                    p.TargetId, p.Damage, p.Duration)
                {
                    Age = p.Age
                });
            s.World.NextId = saved.NextEntityId;
            s.Elapsed = saved.Elapsed;
            s.Speed = saved.Speed;
            s.Paused = saved.Paused;
            s.Random.Seed = unchecked((ulong)long.Parse(saved.RngSeed, CultureInfo.InvariantCulture));
            s.Random.State = unchecked((ulong)long.Parse(saved.RngState, CultureInfo.InvariantCulture));
            s.NotifyRestored();
        }
    }
}
