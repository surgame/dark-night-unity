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
        public static byte[] Bake(byte[] materials, byte[] shapes, string seed, int left, int top, int stoneSize,
            CaveOutlineSettings outline, CaveModifierStack foreground, ICaveBackgroundGenerator generator,
            CaveModifierStack[] backgroundModifiers, bool[] visible, int softness, Action checkpoint = null)
            => BakeRect(materials, shapes, seed, left, top, Width, Height, stoneSize, outline, foreground,
                generator, backgroundModifiers, visible, softness, checkpoint);

        public static byte[] BakeFull(byte[] materials, byte[] shapes, string seed, int stoneSize,
            CaveOutlineSettings outline, CaveModifierStack foreground, ICaveBackgroundGenerator generator,
            CaveModifierStack[] backgroundModifiers, bool[] visible, int softness, Action checkpoint = null)
            => BakeRect(materials, shapes, seed, 0, 0, WorldWidth, WorldHeight, stoneSize, outline, foreground,
                generator, backgroundModifiers, visible, softness, checkpoint);

        private static byte[] BakeRect(byte[] materials, byte[] shapes, string seed, int left, int top,
            int width, int height, int stoneSize, CaveOutlineSettings outline, CaveModifierStack foreground,
            ICaveBackgroundGenerator generator, CaveModifierStack[] backgroundModifiers, bool[] visible,
            int softness, Action checkpoint)
        {
            const int cellsWide = 320;
            if (materials == null || shapes == null || materials.Length != 320 * 192 || shapes.Length != materials.Length ||
                seed == null || foreground == null || generator == null || backgroundModifiers == null ||
                backgroundModifiers.Length != 3 || visible == null || visible.Length != 3 ||
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
                var reference = new BackgroundBakeDescriptor(Guid.Empty.ToString("N"), seed, materials, shapes);
                var layout = new ModifiedBackgroundLayout(generator.Build(reference, outline, checkpoint),
                    backgroundModifiers, seed, checkpoint);
                layers = BackgroundPageBaker.Bake(layout, left, top, width, height, softness, checkpoint);
            }
            var result = new byte[rock.Length];
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
                    result[p] = rock[p + 3] != 0 ? rock[p] : (byte)Math.Round(r);
                    result[p + 1] = rock[p + 3] != 0 ? rock[p + 1] : (byte)Math.Round(g);
                    result[p + 2] = rock[p + 3] != 0 ? rock[p + 2] : (byte)Math.Round(b);
                    result[p + 3] = 255;
                }
            }
            return result;
        }
    }
}
