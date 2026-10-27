using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>矿物的自然区域编辑页；默认作用于所选区域，全图配方与侧巢重做放入高级操作。</summary>
    internal static class MapWorkbenchMineralPage
    {
        internal static void Build(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            form.Title("矿物", "点击地图选区，或在这里选择；局部重排保持洞形、侧巢和其他区域。");
            form.Basic("矿物算法");
            MapWorkbenchSourcePage.BuildMinerals(form);
            if (!owner.Generation.RegionalMinerals)
            {
                if (owner.Draft.Config.CaveMap.Minerals.Algorithm == Core.Config.Terrain.TerrainMineralSettings.BaseDeposits)
                    form.Note("基础矿床来自天然洞穴的房间供给，不生成侧巢。");
                else
                {
                    form.Field("Config.CaveMap.Minerals.VeinCount", "矿团数量上限");
                    form.Field("Config.CaveMap.Minerals.Nests", "生成侧巢");
                    form.Advanced("近壁矿团");
                    form.Field("Config.CaveMap.Minerals.Seed", "内容种子");
                    form.Field("Config.CaveMap.Minerals.Revision", "内容修订");
                    form.Note("形状与储量沿既有近壁配方；变更内容种子／修订只重排内容。");
                }
                Stale(form);
                form.Advanced("矿物供给与诊断");
                var map = owner.Preview.Displayed;
                form.Note(map == null ? "尚无当前产物。" : "实际矿格 " + map.Deposits.Sum(d => d.Cells.Count) +
                    " · 储量 " + map.Deposits.Sum(d => d.Capacity) + " · 侧巢 " + map.Nests.Count);
                foreach (string line in owner.Generation.ContentDiagnostics) form.Note(line);
                return;
            }
            var plan = owner.Generation.RulePlan;
            if (plan == null)
            {
                form.Help(owner.Generation.CandidateFromSaved ? "此历史地图未保存地下计划。保持原结果；需要局部编辑时，请明确用当前种子重新生成。" :
                    "当前尚无可编辑的同次区域计划，请等待生成或修正算法组合。");
                return;
            }
            form.Basic("所选区域");
            owner.State.SelectedRegion = Math.Min(owner.State.SelectedRegion, plan.Rooms.Count - 1);
            var selector = new DropdownField("当前区域", Enumerable.Range(0, plan.Rooms.Count).Select(i => "区域 R" + i).ToList(),
                owner.State.SelectedRegion);
            selector.RegisterValueChangedCallback(_ =>
            { owner.State.SelectedRegion = selector.index; owner.RebuildPage(); });
            form.Root.Add(selector);
            form.Button("定位所选区域", () =>
            {
                var room = plan.Rooms[owner.State.SelectedRegion]; owner.Preview.FocusCell(room.X, room.Y);
            });
            var current = TerrainRegionCommands.Current(owner);
            string prefix = "rule-r" + owner.State.SelectedRegion + "-";
            var ore = owner.Preview.Displayed.Deposits.Where(d => d.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            int count = ore.Select(d => d.Id.Substring(0, d.Id.LastIndexOf('-'))).Distinct().Count();
            form.Note("当前结果：" + count + " 矿团 · " + ore.Sum(d => d.Capacity) + " 储量").name = "regionSummary";
            var budget = new SliderInt("本区矿团", 0, 16) { value = current?.VeinCount >= 0 ? current.VeinCount : count };
            budget.RegisterValueChangedCallback(e => owner.Execute(() => TerrainRegionCommands.Edit(owner, x => x.VeinCount = e.newValue)));
            form.Root.Add(budget);
            var capacity = new IntegerField("每团储量") { value = current?.Capacity >= 0 ? current.Capacity : owner.Draft.Config.CaveMap.Minerals.Capacity, isDelayed = true };
            capacity.RegisterValueChangedCallback(e => owner.Execute(() => TerrainRegionCommands.Edit(owner, x => x.Capacity = e.newValue)));
            form.Root.Add(capacity);
            form.Button("重排本区矿物", () => TerrainRegionCommands.Reroll(owner), primary: true).name = "rerollRegion";
            if (count == 0) form.Note("本区当前无矿；先增加本区矿团，再重排。");
            form.Note("围岩不足时只减少本区产出，不向邻区借位置或预算；按住对比查看上一次结果。");
            form.Advanced("所选区域");
            var seed = new TextField("本区种子") { value = current?.Seed ?? owner.Draft.Config.CaveMap.Minerals.Seed, isDelayed = true };
            seed.RegisterValueChangedCallback(e => owner.Execute(() => TerrainRegionCommands.Edit(owner, x => x.Seed = e.newValue)));
            form.Root.Add(seed);
            if (current != null && current.SpaceIdentity != owner.Draft.Config.CaveMap.Structure.SpaceKey(owner.Draft.Config.CaveMap.Seed))
                form.Button("将保留覆盖绑定到当前所选区域", () => TerrainRegionCommands.Rebind(owner));
            var advanced = form.Advanced("全图矿物配方");
            const string p = "Config.CaveMap.Minerals.";
            foreach (var pair in new Dictionary<string, string>
            {
                { "Seed", "全图内容种子" }, { "VeinCount", "全图基础矿团" }, { "Capacity", "全图基础储量" },
                { "ExposedShare", "露矿比例上限" }, { "EmptyShare", "无矿区域比例" }, { "RichShare", "富矿区域比例" },
                { "Revision", "全图内容修订" }, { "Nests", "富矿伴巢" }
            }) form.Field(p + pair.Key, pair.Value, advanced);
            form.Button("全图重排矿物与侧巢", owner.Commands.ContentOnly, advanced);
            form.Button("清除全部区域覆盖", () => owner.Commands.Edit("清除区域矿物覆盖", () =>
                owner.Draft.Config.CaveMap.RegionOverrides = Array.Empty<Core.Config.Terrain.CaveRegionOverride>()), advanced);
            form.Field("Config.CaveMap.RegionOverrides", "区域覆盖配方", advanced);
            Stale(form);
            var report = form.Advanced("矿物诊断");
            foreach (string line in owner.Generation.ContentDiagnostics) form.Note(line, report);
        }

        private static void Stale(MapWorkbenchFields form)
        {
            var map = form.Owner.Draft.Config.CaveMap;
            int count = (map.RegionOverrides ?? Array.Empty<Core.Config.Terrain.CaveRegionOverride>()).Count(x =>
                map.Layout != Core.Config.Terrain.TerrainGenerationSettings.ConnectedRooms ||
                !form.Owner.Generation.RegionalMinerals || x.SpaceIdentity != map.Structure.SpaceKey(map.Seed));
            if (count == 0) return;
            form.Help("保留了 " + count + " 项未应用的区域覆盖。修改所选区会明确重新绑定；高级配置可查看或清除，支持Undo。",
                HelpBoxMessageType.Warning);
        }
    }
}
