using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Terrain;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 单一工作台的地图来源与冻结版目录；显式选择规则／作者图，查看版本不等于修改正式新局。
    /// 目录只引用已存在资源，最多展示32版；旧作者图及旧冻结版保留，不制造自动入库或默认切换。
    /// </summary>
    internal static class TerrainWorkbenchSourcePage
    {
        internal static void Build(TerrainWorkbenchFields form)
        {
            var owner = form.Owner; var config = owner.Draft.Config;
            form.Title("地图来源", "新规则、已有作者图和冻结版在这里统一管理；不需要手工清引用。");
            var rules = form.Button("从新规则创建／继续草稿", owner.Commands.NewRules, primary: true);
            rules.EnableInClassList("map-selected", owner.Generation.RuleMode);
            form.Note("九种入口、五类道路、矿室配比与区域内容走同一生成链路。");
            form.Button("使用已有作者图", () => owner.Commands.UseAuthor(
                config.MineMap != null ? config.MineMap : owner.State.PreviousAuthor));
            if (!owner.Generation.RuleMode)
            {
                var author = new ObjectField("作者矿洞") { objectType = typeof(MineMapAsset), allowSceneObjects = false };
                author.SetValueWithoutNotify(config.MineMap);
                author.RegisterValueChangedCallback(change => owner.Execute(() =>
                    owner.Commands.UseAuthor((MineMapAsset)change.newValue)));
                author.AddToClassList("map-author-edit"); form.Root.Add(author);
                if (config.MineMap == null) form.Help("尚未指定作者图；空引用使用旧随机来源。新制作建议选择新规则。");
            }
            form.Heading("正式新局当前引用");
            var installed = owner.Generation.InstalledConfig.ReviewedMap;
            form.Note(installed == null ? "未装配冻结版本。" : AssetDatabase.GetAssetPath(installed));
            form.Button("只读查看正式引用", owner.Commands.ViewInstalled);
            form.Heading("已有冻结版本");
            var selected = new ObjectField("版本") { objectType = typeof(ReviewedMineMap), allowSceneObjects = false };
            selected.SetValueWithoutNotify(owner.State.FrozenVersion);
            selected.RegisterValueChangedCallback(change =>
            {
                owner.State.FrozenVersion = (ReviewedMineMap)change.newValue;
                owner.Refresh();
            });
            form.Root.Add(selected);
            form.Button("查看所选版本（不装配）", () => owner.Commands.ViewVersion(owner.State.FrozenVersion));
            string[] guids = AssetDatabase.FindAssets("t:ReviewedMineMap",
                new[] { "Assets/DarkNights/Res/Terrain/StrataCave/JointMaps" });
            var folder = form.Fold("版本目录（最新32版）");
            foreach (string path in guids.Select(AssetDatabase.GUIDToAssetPath).OrderByDescending(p => p).Take(32))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ReviewedMineMap>(path);
                if (asset == null) continue;
                string label = Path.GetFileName(Path.GetDirectoryName(path));
                var button = form.Button(label + (asset == installed ? " · 正式新局" : ""), () =>
                    owner.Commands.ViewVersion(asset), folder);
                button.tooltip = path;
            }
            form.Help("只查看不会换图。作者草稿保存、生成候选、创建冻结版本与用于新局分别执行。");
            form.Button("检查与使用 →", () => owner.SelectPage("release"));
        }
    }
}
