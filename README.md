# Dark Nights · Unity

《Dark Nights》Unity 移植工程。环境基线与独立 [LAN 合作 Sample](docs/LAN_SAMPLE.md) 已完成，Windows Mono 和 IL2CPP Release＋High 裁剪均有四进程及真实 UDP 弱网验证记录。灰松谷权威规则、旧档核心、可编辑布局来源及首批 WorldSession／Worker 正式对象合同已迁移并通过 Editor 与双后端 Player 检查；完整对象、美术、正式可玩场景与正式联机尚未完成。

2026-09-11 更新[完整移植方案](docs/MIGRATION_PLAN.md)：保留 C# 权威规则核心，接入 YYGC 对象、命令和状态链，使用原生 Prefab / UGUI；默认共享控制并保留 HostOnly 开关。配置、[核心规则迁移](docs/CORE_MIGRATION.md)、[原子存储](docs/SAVE_FORMAT.md)、M0 探针、[权威会话业务层](docs/SESSION_AUTHORITY.md)及[冻结展示副本／时钟](docs/SESSION_PROJECTION.md)已有实现和验证；网络／Entry 接线、文件编排与 UI 仍待完成。后续按[直达 M5 的连续执行路线](docs/M5_EXECUTION.md)推进，见[当前进展](docs/DEVELOPMENT.md#implementation-progress)。

目录设计已收口为 `Assets/DarkNights/Scripts` 与 `Res` 分离；代码采用 Core、Runtime、View、Entry，资源按对象／面板集中维护定义、Prefab 和专用资源。Addressables 不要求游戏素材目录采用特殊名称，仍通过 ObjectDefinition 驱动加载与绑定；现有 AddressableAssetsData 配置位置保留。详见[目录及绑定要求](docs/MIGRATION_PLAN.md#directory-and-assets)。目前已建立四个运行程序集、Editor/Tests、配置与 Pinewatch 场景，以及 Worker／WorldSession 对象目录；其余对象、UI 与美术按功能扩展。

`Game/` 是 Unity 宿主，[ProjectVersion](Game/ProjectSettings/ProjectVersion.txt) 为 `6000.4.9f1`。先运行 `tools/prepare-lan-sample.ps1` 与 `tools/prepare-fishnet.ps1` 准备锁定提交的 `.deps/YYGC`／`.deps/FishNet`，不直接引用用户维护的框架工作区。FishNet 4.7.2 带断线分片清理补丁，见[恢复接入](docs/NETWORK_RECOVERY.md)。R3、MemoryPack、UniTask 等依赖已导入；VitalRouter 使用 YYGC 要求的完成语义修正版。内部产品名暂保留 `DNights`。

原评估日期：2026-09-10；Sample 验证日期：2026-09-11。本仓库分支为 `main`。本次未修改 `D:\Developer\YYGC`、Godot 基线或参考素材；下方评估输入表保留历史时点。

## 核心判断

YYGC 适合作为应用、表现与联机基础：已有启动编排、DI、ObjectDefinition/ObjectInstance/ObjectView、UGUI、本地交互仲裁、网络命令、服务端状态发布和单对象首次快照。[早期能力复评](docs/YYGC_REASSESSMENT.md)说明修正缘由，当前 Sample 记录了修正后可复用的命令与状态路径。

保留普通 C# 模拟，由房主唯一结算世界；游戏补营地权限、业务去重、投影、Ready、epoch 和恢复流程。首个切片用会话级 StatefulBehaviour/StateSynchronizer 同步可靠完整投影，测量后再决定分块与拆流。角色、建筑和资源点使用 YYGC 本地对象视图与 Unity Prefab。各玩家独立选择，SharedCamp / HostOnly 由服务端统一校验，关闭共同操作不会改变模拟或单位所有权。

早期复评的生成器、首状态和序列化问题属于当时版本。新 Sample 已在 YYGC `10b8f0e` 上复用完整命令与状态链，通过真实多进程验证；独立程序集补丁、依赖和未验收边界见 [Sample 说明](docs/LAN_SAMPLE.md)。联机尚未正式生产使用。

整体难度为中高。扣除已完成的 M0 和大部分 M1 后，剩余工作暂估 **16–26 人日，预留后约 20–33 人日**；按一名熟悉 Unity/C# 的全职开发者约 4–7 工作周。估算含正式集成和验收，不含公网中继、平台接入与房主迁移，需在正式联机切片取得投影测量后校正。详见[执行计划](docs/DEVELOPMENT.md)。

## 范围与假设

| 项目 | 状态 |
|---|---|
| 2–4 人合作，共享一个营地 | 用户已确认 |
| 灰松谷、三夜、既有素材与规则 | 本次内容基线 |
| Windows 桌面、房主主持、先局域网／直连 | 方案与估算假设；公网入口尚未确认 |
| 客户端独立镜头、选择与建造预览 | 联机设计要求 |
| 默认共享控制，可切仅房主操作 | 本次方案；关闭后普通玩家只观看／查看，服务端统一限制直接命令与自动派工 |
| 暂停、倍速、提前入夜、存档权限 | 会话业务层已实现时间及加载权限；保存命令、产品入口与网络接线待完成 |
| 全新地图、PVP、锁步、回滚、房主迁移 | 本轮估算范围之外 |

## 阅读入口

2026-09-11 开工准备：已接入官方 Unity MCP（CLI `1.0.0-beta.9`／Pipeline `0.6.0-exp.1`），通过 `6000.4.9f1` Editor 编译、stdio 调用和重载复查；新增依赖后重建 Mono／IL2CPP，四进程基础与弱网检查共 120 项通过。正式玩法仍未迁移，见[接入与兼容性记录](docs/DEPENDENCIES.md#官方-unity-mcp-开发工具)。

| 要解决的问题 | 文档 |
|---|---|
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

精确规模、关键源文件 SHA-256、原始素材核验及历史 Git 状态见[冻结评估证据](docs/evidence/assessment-2026-09-10.json)。本次已核对 Godot 仍为该提交；当前 YYGC 为 `10b8f0e` / `0.3.0-preview.1`，其隔离依赖与补丁来源见 Sample，不将预览版本写成已发布稳定版本。

## 本仓库当前可执行的检查

在本目录使用 PowerShell 7：

```powershell
pwsh -NoProfile -File .\tools\collect-assessment.ps1 -FrameworkPath 'D:\Developer\YYGC' -GamePath '..\projects'
git status --short
```

采集脚本只读两个输入目录，输出到忽略的 `artifacts/assessment-current.json`；不启动 Unity，不修改框架或游戏。冻结证据保留原评估时点，常规重跑不会覆盖。

正式 Bootstrap 场景保留，AppStartup 已接入规则加载模块；全局对象定义与命令／状态注册表仍保持环境基线。Sample 使用自己的定义、StateData、NetworkCommand、Prefab 和场景。`Library/`、构建输出、`.deps/` 和日志不提交，冻结验证摘要在 `docs/evidence/`。
