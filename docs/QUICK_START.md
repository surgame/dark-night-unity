# 人工开发 Quick start

`Game/` 使用 Unity `6000.4.9f1` 和 **Linear** 色彩空间。正式入口继续使用 Bootstrap；游戏操作、当前 Player 和复跑条件见 [Player 指南](PLAYER_GUIDE.md)。独立模板位于 `Assets/Samples/LanCoop/Content/LanCoop.unity`，只作为 [LAN Sample](LAN_SAMPLE.md) 对照。

2026-09-16 当前状态：YYGC 统一对象迁移 U0–U5 已完成，Core 只保留纯算法、只读配置和数据合同。产品默认固定为主角操控，每个 Ready 玩家由服务端直接分配一名可用友军；顶部主角工具栏与旧营地操作入口暂时隐藏，快捷键继续生效，仅显式 `--dn-camp-mode` 开发回归保留旧 UI／后端。输入改键及 Sample 已实现；游戏协议 8，新档格式 v3，YYGC 锁定隔离输入提交 `0c7cec0`。本批验收和当前 Player `artifacts/hero-input/player-mono-default-hero-r3` 以[联合执行文档](HERO_INPUT_EXECUTION.md)为准。前台性能暂缓，IL2CPP 和双机器 LAN 仍待条件，M5 尚未全部完成；历史世界表现证据见 [Linear 世界表现验收](M5_WORLD_PRESENTATION.md)。

## 先读什么

1. 读[README](../README.md)确认当前状态、范围与联机假设。
2. 读最新[移植方案](MIGRATION_PLAN.md)和[LAN Sample](LAN_SAMPLE.md)，区分已验证路径与正式游戏待补内容；早期[YYGC 能力复评](YYGC_REASSESSMENT.md)用于了解历史修正缘由。
3. 读[技术架构](ARCHITECTURE.md)的程序集表，再读[联机设计](MULTIPLAYER.md)的权限和请求流水线。
4. 按 [M5 当前清单](M5_EXECUTION.md) 继续未验收项；[开发执行计划](DEVELOPMENT.md)保留阶段合同和历史记录，不重新实施已完成的 M0／M1。

## 准备已有工程

1. 在当前工作分支核对 Git 状态与 C／D 可用空间；本批分支为 `codex/hero-input`。确认已有编辑和产物保留范围，见[清理清单](STAGE_CLEANUP_INVENTORY.md)。
2. 需要建立或核对依赖时，依次运行 `pwsh -File tools/prepare-lan-sample.ps1` 与 `pwsh -File tools/prepare-fishnet.ps1`。它们准备 `.deps/YYGC-unified`／`.deps/FishNet` 的锁定输入和补丁；发现未知改动时应保留并检查，不重置用户维护的 `D:\Developer\YYGC`。
3. 用 `6000.4.9f1` 打开 `Game/`，复用已有导入缓存；依赖以提交的 `Packages/manifest.json`、`packages-lock.json` 和 [依赖说明](DEPENDENCIES.md)为准。不要另复制整个 Library。
4. 等本批脚本编译就绪后，再执行依赖它们的资源操作或测试。正式 Prefab、场景、注册源和 Addressables 配置已经存在；普通导入／构建不运行资源初始化脚本。

早期环境创建步骤属于已完成的 M0／M1，见[开发记录](DEVELOPMENT.md)。不要重新运行 `Initialize Environment`、`Install Initial` 或一次性资源校准来覆盖人工资产。原 GUID、Prefab overrides 和唯一场景布局继续保留。

## 从现有代码理解规则

先沿当前 Unity 实现阅读；以下路径均已存在，相对于 `Game/Assets/DarkNights/Scripts`。Godot `../projects` 仅作为冻结规则和表现参考，不用于日常构建或继续开发。

| 顺序 | 入口 | 重点 |
|---|---|---|
| 1 | [SessionAuthority](../Game/Assets/DarkNights/Scripts/Runtime/Session/SessionAuthority.cs) | 可信连接、权限、策略版本、去重与请求执行 |
| 2 | [ObjectSession](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ObjectSession.cs) | 组合 YYGC 能力、对象索引和生命周期，不另持有运行世界 |
| 3 | [CampSimulationBehaviour](../Game/Assets/DarkNights/Scripts/Runtime/Objects/CampSimulationBehaviour.cs) | 会话模拟状态；个体状态归各自 Behaviour／State |
| 4 | [ObjectConstruction](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ObjectConstruction.cs) | 放置校验、支付与创建事务；工位分配见同目录 ObjectWorkOrders |
| 5 | [ObjectReplica](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ObjectReplica.cs) | 客户端只读对象副本，与权威业务状态分离 |
| 6 | [ObjectSnapshotMapper](../Game/Assets/DarkNights/Scripts/Runtime/Objects/ObjectSnapshotMapper.cs) | 从所属状态捕获冻结存档；恢复由 ObjectWorldRestore 处理 |

