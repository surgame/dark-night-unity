# Dark Nights · Unity

2026-09-21 新增[远征营地玩法评估与执行方案](docs/EXPEDITION_CAMP_EXECUTION.md)，分支 `codex/expedition-camp-plan`，基于 main `f28bb7d`。规划紧凑洞室、背景墙矿物与透岩矿光，再分阶段接入采矿返船、机器人展开、中继作业、撤离与舱段成长。本轮仅评估和文档，玩法／地图修改尚未实施，协议 11／存档 v7 不变。

2026-09-20 最新[洞穴地图工作台](docs/CAVE_WORKSHOP.md)已提供新岩层美术、独立坡形碰撞与行走／破坏测试。打开 `Dark Nights/Debug/打开天然洞穴实验` 后 Play；Tab 切换观察。它仍是独立测试入口，正式开局／存档尚未切换。[视觉与空间目标](docs/CAVE_EXPLORATION_TARGETS.md)保留原始计划与未完成门槛。

2026-09-20 新实验分支 `codex/cave-exploration-art` 合入 main `6b7b74c` 的手持装备，保留地图手采、矿床与恢复；协议 **11**／存档 **v7**。新增[天然洞穴技术原型](docs/CAVE_EXPLORATION_ART.md)：10–13 个不规则洞室、双入口、回环与部分掩埋通路，独立 Debug 场景可 Play。**新像素美术、独立斜面块与匹配碰撞仍未完成**，当前旧图集只作技术预览；完整游戏策划未扩入本轮。以下记录均为各自历史切片，不替代本批证据。

2026-09-19 当前地图改造验收只保留手采、有限炸破、矿室矿床、最终地图同步与保存恢复；钻机、无人机、自动采矿和自动物流整链已从产品代码、资产及本轮合并条件删除。地形命令使用服务端 `CommitId` 权威提交，`RequestId` 在完整玩法授权前幂等缓存；当前协议 10、存档 v6。主 Unity `6000.4.9f1` 刷新、完整 Editor **196/196**、Core **1048/1048**、Terrain 24 向量／100 seed、ArchitectureGuard **372/12/0** 和 Mono 启动 **6/6** 已通过；同一 Mono 的正常网络与 `200 ms RTT + 5% loss + 25 ms jitter` 各 **18/18**，覆盖 Host＋Client、LateJoin、重连、幂等、局部刷新及真实写盘重启恢复。M6 价值比例、前台性能、IL2CPP、双机和真正新机器依赖恢复不在本批已完成范围。
2026-09-20 完成[主角手持装备开发](docs/HERO_HANDHELD_EQUIPMENT.md)：三套分层 Aseprite 像素素材与图标，手枪／128 槽投射物池、蓄力抛物线黏性炸药、仅动作矿镐及底部四槽栏。复用 YYGC 输入／状态和 R3 表现链，协议 10／存档 v5。基于本次最新 main `b96d8dc`，Unity 6000.4.9f1 编译通过；**首批主角测试 22/22 通过；全批 77 通过、1 加载超时、120 未完成，尚未构建 Player**，后续按[冒烟／回归文档](docs/HERO_HANDHELD_TEST_PLAN.md)执行。YYGC 仍为 `12b253c`；保留本机漂移的 AnyRules 缓存，新隔离缓存恢复原锁并核对 442 文件。

2026-09-17 新增独立[随机地图 Debug Bootstrap](docs/TERRAIN_DEBUG_BOOTSTRAP.md)：菜单 `Dark Nights/Debug/打开随机地图 Bootstrap` 后直接 Play，角色出生于随机入口洞室，WASD 穿墙飞行，镜头近距离跟随且可调；参数实时重建、换种子与 8 房间定位。调试地图没有正式营地的 72 列平地覆盖，原 Pinewatch 场景保留。

2026-09-17 默认正式入口已接入[随机灰松谷](docs/RANDOM_PINEWATCH.md)：保留原 Pinewatch，新增独立随机关卡模板，菜单选择地图时后台生成并在开局复用。正式协议 9／存档 v4，主角查询权威地形，地图完整同步后才 Ready；保存恢复最终格子。新 Mono 位于 `artifacts/random-map/player-mono/DarkNights.exe`，操作和本批验收见切片文档；以下记录保留各历史构建身份。

