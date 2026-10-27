using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Objects.Definition;
using UnityEditor;

namespace DarkNights.Editor
{
    /// <summary>
    /// 按游戏、设置和开发组织的业务导航目录；常用操作与模块扩展共用一份可搜索元数据。
    /// 只引用原有作者来源和编辑器，历史资源不因导航收敛被移除或恢复为正式玩法。
    /// </summary>
    internal static class DarkNightsWorkbenchCatalog
    {
        internal const string Root = "Assets/DarkNights/Res/";
        internal const string Session = Root + "Objects/WorldSession/WorldSession.asset";
        internal const string ShipTrade = Root + "Objects/ShipTrade/";
        internal const string Levels = "关卡", Ship = "飞船", Objects = "角色与装备", Map = "地图";
        internal const string Input = "键位", Graphics = "图形", Diagnostics = "开发工具";
        internal static IReadOnlyList<(string Id, string Title, string Hint, string Description, string Section)> Tasks { get; } =
            Array.AsReadOnly(new[]
            {
                ("level", Levels, "正式装配 · 场景 · 试玩", "查看正式场景、地图引用和当前试玩来源。", "游戏"),
                ("ship", Ship, "船舱服务 · 航程 · 界面", "编辑船体和船舱服务，查看正式航程与共享游戏界面来源。", "游戏"),
                ("map", Map, "地图设计工作台", "所有地图制作统一进入现有地图设计工作台。", "游戏"),
                ("objects", Objects, "角色 · 手电 · 武器", "通过原生工坊编辑角色、手持装备与能力。", "游戏"),
                ("input", Input, "默认动作 · 本机改键", "定位默认输入资产，区分项目键位与玩家本机覆盖。", "设置"),
                ("graphics", Graphics, "画质 · 管线 · 光照", "使用原生项目图形设置，复杂光照参数放入扩展。", "设置"),
                ("diagnostics", Diagnostics, "诊断 · 存档 · 构建", "只读查看运行状态；低频工程操作放入扩展。", "开发")
            });

