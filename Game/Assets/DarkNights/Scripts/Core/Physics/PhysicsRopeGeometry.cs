using System;
using System.Linq;
using DarkNights.Core.Config;

namespace DarkNights.Core.Physics
{
    /// <summary>
    /// 绳索实际关节端点的纯几何合同；共同用于可信距离校验、本地目标选取及只读显示。
    /// 根坐标在脚底，箱子旋转后端点随本地中心转动，不能用根间距离代替关节距离。
    /// </summary>
    public static class PhysicsRopeGeometry
    {
        public static (float X, float Height) Anchor(int id, float x, float height, float angle, PhysicsWorldDefinition rules)
        {
            float half = id > 0 ? HeroControlDefinition.BodyHeight * .5f :
                rules.Props.Single(p => p.Id == id).BodyHeight * .5f;
            double radians = angle * Math.PI / 180;
            return (x - (float)Math.Sin(radians) * half, height + (float)Math.Cos(radians) * half);
        }

        public static float Distance(PhysicsBodySnapshot from, PhysicsBodySnapshot to, PhysicsWorldDefinition rules)
        {
            var a = Anchor(from.Id, from.X, from.Height, from.Angle, rules);
            var b = Anchor(to.Id, to.X, to.Height, to.Angle, rules);
            float dx = a.X - b.X, dy = a.Height - b.Height;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
