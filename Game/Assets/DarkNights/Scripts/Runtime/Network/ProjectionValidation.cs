using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 在客户端创建只读副本前校验具体投影字段及有界集合；不重算资源、伤害或目标决策。
    /// 校验失败不能替换当前帧，也不发送 Ready。内容类别以握手匹配后的只读目录为准。
    /// </summary>
    internal static class ProjectionValidation
    {
        public static void Validate(SessionWire frame, GameCatalog catalog, LevelLayout layout)
        {
            Require(frame.Events != null && frame.Events.Length <= SessionViewData.MaximumEvents);
            Require(frame.Remnants != null && frame.Remnants.Length <= SessionViewData.MaximumRemnants);
            ValidateEvents(frame.Events, frame.ServerTick, catalog, layout);
            ValidateEvents(frame.Remnants, frame.ServerTick, catalog, layout);
            foreach (var item in frame.Remnants)
            {
                Require(item.Type == "effect" && (item.Kind == "corpse" || item.Kind == "rubble") && item.Text.Length == 0 && item.Detail.Length == 0);
                var duplicate = frame.Events.FirstOrDefault(e => e.Sequence == item.Sequence);
                Require(duplicate == null || (duplicate.Type == item.Type && duplicate.Tick == item.Tick && duplicate.Kind == item.Kind &&
                    duplicate.X == item.X && duplicate.Y == item.Y && duplicate.ContentId == item.ContentId && duplicate.Face == item.Face));
            }
            WorldWire world = frame.World;
            Require(world != null && world.Camp != null && world.Actors != null && world.Buildings != null &&
                world.Worksites != null && world.Projectiles != null);
            Require((long)world.Actors.Length + world.Buildings.Length + world.Worksites.Length <= WorldViewData.MaximumEntities &&
                world.Projectiles.Length <= WorldViewData.MaximumProjectiles);
            Require(world.Identities != null && world.Identities.Length == world.Actors.Length + world.Buildings.Length + world.Worksites.Length);
            foreach (var identity in world.Identities)
                Require(identity != null && identity.Id > 0 && Text(identity.DefinitionGuid, 36) &&
                    Guid.TryParse(identity.DefinitionGuid, out var guid) && guid != Guid.Empty && Text(identity.PlacementKey, 80));
            var camp = world.Camp;
            Require(camp.Stock != null && camp.Gathered != null && camp.Stock.Freeze().IsValid() && camp.Gathered.Freeze().IsValid());
            Require(camp.Population >= 0 && camp.Capacity >= 0 && camp.EnemyCount >= 0 && camp.Kills >= 0 && camp.Lost >= 0 &&
                camp.WaveIndex >= 0 && camp.WaveIndex < catalog.Level.Waves.Count &&
                Enum.TryParse<WavePhase>(camp.WavePhase, out var phase) && Enum.IsDefined(typeof(WavePhase), phase) &&
                Enum.TryParse<SessionMode>(camp.Mode, out var mode) && Enum.IsDefined(typeof(SessionMode), mode));
            Nonnegative(camp.RecruitCooldown, camp.DayRemaining);
            Require(camp.NextSpawn >= 0 && camp.NextSpawn <= catalog.Level.Waves[camp.WaveIndex].Enemies.Count);
            int trainingCount = 0;
            foreach (var a in world.Actors)
            {
                Require(a != null && Text(a.Kind, 64) && Text(a.Name, 256) && catalog.Balance.Units.ContainsKey(a.Kind) &&
                    Enum.TryParse<ActorActivity>(a.Activity, out var activity) && Enum.IsDefined(typeof(ActorActivity), activity));
                Position(a.X, layout);
                Nonnegative(a.Hp, a.ActionTime, a.HitFlash);
                // 原版前摇在命中 tick 减过零后保留负余量；只验证有限值，不能改变攻击时机。
                Require(Finite(a.Windup));
                Require(a.Face == -1 || a.Face == 0 || a.Face == 1);
                Require(Finite(a.Height) && a.Height >= (layout.RandomTerrain ? Core.Config.Terrain.PlayableTerrain.MinimumHeight : 0) && a.Height <= (catalog.Balance.HeroControl?.MaximumHeight ?? 0) &&
                    Finite(a.VerticalSpeed) && Math.Abs(a.VerticalSpeed) <= 1000 && a.SupportPlatform >= -1 &&
                    a.SelectedItem >= 0 && a.SelectedItem <= 3 && a.SelectionRevision >= 0 && a.ControlLease >= 0 &&
                    a.ControllerSlot >= -1 && a.ControllerSlot <= 3 && (a.ControllerSlot < 0 || (a.ManualControl && !a.Enemy)) &&
                    Finite(a.JetpackFuel) && a.JetpackFuel >= 0 && a.JetpackFuel <= (catalog.Balance.HeroControl?.FuelSeconds ?? 0) &&
                    a.ExplosiveCharges >= 0 && a.ExplosiveCharges <= 1000 && a.DrillCharges >= 0 && a.DrillCharges <= 1000);
                Require(a.SupportPlatform <= 0 || layout.Platforms.Any(p => p.Id == a.SupportPlatform));
            }
            Require(!world.Actors.Where(a => a.ControllerSlot >= 0).GroupBy(a => a.ControllerSlot).Any(g => g.Count() > 1));
            foreach (var b in world.Buildings)
            {
                Require(b != null && Text(b.Kind, 64) && catalog.Balance.Buildings.ContainsKey(b.Kind) && b.Training != null);
                trainingCount += b.Training.Length;
                Require(trainingCount <= WorldViewData.MaximumEntities);
                Position(b.X, layout);
                Nonnegative(b.Hp, b.Progress, b.HitFlash);
                Require(b.Progress <= 1);
                foreach (var t in b.Training)
                {
                    Require(t != null && Text(t.Kind, 64) && catalog.Balance.Units.ContainsKey(t.Kind) &&
                        world.Actors.Any(a => a.Id == t.ActorId));
                    Nonnegative(t.Remaining);
                }
            }
            foreach (var w in world.Worksites)
            {
                if (w != null && (w.IsMineralDeposit || w.Kind == "mineral-deposit"))
                {
                    Require(w.IsMineralDeposit && w.Kind == "mineral-deposit" && Text(w.RoomKind, 32) && Text(w.Rarity, 16) &&
                        w.Capacity > 0 && w.Capacity <= 1000000 && w.Amount >= 0 && w.Amount <= w.Capacity &&
                        Finite(w.Y) && w.Y >= 0 && w.Y < Core.Config.Terrain.TerrainGenerationSettings.Height &&
                        w.WorkerId == 0 && w.FarmId == 0 && w.DrillId >= 0 &&
                        Finite(w.Progress) && w.Progress >= 0 && w.Progress < 1 &&
                        Enum.TryParse<DarkNights.Core.Config.MineralDepositStage>(w.Stage, out var stage) &&
                        Enum.IsDefined(typeof(DarkNights.Core.Config.MineralDepositStage), stage));
                    Position(w.X, layout);
                    continue;
                }
                Require(w != null && Text(w.Kind, 64) &&
                    (w.Kind == "worksite.mineral-drill" || catalog.Balance.Worksites.ContainsKey(w.Kind)) &&
                    (w.Kind != "worksite.mineral-drill" || (w.Amount >= 0 && (w.Variant == 1 || w.Variant == 2))) &&
                    (w.Kind == "worksite.mineral-drill" || (w.Amount >= -1 && w.Variant >= 0)));
                Position(w.X, layout);
                Nonnegative(w.Progress);
            }
            foreach (var p in world.Projectiles)
            {
                Require(p != null);
                Nonnegative(p.Age, p.Duration);
                Require(p.Duration > 0 && p.Age <= p.Duration);
                Require(Finite(p.FromX) && Finite(p.FromY) && Finite(p.ToX) && Finite(p.ToY));
            }
        }

        private static void ValidateEvents(PresentationWire[] items, long serverTick, GameCatalog catalog, LevelLayout layout)
        {
            long sequence = 0;
            foreach (PresentationWire item in items)
            {
                Require(item != null && item.Sequence > sequence && item.Tick >= 0 && item.Tick <= serverTick &&
                    Text(item.Text, 256) && Text(item.Detail, 256) && Text(item.Kind, 16) && Text(item.ContentId, 64) &&
                    Finite(item.Volume) && item.Volume >= -80 && item.Volume <= 6);
                sequence = item.Sequence;
                Require(item.Type == "message" || item.Type == "banner" || item.Type == "sound" || item.Type == "effect");
                if (item.Type != "effect")
                {
                    Require(item.Kind.Length == 0 && item.ContentId.Length == 0 && item.X == 0 && item.Y == 0 && item.Face == 1 && !item.Enemy);
                    if (item.Type == "sound") Require(item.Text.StartsWith("snd_", StringComparison.Ordinal) && item.Text.Length <= 64);
                    continue;
                }
                Position(item.X, layout);
                Require(Finite(item.Y) && Math.Abs(item.Y - layout.GroundY) <= 1000 && (item.Face == -1 || item.Face == 0 || item.Face == 1));
                Require(item.Kind == "damage" || item.Kind == "resource" || item.Kind == "corpse" || item.Kind == "rubble");
                if (item.Kind == "corpse") Require(catalog.Balance.Units.ContainsKey(item.ContentId));
                if (item.Kind == "rubble") Require(catalog.Balance.Buildings.ContainsKey(item.ContentId));
                if (item.Kind == "resource") Require(GameText.ResourceIds.Contains(item.ContentId));
            }
        }

        private static void Position(double x, LevelLayout layout) => Require(Finite(x) && x >= -100 && x <= layout.WorldWidth + 100);
        private static bool Text(string value, int maximum) => value != null && value.Length <= maximum;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void Nonnegative(params double[] values)
        {
            foreach (double value in values) Require(Finite(value) && value >= 0);
        }
        private static void Require(bool valid)
        {
            if (!valid) throw new FormatException("Invalid complete world projection.");
        }
    }
}
