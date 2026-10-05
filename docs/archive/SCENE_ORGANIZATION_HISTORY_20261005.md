# Unity 场景索引

> 2026-10-05 整理前的历史快照。日期、版本、旧路径与验证状态按原时点保存，链接按归档位置更新；现行入口见[当前文档](../SCENES.md)。

2026-10-03 工作台重排为配置编辑、场景与测试、工具与预览三个工作区；场景在列表中直接打开。静态／随机营地回归已从工作台目录撤下，“旧玩法回归”分类退出，工作台保留 14 个场景入口。下方 16 个场景为项目资产盘点，两个旧营地的构建与历史测试引用仍在，本批没有删除或移动场景。见[工作台说明](../EDITOR_WORKBENCH.md)。

2026-10-02 开发已实施：[快速测试操作说明](QUICK_TEST_SCENES.md)。主菜单 Debug Hub 提供“已着陆 · 矿镐”，本机记住测试 ID。6 个场景已通过 Unity AssetDatabase 迁移，原 GUID 和场景／meta 字节保持；16 个场景全部保留，SampleScene 退出构建列表。Unity 编译、规则／恢复／目录合同 21/21、主菜单实际 Play 1/1、快捷键与实际 GUI 点击 1/1 通过；已检查画面及跨 Editor 重启记忆。当前验收限定 Editor 使用范围，没有新 Player 或多人快速局证据。下方盘点与复评记录的是迁移前状态，不再作为当前打开路径。

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

Build Settings 当前为 Bootstrap、StaticCampRegression、RandomCampRegression、Expedition 4 项，与 `GamePlayerBuild` 的显式列表一致。游戏入口路径由 `GameScenePaths`、`RandomLevelEntry` 管理，参考／预览／测试／退役路径由 `TerrainScenePaths` 管理。下面为本轮迁移前盘点及历史证据，原日期、原路径保留。

## 2026-10-02 迁移前场景盘点与矿镐专项入口建议

本次先整理用途及退役建议，**未移动、改名或删除场景，未实现快速着陆入口**。核对范围为当前 `Game/Assets` 下全部 `.unity`、各场景 `.meta`、项目设置、菜单、启动代码、构建代码和测试引用；不把 `Library`、依赖缓存及包内示例计入项目场景数量。以下为源码／序列化检查，没有切换用户当前场景、进入或退出 Play，也没有运行 Unity 验收或构建。

当前共 **16 个场景**：正式启动／关卡 4 个、当前地形工作台 2 个、专用地形测试 2 个、退役地形实验 3 个、模板／独立示例 5 个。所有表内路径相对 `D:\Developer\MiniGames\Dark Nights\unity-projects\Game\Assets`；同名 `Pinewatch.unity` 必须按完整路径区分。

