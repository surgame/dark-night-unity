using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// 制作配置的导入边界；调用者必须传入自有副本，标准配置的布局和矿物阶段在此一次补齐。
    /// 历史窗口附带的矿团／侧巢只作为导入参数，转换后生成不再读取窗口、文档附加字段或本机默认值。
    /// </summary>
    public static class TerrainAuthoringImport
    {
        public static void Normalize(ExpeditionFlowConfig input, int importedVeinCount = 6, bool importedNests = true)
        {
            if (input == null || input.CaveMap == null) throw new ArgumentException("缺少地图配置。");
            bool hasMinerals = !string.IsNullOrEmpty(input.CaveMap.Layout) && input.CaveMap.Minerals != null;
            var settings = input.FreezeCaveMap();
            if (!hasMinerals && settings.Minerals.Algorithm != TerrainMineralSettings.RegionalVeins)
                settings.Minerals = new TerrainMineralSettings
                {
                    Algorithm = TerrainMineralSettings.WallVeins,
                    VeinCount = importedVeinCount, Nests = importedNests
                }.CopyValidated();
            input.CaveMap = settings;
        }
    }
}
