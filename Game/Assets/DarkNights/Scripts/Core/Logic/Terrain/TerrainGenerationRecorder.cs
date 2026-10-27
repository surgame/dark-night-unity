using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 单次生成所有者使用的有界步骤记录器；只比较本次真实写入，不复制另一套生成算法。
    /// 默认仅保留上一步数组和八项摘要，按需记录时才冻结中间材料／坡形供作者预演。
    /// </summary>
    public sealed class TerrainGenerationRecorder
    {
        private readonly bool capture;
        private byte[] previous, previousShapes;
        private readonly List<TerrainGenerationStep> steps = new List<TerrainGenerationStep>();
        public TerrainGenerationRecorder(bool captureSnapshots = false) { capture = captureSnapshots; }

        public void Add(string name, byte[] cells, byte[] shapes = null, PlayableTerrain map = null)
        {
            int changed = 0, geometry = 0, left = 320, top = 192, right = -1, bottom = -1;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != (previous == null ? 0 : previous[i]))
                {
                    changed++; left = System.Math.Min(left, i % 320); right = System.Math.Max(right, i % 320);
                    top = System.Math.Min(top, i / 320); bottom = System.Math.Max(bottom, i / 320);
                }
                if ((shapes == null ? 0 : shapes[i]) != (previousShapes == null ? 0 : previousShapes[i])) geometry++;
            }
            steps.Add(new TerrainGenerationStep(name, changed, geometry,
                map?.Deposits.Sum(d => d.Cells.Count) ?? 0, map?.Nests.Count ?? 0,
                right < 0 ? -1 : left, bottom < 0 ? -1 : top, right, bottom,
                capture ? cells : null, capture ? shapes : null));
            previous = (byte[])cells.Clone();
            previousShapes = shapes == null ? null : (byte[])shapes.Clone();
        }

        public TerrainGenerationStep[] Finish() => steps.ToArray();
    }
}
