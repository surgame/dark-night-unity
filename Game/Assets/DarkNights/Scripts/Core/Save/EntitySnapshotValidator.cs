using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using static DarkNights.Core.Save.ValidationContext;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 验证各类实体的局部属性以及训练队列的基本成员身份。
    /// 这里确认HP、计时和内容ID范围，双向工作与目标关系由RelationshipValidator继续检查。
    /// </summary>
    internal static class EntitySnapshotValidator
    {

        public static string Validate(ValidationContext c)
        {
            int taverns = c.Buildings.Values.Count(b => b.Kind == "tavern");
            int expectedTaverns = c.Saved.Mode == SessionMode.Lost ? 0 : 1;
            if (taverns != expectedTaverns)
                return "营地必须拥有一座酒馆";
            foreach (var b in c.Buildings.Values)
            {
                if (!c.Catalog.Balance.Buildings.TryGetValue(b.Kind, out var d))
                    return "未知建筑";
                if (!Number(b.Hp, 0.001, d.Hp + 0.001) || !Number(b.Progress, 0, 1) ||
                    !Id(b.WorkerId) || !Number(b.AttackClock, 0, 10))
                    return "建筑生命或施工进度无效";
                if (b.TrainingQueue == null || b.TrainingQueue.Count > c.Catalog.Balance.Economy.TrainingQueueLimit ||
                    b.TrainingQueue.Any(t => t == null))
                    return "训练队列无效";
                if (b.Progress >= 1 && b.WorkerId != 0)
                    return "已完成建筑仍占用工人";
                if (b.TrainingQueue.Count > 0 && (b.Kind != "barracks" || b.Progress < 1))
                    return "训练必须归属已完成兵营";
                foreach (var t in b.TrainingQueue)
                {
                    if (!Id(t.ActorId, 1) || t.Kind is not ("spearman" or "archer") ||
                        !Number(t.Remaining, 0, c.Catalog.Balance.Economy.TrainingSeconds) || !c.Actors.ContainsKey(t.ActorId) ||
                        !c.TrainingOwners.TryAdd(t.ActorId, b.Id))
                        return "训练记录无效或人员重复";
                }
            }
            foreach (var w in c.Sites.Values)
            {
                if (w.IsMineralDeposit || w.Kind == "mineral-deposit")
                {
                    if (!w.IsMineralDeposit || w.Kind != "mineral-deposit" || w.RoomKind == null || w.RoomKind.Length > 32 ||
                        w.Rarity == null || w.Rarity.Length > 16 || w.Capacity < 1 || w.Capacity > 1000000 ||
                        w.Amount < 0 || w.Amount > w.Capacity || !Number(w.Y, 0, Config.Terrain.TerrainGenerationSettings.Height - 1) ||
                        Math.Abs(w.Y - Math.Round(w.Y)) > 0.001 ||
                        !Id(w.WorkerId) || w.WorkerId != 0 ||
                        !Id(w.FarmId) || w.FarmId != 0 || w.DrillId < 0 || !Number(w.Progress, 0, 1) ||
                        !Enum.IsDefined(typeof(MineralDepositStage), ParseStage(w.Stage)))
                        return "矿床状态无效";
                    continue;
                }
                if (w.Kind == "worksite.mineral-drill")
                {
                    if (w.Amount < 0 || w.Amount > 1000000 || w.WorkerId != 0 || w.FarmId != 0 ||
                        (w.Variant != 1 && w.Variant != 2) || !Number(w.Progress, 0, 1)) return "钻机状态无效";
                    continue;
                }
                if (!c.Catalog.Balance.Worksites.TryGetValue(w.Kind, out var d))
                    return "未知工作点";
                if (w.Amount is < -1 or > 1000000 || !Number(w.Progress, 0, d.Interval) || w.Variant is < 0 or > 3 ||
                    !Id(w.WorkerId) || !Id(w.FarmId) || (w.Amount == 0 && w.WorkerId != 0))
                    return "工作点状态无效";
            }
            foreach (var a in c.Actors.Values)
            {
                if (!c.Catalog.Balance.Units.TryGetValue(a.Kind, out var d) || a.Name == null || a.Name.Length > 80 ||
                    a.Enemy != (a.Kind is "zombie" or "ghoul" or "armored"))
                    return "单位阵营或姓名无效";
                if (!Number(a.Hp, 0.001, d.Hp) || !Enum.IsDefined(typeof(ActorActivity), a.State) || !Id(a.TargetId) ||
                    !Number(a.MoveX, 0, c.Layout.WorldWidth) || !Number(a.RallyX, 0, c.Layout.WorldWidth) ||
                    a.Face is not (-1 or 1))
                    return "单位生命、状态或目标无效";
                if (!Number(a.ActionTime, 0, 1000000) || !Number(a.AttackClock, 0, 10) ||
                    !Number(a.Windup, -1, 10) || !Number(a.AiClock, -1, 1))
                    return "单位攻击计时无效";
                var hero = c.Catalog.Balance.HeroControl;
                if (!Number(a.Height, c.Saved.Terrain == null ? 0 : Config.Terrain.PlayableTerrain.MinimumHeight, hero?.MaximumHeight ?? 0) || !Number(a.VerticalSpeed, -1000, 1000) ||
                    !Number(a.DropRemaining, 0, hero?.DropSeconds ?? 0) || !Number(a.JetpackFuel, 0, hero?.FuelSeconds ?? 0) ||
                    a.ExplosiveCharges < 0 || a.ExplosiveCharges > 1000 || a.DrillCharges < 0 || a.DrillCharges > 1000 ||
                    a.SelectedItem < 0 || a.SelectedItem > 3 || a.SelectionRevision < 0 || a.SupportPlatform < -1 || a.IgnoredPlatform < 0 ||
                    (a.Enemy && (a.ManualControl || a.Height != 0 || a.JetpackEquipped)) ||
                    (a.SupportPlatform == 0 && ((c.Saved.Terrain == null && a.Height != 0) || a.VerticalSpeed != 0)) ||
                    (a.SupportPlatform > 0 && !c.Layout.Platforms.Any(p => p.Id == a.SupportPlatform && p.Contains((float)a.X) &&
                        Math.Abs(p.Height - a.Height) < 0.001 && a.VerticalSpeed == 0)) ||
                    (a.IgnoredPlatform > 0 && !c.Layout.Platforms.Any(p => p.Id == a.IgnoredPlatform)))
                    return "主角运动或背包状态无效";
            }
            return "";
        }

        private static MineralDepositStage ParseStage(string value) =>
            Enum.TryParse(value, out MineralDepositStage stage) ? stage : (MineralDepositStage)(-1);
    }
}