| 分组 | 场景路径 | 实际用途与处理建议 |
| --- | --- | --- |
| 正式 | `Scenes/Bootstrap.unity` | 产品启动、资源与网络装配；保留，完整游戏从这里进入。 |
| 正式 | `DarkNights/Res/Scenes/Expedition/Expedition.unity` | 当前正式远征内容；保留。打开后 Play 会由 `ScenePlaySelection` 经 Bootstrap 装配，仍从太空待命开始。 |
| 旧玩法回归 | `DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity` | 旧营地静态布局，`--dn-camp-mode` 仍直接引用；保留，退出日常远征导航即可，不能按过期资源直接删除。 |
| 旧玩法回归 | `DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity` | 旧随机灰松谷模板；仍在正式 Player 显式构建列表及 Editor 场景选择链中，保留供专项回归。当前默认启动不选它。 |
| 地形预览 | `DarkNights/Res/Scenes/Workbenches/Terrain/RandomCave.unity` | 正式配置的地图／岩壁／背景／通行工作台；建议作为日常地形预览唯一入口。 |
| 地形预览 | `DarkNights/Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` | Play 已与 RandomCave 同走正式生成；额外保留 `EditorOnly` 的静态参考画面。功能重叠，可退出日常导航，但整合前保留参考资产、GUID 及现有测试。 |
| 专用测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainTest.unity` | DualGrid 单机预览／探针；`TerrainNetworkProbe` 仍会加载它，不能删。 |
| 专用测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainNetworkTest.unity` | 独立地图网络探针，`TerrainPlayerBuild` 的专用 Player 入口；保留，不用于矿镐产品验收。 |
| 退役候选 | `DarkNights/Res/Scenes/Workbenches/Terrain/(old)/TerrainDebugBootstrap(old).unity` | 旧八房间地图调试；未绑定当前入口必需的 `MapAssemblySource`，源码检查确认不能作为当前直接 Play 入口。可列入退役处理清单。 |
| 退役候选 | `DarkNights/Res/Scenes/Workbenches/Terrain/(old)/CaveExploration(old).unity` | 旧洞穴定义／样式实验，已被 StrataCave 工作台替代；它已有正式 `MapAssemblySource` 绑定，不应与上项一起断言为缺绑定。保留历史对照用途，是否退役另行处理。 |
| 退役候选 | `DarkNights/Res/Scenes/PendingDeletion/Terrain/CaveContourStatic.unity` | 初批旧前景＋新背景对照，已被替代；无正式构建或日常入口，且缺当前 `MapAssemblySource`。最明确的退役候选，仍未删除。 |
| 模板残留 | `Scenes/SampleScene.unity` | 仅有相机与 2D 全局光，但 `ProjectSettings.asset.templateDefaultScene` 仍引用它，Build Settings 也启用它。建议先从正式 Build Settings 移出；实际删除前处理模板引用。 |
| 编辑器模板 | `Settings/Scenes/URP2DSceneTemplate.unity` | 被 `Settings/Lit2DSceneTemplate.scenetemplate` 引用，用于新建 2D 场景；保留。 |
| 独立示例 | `Samples/LanCoop/Content/LanCoop.unity` | 框架联机样板，有自身启动和验证流程；保留，与正式玩法分开。 |
| 独立示例 | `Samples/YYGCInputActions/Content/InputActions.unity` | 输入、重绑定及模态交互样板，有独立场景测试；保留。 |
| 第三方示例 | `Plugins/EdgarDev/Smart Console/Demo/Demo.unity` | Smart Console 插件演示；退出日常导航即可，不因本次整理改动插件包。 |

Build Settings 当前启用 5 项：Bootstrap、SampleScene、静态 Pinewatch、随机 Pinewatch、Expedition。**正式 `GamePlayerBuild` 显式构建的只有后者中的 4 个游戏场景，不包含 SampleScene**；Build Settings 的残留与正式构建内容要分开判断。旧营地及随机模板目前也包含在这个显式构建列表中，减少发行场景需同时核对构建与回归用途。

### 退役边界与推荐导航

三个退役地形实验仍被 `TerrainSceneCatalogTests` 要求存在并保留 GUID；旧场景路径仍出现在 `TerrainScenePaths`、旧菜单／整理代码及 `tools/contour-reference/SetupContour.cs`。当前没有查到它们的产品加载或正式构建入口，但不能将“退出玩法”当作“无任何引用”。后续退役需同步调整现行菜单／路径合同／测试，保留历史证据与共享资源；本次没有核实关联美术的独占关系，因此不提出整目录删除。

建议日常“场景”导航收敛为：**正式游戏（Bootstrap）、地形预览（RandomCave）、矿镐专项（已着陆）**。第三项目前不存在，是下述方案的拟议入口。ReferenceChamber 移到“参考对照”，两个 Pinewatch 移到“旧玩法回归”，Tests／Samples 保持各自专用分类，退役场景仅供历史查看。先改导航的中文用途标签就能减少混淆，无需立即移动场景或改变 GUID。本段为建议，尚未修改工作台菜单。

### “已着陆＋一把矿镐”的最小方案

现有生成工作台**不能替代正式矿镐验收**：`CaveWorkshopInput` 左右键调用的是调试破坏，`CaveWorkshopSession` 使用离线人物与调试装备，不运行正式 `HeroEquipment → MiningToolBehaviour` 挥镐、装备槽、货袋和交易流程。地图共用不等于整套玩法共用。

短期可用当前版本的新局正常购镐、着陆并出舱后，保存到一个专用测试槽，之后从正式菜单加载。源码已有着陆航程、地图和装备的保存恢复链；这能省掉后续重复航行，但本次没有验证当前候选完整恢复。须使用当前 v15／当前配置指纹产生的新档；工具、地图、规则或依赖指纹变化后可能需要重新制作，不能依赖旧档迁移，也不覆盖用户已有存档。

