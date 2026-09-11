using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;

namespace DarkNights.View
{
    /// <summary>
    /// 把冻结实体和本地选择 ID 转为原版选择信息及悬停文案，不缓存可写实体。
    /// 找不到当前 epoch 的实体时显示营地默认说明，不以过期对象或文字反向驱动玩法。
    /// </summary>
    public static class SelectionReadout
    {
        public static (string Title, string State, string Info, float Hp) Describe(WorldViewData world, IReadOnlyList<int> ids, GameCatalog catalog)
        {
            if (ids.Count > 1)
            {
                int workers = world.Actors.Count(a => ids.Contains(a.Id) && a.Kind == "worker");
                return ($"{ids.Count} 名居民", $"工人 {workers} · 守卫 {ids.Count - workers}",
                    "右键地面集结 / 右键敌人攻击\n资源点会分配给空闲工作名额", -1);
            }
            int id = ids.Count == 0 ? 0 : ids[0];
            ActorViewData actor = world.Actors.FirstOrDefault(a => a.Id == id);
            if (actor != null)
            {
                UnitDefinition definition = catalog.Balance.Units[actor.Kind];
                return (actor.Enemy ? definition.Name : $"{actor.Name} · {definition.Name}", Status(world, actor),
                    $"生命 {Math.Ceiling(actor.Hp)}/{definition.Hp}  ·  护甲 {definition.Armor}\n伤害 {definition.Damage[0]}–{definition.Damage[1]}  ·  {(actor.Kind == "worker" ? "右键采集 / 施工" : "自动攻击附近敌人")}",
                    (float)(actor.Hp / definition.Hp));
            }
            BuildingViewData building = world.Buildings.FirstOrDefault(b => b.Id == id);
            if (building != null)
            {
                BuildingDefinition definition = catalog.Balance.Buildings[building.Kind];
                string state = building.Progress >= 1 ? "已完工" : $"施工 {Math.Round(building.Progress * 100)}% · {(building.WorkerId == 0 ? "等待工人" : "正在建设")}";
                if (building.Training.Count > 0) state = $"训练 {building.Training.Count} 人 · 当前 {building.Training[0].Remaining:F1}s";
                if (building.Kind == "farm" && building.Progress >= 1)
                    state = world.Worksites.FirstOrDefault(site => site.Id == building.FarmSiteId)?.WorkerId > 0 ? "正在耕作" : "需要一名工人耕作";
                return (definition.Name, state, $"生命 {Math.Ceiling(building.Hp)}/{definition.Hp}\n{GameText.Building(catalog, building.Kind)}", (float)(building.Hp / definition.Hp));
            }
            WorksiteViewData worksite = world.Worksites.FirstOrDefault(w => w.Id == id);
            if (worksite != null)
            {
                WorksiteDefinition definition = catalog.Balance.Worksites[worksite.Kind];
                return (definition.Name, worksite.WorkerId == 0 ? "空闲工作点" : "一名工人已占用",
                    $"剩余 {worksite.Amount}\n每{GameText.Number(definition.Interval)}秒产出{definition.Yield}{GameText.ResourceName(worksite.Kind)}", -1);
            }
            return ("营地指挥", "守住酒馆，等到黎明", "左键选择 · 右键安排工作\nG选择守卫 · I选择空闲工人", -1);
        }

        public static string Status(WorldViewData world, ActorViewData actor) => actor.Activity switch
        {
            "Idle" => actor.Kind == "worker" ? "等待安排" : "守卫待命",
            "Move" => "前往指定位置", "WorkMove" => "前往工作点",
            "Work" => "正在采集" + GameText.ResourceName(world.Worksites.FirstOrDefault(site => site.Id == actor.TargetId)?.Kind ?? ""),
            "BuildMove" => "前往工地", "Build" => "正在施工", "TrainingMove" => "前往兵营", "Training" => "训练 / 排队中",
            "Attack" => actor.Walking ? "追击敌人" : "战斗中", _ => "等待安排"
        };

        public static string Hint(WorldViewData world, int id, GameCatalog catalog)
        {
            var site = world.Worksites.FirstOrDefault(w => w.Id == id);
            if (site != null) return $"{catalog.Balance.Worksites[site.Kind].Name} · 剩余{site.Amount} · {(site.WorkerId == 0 ? "右键安排工人" : "工作点已有工人")}";
            var building = world.Buildings.FirstOrDefault(b => b.Id == id);
            if (building != null) return $"{catalog.Balance.Buildings[building.Kind].Name} · 生命{Math.Ceiling(building.Hp)}/{catalog.Balance.Buildings[building.Kind].Hp} · {GameText.Building(catalog, building.Kind)}";
            var actor = world.Actors.FirstOrDefault(a => a.Id == id);
            if (actor != null) return $"{actor.Name} · {catalog.Balance.Units[actor.Kind].Name} · {(actor.Enemy ? "右键指定攻击" : Status(world, actor))}";
            return "左键选择 · 右键执行 · A/D移动镜头 · 滚轮缩放 · 点击小地图定位";
        }
    }
}
