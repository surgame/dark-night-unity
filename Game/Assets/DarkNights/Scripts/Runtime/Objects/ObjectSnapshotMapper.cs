using System;
using System.Globalization;
using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 直接在 YYGC 业务状态与冻结 v2 DTO 之间转换，不构造或调用旧实体。
    /// 捕获脱离对象池，恢复只初始化尚未激活的候选实例。
    /// </summary>
    internal static class ObjectSnapshotMapper
    {
        internal static SessionSnapshot Capture(ObjectSession session)
        {
            CampSimulationState camp = session.Camp.Read();
            EconomyState economy = session.Economy.Read();
            var actors = session.Index.Actors.Select(actor =>
            {
                ActorState a = actor.Read();
                return new ActorSnapshot(a.Id, actor.RuleKey, a.Enemy, a.Name, a.X, a.Hp, a.Activity,
                    a.TargetId, a.MoveX, a.RallyX, a.Face, a.ActionTime, a.AttackClock, a.Windup,
                    a.HitPending, a.ForcedAttack, a.AiClock);
            }).ToArray();
            var buildings = session.Index.Buildings.Select(building =>
            {
                BuildingState b = building.Read();
                return new BuildingSnapshot(b.Id, building.RuleKey, b.X, b.Hp, b.Progress,
                    b.WorkerId, b.AttackClock, b.TrainingQueue.Select(t => new TrainingSnapshot(t.ActorId, t.Kind, t.Remaining)).ToArray());
            }).ToArray();
            var sites = session.Index.Worksites.Select(site =>
            {
                WorksiteState w = site.Read();
                return new WorksiteSnapshot(w.Id, site.RuleKey, w.X, w.WorkerId, w.Amount, w.Progress, w.Variant, w.FarmId);
            }).ToArray();
            var identities = session.Index.FreezeOrder().Select(e => new EntityIdentityData(e.Id, e.DefinitionGuid, e.PlacementKey)).ToArray();
            WaveState wave = session.Waves.Read();
            var shots = session.Projectiles.Read().Shots.Select(p => new ProjectileSnapshot(
                new double[] { p.FromX, p.FromY }, new double[] { p.ToX, p.ToY }, p.TargetId, p.Damage, p.Age, p.Duration)).ToArray();
            return new SessionSnapshot(2, session.Catalog.Level.Id,
                new EconomySnapshot(session.Economy.Stock, economy.UpkeepElapsed, economy.StarvationElapsed, economy.RecruitCooldown),
                new WaveSnapshot(wave.Index, wave.Phase, wave.DayRemaining, wave.SpawnElapsed, wave.NextSpawn),
                camp.Elapsed, camp.Speed, camp.Paused, camp.NextEntityId,
                unchecked((long)camp.RandomSeed).ToString(CultureInfo.InvariantCulture),
                unchecked((long)camp.RandomState).ToString(CultureInfo.InvariantCulture),
                actors, buildings, sites, shots,
                new StatisticsSnapshot(camp.Kills, camp.Lost, session.Economy.Gathered),
                camp.Mode, identities);
        }

        internal static CampSimulationState Camp(SessionSnapshot s) => new CampSimulationState
        {
            Mode = s.Mode, Elapsed = s.Elapsed, Paused = s.Paused, Speed = (int)s.Speed,
            NextEntityId = s.NextEntityId, Kills = s.Stats.Kills, Lost = s.Stats.Lost,
            RandomSeed = unchecked((ulong)long.Parse(s.RngSeed, CultureInfo.InvariantCulture)),
            RandomState = unchecked((ulong)long.Parse(s.RngState, CultureInfo.InvariantCulture))
        };

        internal static EconomyState Economy(SessionSnapshot s) => new EconomyState
        {
            Food = s.Economy.Resources.Food, Wood = s.Economy.Resources.Wood, Stone = s.Economy.Resources.Stone,
            Iron = s.Economy.Resources.Iron, Gold = s.Economy.Resources.Gold,
            UpkeepElapsed = s.Economy.UpkeepElapsed, StarvationElapsed = s.Economy.StarvationElapsed,
            RecruitCooldown = s.Economy.RecruitCooldown,
            GatheredFood = s.Stats.Gathered.Food, GatheredWood = s.Stats.Gathered.Wood,
            GatheredStone = s.Stats.Gathered.Stone, GatheredIron = s.Stats.Gathered.Iron, GatheredGold = s.Stats.Gathered.Gold
        };

        internal static WaveState Wave(SessionSnapshot s) => new WaveState
        {
            Index = s.Wave.Index, Phase = s.Wave.Phase, DayRemaining = s.Wave.DayRemaining,
            SpawnElapsed = s.Wave.SpawnElapsed, NextSpawn = s.Wave.NextSpawn
        };

        internal static ProjectileState Projectiles(SessionSnapshot s) => new ProjectileState
        {
            NextViewId = s.Projectiles.Count + 1,
            Shots = s.Projectiles.Select((p, index) => new ProjectileFlight
            {
                ViewId = index + 1, FromX = (float)p.From[0], FromY = (float)p.From[1],
                ToX = (float)p.To[0], ToY = (float)p.To[1], TargetId = p.TargetId,
                Damage = p.Damage, Age = p.Age, Duration = p.Duration
            }).ToArray()
        };

        internal static void Initialize(ObjectInstance owner, int id, SessionSnapshot snapshot)
        {
            string placement = snapshot.Identities.Single(i => i.Id == id).PlacementKey;
            if (owner.GetBehaviour<ActorBehaviour>() is ActorBehaviour actor)
            {
                ActorSnapshot a = snapshot.Actors.Single(value => value.Id == id);
                actor.PrepareState(new ActorState
                {
                    Id = id, PlacementKey = placement, Name = a.Name, Enemy = a.Enemy,
                    X = (float)a.X, Hp = a.Hp, Activity = a.State, TargetId = a.TargetId,
                    MoveX = (float)a.MoveX, RallyX = (float)a.RallyX, Face = (float)a.Face,
                    ActionTime = a.ActionTime, AttackClock = a.AttackClock, Windup = a.Windup,
                    HitPending = a.HitPending, ForcedAttack = a.ForcedAttack, AiClock = a.AiClock
                });
            }
            else if (owner.GetBehaviour<BuildingBehaviour>() is BuildingBehaviour building)
            {
                BuildingSnapshot b = snapshot.Buildings.Single(value => value.Id == id);
                building.PrepareState(new BuildingState
                {
                    Id = id, PlacementKey = placement, X = (float)b.X, Hp = b.Hp, Progress = b.Progress,
                    WorkerId = b.WorkerId, AttackClock = b.AttackClock,
                    FarmSiteId = snapshot.Worksites.FirstOrDefault(w => w.FarmId == id)?.Id ?? 0,
                    TrainingQueue = b.TrainingQueue.Select(t => new TrainingStateEntry(t.ActorId, t.Kind, t.Remaining)).ToArray()
                });
            }
            else if (owner.GetBehaviour<WorksiteBehaviour>() is WorksiteBehaviour site)
            {
                WorksiteSnapshot w = snapshot.Worksites.Single(value => value.Id == id);
                site.PrepareState(new WorksiteState
                {
                    Id = id, PlacementKey = placement, X = (float)w.X, WorkerId = w.WorkerId,
                    Amount = w.Amount, Progress = w.Progress, Variant = w.Variant, FarmId = w.FarmId
                });
            }
            else throw new InvalidOperationException("Restored object has no state owner.");
        }

        internal static Action<ObjectInstance> FreezeInitializer(IEntityBehaviour entity)
        {
            if (entity is ActorBehaviour actor)
            {
                ActorState frozen = actor.CaptureState();
                return owner => owner.GetBehaviour<ActorBehaviour>().PrepareState(frozen);
            }
            if (entity is BuildingBehaviour building)
            {
                BuildingState frozen = building.CaptureState();
                return owner => owner.GetBehaviour<BuildingBehaviour>().PrepareState(frozen);
            }
            WorksiteState site = ((WorksiteBehaviour)entity).CaptureState();
            return owner => owner.GetBehaviour<WorksiteBehaviour>().PrepareState(site);
        }
    }
}
