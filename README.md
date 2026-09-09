# Dark Nights · Unity

《Dark Nights》Unity 工程筹备仓库，基于 YYGC 评估单关卡与合作联机方案。当前交付框架评估、架构设计、工作量估算和开发约束；尚未生成可打开运行的 Unity 项目，未完成 Unity 编译或联机验证。

评估日期：2026-09-10。现有游戏工程和 YYGC 均只读检查。本仓库独立初始化 Git，分支为 `main`。

## 核心判断

YYGC 适合作为应用与表现层基础：已有启动编排、DI、ObjectDefinition/ObjectInstance/ObjectView、原生 Prefab 与 UGUI 工作流，以及 FishNet 命令和状态同步。接入前需完成依赖闭环、Player 编译和网络生命周期验证，不能把现有同步组件的存在等同于合作玩法已经完成。

建议保留普通 C# 模拟，由房主唯一结算世界；玩家提交明确的操作命令，通过会话级 FishNet 适配器同步营地快照。角色、建筑和资源点使用 YYGC 本地对象视图与 Unity Prefab，不逐个重写为独立结算 HP/资源的网络 Behaviour。

整体难度为中高。单关卡可验收的合作版本估算 **24–40 人日基础工作量，预留后约 30–50 人日**；按一名熟悉 Unity/C# 的全职开发者约 6–10 工作周。估算含框架适配和验证，不含公网中继、平台接入与房主迁移。详见[执行计划](docs/DEVELOPMENT.md)。

## 范围与假设

| 项目 | 状态 |
|---|---|
| 2–4 人合作，共享一个营地 | 用户已确认 |
| 灰松谷、三夜、既有素材与规则 | 本次内容基线 |
| Windows 桌面、房主主持、先局域网／直连 | 方案与估算假设；公网入口尚未确认 |
| 客户端独立镜头、选择与建造预览 | 联机设计要求 |
| 共享控制，服务端串行处理冲突 | 首版建议规则，见[联机设计](docs/MULTIPLAYER.md) |
| 暂停、倍速、提前入夜、存档权限 | 已给出明确建议，尚未实现 |
| 全新地图、PVP、锁步、回滚、房主迁移 | 本轮估算范围之外 |

## 阅读入口

| 要解决的问题 | 文档 |
|---|---|
| YYGC 哪些可复用、哪些需要修正或验证 | [框架评估](docs/FRAMEWORK_REVIEW.md) |
| Unity 版本、包依赖、生成器与构建准备 | [依赖与环境](docs/DEPENDENCIES.md) |
| 状态归属、程序集与职责目录 | [技术架构](docs/ARCHITECTURE.md) |
| 命令权限、同步、晚加入、重连、暂停与存档 | [联机设计](docs/MULTIPLAYER.md) |
| C#、数值、布局、Prefab、动画与旧档如何迁移 | [适配方案](docs/MIGRATION_PLAN.md) |
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
| 本机观测到的 Unity | `6000.2.13f1`；YYGC 包声明 `6000.2` + `35f1`，需先核实这一差异 |

精确规模、关键源文件 SHA-256、原始素材核验及 Git 状态见[冻结评估证据](docs/evidence/assessment-2026-09-10.json)。YYGC 当前工作区不等于可锁定的发布版本；后续接入必须选定已提交版本并重新验证。

## 本仓库当前可执行的检查

在本目录使用 PowerShell 7：

```powershell
pwsh -NoProfile -File .\tools\collect-assessment.ps1 -FrameworkPath 'D:\Developer\YYGC' -GamePath '..\projects'
git status --short
```

采集脚本只读两个输入目录，输出到忽略的 `artifacts/assessment-current.json`；不启动 Unity，不修改框架或游戏。冻结证据保留原评估时点，常规重跑不会覆盖。

`Assets/`、`Packages/`、`ProjectSettings/` 在执行计划 M0 完成依赖选择后由 Unity 正常建立。本轮没有用未知包版本拼出一个未经验证的 `manifest.json`。
