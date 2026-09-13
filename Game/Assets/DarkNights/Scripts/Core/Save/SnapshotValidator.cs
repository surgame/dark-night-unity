using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using DarkNights.Core.Config;
using DarkNights.Core.Save;
using DarkNights.Core.Logic.State;
using static DarkNights.Core.Save.ValidationContext;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 存档验证总入口，先验证整体类型、计时和实体身份，再调用状态与关系检查。
    /// 返回可读错误而不触碰当前会话；只有空错误才允许进入映射恢复。
    /// </summary>
    public static class SnapshotValidator
    {
        public static string Validate(SessionSnapshot s, GameCatalog catalog, LevelLayout layout)
        {
            if (s == null || (s.SchemaVersion != 1 && s.SchemaVersion != 2) || s.LevelId != catalog.Level.Id)
                return "存档版本或关卡不匹配";
            if (!Number(s.Elapsed, 0, 1000000) || s.Speed is not (1 or 2))
                return "时钟状态无效";
            if (s.SchemaVersion == 1 && (!Number(s.CameraX, 0, layout.WorldWidth) || !Number(s.CameraZoom, 1.8, 4.5)))
                return "镜头状态无效";
            foreach (string text in new[] { s.RngSeed, s.RngState })
                if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) ||
                    value.ToString(CultureInfo.InvariantCulture) != text)
                    return "随机状态无效";
            if (s.Economy?.Resources == null || !s.Economy.Resources.IsValid())
                return "资源数据无效";
            if (!Number(s.Economy.UpkeepElapsed, 0, 60) || !Number(s.Economy.StarvationElapsed, 0, 60) ||
                !Number(s.Economy.RecruitCooldown, 0, 60))
                return "经济计时无效";
            if (s.Stats?.Gathered == null || !s.Stats.Gathered.IsValid() || !Id(s.Stats.Kills) || !Id(s.Stats.Lost))
                return "统计数据无效";
            if (s.Wave == null || s.Wave.Index < 0 || s.Wave.Index >= catalog.Level.Waves.Count || !Enum.IsDefined(typeof(WavePhase), s.Wave.Phase))
                return "夜袭阶段无效";
            var wave = catalog.Level.Waves[s.Wave.Index];
            if (!Number(s.Wave.DayRemaining, 0, wave.DaySeconds + 0.001) || !Number(s.Wave.SpawnElapsed, 0, 1000000) ||
                s.Wave.NextSpawn < 0 || s.Wave.NextSpawn > wave.Enemies.Count ||
                (s.Wave.Phase == WavePhase.Day && s.Wave.NextSpawn != 0))
                return "夜袭计时无效";
            if (s.Actors == null || s.Buildings == null || s.Worksites == null || s.Projectiles == null || s.SelectedIds == null ||
                s.Actors.Count + s.Buildings.Count + s.Worksites.Count > 256 || s.Projectiles.Count > 1024 ||
                s.SelectedIds.Count > 256)
                return "实体列表无效或过大";
            if (s.Actors.Any(a => a == null) || s.Buildings.Any(b => b == null) ||
                s.Worksites.Any(w => w == null) || s.Projectiles.Any(p => p == null))
                return "实体记录为空";
            var identities = s.Actors.Select(a => (a.Id, a.Kind, a.X))
                .Concat(s.Buildings.Select(b => (b.Id, b.Kind, b.X))).Concat(s.Worksites.Select(w => (w.Id, w.Kind, w.X))).ToArray();
            var ids = new HashSet<int>();
            foreach (var entry in identities)
                if (!Id(entry.Id, 1) || string.IsNullOrEmpty(entry.Kind) || !Number(entry.X, 0, layout.WorldWidth) || !ids.Add(entry.Id))
                    return "实体身份或坐标无效";
            if (s.NextEntityId <= ids.DefaultIfEmpty(0).Max() || s.NextEntityId > 1000001)
                return "实体ID序列无效";
            if (s.SchemaVersion == 2 && (!Enum.IsDefined(typeof(SessionMode), s.Mode) || s.Mode == SessionMode.Menu ||
                s.Identities.Count != ids.Count || s.Identities.Any(i => i == null || !ids.Contains(i.Id)) ||
                s.Identities.Select(i => i.Id).Distinct().Count() != ids.Count ||
                s.Identities.Where(i => i.PlacementKey.Length != 0).GroupBy(i => i.PlacementKey).Any(g => g.Count() != 1)))
                return "定义或场景身份关系无效";
            var context = new ValidationContext(s, catalog, layout);
            string error = EntitySnapshotValidator.Validate(context);
            if (error.Length == 0)
                error = RelationshipValidator.Validate(context);
            if (error.Length != 0)
                return error;
            foreach (var shot in s.Projectiles)
            {
                if (!Point(shot.From, catalog, layout) || !Point(shot.To, catalog, layout) || !Id(shot.TargetId, 1) ||
                    shot.Damage is < 1 or > 1000 || !Number(shot.Duration, 0.01, 10) || !Number(shot.Age, 0, shot.Duration))
                    return "箭矢数据或计时无效";
            }
            return s.SelectedIds.Any(id => !ids.Contains(id)) || s.SelectedIds.Distinct().Count() != s.SelectedIds.Count
                ? "选择集引用不存在或重复的实体" : "";
        }

        private static bool Point(IReadOnlyList<double> point, GameCatalog catalog, LevelLayout layout) => point is { Count: 2 } &&
            Number(point[0], -64, layout.WorldWidth + 64) && Number(point[1], -200, layout.GroundY + 64);
    }
}
