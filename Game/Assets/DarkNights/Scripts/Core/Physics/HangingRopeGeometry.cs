using System;
using System.Linq;

namespace DarkNights.Core.Physics
{
    /// <summary>
    /// 吊绳弧长与冻结绳节之间的纯几何换算；执行验证和客户端候选选择共用。
    /// 只读取快照，不从表现坐标或引擎对象反推权威抓点。
    /// </summary>
    public static class HangingRopeGeometry
    {
        public const float HandHeight = 34;
        public static (float X, float Height) Point(PhysicsBodySnapshot body, float localHeight)
        {
            double angle = body.Angle * Math.PI / 180;
            return (body.X - (float)Math.Sin(angle) * localHeight,
                body.Height + (float)Math.Cos(angle) * localHeight);
        }

        public static (int BodyId, float LocalHeight) Locate(HangingRopeDefinition rope, float position)
        {
            int part = Math.Min(rope.Count - 1, Math.Max(0, (int)(position / rope.SegmentLength)));
            return (rope.FirstBodyId - part, rope.SegmentLength - (position - part * rope.SegmentLength));
        }

        public static (int BodyId, float Position, float Distance) Closest(HangingRopeDefinition rope,
            PhysicsWorldSnapshot snapshot, float x, float height)
        {
            float best = float.MaxValue, position = 0;
            for (int i = 0; i < rope.Count; i++)
            {
                var body = snapshot.Bodies.Single(b => b.Id == rope.FirstBodyId - i);
                var top = Point(body, rope.SegmentLength);
                var bottom = Point(body, 0);
                float dx = bottom.X - top.X, dy = bottom.Height - top.Height;
                float t = Math.Clamp(((x - top.X) * dx + (height - top.Height) * dy) /
                    (rope.SegmentLength * rope.SegmentLength), 0, 1);
                float px = top.X + t * dx - x, py = top.Height + t * dy - height;
                float distance = (float)Math.Sqrt(px * px + py * py);
                if (distance >= best) continue;
                best = distance; position = (i + t) * rope.SegmentLength;
            }
            return (Locate(rope, position).BodyId, position, best);
        }
    }
}
