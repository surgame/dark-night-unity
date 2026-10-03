using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Editor
{
    /// <summary>
    /// 四类项目任务的只读操作目录；原生编辑器拥有列表、编辑和保存，项目只声明工具及常用作者来源。
    /// 场景目录保持独立，不把单个打开场景操作扩成任务页面。
    /// </summary>
    internal static class DarkNightsWorkbenchCatalog
    {
        internal const string Root = "Assets/DarkNights/Res/";
        internal const string Session = Root + "Objects/WorldSession/WorldSession.asset";
        internal const string ShipTrade = Root + "Objects/ShipTrade/";
        internal const string Objects = "对象与装备", Journey = "星球与航程", Map = "地图与表现", UI = "界面资源";
        internal static IReadOnlyList<DarkNightsWorkbenchEntry> Entries { get; } = System.Array.AsReadOnly(new[]
        {
            new DarkNightsWorkbenchEntry("objects", DarkNightsWorkbenchEntryKind.Tool, Objects, "Definition Workshop",
                "原生列表、分类树、搜索、能力装配与共享配置编辑。",
                "编辑和保存由原生 Workshop 管理。", "打开 Workshop", DarkNightsNativeWorkspace.Workshop),
            new DarkNightsWorkbenchEntry("viewer", DarkNightsWorkbenchEntryKind.Tool, Objects, "YYGC Viewer",
                "数据库／Addressables 总览与注册诊断。",
                "按需打开 Viewer，不启动对象管理套件的其他窗口。", "打开 Viewer", DarkNightsNativeWorkspace.Viewer),
            new DarkNightsWorkbenchEntry("mining", DarkNightsWorkbenchEntryKind.Tool, Objects, "采集装配与目标匹配",
                "只读检查工具、矿床、白名单及目标匹配；配置在 Workshop 编辑。",
                "校验不会补配置或保存资产。", "打开采集校验", DarkNightsNativeWorkspace.Mining,
                ShipTrade + "item-pickaxe.asset", Root + "Objects/MineralDeposit/MineralDeposit.asset"),
            new DarkNightsWorkbenchEntry("journey", DarkNightsWorkbenchEntryKind.Tool, Journey, "星球与航程编辑器",
                "目的地、地图生成、航程过场与蓝图预览。",
                "保留原窗口的草稿、冲突检查、应用与取消。", "编辑星球与航程", DarkNightsNativeWorkspace.Journey, Session),
            new DarkNightsWorkbenchEntry("rules", DarkNightsWorkbenchEntryKind.Editor, Journey, "数值规则",
                "经济、交易和波次规则的唯一 JSON 来源。", "使用外部编辑器保存。", "", null, Root + "Config/balance.json"),
            new DarkNightsWorkbenchEntry("terrain", DarkNightsWorkbenchEntryKind.Tool, Map, "地形业务与耐久",
                "Profile、规则瓦片耐久和离线单格试采。",
                "编译并应用由原窗口执行，保留草稿和冲突检查。", "编辑地形业务", DarkNightsNativeWorkspace.Terrain, Session),
            new DarkNightsWorkbenchEntry("visual", DarkNightsWorkbenchEntryKind.Tool, Map, "Cave Wall Tuner",
                "岩壁、三层背景、点缀与地表画布。",
                "离屏预览与样式／地图草稿由原窗口释放、应用或取消。", "调校岩壁与背景", DarkNightsNativeWorkspace.Visual,
                Root + "Terrain/StrataCave/Style.asset", Root + "Terrain/StrataCave/Background.asset"),
            new DarkNightsWorkbenchEntry("ui", DarkNightsWorkbenchEntryKind.Editor, UI, "飞船装备界面来源",
                "UI Builder、样式、主题与原生 PanelSettings。",
                "源文件用原生编辑器打开；PanelSettings 单独保存。", "", null,
                Root + "UI/ShipEquipment/ShipEquipmentPanelSettings.asset", Root + "UI/ShipEquipment/ShipEquipment.uxml",
                Root + "UI/ShipEquipment/ShipEquipment.uss", Root + "UI/ShipEquipment/ShipEquipmentTheme.tss")
        }.Concat(GameSceneWorkbenchCatalog.Entries).ToArray());
    }
}
