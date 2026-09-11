using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Commands
{
    /// <summary>
    /// 执行已经选定的世界目标命令，不负责鼠标坐标或图像命中。群体采集自动寻找同类空工位，直接攻击与普通移动保持各自的追击规则。
    /// </summary>
    public sealed class UnitOrders
    {
        private readonly GameSession session;

        public UnitOrders(GameSession session)
        {
            this.session = session;
        }

        public int Issue(IReadOnlyList<int> actorIds, int targetId, float x)
        {
            if (session.Mode != SessionMode.Playing || float.IsNaN(x) || float.IsInfinity(x) ||
                !CommandActors.TryResolve(session, actorIds, out var selected)) return 0;
            var target = session.World.Find(targetId);
            if (targetId != 0 && target == null) return 0;
            if (selected.Length == 0)
            {
                session.Feedback.Notify("先左键选择居民，再右键下达命令。");
                return 0;
            }
            if (target is Building { Kind: "farm", IsComplete: true } farm)
                target = session.World.Find(farm.FarmSiteId);
            int assigned = 0;
            foreach (var actor in selected)
            {
                if (actor.IsTraining)
                    continue;
                if (target is Worksite desired)
                {
                    if (actor.Kind != "worker")
                        continue;
                    var site = desired.WorkerId == 0 || desired.WorkerId == actor.Id ? desired :
                        session.World.Worksites.Where(s => s.Kind == desired.Kind && s.WorkerId == 0 && s.Amount != 0)
                            .OrderBy(s => Math.Abs(s.X - desired.X)).FirstOrDefault();
                    if (site != null && session.Work.Assign(actor, site))
                        assigned++;
                }
                else if (target is Building { IsComplete: false } building)
                {
                    if (session.Work.Assign(actor, building))
                        assigned++;
                }
                else if (target is Actor { Enemy: true } enemy)
                {
                    actor.ClearOrder();
                    actor.TargetId = enemy.Id;
                    actor.State = ActorActivity.Attack;
                    actor.ForcedAttack = true;
                    assigned++;
                }
                else
                {
                    float offset = (assigned - (selected.Length - 1) * 0.5f) * 8;
                    if (actor.Kind == "archer" && selected.Length > 1)
                        offset -= 32;
                    actor.OrderMove(x + offset);
                    assigned++;
                }
            }
            if (assigned > 0)
            {
                session.Feedback.Emit(new("command", x, session.GroundY));
                if (target is Worksite site)
                    session.Feedback.Notify($"已安排{assigned}名工人采集{GameText.ResourceName(site.Kind)}。");
            }
            else
                session.Feedback.Notify("目标已被占用，或所选单位正在训练。工作和施工需要工人。", true);
            return assigned;
        }
    }
}
