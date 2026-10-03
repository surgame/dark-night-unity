using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// 对完整身体执行有界斜向扫掠；可行走坡面接触与墙、顶阻挡分别处理，剩余横移沿真实支撑继续。
    /// 只写调用方 ActorState，不拥有额外运行模型；净空仍复用 TerrainBodyCollision，接触最多二分十二次。
    /// </summary>
    internal static class TerrainBodySweep
    {
        private const float ContactTolerance = .002f;
        private static float Width(ActorState s) => s.ManualControl ? HeroControlDefinition.BodyHalfWidth : 5;
        private static float Height(ActorState s) => s.ManualControl ? HeroControlDefinition.BodyHeight : 22;
        private static bool Blocked(IReadOnlyGrid map, ActorState s, float x, float y) =>
            TerrainBodyCollision.Blocked(map, x, y, Width(s), Height(s));
        private static bool Ground(IReadOnlyGrid map, ActorState s, float x, float high, float low, out float y) =>
            TerrainBodyCollision.Ground(map, x, high, low, Width(s), Height(s), out y);

        public static float LaunchDistance(IReadOnlyGrid map, ActorState s, float dx, float dy)
        {
            if (dy <= 0 || dx == 0) return dx;
            float clearance = Math.Min(.5f, dy * .5f);
            bool Clear(float ratio) => !Ground(map, s, s.X + dx * ratio,
                s.Height + Math.Abs(dx) + .2f, s.Height - Math.Abs(dx) - .2f, out float floor) ||
                floor < s.Height + dy - clearance;
            if (Clear(1)) return dx;
            // 起跳首步保留真实离坡净空，只裁去会立即撞回坡面的水平量；不增加竖直冲量。
            float low = 0, high = 1;
            for (int n = 0; n < 12; n++)
            {
                float middle = (low + high) * .5f;
                if (Clear(middle)) low = middle; else high = middle;
            }
            return dx * low;
        }

        public static bool Move(IReadOnlyGrid map, ActorState s, float dx, float dy, bool grounded)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 2));
            float stepX = dx / steps, stepY = dy / steps;
            for (int i = 0; i < steps; i++)
            {
                if (grounded)
                {
                    grounded = Walk(map, s, stepX);
                    if (grounded) continue;
                    // 走出支撑后由下一步重力接管；本步不把贴地高度变化继承为空中冲量。
                    s.SupportPlatform = -1;
                    stepY = 0;
                    continue;
                }
                float x = s.X, y = s.Height;
                float nextX = x + stepX, nextY = y + stepY;
                if (CrossesFloor(map, s, x, y, nextX, nextY, out _))
                {
                    float low = 0, high = 1;
                    for (int n = 0; n < 12; n++)
                    {
                        float middle = (low + high) * .5f;
                        if (CrossesFloor(map, s, x, y, x + stepX * middle, y + stepY * middle, out _)) high = middle;
                        else low = middle;
                    }
                    float hitX = x + stepX * high;
                    if (Ground(map, s, hitX, y + Math.Abs(stepX) + .2f,
                        Math.Min(y, nextY) - .1f, out float floor))
                    {
                        s.X = hitX; s.Height = floor; s.VerticalSpeed = 0;
                        s.SupportPlatform = 0; s.JumpAscending = false;
                        grounded = Walk(map, s, stepX * (1 - high));
                        stepY = 0;
                        continue;
                    }
                }
                if (!Blocked(map, s, nextX, nextY)) { s.X = nextX; s.Height = nextY; continue; }
                // 非地面碰撞先到达真实接触位置，再保留未受阻轴；不丢掉整个分步的有效距离。
                float ratio = ClearRatio(map, s, x, y, stepX, stepY);
                s.X = x + stepX * ratio; s.Height = y + stepY * ratio;
                float remainingY = nextY - s.Height;
                float vertical = ClearRatio(map, s, s.X, s.Height, 0, remainingY);
                s.Height += remainingY * vertical;
                if (vertical < 1 && Math.Abs(remainingY) > ContactTolerance)
                { s.VerticalSpeed = 0; s.JumpAscending = false; stepY = 0; }
                float remainingX = nextX - s.X;
                s.X += remainingX * ClearRatio(map, s, s.X, s.Height, remainingX, 0);
            }
            return grounded;
        }

        private static bool CrossesFloor(IReadOnlyGrid map, ActorState s, float x, float y,
            float nextX, float nextY, out float floor)
        {
            float high = y + Math.Abs(nextX - x) + .2f;
            if (!Ground(map, s, nextX, high, Math.Min(y, nextY) - .1f, out floor) || floor < nextY - ContactTolerance)
                return false;
            // 从缺口上方下降到新地面时，旧横坐标可以没有支撑；脚底从表面上方扫过即可着地。
            if (nextY <= y && floor <= y + ContactTolerance) return true;
            // 初始位置必须在支撑表面上方；上升远离平地时不吸回，坡面追上角色时才建立接触。
            if (!Ground(map, s, x, y + .1f, Math.Min(y, nextY) - Math.Abs(nextX - x) - .2f, out float previous))
                return false;
            return previous <= y + ContactTolerance && floor - nextY >= previous - y - ContactTolerance;
        }

        private static bool Walk(IReadOnlyGrid map, ActorState s, float dx)
        {
            float x = s.X, y = s.Height;
            bool Follow(float ratio, out float floor) => Ground(map, s, x + dx * ratio, y + 2.05f, y - 2.05f, out floor);
            if (Follow(1, out float nextFloor)) { s.X = x + dx; s.Height = nextFloor; return true; }
            if (!Blocked(map, s, x + dx, y)) { s.X = x + dx; return false; }
            float low = 0, high = 1;
            for (int n = 0; n < 12; n++)
            {
                float middle = (low + high) * .5f;
                if (Follow(middle, out _)) low = middle; else high = middle;
            }
            if (Follow(low, out float floor)) { s.X = x + dx * low; s.Height = floor; }
            return true;
        }

        private static float ClearRatio(IReadOnlyGrid map, ActorState s, float x, float y, float dx, float dy)
        {
            if (!Blocked(map, s, x + dx, y + dy)) return 1;
            float low = 0, high = 1;
            for (int n = 0; n < 12; n++)
            {
                float middle = (low + high) * .5f;
                if (Blocked(map, s, x + dx * middle, y + dy * middle)) high = middle; else low = middle;
            }
            return low;
        }
    }
}
