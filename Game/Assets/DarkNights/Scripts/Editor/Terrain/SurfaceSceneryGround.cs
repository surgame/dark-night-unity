using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>从当前最终岩壁算法采样近景支撑的像素高度；只调整美术锚点，逻辑格、坡形和权威碰撞保持独立。</summary>
    internal sealed class SurfaceSceneryGround
    {
        private readonly CaveMaskRegion pixels;
        internal SurfaceSceneryGround(PlayableTerrain map, CaveTerrainStyle style, TerrainStyleDrafts drafts, int maximumRow)
        {
            if (style == null || !style.ProceduralRock) return;
            var modifiers = drafts == null ? style.CaptureModifiers() : drafts.Capture(style.Modifiers);
            if (style.ImmediateForeground) modifiers = modifiers.AsLocalForeground();
            if (!modifiers.SupportsLocalRoundedCluster)
                throw new InvalidOperationException("装饰贴合采样要求当前Local前景算法；保留旧布局，不以逻辑格冒充最终轮廓。");
            byte[] shapes = map.CopyShapes();
            bool Solid(int x, int y) => map.Material(x / 8, y / 8) != 0 &&
                TerrainShapeGeometry.Contains((TerrainCellShape)shapes[y / 8 * 320 + x / 8],
                    (x % 8 + .5f) / 8, 1 - (y % 8 + .5f) / 8);
            pixels = CaveModifiedTerrain.BakeRockRegion(Solid, 2560, 1536, map.Seed,
                style.CaptureOutline(), modifiers, 0, 0, 2560, Math.Min(1536, (maximumRow + 5) * 8),
                0, style.DecorationStartRow * 8);
        }

        internal float Offset(int column, int row, byte shape)
        {
            if (pixels == null) return 0;
            int x = column * 8 + 4;
            int top = Math.Max(0, row * 8 - 24), end = Math.Min(pixels.Height, row * 8 + 32);
            int y = top;
            while (y < end && !pixels.Solid(x, y)) y++;
            if (y == end) throw new InvalidOperationException("最终轮廓附近没有可见装饰支撑。");
            float logical = row * 8 + (1 - TerrainShapeGeometry.Edge((TerrainCellShape)shape, .5f)) * 8;
            return logical - y + .5f;
        }
    }
}
