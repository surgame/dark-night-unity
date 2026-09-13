using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 放置、同步装配和一次性支付的会话协调；自身不保存建筑或经济状态。
    /// 全部装配成功后才进入支付及派工，失败撤销新对象和本次 ID 草稿。
    /// </summary>
    public sealed class ObjectConstruction
    {
        private readonly ObjectSession session;
        internal ObjectConstruction(ObjectSession session) { this.session = session; }

        public string PlacementError(string kind, float x)
        {
            if (string.IsNullOrEmpty(kind) || !session.Catalog.Balance.Buildings.TryGetValue(kind, out var definition) || kind == "tavern")
                return "无法建造此建筑";
            if (float.IsNaN(x) || float.IsInfinity(x)) return "建造坐标无效";
            float width = definition.Width;
            if (!PlacementGeometry.Within(x, width, session.Layout.BuildMinX, session.Layout.BuildMaxX))
                return "请在营地范围内建造";
            foreach (BuildingBehaviour building in session.Index.Buildings)
                if (PlacementGeometry.BuildingOverlap(x, width, building.X, building.Definition.Width))
                    return "与已有建筑过近";
            foreach (WorksiteBehaviour site in session.Index.Worksites)
                if (site.Amount != 0 && site.FarmId == 0 &&
                    PlacementGeometry.WorksiteOverlap(x, width, site.X, site.Definition.Width))
                    return "不能覆盖资源工作点";
            return "";
        }

        internal int Place(string kind, float x, IReadOnlyList<int> ids)
        {
            ActorBehaviour[] candidates = session.Work.Resolve(ids);
            if (candidates == null || session.Camp.Read().Mode != SessionMode.Playing) return 0;
            x = PlacementGeometry.Snap(x);
            string error = PlacementError(kind, x);
            if (error.Length != 0) { session.Notify(error, true); return 0; }
            if (!candidates.Any(a => a.RuleKey == "worker" && !a.IsTraining))
                candidates = session.Index.Actors.ToArray();
            ActorBehaviour worker = candidates.Where(a => !a.Enemy && a.RuleKey == "worker" &&
                !a.IsTraining && a.Activity != ActorActivity.Attack).OrderBy(a => Math.Abs(a.X - x)).FirstOrDefault();
            if (worker == null) { session.Notify("需要一名可以施工的工人。", true); return 0; }
            BuildingDefinition rules = session.Catalog.Balance.Buildings[kind];
            if (!session.Economy.CanPay(rules.Cost))
            {
                session.Notify("资源不足：需要 " + GameText.Cost(rules.Cost), true);
                return 0;
            }
            var definition = session.Resources.Find(kind);
            BuildingBehaviour building;
            try { building = (BuildingBehaviour)session.Create(definition, x, "", false, 0, "", null); }
            catch (InvalidOperationException assemblyError)
            {
                throw new SessionOperationException(SessionResultCode.ObjectUnavailable, "建筑装配未就绪，未扣除资源。", assemblyError);
            }
            if (!session.Economy.Pay(rules.Cost) || !session.Work.Assign(worker, building))
                throw new InvalidOperationException("Prepared construction no longer satisfies its atomic commit.");
            session.Notify(rules.Name + "开始施工，" + worker.Name + "已前往工地。");
            return building.Id;
        }
    }
}
