using System.Linq;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 既有会话统一的角色调度屏障；格运动顺序沿用原入口，物理候选收集全体后只推进一次。
    /// 只组合已有角色引用，不拥有角色状态，不从独立FixedUpdate再次推进。
    /// </summary>
    internal static class SessionActorMotion
    {
        internal static void Advance(ObjectSession world, double delta)
        {
            var actors = world.Index.Actors.Where(a => a.Read().ManualControl && a.Hp > 0).ToArray();
            if (world.Physics?.Enabled == true) world.Physics.Advance(actors, delta);
            else foreach (var actor in actors)
            {
                if (!world.IsExpedition && world.Camp.Read().Mode != Core.Logic.State.SessionMode.Playing) break;
                actor.Tick(delta);
            }
        }
    }
}
