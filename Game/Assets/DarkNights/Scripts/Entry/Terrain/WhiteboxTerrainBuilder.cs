using System;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>
    /// 将入口作者折线冻结为既有PlayableTerrain及12种坡形；只用于本地白模，不调用正式地图生成器。
    /// 每格以同一离散形状掩码匹配，无法表达的轮廓明确拒绝，不用全格阶梯或近似碰撞代替斜坡。
    /// 生成数据交给SessionTerrain唯一拥有，原生显示网格从同一冻结数据派生。
    /// </summary>
    public static class WhiteboxTerrainBuilder
    {
        private static readonly ulong[] ShapeMasks = CreateMasks();
        public static string Fingerprint(WhiteboxTerrainAuthoring source)
        {
            string json = JsonUtility.ToJson(new Source(source));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
        }

        public static PlayableTerrain Build(WhiteboxTerrainAuthoring source)
        {
            source.Validate();
            int width = TerrainGenerationSettings.Width, height = TerrainGenerationSettings.Height;
            var cells = new byte[width * height];
            var shapes = new byte[cells.Length];
            var protection = new bool[cells.Length];
            for (int row = 0; row < height - 1; row++)
                for (int u = 0; u < width && u * .16f < source.Width; u++)
                {
                    ulong mask = Sample(source, u, row);
                    if (mask == 0) continue;
                    int shape = Array.IndexOf(ShapeMasks, mask);
                    if (shape < 0)
                        throw new InvalidOperationException("入口轮廓不属于现有坡形合同，请对齐16像素格和半坡：" + u + "," + row);
                    int index = row * width + u;
                    cells[index] = 2;
                    shapes[index] = (byte)shape;
                }
            for (int u = 0; u < width; u++)
            {
                cells[(height - 1) * width + u] = 8;
                protection[(height - 1) * width + u] = true;
            }
            return new PlayableTerrain(Guid.NewGuid().ToString("N"), source.ModuleId, cells, protection,
                new bool[cells.Length], Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>(), shapes);
        }

        private static ulong Sample(WhiteboxTerrainAuthoring source, int u, int row)
        {
            ulong mask = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                float px = (u - .5f + (x + .31f) / 8) * PlayableTerrain.CellPixels / EntityView.PixelsPerUnit;
                float py = (PlayableTerrain.OriginY - (row + .5f) * PlayableTerrain.CellPixels +
                    (y + .67f) / 8 * PlayableTerrain.CellPixels) / EntityView.PixelsPerUnit;
                bool ground = py < WhiteboxTerrainAuthoring.Height(source.Floor, px);
                bool roof = px >= source.Ceiling[0].x && py > WhiteboxTerrainAuthoring.Height(source.Ceiling, px);
                if (ground || roof) mask |= 1UL << (y * 8 + x);
            }
            return mask;
        }

        private static ulong[] CreateMasks()
        {
            var result = new ulong[13];
            for (int shape = 0; shape < result.Length; shape++)
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                    if (TerrainShapeGeometry.Contains((TerrainCellShape)shape, (x + .31f) / 8, (y + .67f) / 8))
                        result[shape] |= 1UL << (y * 8 + x);
            return result;
        }

        /// <summary>仅用于轮廓来源摘要的临时序列化副本，不包含网格、会话或角色状态。</summary>
        [Serializable]
        private sealed class Source
        {
            public string Id;
            public float Width, Bottom, Top;
            public Vector2[] Floor, Ceiling;
            public Source(WhiteboxTerrainAuthoring source)
            {
                Id = source.ModuleId; Width = source.Width;
                Bottom = source.DisplayBottom; Top = source.DisplayTop;
                Floor = source.Floor; Ceiling = source.Ceiling;
            }
        }
    }
}