2026-09-17 修复 Bootstrap 因 `TerrainEditCommand` 漏注册而中断、界面空白的问题：YYGC 独立修复锁定 `12b253c`，通过真实生成器补齐 Tag 3，原游戏编号保持。新增 Editor 2/2、实际主菜单／开局画面、新 Mono 启动 6/6 与双进程会话 13/13 通过；产物 `artifacts/bootstrap-registry/player-mono`。详见[改动账本](docs/YYGC_CHANGES.md)及[证据](docs/evidence/bootstrap-registry-2026-09-17.json)。其余历史验收与 M5 边界不变。

2026-09-16 新增独立的[地图生成与可破坏地形基座](docs/TERRAIN_GENERATION.md)，分支 codex/dualgrid-map-generator：八类测试材料、AnyRuleD DualGrid 配置、六类地表／洞穴生成和 Editor 导出工具。地图使用单一权威状态、区块同步和本地页面渲染，静态地图不持续重建或发布。正式采矿、人物地形碰撞与地图存档接线仍待后续；不替换 Pinewatch 或改变下方正式玩法验收结论。

2026-09-16 已完成[默认主角入口收尾](docs/HERO_INPUT_EXECUTION.md)：协议仍为 8、存档仍为 v3，YYGC 锁定提交仍为 `0c7cec0`。每个有权限的玩家首次完成完整投影 Ready 时，服务端直接新建一名专属默认村民并接管，不再从场景现有闲置村民中选择；重复 Ready 不重复生成，重连上线生成新人，HostOnly 回到 SharedCamp 则恢复原专属人物。产品默认移除会遮挡画面的顶部主角工具栏，并隐藏营地建造、训练、招募、修缮入口；快捷键继续直接生效。新增跟进受影响 Editor／Play 20/20、架构守卫 316 文件／12 自测／0 错误，新 Mono 独立 Host＋客户端和实际 UI 捕获 39/39；累计 178 个不同游戏用例按影响合并通过。当前 Player 为 `artifacts/hero-input/player-mono-generated-villager-r2`（414 文件／199383292 字节）。输入 Sample 34 项及历史 350／155 项保持原构建身份，未重跑；前台性能、IL2CPP、双机器 LAN 与 M5 状态不变。

2026-09-15 完成[右键指令圈本地化与复用](docs/LOCAL_COMMAND_RINGS.md)：移除服务端圆圈事件，发起端立即显示并复用八个原生实例，暂停、换局和重连按本地生命周期处理。相关 Editor 24/24、架构守卫 294 文件／12 自测／0 错误、同一新 Mono 的启动／会话／战斗／双端隔离及弱网共 72/72 通过；四张隔离截图已复核。当前切片 Player 位于 `artifacts/local-command-rings/player-mono`。本批清理被自动审批拒绝，约 34.18 MiB 已列账保留；前台性能、IL2CPP、双机器 LAN 和 M5 状态不变。

2026-09-15 已完成 Pinewatch 场景放置收口：16 个正式对象 Prefab 根直接位于 Buildings／Worksites／Actors 分组，不再保留辅助父节点或 `VisualPreview`。`LevelPlacementMarker` 已收窄并更名为 `ScenePlacement`；内部放置身份由 Editor 自动生成、复制时自动换新且在 Inspector 只读，创建顺序唯一取自分组内 sibling 顺序，不再维护 `SpawnOrder`。实例名称与外观变体仍作为场景初值保留。冻结布局与场景结构相关用例通过，架构守卫 292 文件／12 自测／0 错误；本切片未修改 YYGC、规则、美术或 Prefab，未构建 Player，不改变性能、IL2CPP、双机器 LAN 与 M5 边界。

2026-09-14 最新完成[原生对象主视图统一](docs/NATIVE_OBJECT_VIEWS.md)：15 个正式实体 Prefab 现在各自只保留一个 `ActorView`／`BuildingView`／`WorksiteView` 主视图，删除并行 `NativeVisual` 和 `"visual"` 自绑定，Pinewatch 的 16 个放置标记已同步。Unity 编译、相关装配／资源／表现测试和架构守卫通过；最终完整批次为 155/156，唯一失败是未修改的按钮主题 EditMode 帧调度测试，单独复跑仍为 5/6。本切片没有修改 YYGC、没有生成新 Player，也不改变前台性能、IL2CPP、双机器 LAN 与 M5 未完成边界。

