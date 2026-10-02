using DarkNights.Editor.Terrain;
using DarkNights.Entry;
using UnityEditor;

namespace DarkNights.Editor
{
    /// <summary>全部项目场景的用途导航；日常、参考、回归、探针和退役分开，退役项只定位，避免重新生成旧内容。</summary>
    internal static class GameSceneWorkbenchCatalog
    {
        internal static readonly DarkNightsWorkbenchEntry[] Entries =
        {
            Scene("bootstrap", "日常开发", "正式游戏入口", GameScenePaths.Bootstrap, "从主菜单开始完整游戏。"),
            Scene("random", "日常开发", "地形预览", TerrainScenePaths.RandomCave, "正式星球地图与岩壁调试；点击破坏属于预览，不是正式挥镐。"),
            Scene("quick-tests", "快速测试", "已着陆 · 矿镐", GameScenePaths.Bootstrap,
                "Play 主菜单后按 F1 打开 Debug Hub 的快速测试页，点击启动；记住上次测试项，每次是干净新局。"),
            Scene("expedition", "正式内容", "正式远征内容", Entry.Terrain.RandomLevelEntry.ExpeditionScenePath,
                "游戏内容场景；完整装配仍由 Bootstrap 负责，直接 Play 默认从太空开始。"),
            Scene("reference", "参考对照", "岩层参考画面", TerrainScenePaths.ReferenceChamber,
                "保留编辑时静态参考画面；Play 与地形预览使用同一正式生成配置。"),
            Scene("camp-static", "旧玩法回归", "静态营地回归", GameScenePaths.StaticCamp, "既有营地布局及 --dn-camp-mode 回归。"),
            Scene("camp-random", "旧玩法回归", "随机营地回归", GameScenePaths.RandomCamp, "旧随机灰松谷模板及专用回归。"),
            Scene("terrain-test", "专用测试", "DualGrid 单机探针", TerrainScenePaths.TerrainTest, "仅供地图与渲染探针。"),
            Scene("terrain-network", "专用测试", "地图联机探针", TerrainScenePaths.TerrainNetworkTest, "独立地图测试 Player，不运行正式采矿流程。"),
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
            new DarkNightsWorkbenchEntry(id, group, title, description, "打开前沿用 Unity 的未保存场景提示。", "打开场景",
                () => TerrainWorkbenchScenes.Open(path), path);
        private static DarkNightsWorkbenchEntry Locate(string id, string group, string title, string path, string description) =>
            new DarkNightsWorkbenchEntry(id, group, title, description, "仅定位现有资产，不重建或运行旧场景。", "在 Project 中定位",
                () => DarkNightsWorkbenchWindow.Locate(AssetDatabase.LoadMainAssetAtPath(path)), path);
    }
}
