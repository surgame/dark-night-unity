using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 历史输入的单一纯数据导入边界；把隐式来源与混合规则转换为显式布局和矿物参数。
    /// 转换后清除历史规则载体，生成阶段只消费标准配方，不按历史版本或开关分流。
    /// </summary>
    public static class TerrainRecipeImport
    {
        internal static void Normalize(TerrainGenerationSettings target)
        {
            var input = target.Rules;
            if (string.IsNullOrEmpty(target.Layout))
            {
                bool network = input?.Enabled == true;
                target.Layout = network ? TerrainGenerationSettings.ConnectedRooms : TerrainGenerationSettings.NaturalLayout;
                if (network)
                {
                    target.Structure = new CaveLayoutSettings
                    {
                        Version = input.Version, Templates = input.Templates, Entrance = input.Entrance,
                        Road = input.Road, RoomMix = input.RoomMix, SpaceRevision = input.SpaceRevision,
                        RoomCount = input.RoomCount, RoomScale = input.RoomScale, RoadRadius = input.RoadRadius,
                        BranchShare = input.BranchShare, BranchDepth = input.BranchDepth, RockGap = input.RockGap
                    };
                    target.Minerals = new TerrainMineralSettings
                    {
                        Algorithm = TerrainMineralSettings.RegionalVeins, Seed = input.ContentSeed,
                        Revision = input.ContentRevision, VeinCount = input.VeinCount, Capacity = input.Capacity,
                        EmptyShare = input.EmptyShare, ExposedShare = input.ExposedShare,
                        RichShare = input.RichShare, Nests = input.Nests
                    };
                }
            }
            target.Structure = (target.Structure ?? new CaveLayoutSettings()).CopyValidated();
            target.Minerals = (target.Minerals ?? new TerrainMineralSettings()).CopyValidated();
            target.Rules = null;
        }
    }
}
