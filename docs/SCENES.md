# Unity 场景索引

2026-10-02 当前正式场景入口已聚合到 [Dark Nights 工作台](EDITOR_WORKBENCH.md) 的“场景”分组。

2026-09-30。**`ReferenceChamber`、`RandomCave` 与 `Cave Wall Tuner` 均预览同一正式星球地图生成链；正式游戏从 `Bootstrap → Expedition` 进入。** 两个工作台场景保留原名称与 GUID，通过 `Dark Nights / 工作台 → 场景` 打开后直接 Play；地图输入都来自 `WorldSession.asset` 的洞穴配置、星球和种子。

地形配置、地图格子、Prefab 和贴图继续留在各自的 `Res/Terrain/` 资源目录。旧场景通过 Unity AssetDatabase 移入 `(old)` 并在文件名追加 `(old)`，保留原 `.meta`／GUID 和场景内容。

新版工作台 Play 左栏现已接入[运行时 Cave Wall Tuner 功能](RUNTIME_TERRAIN_TUNER.md)：地图／岩壁／背景／显示／状态五页，包含造型子页、草稿应用取消、临时拆填及自适应缩放。独立编辑器窗口继续保留。

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
