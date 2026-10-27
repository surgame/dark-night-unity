using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>所选自然区的作者覆盖操作；只写同一草稿，原生Undo记录配方，区域选择不改变地图。</summary>
    internal static class TerrainRegionCommands
    {
        internal static CaveRegionOverride Current(MapWorkbenchWindow owner)
        {
            int region = owner.State.SelectedRegion;
            var settings = owner.Draft.Config.CaveMap;
            return settings.RegionOverrides?.FirstOrDefault(x => x.Region == region);
        }

        internal static void Edit(MapWorkbenchWindow owner, Action<CaveRegionOverride> change)
        {
            if (!owner.Generation.RegionalMinerals || owner.Generation.LastCandidate == null)
                throw new InvalidOperationException("请等待当前规则地图同步，再编辑局部矿物。");
            owner.Commands.Edit("修改所选区域矿物", () =>
            {
                var settings = owner.Draft.Config.CaveMap;
                var entries = (settings.RegionOverrides ?? Array.Empty<CaveRegionOverride>()).ToList();
                var current = entries.FirstOrDefault(x => x.Region == owner.State.SelectedRegion);
                if (current == null)
                {
                    current = new CaveRegionOverride
                    {
                        Region = owner.State.SelectedRegion,
                        SpaceIdentity = settings.Structure.SpaceKey(owner.Generation.LastCandidate.Seed),
                        Seed = settings.Minerals.Seed
                    };
                    entries.Add(current);
                }
                change(current);
                current.SpaceIdentity = settings.Structure.SpaceKey(owner.Generation.LastCandidate.Seed);
                settings.RegionOverrides = entries.OrderBy(x => x.Region).ToArray();
            });
        }

        internal static void Reroll(MapWorkbenchWindow owner) =>
            Edit(owner, region => region.Revision = checked(region.Revision + 1));

        internal static void Rebind(MapWorkbenchWindow owner) => Edit(owner, _ => { });
    }
}
