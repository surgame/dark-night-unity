using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>编辑器专用的只读材质烘焙；可生成完整地图或测试用局部矩形，不创建地图会话或修改资产。</summary>
    public static class TerrainStylePreviewBaker
    {
        public const int Width = 504, Height = 312;
        public const int WorldWidth = 2560, WorldHeight = 1536;
        private const int PageSize = 256, CellsWide = 320;

        public static byte[] Bake(byte[] materials, byte[] shapes, string seed, int left, int top, int stoneSize,
            CaveOutlineSettings outline, CaveModifierStack foreground, ICaveBackgroundGenerator generator,
            CaveModifierStack[] backgroundModifiers, bool[] visible, int softness, Action checkpoint = null)
            => BakeRect(materials, shapes, seed, left, top, Width, Height, stoneSize, outline, foreground,
                generator, backgroundModifiers, visible, softness, checkpoint);

        public static byte[] BakeFull(byte[] materials, byte[] shapes, string seed, int stoneSize,
            CaveOutlineSettings outline, CaveModifierStack foreground, ICaveBackgroundGenerator generator,
            CaveModifierStack[] backgroundModifiers, bool[] visible, int softness, Action checkpoint = null,
            byte[] backgroundMaterials = null, byte[] backgroundShapes = null)
            => BakeRect(materials, shapes, seed, 0, 0, WorldWidth, WorldHeight, stoneSize, outline, foreground,
                generator, backgroundModifiers, visible, softness, checkpoint, backgroundMaterials, backgroundShapes);

        public static (byte[] Pixels, byte[] Background, byte[] Materials, byte[] Shapes, CaveMaskField Field)
            BakeFullFrame(byte[] materials, byte[] shapes, string seed, int stoneSize,
            CaveOutlineSettings outline, CaveModifierStack foreground, ICaveBackgroundGenerator generator,
            CaveModifierStack[] backgroundModifiers, bool[] visible, int softness, Action checkpoint = null,
            byte[] backgroundMaterials = null, byte[] backgroundShapes = null)
            => BakeRectFrame(materials, shapes, seed, 0, 0, WorldWidth, WorldHeight, stoneSize, outline, foreground,
                generator, backgroundModifiers, visible, softness, checkpoint, backgroundMaterials, backgroundShapes);

        public static (byte[] Pixels, byte[] Background, byte[] Materials, byte[] Shapes, CaveMaskField Field)
            UpdateFrame((byte[] Pixels, byte[] Background, byte[] Materials, byte[] Shapes, CaveMaskField Field) previous,
            byte[] materials, byte[] shapes, string seed, int stoneSize,
            CaveOutlineSettings outline, CaveModifierStack foreground, Action checkpoint = null)
        {
            if (previous.Pixels == null || previous.Pixels.Length != WorldWidth * WorldHeight * 4 ||
                previous.Background.Length != previous.Pixels.Length || materials == null || shapes == null ||
                materials.Length != CellsWide * 192 || shapes.Length != materials.Length || foreground == null)
                throw new ArgumentException("局部预览缺少有效的完整画面。");
            bool Solid(int x, int y)
            {
                int cell = y / 8 * CellsWide + x / 8;
                return materials[cell] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)shapes[cell],
                    (x % 8 + .5f) / 8, 1 - (y % 8 + .5f) / 8);
            }
            CaveMaskField field = foreground.Enabled
                ? CaveModifiedTerrain.Bake(Solid, WorldWidth, WorldHeight, seed, outline, foreground, 43 * 8, checkpoint)
                : null;
            bool[] dirty = foreground.Enabled
                ? CaveMaskChanges.DirtyPages(previous.Field, field, PageSize, checkpoint: checkpoint)
                : DirtyCellPages(previous, materials, shapes, outline, checkpoint);
            byte[] pixels = (byte[])previous.Pixels.Clone();
            for (int page = 0; page < dirty.Length; page++)
            {
                if (!dirty[page]) continue;
                checkpoint?.Invoke();
                int left = page % 10 * PageSize, top = page / 10 * PageSize;
                byte[] rock = CaveRockBaker.Bake(Solid, WorldWidth, WorldHeight, seed, left, top,
                    PageSize, PageSize, checkpoint, stoneSize, outline, field);
                for (int y = 0; y < PageSize; y++)
                {
                    int row = ((top + y) * WorldWidth + left) * 4;
                    int source = y * PageSize * 4;
                    for (int x = 0; x < PageSize; x++)
                    {
                        int src = source + x * 4, dst = row + x * 4;
                        if (rock[src + 3] != 0) Buffer.BlockCopy(rock, src, pixels, dst, 3);
                        else Buffer.BlockCopy(previous.Background, dst, pixels, dst, 3);
                    }
                }
            }
            return (pixels, previous.Background, (byte[])materials.Clone(), (byte[])shapes.Clone(), field);
        }

        private static bool[] DirtyCellPages(
            (byte[] Pixels, byte[] Background, byte[] Materials, byte[] Shapes, CaveMaskField Field) previous,
            byte[] materials, byte[] shapes,
            CaveOutlineSettings outline, Action checkpoint)
        {
            var dirty = new bool[60];
            int reach = CaveRockBaker.DistanceCap + (outline?.Reach ?? 0) + 3;
            for (int y = 0; y < 192; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < CellsWide; x++)
                {
                    int cell = y * CellsWide + x;
                    if (materials[cell] == previous.Materials[cell] && shapes[cell] == previous.Shapes[cell]) continue;
                    for (int py = Math.Max(0, (y * 8 - reach) / PageSize); py <= Math.Min(5, (y * 8 + 7 + reach) / PageSize); py++)
                        for (int px = Math.Max(0, (x * 8 - reach) / PageSize); px <= Math.Min(9, (x * 8 + 7 + reach) / PageSize); px++)
                            dirty[py * 10 + px] = true;
                }
            }
            return dirty;
        }

        private static byte[] BakeRect(byte[] materials, byte[] shapes, string seed, int left, int top,
            int width, int height, int stoneSize, CaveOutlineSettings outline, CaveModifierStack foreground,
            ICaveBackgroundGenerator generator, CaveModifierStack[] backgroundModifiers, bool[] visible,
            int softness, Action checkpoint, byte[] backgroundMaterials = null, byte[] backgroundShapes = null)
            => BakeRectFrame(materials, shapes, seed, left, top, width, height, stoneSize, outline, foreground,
                generator, backgroundModifiers, visible, softness, checkpoint, backgroundMaterials, backgroundShapes).Pixels;

        private static (byte[] Pixels, byte[] Background, byte[] Materials, byte[] Shapes, CaveMaskField Field)
            BakeRectFrame(byte[] materials, byte[] shapes, string seed, int left, int top,
            int width, int height, int stoneSize, CaveOutlineSettings outline, CaveModifierStack foreground,
            ICaveBackgroundGenerator generator, CaveModifierStack[] backgroundModifiers, bool[] visible,
            int softness, Action checkpoint, byte[] backgroundMaterials = null, byte[] backgroundShapes = null)
        {
            const int cellsWide = 320;
            if (materials == null || shapes == null || materials.Length != 320 * 192 || shapes.Length != materials.Length ||
                seed == null || foreground == null || generator == null || backgroundModifiers == null ||
                backgroundModifiers.Length != 3 || visible == null || visible.Length != 3 ||
                (backgroundMaterials != null && backgroundMaterials.Length != materials.Length) ||
                (backgroundShapes != null && backgroundShapes.Length != shapes.Length) ||
                left < 0 || top < 0 || width < 1 || height < 1 || left + width > WorldWidth || top + height > WorldHeight)
                throw new ArgumentException("预览输入或窗口范围无效。");
            bool Solid(int x, int y)
            {
                int cell = y / 8 * cellsWide + x / 8;
                return materials[cell] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)shapes[cell],
                    (x % 8 + .5f) / 8, 1 - (y % 8 + .5f) / 8);
            }
            CaveMaskField field = foreground.Enabled
                ? CaveModifiedTerrain.Bake(Solid, WorldWidth, WorldHeight, seed, outline, foreground, 43 * 8, checkpoint)
                : null;
            var rock = CaveRockBaker.Bake(Solid, WorldWidth, WorldHeight, seed, left, top, width, height,
                checkpoint, stoneSize, outline, field);
            byte[][] layers = null;
            if (visible[0] || visible[1] || visible[2])
            {
                var reference = new BackgroundBakeDescriptor(Guid.Empty.ToString("N"), seed,
                    backgroundMaterials ?? materials, backgroundShapes ?? shapes);
                var layout = new ModifiedBackgroundLayout(generator.Build(reference, outline, checkpoint),
                    backgroundModifiers, seed, checkpoint);
                layers = BackgroundPageBaker.Bake(layout, left, top, width, height, softness, checkpoint);
            }
            var result = new byte[rock.Length];
            var backgroundPixels = new byte[rock.Length];
            for (int y = 0; y < height; y++)
            {
                checkpoint?.Invoke();
                for (int x = 0; x < width; x++)
                {
                    int p = (y * width + x) * 4;
                    // 后壁的静态暖岩底色；本窗口不模拟场景灯、角色及动态矿光。
                    double r = 36, g = 28, b = 23;
                    if (layers != null)
                        for (int layer = 2; layer >= 0; layer--)
                        {
                            if (!visible[layer]) continue;
                            var pixels = layers[layer]; double a = pixels[p + 3] / 255.0;
                            r += (pixels[p] - r) * a; g += (pixels[p + 1] - g) * a; b += (pixels[p + 2] - b) * a;
                        }
                    backgroundPixels[p] = (byte)Math.Round(r);
                    backgroundPixels[p + 1] = (byte)Math.Round(g);
                    backgroundPixels[p + 2] = (byte)Math.Round(b);
                    result[p] = rock[p + 3] != 0 ? rock[p] : backgroundPixels[p];
                    result[p + 1] = rock[p + 3] != 0 ? rock[p + 1] : backgroundPixels[p + 1];
                    result[p + 2] = rock[p + 3] != 0 ? rock[p + 2] : backgroundPixels[p + 2];
                    result[p + 3] = backgroundPixels[p + 3] = 255;
                }
            }
            return (result, backgroundPixels, (byte[])materials.Clone(), (byte[])shapes.Clone(), field);
        }
    }
}
