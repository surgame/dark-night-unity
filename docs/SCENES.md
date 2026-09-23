# Unity 场景索引

2026-09-23。场景文件集中按用途存放；地形的配置、地图格子、Prefab 和贴图继续留在各自的 `Res/Terrain/` 资源目录，不为了整理场景而搬动源资产。移动时使用 Unity AssetDatabase 并保留了原 `.meta`／GUID。

| 用途 | 路径（相对 `Game/Assets`） | 入口与边界 |
| --- | --- | --- |
| 产品启动 | `Scenes/Bootstrap.unity` | 正式启动入口；不移动、不改类型名 |
| 正式旧营地 | `DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity` | 已有内容及兼容回归 |
| 随机关卡模板 | `DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity` | 由正式选图入口使用，与上项同名但 GUID 不同 |
| 正式远征 | `DarkNights/Res/Scenes/Expedition/Expedition.unity` | 当前产品入口 |
| 固定岩层样板 | `DarkNights/Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` | 地形 Modifier、岩层与背景的固定对照；对应资产仍在 `Res/Terrain/StrataCave/` |
| 随机岩层样板 | `DarkNights/Res/Scenes/Workbenches/Terrain/RandomCave.unity` | 随机地图工作台 |
| 天然洞穴实验 | `DarkNights/Res/Scenes/Workbenches/Terrain/CaveExploration.unity` | `Dark Nights/Debug/打开天然洞穴实验` |
| 随机地图 Debug | `DarkNights/Res/Scenes/Workbenches/Terrain/TerrainDebugBootstrap.unity` | `Dark Nights/Debug/打开随机地图 Bootstrap` |
| DualGrid 单机测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainTest.unity` | 地图预览和专用测试 Player |
| DualGrid 联机测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainNetworkTest.unity` | 独立网络探针与专用测试 Player |

## 删除待定（未删除）

| 场景（相对 `Game/Assets`） | 原位置 | 判断与处置 |
| --- | --- | --- |
| `DarkNights/Res/Scenes/PendingDeletion/Terrain/CaveContourStatic.unity` | `DarkNights/Res/Terrain/CaveContourStatic/CaveContourStatic.unity` | 初批“旧前景＋新背景”阶段性对照，已被独立 `StrataCave` 样板替代且视觉未达标；不在 Build Settings，当前产品及菜单无入口。已连同 `.meta` 移入删除待定，GUID `83de985c436433b4993b518d1e2fee37` 不变。旧的一次性 SetupContour 工具改为在此路径创建／打开，不作为正式入口。 |

此处只是隔离候选，仍可在 Unity 中打开或按原 GUID 移回；没有实际删除。`Res/Terrain/CaveContourStatic/` 中的配置、美术等资源没有随场景移动，是否独占和是否可删尚未核实，须单独确认。后续实际删除要重新核对引用并获得明确授权。

其余看似旧的场景当前仍有用途，故保留：`CaveExploration` 与 `TerrainDebugBootstrap` 有 Debug 菜单；`TerrainTest` 与 `TerrainNetworkTest` 有地图／网络测试及专用构建入口；旧 `Pinewatch` 用于已有内容和兼容回归。`Assets/Scenes/SampleScene.unity` 仍在 Build Settings 且是项目模板默认场景，`Assets/Settings/Scenes/URP2DSceneTemplate.unity` 是 URP 模板；`Assets/Samples/LanCoop/Content/LanCoop.unity` 和 `Assets/Samples/YYGCInputActions/Content/InputActions.unity` 属于各自 Sample。未将这些场景误判为废弃，也未删除或改名。

现有 Build Settings 五项（Bootstrap、SampleScene、Pinewatch、RandomPinewatch、Expedition）保持原样；工作台与地形测试场景由明确的菜单或测试构建入口选用，不加入正式构建列表。

编辑器代码的地形场景路径统一在 `Scripts/Editor/Terrain/TerrainScenePaths.cs`；新增同类场景时先选 `Workbenches` 或 `Tests`，不要再把 `.unity` 放进配置／贴图文件夹。历史证据中的旧路径保留当时事实，不应当作当前打开路径。

本次整理了 7 个场景及其 `.meta`（其中旧轮廓对照再移至删除待定），逐个与移动前 Git blob 对照均相同；Build Settings／项目默认场景文件未变。Unity Editor 重新编译无错误，定向场景回归 3/3（原 GUID、构建列表、固定／随机样板引用及其他场景只读打开）；Core 构建无警告／错误，ArchitectureGuard 488 文件、12 自测、0 错误。未重新构建 Player，也未将此结果外推为完整联机或画面验收。