跟一次“选中工人→放置住宅”的链路：本地输入形成明确请求参数，经 YYGC 命令链进入 SessionAuthority，再由 ObjectSessionCommands 调用所属能力。支付和创建在权威端执行；选择、预览与镜头属于本地表现。不要把已退出的 GameSession／WorldState 或独立 Core 实体重新接回运行入口。

## 根据任务找到当前 Unity 目录

路径相对于 `Game/Assets/DarkNights`。Scripts 为代码区，Res 为资源区，完整状态归属见[技术架构](ARCHITECTURE.md)。

| 开发任务 | 归属 |
|---|---|
| 改成本、伤害、建造时间 | Res/Config 的 JSON；Scripts/Core 只读 |
| 改采集、训练、攻击规则 | Scripts/Runtime/Objects 中所属 Behaviour／能力与 Unity 规则回归；纯计算才放 Core/Logic |
| 新增一种玩家命令 | Scripts/Runtime/Session、Runtime/Network 和所属 Objects 能力；验证可信来源、权限、参数和去重 |
| 关闭共同操作／调整房间控制权限 | Runtime/Session 的 SessionAuthority 统一校验 PolicyRevision 和共享策略；枚举位于 Core/Logic/State；直接命令、自动派工和训练共用校验 |
| 改同步频率、加入或重连 | Scripts/Runtime/Network，不能改客户端 HP 算法 |
| 换图、动画、角色锚点 | Res/Objects 下所属对象目录；原图引用 Res/Art/Original，改图放 Res/Art/Custom；在 ArtReview 检查 |
| 调 HUD 布局 | Res/UI/HUD；动态显示代码在 Scripts/View |
| 排查资源加载、对象装配与组件绑定 | Scripts/Runtime/Framework、Scripts/View 及 Res 中所属对象的 Definition／Prefab；不靠 GetComponent 兜底缺失绑定 |
| 改初始摆放 | Res/Scenes/Pinewatch/Pinewatch.unity；正式 Prefab 直接放在 Buildings／Worksites／Actors 分组，Hierarchy 顺序就是创建顺序；ScenePlacement 只保存自动身份和实例初值，Loader 提供 Definition 并接管原对象 |
| 改保存格式 | Core/Save 冻结合同＋Runtime/Save 文件边界＋Runtime/Objects 捕获／恢复；当前仅 v3，不要求旧档迁移 |
| 改主角操控／默认人物 | Entry/HeroPlayerController、Runtime/Session/SessionHeroControl、Runtime/Network/SetReadyCommand、View/GameInputActions 与 Runtime/Objects/Hero*Behaviour；保持原生 Player 动作名称和服务端唯一分配 |
| 学习 YYGC 输入 | `Assets/Samples/YYGCInputActions/Content/InputActions.unity` 与同目录上层 README；框架源在 `Samples~/InputActions` |
| 改YYGC通用代码 | 独立框架checkout，先确认必要范围与工作区状态 |

## 当前可以运行的命令

在 `unity-projects` 下：

```powershell
git status --short
git log -1 --oneline
python tools/measure-stage-storage.py --output artifacts/storage-review
```

这些命令读取状态和盘点空间，不启动 Unity／Player 或执行删除。原始评估与冻结夹具保留；不重生成旧期望来掩盖差异。

实际规则／Editor 覆盖见[回归映射](YYGC_UNIFIED_TEST_COVERAGE.md)，当前 Player 复跑参数见[主角与输入联合执行](HERO_INPUT_EXECUTION.md#复验与示例入口)。只验证受本批改动影响的范围；输入未变时复用已通过证据。新构建输出到新的空目录，后续脚本显式传入 `-PlayerPath`，避免使用历史默认产物。手写 C# 遵守 C# 9／.NET Standard 2.1、中文 XML summary 和 300 行上限。
