# Unity 场景索引

日常从工作台打开正式入口、预览和参考；完整玩法从 Bootstrap 装配。工作台保留14个场景／资料入口，资产盘点仍为16个场景；静态和随机营地用于专项回归。

## 当前场景路径（2026-10-02 整理后）

路径相对 `Game/Assets`；普通游戏仍从 Bootstrap 进入，快速测试不新增场景。

| 用途 | 当前路径 |
| --- | --- |
| 正式游戏入口／快速测试主菜单 | `Scenes/Bootstrap.unity` |
| 正式远征内容 | `DarkNights/Res/Scenes/Expedition/Expedition.unity` |
| 地形预览 | `DarkNights/Res/Scenes/Workbenches/Terrain/RandomCave.unity` |
| 编辑时参考画面与同源 Play 预览 | `DarkNights/Res/Scenes/References/Terrain/ReferenceChamber.unity` |
| 静态营地回归 | `DarkNights/Res/Scenes/Regression/Camp/StaticCampRegression.unity` |
| 随机营地回归 | `DarkNights/Res/Scenes/Regression/Camp/RandomCampRegression.unity` |
| 地图单机探针 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainTest.unity` |
| 地图网络探针 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainNetworkTest.unity` |
| 已退役：八房间调试 | `DarkNights/Res/Scenes/Archive/Terrain/TerrainDebugBootstrap(old).unity` |
| 已退役：旧洞穴实验 | `DarkNights/Res/Scenes/Archive/Terrain/CaveExploration(old).unity` |
| 已退役：早期背景对照 | `DarkNights/Res/Scenes/Archive/Terrain/CaveContourStatic.unity` |
| 默认空场景模板（不构建） | `Scenes/SampleScene.unity` |
| URP 2D 编辑器模板 | `Settings/Scenes/URP2DSceneTemplate.unity` |
| LAN 独立样例 | `Samples/LanCoop/Content/LanCoop.unity` |
| 输入独立样例 | `Samples/YYGCInputActions/Content/InputActions.unity` |
| 第三方 Console 演示 | `Plugins/EdgarDev/Smart Console/Demo/Demo.unity` |

Build Settings 当前为 Bootstrap、StaticCampRegression、RandomCampRegression、Expedition 4 项，与 `GamePlayerBuild` 的显式列表一致。游戏入口路径由 `GameScenePaths`、`RandomLevelEntry` 管理，参考／预览／测试／退役路径由 `TerrainScenePaths` 管理。


迁移前路径、场景审查与验证过程见[历史快照](archive/SCENE_ORGANIZATION_HISTORY_20261005.md)，快速局操作见[归档操作记录](archive/QUICK_TEST_SCENES.md)。本次整理未移动场景、Prefab 或作者资源。
