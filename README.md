# Dark Nights · Unity

当前接续：[U6 性能修正](docs/YYGC_UNIFIED_PERFORMANCE.md)锁定 YYGC `745f3d2`；`a7bb926` 的正式 Mono 347 项、Sample 60 项通过。长测发现容量投影积压，已接入协议 7 有界压缩并通过最终 144 项 Editor／Play，新正式 Player 待验。用户选择暂缓前台验收。下文按输入保留历史证据，不表示性能已签署。

2026-09-13 已采用 YYGC 统一对象架构：业务 Behaviour／State 接管运行实体，Core 只保留纯算法与数据合同；允许针对 YYGC 能力限制或 BUG 升级适配，不做旧数据兼容。实施分支为 `codex/yygc-unified-object-migration`，见[分阶段重构执行计划](docs/YYGC_UNIFIED_REFACTOR_PLAN.md)。**U0–U5 已完成，框架锁定 8faf74f；134 项 Editor／Play 与 1,043 项纯计算回归通过。U6 最终 Mono 已从干净源码构建，同一产物通过 347 项自动检查；容量性能和被自动审批拦截的临时目录清理仍未完成，M5 不签署完成。** Player 在 `artifacts/yygc-unified/u6/player-mono`，详见[实施记录](docs/YYGC_UNIFIED_IMPLEMENTATION.md)和[U6 证据](docs/evidence/yygc-unified-u6.json)。下文按日期保留历史验收状态。

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
