using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// E键的物理研究语义适配；只从冻结帧选择最近端点并发送请求，不在本地创建约束。
    /// 服务端复查可信连接、控制租约、距离和目标存活；已有绳索时请求解除自己的连接。
    /// </summary>
    internal static class HeroPhysicsLink
    {
        internal static async UniTask Send(SessionNetwork network, ActorViewData actor, Core.Physics.PhysicsWorldDefinition rules)
        {
            var frame = network.Client.Replica.Current;
            var snapshot = frame?.World.Physics;
            if (!network.Client.Ready || actor == null || snapshot == null || rules == null || frame.Paused || frame.Loading) return;
            var origin = snapshot.Bodies.FirstOrDefault(b => b.Id == actor.Id);
            if (origin == null) return;
            int target = 0;
            if (!snapshot.Ropes.Any(r => r.OwnerId == actor.Id))
            {
                var grip = rules.HangingRopes.Select(r => (Rope: r,
                    Grip: Core.Physics.HangingRopeGeometry.Closest(r, snapshot, origin.X,
                        origin.Height + Core.Physics.HangingRopeGeometry.HandHeight)))
                    .Where(g => g.Grip.Distance <= g.Rope.Reach && g.Grip.Position >= Math.Min(40, g.Rope.Length * .1f) && g.Grip.Position <= g.Rope.Length - 4)
                    .OrderBy(g => g.Grip.Distance).ThenBy(g => g.Rope.Id).ToArray();
                if (grip.Length != 0) target = grip[0].Grip.BodyId;
                var nearest = snapshot.Bodies.Where(b => b.Id != actor.Id && !rules.HangingRopes.Any(r => r.Contains(b.Id)))
                    .OrderBy(b => Core.Physics.PhysicsRopeGeometry.Distance(origin, b, rules))
                    .ThenBy(b => b.Id).FirstOrDefault();
                if (target == 0 && nearest == null) return;
                if (target == 0) target = nearest.Id;
            }
            try { await network.Client.Send(SessionOperation.PhysicsLink, new[] { actor.Id },
                target: target, controlLease: actor.ControlLease); }
            catch (Exception error) { Debug.LogError("物理牵引请求失败：" + error.Message); }
        }
    }
}
