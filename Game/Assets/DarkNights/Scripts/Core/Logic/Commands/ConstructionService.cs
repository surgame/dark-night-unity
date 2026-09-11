using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Commands
{
    /// <summary>
    /// 负责放置规则、一次性支付与施工进度。位置/工人验证通过后才创建地基，完工不会覆盖已经受到的伤害，农田工人自动转入耕作。
    /// </summary>
    public sealed class ConstructionService
    {
        private readonly GameSession session;

        public ConstructionService(GameSession session)
        {
            this.session = session;
        }

        public string PlacementError(string kind, float x)
        {
            if (string.IsNullOrEmpty(kind) || !session.Catalog.Balance.Buildings.TryGetValue(kind, out var definition) || kind == "tavern")
                return "无法建造此建筑";
            if (float.IsNaN(x) || float.IsInfinity(x)) return "建造坐标无效";
            float width = definition.Width;
            if (x - width * 0.5 < session.Layout.BuildMinX || x + width * 0.5 > session.Layout.BuildMaxX)
                return "请在营地范围内建造";
            foreach (var building in session.World.Buildings)
                if (Math.Abs(x - building.X) < (width + building.Definition.Width) * 0.5 + 8)
                    return "与已有建筑过近";
            foreach (var site in session.World.Worksites)
                if (site.Amount != 0 && site.FarmId == 0 && Math.Abs(x - site.X) < (width + site.Definition.Width) * 0.5 + 6)
                    return "不能覆盖资源工作点";
            return "";
        }

        public bool Place(string kind, float x, IReadOnlyList<int> actorIds)
        {
            if (session.Mode != SessionMode.Playing || string.IsNullOrEmpty(kind) || !CommandActors.TryResolve(session, actorIds, out var candidates))
                return false;
            x = (float)SimulationMath.Snapped((double)x, 4);
            string error = PlacementError(kind, x);
            if (error.Length != 0)
            {
                session.Feedback.Notify(error, true);
                return false;
            }
            if (!candidates.Any(a => a.Kind == "worker" && !a.IsTraining))
                candidates = session.World.Actors.ToArray();
            var worker = candidates.Where(a => !a.Enemy && a.Kind == "worker" && !a.IsTraining && a.State != ActorActivity.Attack)
                .OrderBy(a => Math.Abs(a.X - x)).FirstOrDefault();
            if (worker == null)
            {
                session.Feedback.Notify("需要一名可以施工的工人。", true);
                return false;
            }
            var definition = session.Catalog.Balance.Buildings[kind];
            if (!session.Economy.Pay(definition.Cost))
            {
                session.Feedback.Notify("资源不足：需要 " + GameText.Cost(definition.Cost), true);
                return false;
            }
            var building = session.Lifecycle.SpawnBuilding(kind, x);
            session.Work.Assign(worker, building);
            session.Feedback.Notify($"{definition.Name}开始施工，{worker.Name}已前往工地。");
            return true;
        }

        internal void Advance(Building building, double delta)
        {
            var worker = session.World.Find<Actor>(building.WorkerId);
            if (worker == null || worker.State != ActorActivity.Build || worker.TargetId != building.Id)
                return;
            double increment = Math.Min(1 - building.Progress, delta / building.Definition.BuildSeconds);
            building.Progress = Math.Min(1, building.Progress + increment);
            building.Hp = Math.Min(building.MaximumHp, building.Hp + increment * building.MaximumHp * 0.8);
            if (!building.IsComplete)
                return;
            worker.ClearOrder();
            if (building.Kind == "farm")
            {
                var site = session.Lifecycle.SpawnSite("food", building.X, 0, building.Id);
                building.FarmSiteId = site.Id;
                session.Work.Assign(worker, site);
            }
            session.Feedback.Notify($"{building.Definition.Name}已建成。");
            session.Feedback.PlaySound("snd_upgrade_bld");
        }
    }
}
