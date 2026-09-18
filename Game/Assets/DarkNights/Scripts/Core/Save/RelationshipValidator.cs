using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 检查工人、工位、训练和攻击目标之间的双向关系。所有索引来自已通过局部验证的DTO；关系错误会在当前营地被替换前阻止读取。
    /// </summary>
    internal static class RelationshipValidator
    {

        public static string Validate(ValidationContext c)
        {
            var farms = new HashSet<int>();
            foreach (var site in c.Sites.Values)
            {
                if (site.IsMineralDeposit || site.Kind == "mineral-deposit")
                {
                    if (!site.IsMineralDeposit || site.WorkerId != 0 || site.FarmId != 0 || site.Amount < 0 || site.Amount > site.Capacity)
                        return "矿床关系无效";
                }
                else if (site.Kind == "food")
                {
                    if (!c.Buildings.TryGetValue(site.FarmId, out var farm) || farm.Kind != "farm" || farm.Progress < 1 ||
                        !farms.Add(farm.Id) || site.Amount != -1 || Math.Abs(site.X - farm.X) > 0.01)
                        return "农田工作点关系无效";
                }
                else if (site.FarmId != 0 || site.Amount < 0)
                    return "资源存量或归属无效";
            }
            if (c.Buildings.Values.Any(b => b.Kind == "farm" && b.Progress >= 1 && !farms.Contains(b.Id)))
                return "已完成农田缺少工作点";
            foreach (var a in c.Actors.Values)
            {
                bool training = a.State is ActorActivity.Training or ActorActivity.TrainingMove;
                if (a.State is ActorActivity.Idle or ActorActivity.Move)
                {
                    if (a.TargetId != 0)
                        return "空闲或移动单位仍引用目标";
                }
                else if (!c.Has(a.TargetId))
                    return "单位引用不存在的目标";
                if (a.State is ActorActivity.Work or ActorActivity.WorkMove)
                {
                    if (a.Kind != "worker" || !c.Sites.TryGetValue(a.TargetId, out var site) || site.WorkerId != a.Id)
                        return "采集占用关系不一致";
                }
                else if (a.State is ActorActivity.Build or ActorActivity.BuildMove)
                {
                    if (a.Kind != "worker" || !c.Buildings.TryGetValue(a.TargetId, out var building) ||
                        building.WorkerId != a.Id || building.Progress >= 1)
                        return "施工占用关系不一致";
                }
                else if (training)
                {
                    if (a.Kind != "worker" || !c.TrainingOwners.TryGetValue(a.Id, out int owner) || owner != a.TargetId)
                        return "单位训练关系不一致";
                }
                else if (a.State == ActorActivity.Attack)
                {
                    if (c.Sites.ContainsKey(a.TargetId) ||
                        (c.Actors.TryGetValue(a.TargetId, out var enemy) && enemy.Enemy == a.Enemy) ||
                        (c.Buildings.ContainsKey(a.TargetId) && !a.Enemy))
                        return "攻击目标阵营无效";
                }
                if (c.TrainingOwners.ContainsKey(a.Id) && !training)
                    return "训练单位同时执行其他任务";
            }
            foreach (var site in c.Sites.Values)
                if (!Worker(c, site.WorkerId, site.Id, ActorActivity.Work, ActorActivity.WorkMove))
                    return "工作点占用关系失配";
            foreach (var building in c.Buildings.Values)
                if (!Worker(c, building.WorkerId, building.Id, ActorActivity.Build, ActorActivity.BuildMove))
                    return "施工占用关系失配";
            return "";
        }

        private static bool Worker(ValidationContext c, int worker, int target, ActorActivity active, ActorActivity moving) =>
            worker == 0 || (c.Actors.TryGetValue(worker, out var a) && a.Kind == "worker" && a.TargetId == target &&
            (a.State == active || a.State == moving));
    }
}
