using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using UnityEngine.UIElements;
using System.Linq;
using DarkNights.Runtime.Terrain;
using UnityEditor.UIElements;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>顶部文件菜单展开的配置和预设入口；集中打开、复制与资源导入，其他属性页只编辑当前配置。</summary>
    internal static class MapWorkbenchSourcePage
    {
        internal static void Build(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            form.Title("算法与种子", "同一份配方生成完整地图；调参保留本次种子，只有明确抽取才换图。");
            form.Basic("生成算法");
            BuildLayout(form);
            BuildMinerals(form);
            MapWorkbenchSeedControls.Build(form);
            form.Advanced("配方来源与导入");
            var selected = new ObjectField("地图或会话配置") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
            form.Root.Add(selected);
            form.Button("导入为新配置", () => MapWorkbenchFiles.Import(owner, selected.value), primary: true);
            form.Note("支持地图配置和会话配置；导入后另选保存位置，来源文件保持。");
            form.Button("从当前新局配置创建", () => MapWorkbenchFiles.Import(owner, owner.Draft.Source));
            form.Note("工程内的地图配置及只读预制地图见左侧文件列表，可按编号／名称／文件名搜索。");
            form.Button("编辑地表轮廓", () => owner.SelectPage("surface"));
        }

        internal static void BuildMinerals(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            string[] algorithms = owner.Generation.HasRoomPlan ?
                new[] { TerrainMineralSettings.RegionalVeins, TerrainMineralSettings.WallVeins } :
                new[] { TerrainMineralSettings.BaseDeposits, TerrainMineralSettings.WallVeins };
            var minerals = new DropdownField("矿物算法", algorithms.Select(x => x == TerrainMineralSettings.BaseDeposits ?
                "基础矿床" : x == TerrainMineralSettings.WallVeins ? "近壁矿团" : "区域矿物").ToList(),
                Math.Max(0, Array.IndexOf(algorithms, owner.Draft.Config.CaveMap.Minerals.Algorithm)));
            minerals.name = "mineralAlgorithm";
            minerals.RegisterValueChangedCallback(_ => owner.Execute(() =>
            {
                owner.Commands.Edit("调整矿物算法", () =>
                {
                    owner.Draft.Config.CaveMap.Minerals.Algorithm = algorithms[minerals.index];
                });
                owner.RebuildPage();
            }));
            if (Array.IndexOf(algorithms, owner.Draft.Config.CaveMap.Minerals.Algorithm) < 0)
            {
                minerals.SetValueWithoutNotify("当前组合不支持，请选择");
                form.Help(owner.Generation.HasRoomPlan ? "连通洞室没有基础矿床来源；请选择区域矿物或近壁矿团。" :
                    "天然洞穴没有区域计划；请选择基础矿床或近壁矿团。", HelpBoxMessageType.Warning);
            }
            form.Root.Add(minerals);
        }

        internal static void BuildLayout(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            var layout = new DropdownField("布局算法", new List<string> { "连通洞室", "天然洞穴" },
                Math.Max(0, Array.IndexOf(TerrainGenerationSettings.Layouts, owner.Draft.Config.CaveMap.Layout)));
            layout.name = "layoutAlgorithm";
            layout.RegisterValueChangedCallback(_ => owner.Execute(() =>
            {
                owner.Commands.Edit("调整布局算法", () =>
                {
                    var settings = owner.Draft.Config.CaveMap;
                    settings.Layout = TerrainGenerationSettings.Layouts[layout.index];
                });
                owner.RebuildPage();
            }));
            form.Root.Add(layout);
            form.Note("切换布局不自动替换矿物算法；不兼容时请明确选择。历史区域覆盖保留。");
        }
    }
}
