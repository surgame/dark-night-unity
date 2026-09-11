using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Commands
{
    /// <summary>
    /// 处理营地的招募与建筑修缮命令，集中检查人口、冷却、目标和费用。界面只请求操作，所有资源扣除仍通过EconomyService完成。
    /// </summary>
    public sealed class CampCommands
    {
        private readonly GameSession session;

        public CampCommands(GameSession session)
        {
            this.session = session;
        }

        private static readonly string[] Names = new[] { "诺拉", "芬恩", "凯伊", "梅芙", "莱恩", "伊芙", "艾洛", "希尔" };

        public bool Recruit()
        {
            if (session.Mode != SessionMode.Playing) return false;
            var rules = session.Catalog.Balance.Economy;
            if (session.Economy.Population >= session.Economy.Capacity)
            {
                session.Feedback.Notify($"人口容量已满，建造住宅可增加{session.Catalog.Balance.Buildings["house"].Capacity}个名额。", true);
                return false;
            }
            if (session.Economy.RecruitCooldown > 0)
            {
                session.Feedback.Notify($"下一位居民将在{Math.Ceiling(session.Economy.RecruitCooldown)}秒后到达。");
                return false;
            }
            var tavern = session.World.Buildings.LastOrDefault(b => b.Kind == "tavern" && b.IsComplete);
            if (tavern == null || !session.Economy.Pay(rules.RecruitCost))
            {
                session.Feedback.Notify("招募需要酒馆和 " + GameText.Cost(rules.RecruitCost), true);
                return false;
            }
            var actor = session.Lifecycle.SpawnActor("worker", tavern.X + 35, false, Names[session.World.NextId % Names.Length]);
            session.Economy.RecruitCooldown = rules.RecruitSeconds;
            session.Feedback.Notify($"{actor.Name}加入营地。");
            return true;
        }

        public bool Repair(int buildingId)
        {
            var building = session.World.Find<Building>(buildingId);
            if (building == null || !building.IsComplete || building.Hp >= building.MaximumHp)
            {
                session.Feedback.Notify("请选择受损且已完工的建筑。");
                return false;
            }
            if (session.Mode != SessionMode.Playing) return false;
            var rules = session.Catalog.Balance.Economy;
            if (!session.Economy.Pay(rules.RepairCost))
            {
                session.Feedback.Notify("修缮需要 " + GameText.Cost(rules.RepairCost), true);
                return false;
            }
            building.Hp = Math.Min(building.MaximumHp, building.Hp + rules.RepairHp);
            session.Feedback.Notify($"{building.Definition.Name}已修缮。");
            return true;
        }
    }
}