长期建议增加**编辑器专用的一次性“矿镐专项快速开局”预设**，继续复用同一个 Bootstrap、Expedition、WorldSession 与正式装备 Definition，不再复制一份 Expedition 场景，也不修改正式默认开局或航程规则：

1. 从当前正式星球配置生成地图，测试预设固定可复现的星球和种子；不复制另一套地图算法。
2. 由服务端初始化一致的着陆航程、地图身份、船体和乘员，主角在有安全支撑和净空的舱外位置，矿镐按真实工具 Definition 放入并选中装备槽；不只改一个 `JourneyPhase` 或显示一张矿镐图片。
3. 继续等待正式实体、地图、表现与 Ready 门槛完成，之后运行原有移动、选取、连续挥镐、耐久、奖励和货袋代码。预设仅替代测试初始条件，不绕过后续采集授权。
4. 使用独立测试存档位置；每次重进可重新生成初始状态。正常 Bootstrap Play 不携带该预设，仍按现有主流程开始。

落地实现预期涉及少量 Editor 入口和会话初始化装配，不需要改采矿规则、数值、素材、协议或另建运行状态系统。需要验证快速入口的实际装备／出舱／采矿、重复进入不残留状态、普通 Bootstrap 仍走正常航程；如扩展到联机测试，还需独立 Host＋Client 的初始状态与采集收敛。当前只有可行性评估，未将这些检查写成通过。

---

2026-10-02 当前正式场景入口已聚合到 [Dark Nights 工作台](../EDITOR_WORKBENCH.md) 的“场景”分组。

2026-09-30。**`ReferenceChamber`、`RandomCave` 与 `Cave Wall Tuner` 均预览同一正式星球地图生成链；正式游戏从 `Bootstrap → Expedition` 进入。** 两个工作台场景保留原名称与 GUID，通过 `Dark Nights / 工作台 → 场景` 打开后直接 Play；地图输入都来自 `WorldSession.asset` 的洞穴配置、星球和种子。

地形配置、地图格子、Prefab 和贴图继续留在各自的 `Res/Terrain/` 资源目录。旧场景通过 Unity AssetDatabase 移入 `(old)` 并在文件名追加 `(old)`，保留原 `.meta`／GUID 和场景内容。

新版工作台 Play 左栏现已接入[运行时 Cave Wall Tuner 功能](../RUNTIME_TERRAIN_TUNER.md)：地图／岩壁／背景／显示／状态五页，包含造型子页、草稿应用取消、临时拆填及自适应缩放。独立编辑器窗口继续保留。

## 正式接入与当前验收状态

2026-09-29 后续：[洞穴生成统一与通路阻断退出](CAVE_GENERATION_ALIGNMENT.md)。2026-09-30 Tuner 和两个当前工作台均改为只从正式配置生成；旧塌方回填／竖井岩棚已退出，人工固定格子资产保留但不再被当前入口引用。此前 Tuner／RandomCave 同种子逐格一致性已通过；本次新改动未运行测试、Unity 编译或构建。扩展批次留下的 5 项失败、最终空腔连通和人工通行仍未验收。下文按日期记载的固定工作台和旧生成路径属于历史状态。

2026-09-29 主场景地下背景修复：正式 `RandomLevelEntry` 将地形宿主平移并缩放到世界坐标，`CaveVisualSource` 虽给前景材质设置了 `_MapWorldToLocal`，后壁和三层背景页再次复制材质时却丢失该非 Shader Properties 矩阵。工作台宿主处于原点、缩放为 1，因而掩盖了问题。现于两次复制后分别重设矩阵。当前 Unity Editor 编译通过；实际构造的前景、后壁和背景页矩阵均与正式缩放一致。隔离离屏渲染同一地下坐标时，正确矩阵像素为 RGB (47,35,23)，故意恢复单位矩阵后为紫灰色 RGB (55,44,50)。此项确认了背景坐标断点；正式航程连续地下画面及多端验收仍按 A056／A109 待测。

