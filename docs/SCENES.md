# Unity 场景索引

2026-09-26。**新版可运行地图是 `ReferenceChamber`（固定）和 `RandomCave`（随机），正式游戏是 `Bootstrap → Expedition`，编辑器调参使用 `Cave Wall Tuner`。** Unity 的 `Dark Nights / Terrain` 菜单提供三个场景的直接打开入口；工作台直接 Play，正式远征经 Bootstrap 启动会话。

地形配置、地图格子、Prefab 和贴图继续留在各自的 `Res/Terrain/` 资源目录。旧场景通过 Unity AssetDatabase 移入 `(old)` 并在文件名追加 `(old)`，保留原 `.meta`／GUID 和场景内容。

新版工作台 Play 左栏现已接入[运行时 Cave Wall Tuner 功能](RUNTIME_TERRAIN_TUNER.md)：地图／岩壁／背景／显示／状态五页，包含造型子页、草稿应用取消、固定地图保存及自适应缩放。独立编辑器窗口继续保留。

## 正式接入与当前验收状态

`GameSessionStartupModule` 默认选择 `Expedition.unity`；该场景的 `Definition`／`ContourDefinition` 均指向 StrataCave 定义（GUID `d4a19379210ced349b64c1b628f9c7ca`），`CaveStyle`／`StaticBackgroundStyle` 均指向 `Res/Terrain/StrataCave/Style.asset`（GUID `b8f1f94057451ec459e1bac28aba1c8a`）。因此无需 `--dn-contour-static` 就使用新版岩层和三层背景。两个新版工作台使用同一组定义和样式，但地图输入分别为固定蓝图和随机生成；正式远征有自己的权威地图与玩法，接入同一渲染链不表示三者地图布局相同。

最新代码 `CaveTerrainStyle.ImmediateForeground` 默认 `true`，当前 Style 没有序列化覆盖此字段；`CaptureModifiers()` 经 `AsLocalForeground()` 将前景圆簇转换为 LocalV2。源 RoundedRock 资产的版本仍可为 LegacyV1，不能仅看源资产字段或旧文档判断运行时版本。近期输入、只读副本安装和相机完成判定也由正式 `RandomLevelEntry → TerrainPreview` 共用；这些属于**代码已接入**，不代表整项已验收。

09-26 既有 Local Unity [原始 XML](evidence/terrain-local-validation-20260926.xml)／[结果摘要](evidence/terrain-local-validation-20260926.json)为 **32 通过、2 失败**：`ActualPagesStayStaticAndDestructionIsLocal` 在只读 chunk 编辑时报错；`PreviewKeepsCameraPagesVisibleAcrossDirectionChanges` 缺少相机所需页。新视觉用例和严格跨资源发布门槛未完成，前台性能及新 Player 联机不能宣称通过。本次整理入口不处理这些算法问题，也没有切换美术参数或生成 Player。

| 用途 | 路径（相对 `Game/Assets`） | 入口与边界 |
| --- | --- | --- |
| 产品启动 | `Scenes/Bootstrap.unity` | 正式启动入口；不移动、不改类型名 |
| 正式旧营地 | `DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity` | 已有内容及兼容回归 |
| 随机关卡模板 | `DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity` | 由正式选图入口使用，与上项同名但 GUID 不同 |
| 正式远征 | `DarkNights/Res/Scenes/Expedition/Expedition.unity` | 当前产品入口 |
| 新版固定岩层工作台 | `DarkNights/Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` | `Dark Nights/Terrain/打开新版固定地图 ReferenceChamber`；地形 Modifier、岩层与背景的固定对照 |
| 新版随机岩层工作台 | `DarkNights/Res/Scenes/Workbenches/Terrain/RandomCave.unity` | `Dark Nights/Terrain/打开新版随机地图 RandomCave` |
| (old) 天然洞穴实验 | `DarkNights/Res/Scenes/Workbenches/Terrain/(old)/CaveExploration(old).unity` | `Dark Nights/Terrain/(old)/打开天然洞穴实验`；旧 CaveExploration 定义与样式 |
| (old) 随机地图 Debug | `DarkNights/Res/Scenes/Workbenches/Terrain/(old)/TerrainDebugBootstrap(old).unity` | `Dark Nights/Terrain/(old)/打开随机地图 Bootstrap`；旧八房间生成器与测试图集 |
| DualGrid 单机测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainTest.unity` | 地图预览和专用测试 Player |
| DualGrid 联机测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainNetworkTest.unity` | 独立网络探针与专用测试 Player |

## 删除待定（未删除）

| 场景（相对 `Game/Assets`） | 原位置 | 判断与处置 |
| --- | --- | --- |
| `DarkNights/Res/Scenes/PendingDeletion/Terrain/CaveContourStatic.unity` | `DarkNights/Res/Terrain/CaveContourStatic/CaveContourStatic.unity` | 初批“旧前景＋新背景”阶段性对照，已被独立 `StrataCave` 样板替代且视觉未达标；不在 Build Settings，当前产品及菜单无入口。已连同 `.meta` 移入删除待定，GUID `83de985c436433b4993b518d1e2fee37` 不变。旧的一次性 SetupContour 工具改为在此路径创建／打开，不作为正式入口。 |

此处只是隔离候选，仍可在 Unity 中打开或按原 GUID 移回；没有实际删除。`Res/Terrain/CaveContourStatic/` 中的配置、美术等资源没有随场景移动，是否独占和是否可删尚未核实，须单独确认。后续实际删除要重新核对引用并获得明确授权。

`CaveExploration` 与 `TerrainDebugBootstrap` 已归组为 `(old)`，仅供旧版回归。**`TerrainDebugBootstrap.cs` 是新旧工作台共用的运行组件，不能随旧场景删除或整体改成 old。** `TerrainTest` 与 `TerrainNetworkTest` 仍是地图／网络探针，有专用构建入口；旧 `Pinewatch` 用于已有内容和营地回归。它们的独立用途和正式引用没有因本次归组改变。`Assets/Scenes/SampleScene.unity`、URP 模板和 `Assets/Samples/` 保持原样。

现有 Build Settings 五项（Bootstrap、SampleScene、Pinewatch、RandomPinewatch、Expedition）保持原样；工作台与地形测试场景由明确的菜单或测试构建入口选用，不加入正式构建列表。

编辑器代码的地形场景路径统一在 `Scripts/Editor/Terrain/TerrainScenePaths.cs`；新增同类场景时先选 `Workbenches` 或 `Tests`，不要再把 `.unity` 放进配置／贴图文件夹。历史证据中的旧路径保留当时事实，不应当作当前打开路径。

09-23 历史整理共移动 7 个场景及 `.meta`，当时定向场景回归 3/3、ArchitectureGuard 488 文件／12 自测／0 错误。本次 09-26 只归组上述两个旧场景，验收结果另列，不复用历史数字。

09-26 本次 Unity 6000.4.9f1 编译及定向 EditMode **4/4 通过**：7 个场景 GUID、正式构建列表边界、新版工作台引用、正式远征实际样式与 LocalV2 捕获，以及旧场景只读重开。两个被移动的 `.unity` 和 `.meta` 对照移动前 Git blob 字节完全一致；正式场景、Build Settings 和美术资产未改。Unity 留在新版 `ReferenceChamber`，未自动进入 Play。[原始 XML](evidence/terrain-scene-catalog-20260926.xml)与[摘要](evidence/terrain-scene-catalog-20260926.json)保存本批结果。仅 203 字节已消费请求文件移入 `artifacts/待清理/20260926-terrain-scene-entries/` 并列清单，没有删除产物，归档不计释放空间。
