# Dark Nights · Unity

2026-10-04 [斜坡跳跃连续运动](docs/SLOPE_JUMP_FLOW.md)已由 `fix-20261004-slope-jump-flow` 快进合入本地 `main`，代码提交 `a711897`，本轮未推送：连续斜向运动、真实接坡与点跳输入保留，固定／按住控制跳高策略默认固定，普通跳跃之后接续喷气。协议23／AMP1 schema2不变，规则摘要v5／存档v17；首轮Editor66/66，最终源码33/33，后续跨缺口边界修正的Editor补测待执行。YYGC未改，不宣称最终手感、Player或联机验收。

2026-10-03 [工作台原生聚合](docs/EDITOR_WORKBENCH.md)：四类项目任务按需停靠原生编辑器，退出重复 Definition 浏览器和内嵌编辑；场景收为快捷行与折叠目录。获批 YYGC 导航标脏修正已接入，Editor 按不同用例合并16/16、作者资产436项保持；沿用当前分支。

2026-10-03 [氧气业务移除](docs/OXYGEN_REMOVAL_EXECUTION.md)：ref-20261003-remove-oxygen合入本地main，协议 **23**／存档 **v16**／AMP1 schema **2**，YYGC未修改。原后台Editor119/121、原生资源12/12、正常Mono双进程130/130；两项未实现玩法的旧路线用例已移除，本次剩余远征回归6/6。弱网仍失败并延后修复，**整批未通过**，见[验证记录](docs/OXYGEN_REMOVAL_VALIDATION.md)及[失败原因](docs/OXYGEN_REMOVAL_FAILURE_ANALYSIS.md)。前台画面、四人、IL2CPP与双机器不宣称通过。

2026-10-03 [天空与矿洞分层衔接](docs/CAVE_ENTRANCE_ART_LAYERS.md)：复用原近／中／远三层点缀，RGBA 不变；独立绘制及完整后壁覆盖替代 Shader 入口渐隐。没有新 PNG 接入。专项 Editor 36/36、Mono 构建与正式 Play 通过；独立进程及弱网边界见本批证据。

2026-10-02 [快速测试与场景整理](docs/QUICK_TEST_SCENES.md)：主菜单 Debug Hub 一键进入已着陆矿镐局，记住本机选择；6 场景保留 GUID／字节迁移，16 场景完整归类。Editor 规则 21/21、真实主菜单 Play 1/1、快捷键与实际 GUI 点击 1/1 通过，跨 Editor 重启记忆已验。协议 22／存档 v15 不变；没有新 Player 或多人快速局证据。

2026-10-02 [Definition 与 Editor 复评](docs/DEFINITION_EDITOR_REVIEW.md)：已核对全部 44 个定义，列出 8 组运行能力缺口；首批修复 6 项 Editor 保存、状态恢复和生命周期问题。专项 11/11、作者资产与生命周期探针 52/52；手枪／炸药、喷气背包等能力迁移仍按清单推进。

2026-10-02 [统一编辑工作台](docs/EDITOR_WORKBENCH.md)：日常配置、预览与正式场景集中到 `Dark Nights / 工作台`，按用途搜索和定位原始资产；复杂编辑继续复用原有专用窗口，Definition 与草稿的状态归属不变。仅调整 Editor 入口，协议 22／存档 v15 不变。

2026-10-02 [工具 Definition 采集重构](docs/TOOL_DEFINITION_HARVESTING.md)：矿镐能力及参数归自身 Definition，装备槽保存稳定定义身份，矿床仅描述材料与采集要求；默认仍只采前景。协议 **22**／存档 **v15**／AMP1 schema **2**。当前工具配置入口为 `Dark Nights / 工作台 → 工具与采集能力`；先前矿床开关方案已被本轮替代，历史证据保留。

