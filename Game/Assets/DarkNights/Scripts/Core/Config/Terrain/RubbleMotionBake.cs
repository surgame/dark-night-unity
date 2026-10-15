using System;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>局部碎岩轨迹的纯计算烘焙；90Hz重力、有限盒接触、低回弹及地面摩擦生成冻结轨迹。只为单个事件配置计算，不启动第二套运行世界，不写地图或结算伤害。</summary>
    public static class RubbleMotionBake
    {
        public static RubbleCollapseViewData Build(FallingRockViewData[] cells, float floorY, float left, float right)
        {
            var pieces = RubbleFragmentViewData.Create(cells);
            int count = pieces.Length, frames = 301;
            var x = new float[count]; var y = new float[count]; var vx = new float[count]; var vy = new float[count];
            var halfX = new float[count]; var halfY = new float[count]; var inverseMass = new float[count];
            var cos = new float[count]; var sin = new float[count];
            var samples = new float[frames * count * 3];
            var impacts = new double[count];
            for (int i = 0; i < count; i++) impacts[i] = double.PositiveInfinity;
            float minimumX = float.MaxValue, maximumX = float.MinValue, minimumY = float.MaxValue, maximumY = float.MinValue;
            foreach (var cell in cells)
            {
                minimumX = Math.Min(minimumX, cell.SourceX - 8); maximumX = Math.Max(maximumX, cell.SourceX + 8);
                minimumY = Math.Min(minimumY, cell.SourceY - 8); maximumY = Math.Max(maximumY, cell.SourceY + 8);
            }
            for (int i = 0; i < count; i++)
            {
                var piece = pieces[i]; double radians = piece.Angle * Math.PI / 180;
                cos[i] = (float)Math.Cos(radians); sin[i] = (float)Math.Sin(radians);
                halfX[i] = (float)(Math.Abs(Math.Cos(radians)) * piece.Width + Math.Abs(Math.Sin(radians)) * piece.Height) * .5f;
                halfY[i] = (float)(Math.Abs(Math.Sin(radians)) * piece.Width + Math.Abs(Math.Cos(radians)) * piece.Height) * .5f;
                x[i] = Math.Max(minimumX + halfX[i], Math.Min(maximumX - halfX[i], piece.Motion.SourceX));
                y[i] = Math.Max(minimumY + halfY[i], Math.Min(maximumY - halfY[i], piece.Motion.SourceY));
                vx[i] = (i * 17 % 13 - 6) * 3;
                inverseMass[i] = 1 / (piece.Width * piece.Height);
            }
            float dt = (float)RubbleCollapseViewData.FrameSeconds;
            for (int frame = 0; frame < frames; frame++)
            {
                double clock = frame * RubbleCollapseViewData.FrameSeconds;
                for (int i = 0; i < count; i++)
                {
                    if (clock < pieces[i].Motion.Delay) continue;
                    vx[i] = Math.Max(-12, Math.Min(12, vx[i]));
                    vy[i] -= pieces[i].Motion.Gravity * dt; x[i] += vx[i] * dt; y[i] += vy[i] * dt;
                }
                for (int pass = 0; pass < 7; pass++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (clock < pieces[i].Motion.Delay) continue;
                        if (y[i] + halfY[i] > maximumY) { y[i] = maximumY - halfY[i]; vy[i] = Math.Min(0, vy[i]); }
                        // 大块完全离开源岩开口前留在孔内，不能在分裂挤压时被推入相邻洞顶。
                        if (y[i] + halfY[i] > minimumY)
                        {
                            x[i] = Math.Max(minimumX + halfX[i], Math.Min(maximumX - halfX[i], x[i]));
                        }
                        // 作者声明的有限散落带是本切片的侧向预算，不能把碎岩甩进邻近未授权区域。
                        if (x[i] - halfX[i] < left) { x[i] = left + halfX[i]; vx[i] = Math.Abs(vx[i]) * .035f; }
                        if (x[i] + halfX[i] > right) { x[i] = right - halfX[i]; vx[i] = -Math.Abs(vx[i]) * .035f; }
                        if (y[i] - halfY[i] < floorY)
                        {
                            if (double.IsPositiveInfinity(impacts[i])) impacts[i] = clock;
                            y[i] = floorY + halfY[i];
                            if (vy[i] < 0) vy[i] *= -.035f;
                            vx[i] *= .82f;
                        }
                        for (int j = 0; j < i; j++)
                        {
                            if (clock < pieces[j].Motion.Delay) continue;
                            float dx = x[i] - x[j], dy = y[i] - y[j];
                            if (Math.Abs(dx) >= halfX[i] + halfX[j] || Math.Abs(dy) >= halfY[i] + halfY[j]) continue;
                            if (!RubbleContactGeometry.Overlap(dx, dy, pieces[i].Width * .5f, pieces[i].Height * .5f, cos[i], sin[i],
                                pieces[j].Width * .5f, pieces[j].Height * .5f, cos[j], sin[j], out float nx, out float ny, out float overlap)) continue;
                            overlap *= .8f;
                            float mass = inverseMass[i] + inverseMass[j];
                            float a = inverseMass[i] / mass, b = inverseMass[j] / mass;
                            x[i] += nx * overlap * a; x[j] -= nx * overlap * b;
                            y[i] += ny * overlap * a; y[j] -= ny * overlap * b;
                            float relative = (vx[i] - vx[j]) * nx + (vy[i] - vy[j]) * ny;
                            if (relative >= 0) continue;
                            float impulse = -1.035f * relative;
                            if (ny > .4f && vy[i] < -10 && double.IsPositiveInfinity(impacts[i])) impacts[i] = clock;
                            if (ny < -.4f && vy[j] < -10 && double.IsPositiveInfinity(impacts[j])) impacts[j] = clock;
                            vx[i] += nx * impulse * a; vx[j] -= nx * impulse * b;
                            vy[i] += ny * impulse * a; vy[j] -= ny * impulse * b;
                        }
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    y[i] = Math.Min(maximumY - halfY[i], y[i]);
                    if (y[i] + halfY[i] > minimumY)
                        x[i] = Math.Max(minimumX + halfX[i], Math.Min(maximumX - halfX[i], x[i]));
                    int offset = (frame * count + i) * 3;
                    samples[offset] = x[i]; samples[offset + 1] = y[i];
                    float progress = (float)Math.Max(0, Math.Min(1, (clock - pieces[i].Motion.Delay) / .8));
                    samples[offset + 2] = pieces[i].Angle * progress;
                }
            }
            for (int i = 0; i < count; i++) if (double.IsPositiveInfinity(impacts[i])) impacts[i] = (frames - 1) * RubbleCollapseViewData.FrameSeconds;
            return new RubbleCollapseViewData(pieces, samples, impacts, frames, floorY);
        }
    }
}
