using System.Collections.Generic;
using DarkNights.Core.Logic.Entities;

namespace DarkNights.Core.Logic.Commands
{
    /// <summary>
    /// 将显式命令中的 ID 列表一次校验为本世界居民，拒绝重复、敌军、失效及超量输入后才允许写入。
    /// 保留请求顺序以保持群体移动及支付顺序；玩家身份和房间权限仍由 Runtime 验证。
    /// </summary>
    internal static class CommandActors
    {
        public static bool TryResolve(GameSession session, IReadOnlyList<int> ids, out Actor[] actors)
        {
            actors = null;
            if (ids == null || ids.Count > 256) return false;
            var result = new Actor[ids.Count];
            var seen = new HashSet<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                var actor = session.World.Find<Actor>(ids[i]);
                if (actor == null || actor.Enemy || actor.Hp <= 0 || !seen.Add(ids[i])) return false;
                result[i] = actor;
            }
            actors = result;
            return true;
        }
    }
}
