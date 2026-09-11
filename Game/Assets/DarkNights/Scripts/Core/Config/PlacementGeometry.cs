using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 放置占地与网格的无状态公共计算，权威建造和客户端预览共用同一距离公式。
    /// 不结算权限、支付或派工；预览的只读结果不能替代服务端最终验证。
    /// </summary>
    public static class PlacementGeometry
    {
        public static float Snap(float x) => (float)(Math.Floor((double)x / 4 + .5) * 4);
        public static bool Within(float x, float width, float min, float max) =>
            x - width * .5 >= min && x + width * .5 <= max;
        public static bool BuildingOverlap(float x, float width, float otherX, float otherWidth) =>
            Math.Abs(x - otherX) < (width + otherWidth) * .5 + 8;
        public static bool WorksiteOverlap(float x, float width, float otherX, float otherWidth) =>
            Math.Abs(x - otherX) < (width + otherWidth) * .5 + 6;
    }
}
