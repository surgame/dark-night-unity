using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>旋转碎岩盒的有限分离轴接触计算；只返回最小分离方向和深度，供配置烘焙使用。斜接触允许岩块侧滑，不引入持续物理状态或引擎依赖。</summary>
    public static class RubbleContactGeometry
    {
        public static bool Overlap(float dx, float dy, float aw, float ah, float ac, float ass,
            float bw, float bh, float bc, float bs, out float nx, out float ny, out float depth)
        {
            nx = ny = 0; depth = float.MaxValue;
            for (int axis = 0; axis < 4; axis++)
            {
                float ux = axis == 0 ? ac : axis == 1 ? -ass : axis == 2 ? bc : -bs;
                float uy = axis == 0 ? ass : axis == 1 ? ac : axis == 2 ? bs : bc;
                float a = Math.Abs(ux * ac + uy * ass) * aw + Math.Abs(-ux * ass + uy * ac) * ah;
                float b = Math.Abs(ux * bc + uy * bs) * bw + Math.Abs(-ux * bs + uy * bc) * bh;
                float distance = dx * ux + dy * uy, overlap = a + b - Math.Abs(distance);
                if (overlap <= 0) return false;
                if (overlap >= depth) continue;
                depth = overlap; float side = distance >= 0 ? 1 : -1;
                nx = ux * side; ny = uy * side;
            }
            return true;
        }
    }
}