2026-10-01 矿镐表面选取与持续挥镐源码候选：独立触及范围设为 **3 格／48 逻辑像素**，鼠标方向优先选最近真实岩壁表面；按住连续挥镐，每轮落镐只尝试一次伤害，不能越过外层墙。游戏协议 **20**／地图 AMP1 schema **2**／存档 **v14**。源码按用户要求提交至分支 `fix-20261001-pickaxe-surface-swing`；本批未运行测试、编译、Play 或构建。参数、保存指纹变化与待验边界见 [开发执行计划](docs/DEVELOPMENT.md)。

2026-09-30 网格业务化源码候选：正式 WorldSession Definition 增加地形 Profile 与业务装配，非基岩按耐久采集，岩壁和背景矿床目标明确分开。配置入口为 `Dark Nights / Terrain / 网格业务配置工作台`。游戏协议 **19**、地图 AMP1 schema **2**、存档 **v14**；按用户要求未运行测试、构建、Play 或联机验证，不宣称编译或 MVP 验收通过。实现边界见 [开发执行计划](docs/DEVELOPMENT.md)。

2026-09-30 [矿镐网格采集候选](docs/MINING_GRID_EXECUTION.md)：修复远征隐藏整块主角 HUD 导致网格被禁用，并为网格明确设置全视口布局。Local Unity 导入／编译及实际购镐、着陆、出舱后的显示生命周期 **14/14** 通过，专项 Editor **13/13**，按影响合并 **28/30**（两项旧手枪测试未配置初始装备，保留失败）。协议 **18**／存档 **v13**；实际合法格采矿、独立进程联机、事务与保存恢复仍待验证，未构建新 Player，旧构建不计入本批。

2026-09-29 [主角比例与移动调整](docs/HERO_MOVEMENT_SCALE.md)：主角约 3 格高，普通 7 格/秒、Shift 约 11 格/秒；船内外碰撞、工作台和步行动画同步调整，默认镜头扩大视野。协议 **17**／存档 **v13**，旧档保留，请新开局。按用户要求未运行测试或构建，实际画面和通行待验证。

