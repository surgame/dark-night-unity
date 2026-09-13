using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 协调招募、训练和修缮的已授权业务请求，所有库存和任务仍归所属 YYGC 状态。
    /// 先验证能力及参数再支付，训练保留逐单位部分成功语义，不存储全局选择或队列。
    /// </summary>
    public sealed class ObjectCampCommands
    {
        private static readonly string[] Names = { "诺拉", "芬恩", "凯伊", "梅芙", "莱恩", "伊芙", "艾洛", "希尔" };
        private readonly ObjectSession session;
        internal ObjectCampCommands(ObjectSession session) { this.session = session; }

        internal int Recruit()
        {
            if (session.Camp.Read().Mode != SessionMode.Playing) return 0;
            EconomyDefinition rules = session.Catalog.Balance.Economy;
            if (session.Economy.Population >= session.Economy.Capacity)
            {
                session.Notify("人口容量已满，建造住宅可增加" + session.Catalog.Balance.Buildings["house"].Capacity + "个名额。", true);
                return 0;
            }
            if (session.Economy.Read().RecruitCooldown > 0)
            {
                session.Notify("下一位居民将在" + Math.Ceiling(session.Economy.Read().RecruitCooldown) + "秒后到达。");
                return 0;
            }
            BuildingBehaviour tavern = session.Index.Buildings.LastOrDefault(b => b.RuleKey == "tavern" && b.IsComplete);
            if (tavern == null || !session.Economy.CanPay(rules.RecruitCost))
            {
                session.Notify("招募需要酒馆和 " + GameText.Cost(rules.RecruitCost), true);
                return 0;
            }
            ActorBehaviour actor;
            try { actor = session.Lifecycle.SpawnActor("worker", tavern.X + 35, false, Names[session.Camp.Read().NextEntityId % Names.Length]); }
            catch (InvalidOperationException error)
            {
                throw new SessionOperationException(SessionResultCode.ObjectUnavailable, "居民装配未就绪，未扣除资源。", error);
            }
            if (!session.Economy.Pay(rules.RecruitCost)) throw new InvalidOperationException("Prepared recruit lost its payment.");
            session.Economy.Edit().RecruitCooldown = rules.RecruitSeconds;
            session.Notify(actor.Name + "加入营地。");
            return actor.Id;
        }

        internal bool Repair(int id)
        {
            BuildingBehaviour building = session.Index.Find<BuildingBehaviour>(id);
            if (building == null || !building.IsComplete || building.Hp >= building.MaximumHp)
            {
                session.Notify("请选择受损且已完工的建筑。");
                return false;
            }
            if (session.Camp.Read().Mode != SessionMode.Playing) return false;
            EconomyDefinition rules = session.Catalog.Balance.Economy;
            if (!session.Economy.Pay(rules.RepairCost))
            {
                session.Notify("修缮需要 " + GameText.Cost(rules.RepairCost), true);
                return false;
            }
            building.Edit().Hp = Math.Min(building.MaximumHp, building.Hp + rules.RepairHp);
            session.Notify(building.Definition.Name + "已修缮。");
            return true;
        }

        internal int Train(string kind, IReadOnlyList<int> ids)
        {
            if (session.Camp.Read().Mode != SessionMode.Playing || (kind != "spearman" && kind != "archer")) return 0;
            ActorBehaviour[] actors = session.Work.Resolve(ids);
            if (actors == null) return 0;
            int trained = 0;
            ResourceAmounts cost = session.Catalog.Balance.Units[kind].Cost;
            foreach (ActorBehaviour actor in actors)
            {
                if (actor.RuleKey != "worker" || actor.IsTraining) continue;
                BuildingBehaviour barracks = session.Index.Buildings.Where(b => b.RuleKey == "barracks" && b.IsComplete &&
                    b.Read().TrainingQueue.Length < session.Catalog.Balance.Economy.TrainingQueueLimit)
                    .OrderBy(b => Math.Abs(actor.X - b.X)).FirstOrDefault();
                if (barracks == null || !session.Economy.Pay(cost)) break;
                session.Work.Clear(actor);
                actor.Edit().Activity = ActorActivity.TrainingMove;
                actor.Edit().TargetId = barracks.Id;
                var entry = new TrainingStateEntry(actor.Id, kind, session.Catalog.Balance.Economy.TrainingSeconds);
                BuildingState state = barracks.Edit();
                state.TrainingQueue = state.TrainingQueue.Append(entry).ToArray();
                trained++;
            }
            if (trained > 0)
            {
                session.Mutations.AfterCommit(() => session.Feedback.PlaySound("snd_training_start"));
                session.Notify(trained + "名工人前往兵营训练为" + session.Catalog.Balance.Units[kind].Name + "。");
            }
            else session.Notify("训练需要选中工人、空闲的兵营队列及 " + GameText.Cost(cost), true);
            return trained;
        }
    }
}