        internal static IReadOnlyList<DarkNightsWorkbenchEntry> Entries { get; } = Array.AsReadOnly(new[]
        {
            Tool("visual", Map, "地图设计工作台", "地形、矿物、环境、巢穴、塌方及保存与用于新局。", "打开地图工作台",
                DarkNightsNativeWorkspace.Visual),
            Tool("terrain", Map, "地形业务与耐久", "底层Profile、规则瓦片耐久及离线单格试采。", "打开地形业务",
                DarkNightsNativeWorkspace.Terrain, true),
            Tool("objects", Objects, "完整对象与能力工坊", "完整Definition、能力、Prefab与共享配置；保留暂停内容的来源。", "打开Workshop",
                DarkNightsNativeWorkspace.Workshop, true),
            Tool("viewer", Objects, "定义与资源注册诊断", "原生YYGC Viewer，数据库与Addressables注册检查。", "打开Viewer",
                DarkNightsNativeWorkspace.Viewer, true),
            Tool("mining", Objects, "矿镐采集装配与目标匹配", "检查工具、矿床及白名单；配置仍由原生工坊保存。", "打开采集校验",
                DarkNightsNativeWorkspace.Mining, true),
            Tool("session", Ship, "正式航程与会话参数", "正式Enabled、Navigation和准备／到达超时在会话配置编辑。", "编辑正式会话",
                () => DarkNightsNativeWorkspace.Workshop(AssetDatabase.LoadAssetAtPath<ObjectDefinition>(Session)), true),
            Tool("journey", Ship, "星球、泊位与过场草稿", "打开地图工作台原航程扩展；保存与正式应用范围需分别核对。", "打开航程扩展",
                DarkNightsNativeWorkspace.Journey, true),
            Source("rules", Levels, "经济与关卡规则", "数值规则由balance.json拥有；关卡元数据读取pinewatch.json，暂停波次不因此恢复。",
                Root + "Config/balance.json", Root + "Config/pinewatch.json"),
            Source("ui", Ship, "游戏HUD、背包与飞船服务界面", "共享布局包含四格装备栏、B背包、补给商店和操控室星图；布局与渲染设置分开编辑。",
                Root + "UI/ShipEquipment/ShipEquipment.uxml", Root + "UI/ShipEquipment/ShipEquipment.uss",
                Root + "UI/ShipEquipment/ShipEquipmentTheme.tss", Root + "UI/ShipEquipment/ShipEquipmentPanelSettings.asset"),
            Source("main-menu-ui", Levels, "主菜单与房间界面", "开始、继续、房间列表和连接反馈的原生布局与样式。",
                Root + "UI/MainMenu/MainMenu.uxml", Root + "UI/MainMenu/MainMenu.uss",
                Root + "UI/MainMenu/MainMenuTheme.tss", Root + "UI/MainMenu/MainMenuPanelSettings.asset"),
            Source("session-ui", Levels, "暂停、帮助与结果界面", "仍在使用的UGUI作者来源；旧主菜单Prefab不在当前实例化列表。",
                Root + "UI/PauseMenu/PauseMenu.prefab", Root + "UI/Help/Help.prefab", Root + "UI/Result/Result.prefab"),
            Tool("default-input", Input, "默认键位与动作", "使用Input Actions原生编辑器；玩家改键继续由游戏设置保存。", "编辑默认键位",
                () => AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(Root + "Input/Gameplay.inputactions"))),
            Tool("quality", Graphics, "画质设置", "原生项目质量档与各平台质量选择。", "打开画质设置",
                () => SettingsService.OpenProjectSettings("Project/Quality")),
            Tool("graphics", Graphics, "渲染管线设置", "原生项目Graphics与渲染管线来源。", "打开图形设置",
                () => SettingsService.OpenProjectSettings("Project/Graphics")),
            new DarkNightsWorkbenchEntry("lighting-debug", DarkNightsWorkbenchEntryKind.Diagnostic, Graphics, "光照调参台",
                "单灯、场景、返光与灯口；核对实际目标和保存范围。", "原手电光照调试", "打开光照调参台",
                Lighting.FlashlightDebugWindow.Open, true),
            new DarkNightsWorkbenchEntry("hero-ground-profiler", DarkNightsWorkbenchEntryKind.Diagnostic, Diagnostics, "主角地面接触／跳跃Profiler",
                "记录、停止、导出和回看由原浮窗负责，打开不自动录制。", "原主角地面接触／跳跃调试", "打开Profiler",
                HeroGroundProfilerWindow.Open, true)
        }.Concat(GameSceneWorkbenchCatalog.Entries).ToArray());

        internal static string Module(DarkNightsWorkbenchEntry entry)
        {
            var task = Tasks.FirstOrDefault(value => value.Title == entry.Group);
            return task.Id ?? (entry.Kind == DarkNightsWorkbenchEntryKind.Scene ||
                entry.Kind == DarkNightsWorkbenchEntryKind.Reference ? "level" : "diagnostics");
        }

        private static DarkNightsWorkbenchEntry Tool(string id, string group, string title, string description,
            string label, Action open, bool extended = false) =>
            new DarkNightsWorkbenchEntry(id, DarkNightsWorkbenchEntryKind.Tool, group, title, description,
                "原窗口拥有草稿和保存；此入口不自动应用或执行验证。" + Legacy(group), label, open, extended);

        private static DarkNightsWorkbenchEntry Source(string id, string group, string title, string description, params string[] assets) =>
            new DarkNightsWorkbenchEntry(id, DarkNightsWorkbenchEntryKind.Editor, group, title, description,
                "扩展中定位原生作者来源，显式编辑与保存。" + Legacy(group), "查看来源", null, true, assets);

        private static string Legacy(string group) =>
            group == Ship ? " 原分类：星球与航程、界面资源、飞船装备界面来源。" :
            group == Levels ? " 原分类：场景与测试、界面资源。" :
            group == Objects ? " 原分类：对象与装备。" :
            group == Map ? " 原分类：地图与表现。" : "";
    }
}