2026-09-29 地图装配入口：`WorldSession.asset → ExpeditionFlowConfig.CaveMap → TerrainGenerator.GenerateCave` 是 RandomCave 与正式远征共同的洞穴生成输入。RandomCave 通过 `MapAssemblySource` 读取该配置，以 `STRATA-0922` 预览；正式航程冻结同一配置，只用本次星球种子覆盖预览种子。此前正式星球生成会在全部 320 列把地表至天然表面下三格填实，覆盖工作台生成的洞口及浅层轮廓；现仅在飞船泊位列铺设保护平台，其他列保留天然洞穴，另保留天空与下洞步道。两端均通过 `TerrainPreview.ShowCaveReplica` 安装同一 `Style.asset` 和冻结背景参考；前景修饰、三层背景及各层点缀由样式资源装配，业务调用无需传层级开关。`Style.asset.DecorationStartRow` 统一控制前景下缘和背景点缀的候选区域起始行，当前设为默认泊位第 40 行；算法仍按真实轮廓、空隙、密度及间距选取装饰，不会在该行直接画一条边。原先写死的第 43 行只会排除更浅的候选点，不能单独解释整层不可见。工作台运行时改动目前仍是预览草稿，要让其持久影响正式生成需保存样式资产及共用生成配置。该次地图生成入口修改仅做代码静态检查；上方背景坐标修复另做了隔离离屏验证，正式航程的地下连续画面与联机仍未验收。

直接 Play `Bootstrap` 时先进入太空待命；`Orbit`／`Preparing`／`Transit` 只显示船舱和本地太空环境，`RandomLevelEntry` 在这些阶段主动跳过洞穴渲染。走到右侧驾驶台选星球，地图后台生成并到达同步后才创建正式洞穴表现。工作台与正式航程使用相同生成流程；若星球或种子不同，布局仍会不同。地图在选星球时冻结；已在运行中的航程不会因修改源码或样式资产自动重生。

`GameSessionStartupModule` 默认选择 `Expedition.unity`；该场景的 `Definition`／`ContourDefinition` 均指向 StrataCave 定义（GUID `d4a19379210ced349b64c1b628f9c7ca`），`CaveStyle`／`StaticBackgroundStyle` 均指向 `Res/Terrain/StrataCave/Style.asset`（GUID `b8f1f94057451ec459e1bac28aba1c8a`）。因此无需 `--dn-contour-static` 就使用新版岩层和三层背景。两个新版工作台使用同一组定义、样式和正式地图配置；正式远征有自己的权威会话与玩法，地图是否逐格相同取决于星球和种子。

Play 模式下，正式会话在场景 `EntityViews` 下建立运行时分组：活实体按 `Actors/<类型>`、`Buildings/<类型>`、`Worksites/<类型>` 与 `Mineral Deposits` 查找；`Ballistics` 集中放置预热的 128 个投射物表现，箭矢、其他效果、残骸、音频及建造预览各有独立节点。收起 `Ballistics` 可直接在 `Actors/worker` 查看工人。此分组只整理 Hierarchy，不改变对象身份、权威状态或关卡布局；代码已接入，Unity Play 画面尚未验收。

最新代码 `CaveTerrainStyle.ImmediateForeground` 默认 `true`，当前 Style 没有序列化覆盖此字段；`CaptureModifiers()` 经 `AsLocalForeground()` 将前景圆簇转换为 LocalV2。源 RoundedRock 资产的版本仍可为 LegacyV1，不能仅看源资产字段或旧文档判断运行时版本。近期输入、只读副本安装和相机完成判定也由正式 `RandomLevelEntry → TerrainPreview` 共用；这些属于**代码已接入**，不代表整项已验收。

09-26 既有 Local Unity [原始 XML](evidence/terrain-local-validation-20260926.xml)／[结果摘要](evidence/terrain-local-validation-20260926.json)为 **32 通过、2 失败**：`ActualPagesStayStaticAndDestructionIsLocal` 在只读 chunk 编辑时报错；`PreviewKeepsCameraPagesVisibleAcrossDirectionChanges` 缺少相机所需页。新视觉用例和严格跨资源发布门槛未完成，前台性能及新 Player 联机不能宣称通过。本次整理入口不处理这些算法问题，也没有切换美术参数或生成 Player。

