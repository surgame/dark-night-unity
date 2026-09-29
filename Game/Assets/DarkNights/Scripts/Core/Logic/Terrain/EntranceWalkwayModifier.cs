using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>泊位外的下洞步道步骤；依次尝试可站立洞室支撑，拒绝切断原有洞室空腔的候选。</summary>
    public sealed class EntranceWalkwayModifier : ITerrainGenerationModifier
    {
        private const int W = TerrainGenerationSettings.Width;
        private const int H = TerrainGenerationSettings.Height;
        private readonly int headroom;
        private readonly int maxCandidates;
        public string StableId => "entrance-walkway-v2";
        public TerrainGenerationStage Stage => TerrainGenerationStage.AfterDock;

        public EntranceWalkwayModifier(int headroom = 5, int maxCandidates = 64)
        {
            if (headroom < 3 || headroom > 7 || maxCandidates < 1 || maxCandidates > 256)
                throw new ArgumentOutOfRangeException(nameof(headroom), "步道净空须为 3–7 格，候选数须为 1–256。");
            this.headroom = headroom; this.maxCandidates = maxCandidates;
        }

        public void Apply(TerrainGenerationContext context)
        {
            var planet = context.Planet;
            int left = planet.DockColumn - planet.LandingWidth / 2;
            int right = left + planet.LandingWidth - 1;
            var targets = FindTargets(context.Source, planet, left, right);
            byte[] baseline = context.CopyMaterials();
            bool[] baselineProtection = context.CopyProtection();
            bool[] baselineSoft = context.CopySoftRock();
            int attempts = Math.Min(maxCandidates, targets.Count);
            for (int i = 0; i < attempts; i++)
            {
                context.CheckCancellation();
                var target = targets[i];
                int start = target.Column < left ? left - 1 : right + 1;
                var cells = (byte[])baseline.Clone();
                var protection = (bool[])baselineProtection.Clone();
                var soft = (bool[])baselineSoft.Clone();
                var support = new bool[cells.Length];
                Carve(cells, protection, soft, support, start, planet.DockRow, target.Column, target.Floor);
                if (!TerrainCavityConnectivity.PreservesRooms(baseline, cells, context.Source.Rooms, context.Cancelled) &&
                    !WalkwayCavityRepair.TryRepair(baseline, cells, protection, soft, support, context.Cancelled)) continue;
                if (!TerrainCavityConnectivity.PreservesRooms(baseline, cells, context.Source.Rooms, context.Cancelled)) continue;
                context.Replace(cells, protection, soft);
                return;
            }
            throw new InvalidOperationException("入口步道的候选会隔断原有洞室，或找不到可站立支撑；请关闭此步骤或调整星球泊位与种子。");
        }

        private static List<Target> FindTargets(TerrainBlueprint source, PlanetDefinition planet, int left, int right)
        {
            var targets = new List<Target>();
            foreach (var room in source.Rooms)
            {
                for (int x = Math.Max(3, room.Left + 2); x <= Math.Min(W - 4, room.Left + room.Width - 2); x++)
                {
                    if (x >= left && x <= right) continue;
                    int start = x < left ? left - 1 : right + 1;
                    int horizontal = Math.Abs(x - start);
                    int top = Math.Max(planet.DockRow + 8, room.Top + 3);
                    int bottom = Math.Min(H - 5, room.Top + room.Height + 3);
                    for (int floor = top; floor <= bottom; floor++)
                    {
                        if (source.MaterialAt(x, floor) == 0 || source.IsProtected(x, floor) ||
                            source.MaterialAt(x, floor - 1) != 0 || source.MaterialAt(x, floor - 2) != 0 ||
                            source.MaterialAt(x, floor - 3) != 0) continue;
                        int vertical = floor - planet.DockRow;
                        if (horizontal < vertical + 2) continue;
                        targets.Add(new Target(x, floor, horizontal + vertical * 2));
                    }
                }
            }
            targets.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) :
                a.Column != b.Column ? a.Column.CompareTo(b.Column) : a.Floor.CompareTo(b.Floor));
            return targets;
        }

        private void Carve(byte[] cells, bool[] protection, bool[] soft, bool[] support, int start, int top, int end, int bottom)
        {
            int distance = Math.Abs(end - start), direction = Math.Sign(end - start);
            for (int step = 0; step <= distance; step++)
            {
                int x = start + step * direction;
                int floor = top + (int)Math.Round((bottom - top) * step / (double)distance);
                for (int y = floor - headroom; y < floor + 2; y++)
                {
                    int index = y * W + x;
                    cells[index] = (byte)(y < floor ? 0 : 2);
                    support[index] = y >= floor;
                    protection[index] = false; soft[index] = false;
                }
            }
        }

        /// <summary>单个可站立的洞室支撑与搜索评分；仅在候选选择时使用。</summary>
        private struct Target
        {
            public int Column { get; }
            public int Floor { get; }
            public int Score { get; }
            public Target(int column, int floor, int score) { Column = column; Floor = floor; Score = score; }
        }
    }
}