2026-09-29 飞船交易修复候选：修复船内 E 打开商店后界面不可见和输入锁残留，补齐 UI Toolkit 主题与中文 TextCore 字体。协议 **17**／存档 **v12**；Editor 规则／交互和同一 Mono 双进程基本回归已执行，范围及剩余人工项见[验收记录](docs/SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md)，操作步骤见[快速复测](docs/PLAYER_GUIDE.md#ship-trade-quick-check)。

2026-09-28 原地着陆源码候选：星球降落不再要求对准唯一泊位，在飞行范围内具备安全支撑和净空即可原地着陆；游戏协议 **16**／存档 **v11**。旧 Player 与旧验收数字只代表协议 15，候选验证见[实现说明](docs/SPACE_TO_PLANET_IMPLEMENTATION.md)。

2026-09-26 [太空到星球流程](docs/SPACE_TO_PLANET_IMPLEMENTATION.md)已在 Local 导入并构建 Mono r2：太空船内走动、驾驶台选星球、配置过场、后台生成、到达同步、松手缓降与自动着陆下船。协议 **15**／存档 **v11**，UI Toolkit 菜单为 `Dark Nights/配置/星球与航程`。用户后续要求执行全部验收，现已完成 Core、航程纯回归、Editor 与实际 Play 主流程，多人／弱网等结果持续收录到[验收清单](docs/SPACE_TO_PLANET_ACCEPTANCE.md)；不以旧构建数字代替本批结果。2026-09-27 以 Editor 航程 53/53、Mono r3 双进程 127/127、IL2CPP r4 双进程及三组四人全部通过的候选合入 `main`，人工、双机器与前台性能项仍待验收。

2026-09-30 [当前地图入口](docs/SCENES.md)：正式 `Bootstrap → Expedition` 使用 StrataCave 岩层、三层背景及当前共享地形刷新代码。`ReferenceChamber`、`RandomCave` 与 `Cave Wall Tuner` 均从正式配置生成星球地图；旧人工固定格子资产保留但不再作为当前地图来源。Unity 菜单 `Dark Nights / Terrain` 可打开工作台。旧 `TerrainDebugBootstrap`、`CaveExploration` 收进 `(old)`。当前代码默认 `ImmediateForeground = true`，有效前景为 LocalV2；下方旧验收数字只代表对应版本，本次入口统一按用户要求未验证。

2026-09-25 [局部地形即时刷新执行记录](docs/archive/IMMEDIATE_TERRAIN_REFRESH_EXECUTION.md)：隔离分支实现源驱动稀疏安装和 LocalV2 分页岩壁候选；AnyRuleD Core 138/138，纯算法全图/分页遮罩、岩粒及 RGBA 页对照 10 组通过。LegacyV1 仍为默认；Unity 导入/编译、运行画面、网络场景、碰撞、V1/V2 美术门槛与前台性能未验，完整即时刷新未交付。

2026-09-24 [AnyRuleD 地图联网重构](docs/MAP_STATE_NETWORKING.md)在 `ref-20260924-map-state-networking` 开发：纯协议与 FishNet 传输已从 YYGC 业务包拆出，单格 Delta 改为 1 个最终格记录，游戏副本通知已接入范围刷新。现有 Editor 编译通过；本轮 Editor、Mono 联机及独立样板的具体结果以进度文档为准，不沿用旧 Player 数字。

2026-09-24 [地形 Modifier 与可插拔点缀层进度](docs/archive/TERRAIN_MODIFIERS_PROGRESS.md)：下坠岩齿／花菜圆簇已接入主工程，前景及三个背景层可分别配置，点缀生成器可替换。`Cave Wall Tuner` 保留样式与地图草稿 Apply／Cancel、拆填和网格；预览仅在空白离屏宿主中渲染当前地图，共用正式 `TerrainPreview`、AnyRuleD、`CaveVisualSource` 和 Cave shader，不加载正式游戏场景或角色。拆填微基准发现活动 RoundedRock 下单格全图轮廓烘焙约 281–286 ms，之后全量差异比较约 39–132 ms，是画面滞后的主要已测阶段；Unity 端到端及 Player 前台帧时仍待实测，不据微基准宣称通过。

2026-09-22 本分支已接入[可步入远征飞船](docs/archive/WALKABLE_EXPEDITION_SHIP.md)，`ft-20260922-walkable-expedition-ship`，协议 **14**／存档 **v10**。支持船内步行、唯一驾驶席、泊位附近试飞、搬运机器人与侦察机出舱归队；远征默认应用当前 StrataCave 岩层与三层背景。50 张原生素材导入检查 200/200，新增飞船／远征用例 24/24，同一 Mono 正常／弱网三进程各 28/28，Core 1048/1048；Editor 按影响合并 235/236，既有按钮主题 1 项失败留账。实际画面和本批证据见实现说明，不宣称全洞穴航行、异地降落、前台性能、IL2CPP 或双机器通过。

2026-09-22 当前候选为[独立洞穴材质与三层背景](docs/archive/STATIC_CAVE_BACKGROUND_EXECUTION.md)，分支 `codex/static-cave-background`，协议 **13**／存档 **v9**。`Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` 是 HTML 同源固定样板，同目录 `RandomCave.unity` 验证随机地图（2026-09-23 仅整理场景路径，资产仍在 `Res/Terrain/StrataCave/`）；独立 AnyRuleD 规则、原生 8px 岩壁、圆形柔光和角色比例已接入。矿粒及矿光按用户要求暂时隐藏。正式远征以 `--dn-contour-static` 试用，原风格保持默认；结果及前台性能边界见本批记录，下方为历史切片。 外轮廓为独立可配置的六方案，当前 HybridB／OUTLINE-0921／3px／20px／2px、岩块 4px；仅修饰表现，权威碰撞保持原坡形。

2026-09-21 当前切片为[远征营地 Demo 实施与快速验收](docs/archive/EXPEDITION_CAMP_DELIVERY.md)，分支 `codex/expedition-camp-plan`，协议 **12**／存档 **v8**。正式入口接入紧凑洞穴、背景墙矿物和透岩矿光、氧气与货袋、机器人四设备展开、矿工交货、风险撤收、原子结算与三种舱段成长。Editor 按影响合并 213/214 通过，1 项通用按钮主题用例留账，Mono 常规／弱网四进程各 35/35；完整证据、玩法简化和待验边界以交付记录为准，不将原[执行方案](docs/archive/EXPEDITION_CAMP_EXECUTION.md)中的完整性能及全路线门槛写成通过。

当前默认玩法为远征。地图调试通过[正式生成工作台](docs/SCENES.md)进入；旧营地仅作为 `--dn-camp-mode` 的兼容回归入口。各旧版本的验收数字留在对应切片文档中，不作为当前构建结论。过时的指令圈运行链和专用回归已删除。

目录设计已收口为 `Assets/DarkNights/Scripts` 与 `Res` 分离；代码采用 Core、Runtime、View、Entry，资源按对象／面板集中维护定义、Prefab 和专用资源。Addressables 不要求游戏素材目录采用特殊名称，仍通过 ObjectDefinition 驱动加载与绑定；现有 AddressableAssetsData 配置位置保留。详见[目录及绑定要求](docs/archive/MIGRATION_PLAN.md#directory-and-assets)。目前已建立四个运行程序集、Editor/Tests、配置与 Pinewatch 场景、15 类原生对象、效果与五页 UI。

`Game/` 是 Unity 宿主，[ProjectVersion](Game/ProjectSettings/ProjectVersion.txt) 为 `6000.4.9f1`。先运行 `tools/prepare-lan-sample.ps1` 与 `tools/prepare-fishnet.ps1` 准备锁定提交的 `.deps/YYGC-unified`／`.deps/FishNet`，不直接引用用户维护的框架工作区。FishNet 4.7.2 带断线分片清理补丁，见[恢复接入](docs/archive/NETWORK_RECOVERY.md)。R3、MemoryPack、UniTask 等依赖已导入；VitalRouter 使用 YYGC 要求的完成语义修正版。内部产品名暂保留 `DNights`。

原评估日期：2026-09-10；Sample 验证日期：2026-09-11。当时分支为 `main`，当前迁移分支见上方。早期评估未修改 `D:\Developer\YYGC`、Godot 基线或参考素材；2026-09-12 的 Workshop 展示修复已同步 YYGC，逐文件记录见[改动账本](docs/YYGC_CHANGES.md#workshop-display)。下方评估输入表保留历史时点。

## 核心判断

YYGC 适合作为应用、表现与联机基础：已有启动编排、DI、ObjectDefinition/ObjectInstance/ObjectView、UGUI、本地交互仲裁、网络命令、服务端状态发布和单对象首次快照。[早期能力复评](docs/archive/YYGC_REASSESSMENT.md)说明修正缘由，当前 Sample 记录了修正后可复用的命令与状态路径。

保留普通 C# 模拟，由房主唯一结算世界；游戏补营地权限、业务去重、投影、Ready、epoch 和恢复流程。首个切片用会话级 StatefulBehaviour/StateSynchronizer 同步可靠完整投影，测量后再决定分块与拆流。角色、建筑和资源点使用 YYGC 本地对象视图与 Unity Prefab。各玩家独立选择，SharedCamp / HostOnly 由服务端统一校验，关闭共同操作不会改变模拟或单位所有权。

早期复评的生成器、首状态和序列化问题属于当时版本。新 Sample 已在 YYGC `10b8f0e` 上复用完整命令与状态链，通过真实多进程验证；独立程序集补丁、依赖和未验收边界见 [Sample 说明](docs/LAN_SAMPLE.md)。联机尚未正式生产使用。

早期 M0／M1 时点的剩余工作估算为 16–26 人日，预留后约 20–33 人日；该历史估算及 [M5 当时的收尾状态](docs/archive/M5_EXECUTION.md#closeout)不代表当前剩余工作。当前飞船切片与未验边界见[实现说明](docs/archive/WALKABLE_EXPEDITION_SHIP.md)。

## 范围与假设

| 项目 | 状态 |
|---|---|
| 2–4 人合作，共享一个营地 | 用户已确认 |
| 灰松谷、三夜、既有素材与规则 | 本次内容基线 |
| Windows 桌面、房主主持、先局域网／直连 | 方案与估算假设；公网入口尚未确认 |
| 客户端独立镜头、选择与建造预览 | 联机设计要求 |
| 默认共享控制，可切仅房主操作 | 本次方案；关闭后普通玩家只观看／查看，服务端统一限制直接命令与自动派工 |
| 暂停、倍速、提前入夜、存档权限 | 已经统一权威入口接入产品与网络；活跃施工／训练／箭矢组合恢复仍待新 Player 验证 |
| 全新地图、PVP、锁步、回滚、房主迁移 | 本轮估算范围之外 |

## 阅读入口

| 要解决的问题 | 文档 |
|---|---|
| 当前合同、开发入口与近期切片 | [文档索引](docs/README.md) |
| 场景位置、用途与入口 | [场景索引](docs/SCENES.md) |
| 试玩与本机复跑 | [Player 指南](docs/PLAYER_GUIDE.md)、[Quick start](docs/QUICK_START.md) |
| 状态归属、权限、存档与依赖 | [技术架构](docs/ARCHITECTURE.md)、[联机设计](docs/MULTIPLAYER.md)、[存档格式](docs/SAVE_FORMAT.md)、[依赖说明](docs/DEPENDENCIES.md) |
| 历史迁移方案、框架评估、阶段验收与清理列账 | [历史文档索引](docs/archive/README.md) |
| 自动化协作与编码要求 | [AGENTS.md](AGENTS.md) |

## 评估输入

| 输入 | 基线 |
|---|---|
| `D:\Developer\YYGC` | HEAD `6c3e0ff96221ac4a9fdfe0db85bf8f2cdc8dabc9` 加评估时工作区 |
| `../projects` | HEAD `91cb09ff0894f26134f07fd544f1273d9fe7ffaa`，工作区干净 |
| YYGC 用户未提交内容 | 已暂存 `Runtime/UI/UGUI/UGUIManager.cs`；未跟踪 `Tools/IDRegistry备份数据20260812` |
| 实际 Unity Editor | `D:\Program Files\Unity 6000.4.9f1\Editor\Unity.exe`；YYGC 包声明 `6000.2` + `35f1`，已在 6000.4.9f1 编译通过 |

精确规模、关键源文件 SHA-256、原始素材核验及历史 Git 状态见[冻结评估证据](docs/archive/evidence/assessment-2026-09-10.json)。Godot 基线保持该提交；评估后的早期 YYGC 锁定曾为 `ccd61e0` / `0.3.0-preview.1`，包含 Workshop 展示修复与 Definition 场景入口修复；该时点后者的回归待确认。当前隔离依赖、验证边界及补丁见[依赖说明](docs/DEPENDENCIES.md)，不将历史预览版本写成已发布稳定版本。

## 本仓库当前可执行的检查

在本目录使用 PowerShell 7：

```powershell
pwsh -NoProfile -File .\tools\collect-assessment.ps1 -FrameworkPath 'D:\Developer\YYGC' -GamePath '..\projects'
git status --short
```

采集脚本只读两个输入目录，输出到忽略的 `artifacts/assessment-current.json`；不启动 Unity，不修改框架或游戏。冻结证据保留原评估时点，常规重跑不会覆盖。

正式 Bootstrap 场景保留，AppStartup 已接入规则加载模块；全局对象定义与命令／状态注册表仍保持环境基线。Sample 使用自己的定义、StateData、NetworkCommand、Prefab 和场景。`Library/`、构建输出、`.deps/` 和日志不提交，冻结验证摘要在 `docs/evidence/`。