2026-09-14 最新完成 [Linear 世界表现与固定场景验收](docs/M5_WORLD_PRESENTATION.md)，源码 `a4a5450`：完整 Editor／Play 155/155、同一新 Mono 77/77 通过，六种局面及来宾截图已复核。当前 Player 为 `artifacts/m5-world/player-mono`，2,611 个输入和 408 个产物文件最终哈希一致。本阶段实际清理约 63.9 MiB；[255 条清理盘点](docs/STAGE_CLEANUP_INVENTORY.md)包含 8 个仍受审批限制的目标，合计约 6.49 GiB，按用户要求列账交接。**主体迁移 U0–U5 已完成；前台性能由用户暂缓，IL2CPP 和双机器 LAN 仍待条件，M5 尚未完成。**

此前同日完成[终局页面重开修复](docs/M5_RESULT_UI.md)，源码 `af29950`：完整 Editor／Play 155/155、新 Mono 终局／启动／双进程会话 40/40 通过，四张胜负页截图已复核。该批 Player 为 `artifacts/m5-results/player-mono`，408 个文件哈希一致；调试目录清理被自动审批拒绝，未重试。该批未覆盖的固定世界画面对照已由上方最新切片补齐；历史产物和计数不与新批次混用。

此前同日完成 [M5 原生 UI 校准](docs/M5_UI_CALIBRATION.md)，源码提交 `2fff198`：155 项 Editor／Play 按影响合并通过（首轮 154/155，相关 22/22 复验），真实鼠标 19/19，Mono 相关 31 项通过。该批 Player 为 `artifacts/m5-ui/player-mono`，十张截图、408 个文件及哈希按历史输入保留，不与后续构建混计。

协议 7 性能批次：[U6 性能修正](docs/YYGC_UNIFIED_PERFORMANCE.md)锁定 YYGC `745f3d2`；游戏 `4e3798f` 已通过 144 项 Editor／Play、正式 Mono 完整矩阵 350 项及同产物 240 秒摘要容量检查 21 项。48 组容量抽样最大落后 0.3 秒，未再复现此前持续积压；Sample 复用 `a7bb926`／同框架的 60 项证据。用户选择暂缓前台验收，性能仍未签署。

2026-09-13 已采用 YYGC 统一对象架构：业务 Behaviour／State 接管运行实体，Core 只保留纯算法与数据合同；允许针对 YYGC 能力限制或 BUG 升级适配，不做旧数据兼容。实施分支为 `codex/yygc-unified-object-migration`，见[分阶段重构执行计划](docs/YYGC_UNIFIED_REFACTOR_PLAN.md)。**U0–U5 已完成；U6 的本机功能矩阵和后台容量观察通过，前台性能及受自动审批拦截的清理仍有待办，M5 不签署完成。** 该批性能 Player 保留在 `artifacts/yygc-unified/u6/player-mono-compressed`，详见[协议 7 证据](docs/evidence/yygc-unified-u6-compression.json)。[实施记录](docs/YYGC_UNIFIED_IMPLEMENTATION.md)和下文按输入、日期保留历史验收状态，不混用旧构建结果。

2026-09-13 已完成 [Definition 场景入口修复](docs/SCENE_DEFINITIONS.md)：统一 Loader 引用、定义分类和静态视图接管，删除手填映射表。编译完成，测试回归按用户要求待确认；当前 YYGC 锁定 `ccd61e0`，不沿用旧批次通过记录宣称本次已验收。

