using System.Collections.Generic;
using System.Linq;
using DarkNights.Editor.Terrain;

namespace DarkNights.Editor
{
    /// <summary>
    /// 当前正式工具的导航目录；集中声明分组与作者资产，不自动生成、安装、编译或应用配置。
    /// 新入口在此登记，原生 Definition 和专用工作台仍是实际编辑者。
    /// </summary>
    internal static class DarkNightsWorkbenchCatalog
    {
        private const string Root = "Assets/DarkNights/Res/";
        private const string Session = Root + "Objects/WorldSession/WorldSession.asset";
        private const string ShipTrade = Root + "Objects/ShipTrade/";
        private const string Style = Root + "Terrain/StrataCave/Style.asset";
        internal static IReadOnlyList<DarkNightsWorkbenchEntry> Entries { get; } =
            System.Array.AsReadOnly(new[]
            {
                new DarkNightsWorkbenchEntry("objects", DarkNightsWorkbenchEntryKind.Editor, "原生对象", "Definition 浏览器",
                    "搜索游戏的原生对象与界面 Definition，查看能力装配、共享配置和稳定身份。",
                    "直接编辑原资产，单独保存当前 Definition；不创建对象或分配身份。", "", null),
                new DarkNightsWorkbenchEntry("mining", DarkNightsWorkbenchEntryKind.Editor, "玩法", "工具与采集能力",
                    "工具 Definition 决定目标类别、材料、采集等级、白名单与动作参数；矿床 Definition 描述自身材料与要求。",
                    "原生 Definition 编辑器直接保存所选资产，新会话生效。", "打开 Definition 编辑与匹配预览", MiningDefinitionWindow.Open,
                    ShipTrade + "item-pickaxe.asset", Root + "Objects/MineralDeposit/MineralDeposit.asset"),
                new DarkNightsWorkbenchEntry("journey", DarkNightsWorkbenchEntryKind.Tool, "生成与业务", "星球与航程",
                    "管理目的地、生成参数、地形步骤和过场；表格与蓝图预览共用正式会话定义。",
                    "专用编辑器使用草稿，点击应用后才写入 WorldSession。", "打开星球与航程编辑器", ExpeditionFlowWindow.Open, Session),
                new DarkNightsWorkbenchEntry("equipment", DarkNightsWorkbenchEntryKind.Editor, "玩法", "飞船交易与装备",
                    "查看商店、出售服务及手枪、炸药、喷气背包的原生 Definition；交易数值在 balance.json。",
                    "在下方原生 Inspector 编辑，保存当前资产后新会话生效。", "", null,
                    ShipTrade + "ship-service-shop.asset", ShipTrade + "ship-service-sale.asset",
                    ShipTrade + "item-pistol.asset", ShipTrade + "item-bomb.asset", ShipTrade + "item-jetpack.asset"),
                new DarkNightsWorkbenchEntry("rules", DarkNightsWorkbenchEntryKind.Editor, "玩法", "规则与会话",
                    "规则数值来自 balance.json；会话能力、手持武器与远征装配来自 WorldSession Definition。",
                    "JSON 请在外部编辑器保存；Definition 使用原生 Inspector。", "", null,
                    Root + "Config/balance.json", Session),
                new DarkNightsWorkbenchEntry("terrain", DarkNightsWorkbenchEntryKind.Tool, "生成与业务", "地形业务与耐久",
                    "配置 Profile 绑定、规则瓦片耐久，并做离线单格试采；工具能力由工具自己的 Definition 配置。",
                    "专用编辑器保留草稿，编译并应用会创建新目录。", "打开网格业务配置编辑器", TerrainBusinessWindow.Open, Session),
                new DarkNightsWorkbenchEntry("visual", DarkNightsWorkbenchEntryKind.Tool, "画面调校", "岩壁、背景与地表",
                    "Cave Wall Tuner 预览正式生成链，调整岩壁、背景、点缀与地表环境；拆填仅影响预览草稿。",
                    "样式和地图草稿由专用编辑器应用或取消。", "打开 Cave Wall Tuner", TerrainStylePreviewWindow.Open,
                    Style, Root + "Terrain/StrataCave/Background.asset"),
                new DarkNightsWorkbenchEntry("ui", DarkNightsWorkbenchEntryKind.Editor, "界面", "飞船装备界面",
                    "原生 UXML、USS、主题和 PanelSettings；界面布局与业务 Definition 分开维护。",
                    "PanelSettings 在下方编辑；UXML、USS 和主题可用原生编辑器打开。", "", null,
                    Root + "UI/ShipEquipment/ShipEquipmentPanelSettings.asset", Root + "UI/ShipEquipment/ShipEquipment.uxml",
                    Root + "UI/ShipEquipment/ShipEquipment.uss", Root + "UI/ShipEquipment/ShipEquipmentTheme.tss"),
            }.Concat(GameSceneWorkbenchCatalog.Entries).ToArray());
    }
}
