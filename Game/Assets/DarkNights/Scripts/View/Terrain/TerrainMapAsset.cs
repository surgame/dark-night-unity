using System;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>人工可维护的测试地图根资产；保存初始格子文件、生成参数及 AnyRuleD 配置，不保存运行时破坏。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Terrain/Map recipe")]
    public sealed class TerrainMapAsset : ScriptableObject
    {
        public ARDMapDefinition Definition;
        public CaveTerrainStyle CaveStyle;
        public TerrainGenerationSettings Settings = new TerrainGenerationSettings();
        public TextAsset InitialCells;
        public int CellFormat = 1;
        public int GeneratorVersion = TerrainGenerationSettings.GeneratorVersion;
        public bool HasSpawn;
        public Vector2Int SpawnCell;
        public TerrainBlueprint ReadBlueprint()
        {
            if (Definition == null || InitialCells == null || GeneratorVersion != TerrainGenerationSettings.GeneratorVersion)
                throw new InvalidOperationException("地图根配置、初始格子或生成器版本无效。");
            byte[] bytes = InitialCells.bytes;
            int count = TerrainGenerationSettings.Width * TerrainGenerationSettings.Height;
            if ((CellFormat != 1 && CellFormat != 2) || bytes.Length != count * 2) throw new FormatException("初始地图尺寸错误。");
            var shapes = new byte[count];
            var cells = new byte[count]; var protectedCells = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (bytes[i * 2] > 8 || bytes[i * 2 + 1] > (CellFormat == 1 ? 1 : 25)) throw new FormatException("未知地图格子。");
                cells[i] = bytes[i * 2]; protectedCells[i] = (bytes[i * 2 + 1] & 1) != 0;
                shapes[i] = (byte)(bytes[i * 2 + 1] >> 1);
            }
            var rooms = HasSpawn ? new[] { new TerrainRoom("gallery", SpawnCell.x, SpawnCell.y, 18, 10) } : Array.Empty<TerrainRoom>();
            return new TerrainBlueprint(Settings, cells, protectedCells, new int[TerrainGenerationSettings.Width], rooms, new bool[count], Array.Empty<TerrainDepositBlueprint>(), shapes: shapes);
        }
    }
}
