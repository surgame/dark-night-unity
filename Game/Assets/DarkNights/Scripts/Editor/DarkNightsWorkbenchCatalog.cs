using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Editor
{
    /// <summary>
    /// 项目任务与工具的只读操作目录；分类元数据统一驱动左栏、标题和内容分组，新增分类按纵向排列。
    /// 原生编辑器拥有编辑和保存；场景在独立目录直接打开或定位，不为单个场景创建中间页面。
    /// </summary>
    internal static class DarkNightsWorkbenchCatalog
    {
        internal const string Root = "Assets/DarkNights/Res/";
        internal const string Session = Root + "Objects/WorldSession/WorldSession.asset";
        internal const string ShipTrade = Root + "Objects/ShipTrade/";
        internal const string Objects = "对象与装备", Journey = "星球与航程", Map = "地图与表现", UI = "界面资源";
        internal static IReadOnlyList<(string Id, string Title, string Hint, string Description, string Section)> Tasks { get; } =
            System.Array.AsReadOnly(new[]
            {
                ("objects", Objects, "定义 · 装备 · 采集", "编辑对象能力与装备配置，检查采集装配和目标匹配。", "内容制作"),
                ("journey", Journey, "目的地 · 过场 · 规则", "配置星球目的地、航程过场与生成蓝图，定位数值规则。", "内容制作"),
                ("map", Map, "地形 · 岩壁 · 背景", "调整地形业务、岩壁样式与背景，在专用工具中预览。", "内容制作"),
                ("ui", UI, "布局 · 样式 · 主题", "打开界面作者来源，使用原生编辑器维护布局与样式。", "内容制作"),
                ("scenes", "场景与测试", "正式入口 · 预览 · 样例", "按用途查找场景，直接打开或定位；打开前提示保存，不自动 Play。", "运行与检查"),
                ("maintenance", "工程维护", "安装 · 构建 · 验证", "集中管理低频工程操作，展开分组或使用顶部搜索查找。", "运行与检查")
            });
        internal static IReadOnlyList<DarkNightsWorkbenchEntry> Entries { get; } = System.Array.AsReadOnly(new[]
        {
            new DarkNightsWorkbenchEntry("objects", DarkNightsWorkbenchEntryKind.Tool, Objects, "Definition Workshop",
                "原生列表、分类树、搜索、能力装配与共享配置编辑。",
                "编辑和保存由原生 Workshop 管理。", "打开 Workshop", DarkNightsNativeWorkspace.Workshop),
            new DarkNightsWorkbenchEntry("viewer", DarkNightsWorkbenchEntryKind.Tool, Objects, "YYGC Viewer",
                "数据库／Addressables 总览与注册诊断。",
                "按需打开 Viewer，不启动对象管理套件的其他窗口。", "打开 Viewer", DarkNightsNativeWorkspace.Viewer),
            new DarkNightsWorkbenchEntry("mining", DarkNightsWorkbenchEntryKind.Tool, Objects, "采集装配与目标匹配",
                "只读检查矿镐、矿床、白名单及目标匹配；配置在 Workshop 编辑。",
                "校验不会补配置或保存资产。", "打开采集校验", DarkNightsNativeWorkspace.Mining,
                ShipTrade + "item-pickaxe.asset", Root + "Objects/MineralDeposit/MineralDeposit.asset"),
            new DarkNightsWorkbenchEntry("journey", DarkNightsWorkbenchEntryKind.Tool, Journey, "Cave Wall Tuner · 航程设置",
                "在同一星球工作台编辑目的地、降落与航程；保留大画布和原生样式参数。",
                "地图与航程共用 WorldSession 草稿，显式保存。", "编辑航程设置", DarkNightsNativeWorkspace.Journey, Session),
            new DarkNightsWorkbenchEntry("rules", DarkNightsWorkbenchEntryKind.Editor, Journey, "数值规则",
                "经济、交易和波次规则的唯一 JSON 来源。", "使用外部编辑器保存。", "", null, Root + "Config/balance.json"),
            new DarkNightsWorkbenchEntry("terrain", DarkNightsWorkbenchEntryKind.Tool, Map, "地形业务与耐久",
                "Profile、规则瓦片耐久和离线单格试采。",
                "编译并应用由原窗口执行，保留草稿和冲突检查。", "编辑地形业务", DarkNightsNativeWorkspace.Terrain, Session),
            new DarkNightsWorkbenchEntry("visual", DarkNightsWorkbenchEntryKind.Tool, Map, "Cave Wall Tuner",
                "地图生成、岩壁与背景、星球与降落、航程四个折叠组，共用大画布。",
                "地图与航程、表现分别保存；拆填仅作临时预览。", "打开星球工作台", DarkNightsNativeWorkspace.Visual,
                Root + "Terrain/StrataCave/Style.asset", Root + "Terrain/StrataCave/Background.asset"),
            new DarkNightsWorkbenchEntry("ui", DarkNightsWorkbenchEntryKind.Editor, UI, "飞船装备界面来源",
                "UI Builder、样式、主题与原生 PanelSettings。",
                "源文件用原生编辑器打开；PanelSettings 单独保存。", "", null,
                Root + "UI/ShipEquipment/ShipEquipmentPanelSettings.asset", Root + "UI/ShipEquipment/ShipEquipment.uxml",
                Root + "UI/ShipEquipment/ShipEquipment.uss", Root + "UI/ShipEquipment/ShipEquipmentTheme.tss")
        }.Concat(GameSceneWorkbenchCatalog.Entries).ToArray());
    }
}