《Dark Nights》Unity 移植工程。灰松谷规则、15 类原生对象、UGUI、正式四人联机、十槽位存档和重连主体已实现。2026-09-12 验收续跑已完成干净 Mono 构建、活跃存档恢复、并发、九组四进程弱网、三夜及容量上限检查。画面复核发现字体与新增菜单控件需要校准，性能采样仍有证据缺口；IL2CPP 和双机器 LAN 分别待验收，M5 尚未完成。见[本批验收与产物](docs/MONO_ACCEPTANCE.md)和[后续清单](docs/M5_EXECUTION.md#mono-acceptance)。

2026-09-12 C 重构最终 Ready 修复后的同一 Mono 产物已完成复验：并发 13/13、活跃加载 13/13、九组四进程弱网各 22/22、战斗晚加入 9/9、三夜 17/17、容量上限 13/13 均通过。容量窗口在 30.56 秒内发布增加 298，超过要求的 150；本批没有重新构建，也不改变 M5 的性能、IL2CPP 与双机器待验收边界。见[C 重构实施记录](docs/C_REFACTOR_IMPLEMENTATION.md)和[复验证据](docs/evidence/c-refactor-final-matrix-2026-09-12.json)。

2026-09-11 更新[完整移植方案](docs/MIGRATION_PLAN.md)：保留 C# 权威规则核心，接入 YYGC 对象、命令和状态链，使用原生 Prefab / UGUI；默认共享控制并保留 HostOnly 开关。配置、[核心规则迁移](docs/CORE_MIGRATION.md)、[原子存储](docs/SAVE_FORMAT.md)、M0 探针、[权威会话业务层](docs/SESSION_AUTHORITY.md)及[冻结展示副本／时钟](docs/SESSION_PROJECTION.md)已有实现和验证；网络／Entry、文件编排与 UI 已在后续批次接通。后续按[直达 M5 的连续执行路线](docs/M5_EXECUTION.md)推进，见[当前进展](docs/DEVELOPMENT.md#implementation-progress)。

目录设计已收口为 `Assets/DarkNights/Scripts` 与 `Res` 分离；代码采用 Core、Runtime、View、Entry，资源按对象／面板集中维护定义、Prefab 和专用资源。Addressables 不要求游戏素材目录采用特殊名称，仍通过 ObjectDefinition 驱动加载与绑定；现有 AddressableAssetsData 配置位置保留。详见[目录及绑定要求](docs/MIGRATION_PLAN.md#directory-and-assets)。目前已建立四个运行程序集、Editor/Tests、配置与 Pinewatch 场景、15 类原生对象、效果与五页 UI。

`Game/` 是 Unity 宿主，[ProjectVersion](Game/ProjectSettings/ProjectVersion.txt) 为 `6000.4.9f1`。先运行 `tools/prepare-lan-sample.ps1` 与 `tools/prepare-fishnet.ps1` 准备锁定提交的 `.deps/YYGC-unified`／`.deps/FishNet`，不直接引用用户维护的框架工作区。FishNet 4.7.2 带断线分片清理补丁，见[恢复接入](docs/NETWORK_RECOVERY.md)。R3、MemoryPack、UniTask 等依赖已导入；VitalRouter 使用 YYGC 要求的完成语义修正版。内部产品名暂保留 `DNights`。

原评估日期：2026-09-10；Sample 验证日期：2026-09-11。当时分支为 `main`，当前迁移分支见上方。早期评估未修改 `D:\Developer\YYGC`、Godot 基线或参考素材；2026-09-12 的 Workshop 展示修复已同步 YYGC，逐文件记录见[改动账本](docs/YYGC_CHANGES.md#workshop-display)。下方评估输入表保留历史时点。

## 核心判断

YYGC 适合作为应用、表现与联机基础：已有启动编排、DI、ObjectDefinition/ObjectInstance/ObjectView、UGUI、本地交互仲裁、网络命令、服务端状态发布和单对象首次快照。[早期能力复评](docs/YYGC_REASSESSMENT.md)说明修正缘由，当前 Sample 记录了修正后可复用的命令与状态路径。

保留普通 C# 模拟，由房主唯一结算世界；游戏补营地权限、业务去重、投影、Ready、epoch 和恢复流程。首个切片用会话级 StatefulBehaviour/StateSynchronizer 同步可靠完整投影，测量后再决定分块与拆流。角色、建筑和资源点使用 YYGC 本地对象视图与 Unity Prefab。各玩家独立选择，SharedCamp / HostOnly 由服务端统一校验，关闭共同操作不会改变模拟或单位所有权。

早期复评的生成器、首状态和序列化问题属于当时版本。新 Sample 已在 YYGC `10b8f0e` 上复用完整命令与状态链，通过真实多进程验证；独立程序集补丁、依赖和未验收边界见 [Sample 说明](docs/LAN_SAMPLE.md)。联机尚未正式生产使用。

早期 M0／M1 时点的剩余工作估算为 16–26 人日，预留后约 20–33 人日；该历史估算不代表当前剩余工作。当前功能主体已实现，余项为新 Mono 构建、画面对照、活跃状态恢复、普通 Player 性能与最终交付验收，详见[收尾状态](docs/M5_EXECUTION.md#closeout)。

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

2026-09-11 开工准备：已接入官方 Unity MCP（CLI `1.0.0-beta.9`／Pipeline `0.6.0-exp.1`），通过 `6000.4.9f1` Editor 编译、stdio 调用和重载复查；新增依赖后重建 Mono／IL2CPP，四进程基础与弱网检查共 120 项通过。正式玩法仍未迁移，见[接入与兼容性记录](docs/DEPENDENCIES.md#官方-unity-mcp-开发工具)。

| 要解决的问题 | 文档 |
|---|---|
| YYGC 统一对象路线、框架升级、旧模型退出与分阶段验收 | [统一重构执行计划](docs/YYGC_UNIFIED_REFACTOR_PLAN.md) |
| Linear 世界表现修正、固定场景与最新 Mono 证据 | [世界表现验收](docs/M5_WORLD_PRESENTATION.md) |
| 编译缓存、旧构建、依赖解包和受限清理清单 | [阶段清理交接](docs/STAGE_CLEANUP_INVENTORY.md) |
| 本次移植总方案、YYGC 对应、可关闭共享控制和第一步 | [移植方案](docs/MIGRATION_PLAN.md) |
| 立即试用 LAN 模板、R3/VitalRouter 约束、四进程证据 | [LAN Sample](docs/LAN_SAMPLE.md) |
| 早期框架缺口、修正缘由及复用边界 | [YYGC 能力复评](docs/YYGC_REASSESSMENT.md) |
| YYGC 哪些可复用、哪些需要修正或验证 | [框架评估](docs/FRAMEWORK_REVIEW.md) |
| Unity 版本、包依赖、生成器与构建准备 | [依赖与环境](docs/DEPENDENCIES.md) |
| YYGC 当前 GUID / Key、旧整数兼容与网络 V1 / V2 | [定义身份指南](<D:/Developer/YYGC/Documentation~/DEFINITION_IDENTITY.md>) |
| 状态归属、程序集与职责目录 | [技术架构](docs/ARCHITECTURE.md) |
| 命令权限、同步、晚加入、重连、暂停与存档 | [联机设计](docs/MULTIPLAYER.md) |
| 阶段、难度、工期、验收与剩余决策 | [开发执行计划](docs/DEVELOPMENT.md) |
| 人工接手从哪里开始 | [Quick start](docs/QUICK_START.md) |
| 已检查与未检查的边界 | [评估状态](docs/ASSESSMENT_STATUS.md) |
| 自动化协作与编码要求 | [AGENTS.md](AGENTS.md) |

## 评估输入

| 输入 | 基线 |
|---|---|
| `D:\Developer\YYGC` | HEAD `6c3e0ff96221ac4a9fdfe0db85bf8f2cdc8dabc9` 加评估时工作区 |
| `../projects` | HEAD `91cb09ff0894f26134f07fd544f1273d9fe7ffaa`，工作区干净 |
| YYGC 用户未提交内容 | 已暂存 `Runtime/UI/UGUI/UGUIManager.cs`；未跟踪 `Tools/IDRegistry备份数据20260812` |
| 实际 Unity Editor | `D:\Program Files\Unity 6000.4.9f1\Editor\Unity.exe`；YYGC 包声明 `6000.2` + `35f1`，已在 6000.4.9f1 编译通过 |

精确规模、关键源文件 SHA-256、原始素材核验及历史 Git 状态见[冻结评估证据](docs/evidence/assessment-2026-09-10.json)。Godot 基线保持该提交；当前 YYGC 锁定 `ccd61e0` / `0.3.0-preview.1`，包含 Workshop 展示修复与本次 Definition 场景入口修复；后者测试回归待确认。隔离依赖、验证边界及原有运行补丁见[依赖说明](docs/DEPENDENCIES.md)，不将预览版本写成已发布稳定版本。

## 本仓库当前可执行的检查

在本目录使用 PowerShell 7：

```powershell
pwsh -NoProfile -File .\tools\collect-assessment.ps1 -FrameworkPath 'D:\Developer\YYGC' -GamePath '..\projects'
git status --short
```

采集脚本只读两个输入目录，输出到忽略的 `artifacts/assessment-current.json`；不启动 Unity，不修改框架或游戏。冻结证据保留原评估时点，常规重跑不会覆盖。

正式 Bootstrap 场景保留，AppStartup 已接入规则加载模块；全局对象定义与命令／状态注册表仍保持环境基线。Sample 使用自己的定义、StateData、NetworkCommand、Prefab 和场景。`Library/`、构建输出、`.deps/` 和日志不提交，冻结验证摘要在 `docs/evidence/`。