| 用途 | 路径（相对 `Game/Assets`） | 入口与边界 |
| --- | --- | --- |
| 产品启动 | `Scenes/Bootstrap.unity` | 正式启动入口；不移动、不改类型名 |
| 正式旧营地 | `DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity` | 已有内容及兼容回归 |
| 随机关卡模板 | `DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity` | 由正式选图入口使用，与上项同名但 GUID 不同 |
| 正式远征 | `DarkNights/Res/Scenes/Expedition/Expedition.unity` | 当前产品入口 |
| 正式生成工作台（原固定样板场景） | `DarkNights/Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` | `Dark Nights/工作台 → ReferenceChamber 生成工作台`；名称与 GUID 保留，地图改从正式配置生成 |
| 正式生成工作台 | `DarkNights/Res/Scenes/Workbenches/Terrain/RandomCave.unity` | `Dark Nights/工作台 → RandomCave 生成工作台`；同一配置、星球和种子得到同一地图 |
| (old) 天然洞穴实验 | `DarkNights/Res/Scenes/Workbenches/Terrain/(old)/CaveExploration(old).unity` | `Dark Nights/Terrain/(old)/打开天然洞穴实验`；旧 CaveExploration 定义与样式 |
| (old) 随机地图 Debug | `DarkNights/Res/Scenes/Workbenches/Terrain/(old)/TerrainDebugBootstrap(old).unity` | `Dark Nights/Terrain/(old)/打开随机地图 Bootstrap`；旧八房间生成器与测试图集 |
| DualGrid 单机测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainTest.unity` | 地图预览和专用测试 Player |
| DualGrid 联机测试 | `DarkNights/Res/Scenes/Tests/Terrain/TerrainNetworkTest.unity` | 独立网络探针与专用测试 Player |

## 删除待定（未删除）

| 场景（相对 `Game/Assets`） | 原位置 | 判断与处置 |
| --- | --- | --- |
| `DarkNights/Res/Scenes/PendingDeletion/Terrain/CaveContourStatic.unity` | `DarkNights/Res/Terrain/CaveContourStatic/CaveContourStatic.unity` | 初批“旧前景＋新背景”阶段性对照，已被独立 `StrataCave` 样板替代且视觉未达标；不在 Build Settings，当前产品及菜单无入口。已连同 `.meta` 移入删除待定，GUID `83de985c436433b4993b518d1e2fee37` 不变。旧的一次性 SetupContour 工具改为在此路径创建／打开，不作为正式入口。 |

此处只是隔离候选，仍可在 Unity 中打开或按原 GUID 移回；没有实际删除。`Res/Terrain/CaveContourStatic/` 中的配置、美术等资源没有随场景移动，是否独占和是否可删尚未核实，须单独确认。后续实际删除要重新核对引用并获得明确授权。

`CaveExploration` 与 `TerrainDebugBootstrap` 已归组为 `(old)`，仅保留历史场景和引用。当前 `TerrainDebugBootstrap.cs` 要求正式 `MapAssemblySource`，其中旧 `TerrainDebugBootstrap(old)` 场景没有该绑定，不能作为当前可运行地图入口；没有改动其源资产或 GUID。`TerrainTest` 与 `TerrainNetworkTest` 仍是地图／网络探针，有专用构建入口；旧 `Pinewatch` 用于已有内容和营地回归。`Assets/Scenes/SampleScene.unity`、URP 模板和 `Assets/Samples/` 保持原样。

现有 Build Settings 五项（Bootstrap、SampleScene、Pinewatch、RandomPinewatch、Expedition）保持原样；工作台与地形测试场景由明确的菜单或测试构建入口选用，不加入正式构建列表。

编辑器代码的地形场景路径统一在 `Scripts/Editor/Terrain/TerrainScenePaths.cs`；新增同类场景时先选 `Workbenches` 或 `Tests`，不要再把 `.unity` 放进配置／贴图文件夹。历史证据中的旧路径保留当时事实，不应当作当前打开路径。

09-23 历史整理共移动 7 个场景及 `.meta`，当时定向场景回归 3/3、ArchitectureGuard 488 文件／12 自测／0 错误。本次 09-26 只归组上述两个旧场景，验收结果另列，不复用历史数字。

09-26 本次 Unity 6000.4.9f1 编译及定向 EditMode **4/4 通过**：7 个场景 GUID、正式构建列表边界、新版工作台引用、正式远征实际样式与 LocalV2 捕获，以及旧场景只读重开。两个被移动的 `.unity` 和 `.meta` 对照移动前 Git blob 字节完全一致；正式场景、Build Settings 和美术资产未改。Unity 留在新版 `ReferenceChamber`，未自动进入 Play。[原始 XML](evidence/terrain-scene-catalog-20260926.xml)与[摘要](evidence/terrain-scene-catalog-20260926.json)保存本批结果。仅 203 字节已消费请求文件移入 `artifacts/待清理/20260926-terrain-scene-entries/` 并列清单，没有删除产物，归档不计释放空间。
