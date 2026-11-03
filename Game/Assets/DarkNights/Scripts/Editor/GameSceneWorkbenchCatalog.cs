using DarkNights.Editor.Terrain;
using DarkNights.Entry;
using UnityEditor;

namespace DarkNights.Editor
{
    /// <summary>当前工作台的场景导航；旧营地回归退出目录，现行预览、探针和参考按用途呈现，退役资料只定位。</summary>
    internal static class GameSceneWorkbenchCatalog
    {
        internal static readonly DarkNightsWorkbenchEntry[] Entries =
        {
            Scene("mapmodule-entrance", "日常开发", "洞口与浅厅", MapModule.MapModulePaths.Scene,
                "洞口与浅厅示例地图，可编辑模块、检查连接并生成运行地图。"),
            Scene("mapmodule-m17", "日常开发", "M17 · 悬索裂谷", MapModule.M17StudySetup.ScenePath,
                "新版MapModule环境吊绳：E抓放、W/S攀爬、A/D摆荡、空格离绳。"),
            Scene("cooperative-physics-scene", "日常开发", "合作物理候选", PhysicsStudySetup.ScenePath,
                "沿现有模块图启动浮点Host物理候选；可推箱，E连接队友／箱子，再按E解除。"),
            Scene("cave-whitebox", "日常开发", "干净矿洞白模", TerrainScenePaths.CaveWhitebox,
                "正式主角与一段简单平地；Play后直接移动、跳跃，原生场景保存作者布局。"),
            Scene("entrance-gentle", "入口白模", "M01 · 低檐缓坡", TerrainScenePaths.EntranceGentle,
                "天然洞檐、下凹缓坡和回升岩台；只有指定主角与地形。"),
            Scene("entrance-shelf", "入口白模", "M01 · 岩肩错台", TerrainScenePaths.EntranceShelf,
                "短坡连接较平直的错层岩肩；只有指定主角与地形。"),
            Scene("entrance-vault", "入口白模", "M01 · 宽口拱厅", TerrainScenePaths.EntranceVault,
                "宽洞口、拱状顶板和下凹洞厅；只有指定主角与地形。"),
            Scene("entrance-fissure", "入口白模", "M01 · 斜裂窄入口", TerrainScenePaths.EntranceFissure,
                "沿斜裂短段下探、低檐及紧凑内部岩台；只有指定主角与地形。"),
            Scene("vault-strata", "宽洞风格", "M01 · 暖棕层岩", TerrainScenePaths.VaultStrata,
                "方正宽顶与阶状岩台，SVG层岩色块；主角与地形，无物品或过渡块。"),
            Scene("vault-moss", "宽洞风格", "M01 · 青绿岩拱", TerrainScenePaths.VaultMoss,
                "开阔拱口与浅碗地形，青绿岩面和暖色边缘。"),
            Scene("vault-blue", "宽洞风格", "M01 · 冷蓝裂岩", TerrainScenePaths.VaultBlue,
                "偏心高厅与错位顶板，冷蓝裂岩和铜色层线。"),
            Scene("vault-ochre", "宽洞风格", "M01 · 赤赭断层", TerrainScenePaths.VaultOchre,
                "宽厅内的高低岩肩与回升地台，赭红分层截面。"),
            Scene("bootstrap", "日常开发", "正式游戏入口", GameScenePaths.Bootstrap,
                "从主菜单开始完整游戏。矿镐快速测试：Play 后按 F1 → 快速测试 → 已着陆 · 矿镐。"),
            Scene("random", "日常开发", "地形预览", TerrainScenePaths.RandomCave, "正式星球地图与岩壁调试；点击破坏属于预览，不是正式挥镐。"),
            Scene("expedition", "正式内容", "正式远征内容", Entry.Terrain.RandomLevelEntry.ExpeditionScenePath,
                "游戏内容场景；完整装配仍由 Bootstrap 负责，直接 Play 默认从太空开始。"),
            Scene("reference", "参考对照", "岩层参考画面", TerrainScenePaths.ReferenceChamber,
                "保留编辑时静态参考画面；Play 与地形预览使用同一正式生成配置。"),
            Scene("terrain-test", "专用测试", "DualGrid 单机探针", TerrainScenePaths.TerrainTest, "仅供地图与渲染探针。"),
            Scene("terrain-network", "专用测试", "地图联机探针", TerrainScenePaths.TerrainNetworkTest, "独立地图测试 Player，不运行正式采矿流程。"),
            Scene("gameplay-lab", "专用测试", "CL-02 塌方 · 大小混合版", Entry.Terrain.GeologyLabEntry.ScenePath,
                "独立塌方测试：26颗大小混合碎岩、尘雾／石粒、落地堆积和正式矿镐清障。两种触发；重新Play重置。"),
            Scene("lan-sample", "独立样例", "LAN 联机样例", "Assets/Samples/LanCoop/Content/LanCoop.unity", "独立框架联机样板。"),
            Scene("input-sample", "独立样例", "输入与重绑定样例", "Assets/Samples/YYGCInputActions/Content/InputActions.unity", "独立输入、重绑定与模态示例。"),
            Scene("console-demo", "独立样例", "Smart Console 演示", "Assets/Plugins/EdgarDev/Smart Console/Demo/Demo.unity", "插件演示，保留原路径。"),
            Locate("sample-template", "编辑器模板", "默认空场景模板", "Assets/Scenes/SampleScene.unity", "保留默认模板引用，退出正式构建列表。"),
            Locate("urp-template", "编辑器模板", "URP 2D 场景模板", "Assets/Settings/Scenes/URP2DSceneTemplate.unity", "供现有 scenetemplate 创建场景。"),
            Locate("old-debug", "已退役", "旧八房间地图调试", TerrainScenePaths.TerrainDebugBootstrap, "缺当前地图配置绑定，保留历史内容。"),
            Locate("old-cave", "已退役", "旧天然洞穴实验", TerrainScenePaths.CaveExploration, "旧定义与旧样式对照，已被当前工作台替代。"),
            Locate("old-contour", "已退役", "旧前景与背景对照", TerrainScenePaths.PendingCaveContourStatic, "阶段性实验，缺当前地图绑定，不作为运行入口。")
        };
        private static DarkNightsWorkbenchEntry Scene(string id, string group, string title, string path, string description) =>
            new DarkNightsWorkbenchEntry(id, DarkNightsWorkbenchEntryKind.Scene, group, title, description, "打开前沿用 Unity 的未保存场景提示。", "打开场景",
                () => MapWorkbenchScenes.Open(path), path);
        private static DarkNightsWorkbenchEntry Locate(string id, string group, string title, string path, string description) =>
            new DarkNightsWorkbenchEntry(id, DarkNightsWorkbenchEntryKind.Reference, group, title, description, "仅定位现有资产，不重建或运行旧场景。", "在 Project 中定位",
                () => DarkNightsWorkbenchWindow.Locate(AssetDatabase.LoadMainAssetAtPath(path)), path);
    }
}
