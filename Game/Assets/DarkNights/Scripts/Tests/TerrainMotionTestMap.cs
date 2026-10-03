using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Tests
{
    /// <summary>斜坡运动的合成长坡夹具；保留正式格形状和未知边界，初始化只用于未接入会话的测试角色。</summary>
    internal sealed class TerrainMotionTestMap : IReadOnlyGrid
    {
        private readonly GridCell[] cells = new GridCell[320 * 192];
        public WorldIdentity World => default;
        public ulong CommitId => 0;
        public static float BaseHeight => PlayableTerrain.OriginY - 99.5f * PlayableTerrain.CellPixels;
        public GridSample Read(CellCoord position)
        {
            int row = -position.V;
            if (position.U < 0 || position.U >= 320 || row < 0 || row >= 192) return GridSample.Unknown;
            return GridSample.FromCell(cells[row * 320 + position.U]);
        }
        public void Set(int x, int row, TerrainCellShape shape = TerrainCellShape.Full) =>
            cells[row * 320 + x] = new GridCell(2, 0, TerrainShapeGeometry.Encode(shape, false));
        public void Clear(int x, int row) => cells[row * 320 + x] = default;

        public static TerrainMotionTestMap Ramp(float slope, bool mirror = false)
        {
            var map = new TerrainMotionTestMap();
            for (int column = 80; column < 220; column++)
            {
                int offset = Math.Clamp(column - 100, 0, 99);
                int row = 100 - (int)(offset * slope);
                int x = mirror ? 319 - column : column;
                if (column >= 100 && column < 200 && slope > 0)
                {
                    row = 99 - (int)(offset * slope);
                    var shape = slope == 1 ? TerrainCellShape.Rise
                        : offset % 2 == 0 ? TerrainCellShape.RiseLow : TerrainCellShape.RiseHigh;
                    if (mirror) shape = slope == 1 ? TerrainCellShape.Fall
                        : offset % 2 == 0 ? TerrainCellShape.FallLow : TerrainCellShape.FallHigh;
                    map.Set(x, row++, shape);
                }
                for (; row < 106; row++) map.Set(x, row);
            }
            return map;
        }

        public static ActorState Actor(float x, float height, bool jetpack = false)
        {
            var state = new ActorState();
            Set(state, nameof(ActorState.X), x); Set(state, nameof(ActorState.Height), height);
            Set(state, nameof(ActorState.ManualControl), true); Set(state, nameof(ActorState.SupportPlatform), 0);
            Set(state, nameof(ActorState.JetpackOwned), jetpack); Set(state, nameof(ActorState.JetpackEquipped), jetpack);
            Set(state, nameof(ActorState.JetpackFuel), 2d);
            return state;
        }

        public static void Set(ActorState state, string member, object value) => typeof(ActorState).GetProperty(member).SetValue(state, value);
        public static HeroControlDefinition Rules(HeroJumpStrategy strategy = HeroJumpStrategy.Fixed) =>
            new HeroControlDefinition(160, 320, 1000, 70, 2, 2, .3, 16, 11.0 / 7, 112, strategy);
    }
}
