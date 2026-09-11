using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Commands
{
    /// <summary>
    /// 管理工人的付费入队、逐一训练和职业切换。队列由兵营实例拥有，移动期间不推进训练；训练完成保持居民身份与人口总数。
    /// </summary>
    public sealed class TrainingService
    {
        private readonly GameSession session;

        public TrainingService(GameSession session)
        {
            this.session = session;
        }

        public int Start(string kind, IReadOnlyList<int> actorIds)
        {
            if (session.Mode != SessionMode.Playing || kind is not ("spearman" or "archer") ||
                !CommandActors.TryResolve(session, actorIds, out var actors))
                return 0;
            int trained = 0;
            var cost = session.Catalog.Balance.Units[kind].Cost;
            foreach (var actor in actors)
            {
                if (actor.Kind != "worker" || actor.IsTraining)
                    continue;
                var barracks = session.World.Buildings.Where(b => b.Kind == "barracks" && b.IsComplete &&
                    b.TrainingQueue.Count < session.Catalog.Balance.Economy.TrainingQueueLimit)
                    .OrderBy(b => Math.Abs(actor.X - b.X)).FirstOrDefault();
                if (barracks == null || !session.Economy.Pay(cost))
                    break;
                actor.ClearOrder();
                actor.State = ActorActivity.TrainingMove;
                actor.TargetId = barracks.Id;
                barracks.TrainingQueue.Add(new(actor.Id, kind, session.Catalog.Balance.Economy.TrainingSeconds));
                trained++;
            }
            if (trained > 0)
            {
                session.Feedback.PlaySound("snd_training_start");
                session.Feedback.Notify($"{trained}名工人前往兵营训练为{session.Catalog.Balance.Units[kind].Name}。");
            }
            else
                session.Feedback.Notify("训练需要选中工人、空闲的兵营队列及 " + GameText.Cost(cost), true);
            return trained;
        }

        internal void Advance(Building barracks, double delta)
        {
            if (barracks.TrainingQueue.Count == 0)
                return;
            var entry = barracks.TrainingQueue[0];
            var worker = session.World.Find<Actor>(entry.ActorId);
            if (worker == null)
            {
                barracks.TrainingQueue.RemoveAt(0);
                return;
            }
            if (worker.State != ActorActivity.Training)
                return;
            entry.Remaining = Math.Max(0, entry.Remaining - delta);
            if (entry.Remaining > 0)
                return;
            barracks.TrainingQueue.RemoveAt(0);
            worker.Promote(entry.Kind);
            worker.X = barracks.X + barracks.Definition.Width * 0.5f + 9;
            worker.RallyX = worker.X;
            session.Feedback.Notify($"{worker.Name}完成训练：{worker.Definition.Name}。");
            session.Feedback.PlaySound("snd_training_complete");
        }
    }
}
