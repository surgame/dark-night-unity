# Dark Nights · Unity

《Dark Nights》Unity 工程筹备仓库。M0/M1 环境基线已完成；2026-09-11 新增独立 [LAN 合作 Sample](docs/LAN_SAMPLE.md)，通过 Windows Player 四进程及实际 UDP 丢包／乱序验证。灰松谷正式玩法、正式联机和 M2–M5 尚未完成。

`Game/` 是 Unity 宿主，[ProjectVersion](Game/ProjectSettings/ProjectVersion.txt) 为 `6000.4.9f1`。先运行 `tools/prepare-lan-sample.ps1` 准备锁定提交的 `.deps/YYGC`，不直接引用用户维护的框架工作区。R3、MemoryPack、UniTask、FishNet 等依赖已导入；VitalRouter 使用 YYGC 要求的完成语义修正版。内部产品名暂保留 `DNights`。

原评估日期：2026-09-10；Sample 验证日期：2026-09-11。本仓库分支为 `main`。本次未修改 `D:\Developer\YYGC`、Godot 基线或参考素材；下方评估输入表保留历史时点。

## 核心判断

YYGC 适合作为应用、表现与联机基础：已有启动编排、DI、ObjectDefinition/ObjectInstance/ObjectView、UGUI、本地交互仲裁、网络命令、服务端状态发布和单对象首次快照。最新[能力复评](docs/YYGC_REASSESSMENT.md)建议先修正并验证现有联机链路，优先复用 YYGC 的命令与状态同步。

建议保留普通 C# 模拟，由房主唯一结算世界；游戏只补营地权限、业务去重、投影、Ready、epoch 和恢复流程。首个切片优先用会话级 StatefulBehaviour/StateSynchronizer 同步可靠完整投影，测量后再决定分块与拆流。角色、建筑和资源点使用 YYGC 本地对象视图与 Unity Prefab。

早期复评的生成器、首状态和序列化问题属于当时版本。新 Sample 已在 YYGC `10b8f0e` 上复用完整命令与状态链，通过真实多进程验证；独立程序集补丁、依赖和未验收边界见 [Sample 说明](docs/LAN_SAMPLE.md)。联机尚未正式生产使用。

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
| 立即试用 LAN 模板、R3/VitalRouter 约束、四进程证据 | [LAN Sample](docs/LAN_SAMPLE.md) |
| 最新复评：避免重复造轮子、已复现缺口与修正优先级 | [YYGC 能力复评](docs/YYGC_REASSESSMENT.md) |
| YYGC 哪些可复用、哪些需要修正或验证 | [框架评估](docs/FRAMEWORK_REVIEW.md) |
| Unity 版本、包依赖、生成器与构建准备 | [依赖与环境](docs/DEPENDENCIES.md) |
| YYGC 定义 ID 重构（待实施）与版本补齐记录 | [框架重构执行方案](<D:/Developer/YYGC/Documentation~/ID_REGISTRY_REFACTOR_PLAN.md>) |
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
| 实际 Unity Editor | `D:\Program Files\Unity 6000.4.9f1\Editor\Unity.exe`；YYGC 包声明 `6000.2` + `35f1`，已在 6000.4.9f1 编译通过 |

精确规模、关键源文件 SHA-256、原始素材核验及 Git 状态见[冻结评估证据](docs/evidence/assessment-2026-09-10.json)。YYGC 当前工作区不等于可锁定的发布版本；后续接入必须选定已提交版本并重新验证。

## 本仓库当前可执行的检查

在本目录使用 PowerShell 7：

```powershell
pwsh -NoProfile -File .\tools\collect-assessment.ps1 -FrameworkPath 'D:\Developer\YYGC' -GamePath '..\projects'
git status --short
```

采集脚本只读两个输入目录，输出到忽略的 `artifacts/assessment-current.json`；不启动 Unity，不修改框架或游戏。冻结证据保留原评估时点，常规重跑不会覆盖。

正式 Bootstrap、全局定义与注册表仍保持环境基线；Sample 使用自己的定义、StateData、NetworkCommand、Prefab 和场景。`Library/`、构建输出、`.deps/` 和日志不提交，冻结验证摘要在 `docs/evidence/`。
