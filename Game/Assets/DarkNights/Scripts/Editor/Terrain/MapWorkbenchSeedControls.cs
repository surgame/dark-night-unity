using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图规则的种子策略编辑；模式、种子池和本次实际种子保存在同一草稿。
    /// 只有显式抽取会改变实际种子，参数自动预览、保存及重开不触发随机选择。
    /// </summary>
    internal static class MapWorkbenchSeedControls
    {
        internal static void Build(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            var map = owner.Draft.Config.CaveMap;
            var policy = map.SeedSelection ?? new TerrainSeedSettings();
            form.Basic("地图种子");
            var mode = new DropdownField("选择策略", new System.Collections.Generic.List<string>
                { "固定种子", "随机种子", "种子池" }, Math.Max(0, Array.IndexOf(TerrainSeedSettings.Modes, policy.Mode)));
            mode.name = "seedSelectionMode";
            mode.RegisterValueChangedCallback(_ => owner.Execute(() =>
            {
                owner.Commands.Edit("调整种子选择策略", () =>
                {
                    map.SeedSelection = policy;
                    policy.Mode = TerrainSeedSettings.Modes[mode.index];
                    if (policy.Mode == TerrainSeedSettings.Pool && (policy.Seeds == null || policy.Seeds.Length == 0))
                        policy.Seeds = new[] { map.Seed };
                });
                owner.RebuildPage();
            }));
            form.Root.Add(mode);
            var actual = form.Field("Config.CaveMap.Seed",
                policy.Mode == TerrainSeedSettings.Fixed ? "固定种子" : "本次实际种子");
            actual.SetEnabled(policy.Mode == TerrainSeedSettings.Fixed);
            actual.EnableInClassList("map-readonly", policy.Mode != TerrainSeedSettings.Fixed);
            form.Button(policy.Mode == TerrainSeedSettings.Fixed ? "按固定种子生成" : "按策略抽取下一张地图",
                owner.Commands.Reseed);
            form.Note("调参保留本次种子；保存记录策略、实际种子与完整地图。游戏加载结果，不重新抽样。");
            if (policy.Mode == TerrainSeedSettings.Pool)
            {
                form.Advanced("种子池");
                var pool = new TextField("种子池 · 每行一项")
                { multiline = true, isDelayed = true, value = string.Join("\n", policy.Seeds ?? Array.Empty<string>()) };
                pool.name = "seedPool";
                pool.AddToClassList("map-author-edit");
                pool.RegisterValueChangedCallback(change => owner.Execute(() =>
                {
                    string[] values = change.newValue.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => value.Trim()).ToArray();
                    var next = new TerrainSeedSettings { Mode = TerrainSeedSettings.Pool, Seeds = values }.CopyValidated();
                    owner.Commands.Edit("编辑地图种子池", () => map.SeedSelection = next);
                    owner.RebuildPage();
                }));
                form.Root.Add(pool);
            }
        }
    }
}
