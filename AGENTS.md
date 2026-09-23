# Dark Nights Unity 开发约定

2026-09-22 本分支已接入[可步入远征飞船](docs/WALKABLE_EXPEDITION_SHIP.md)，`ft-20260922-walkable-expedition-ship`，协议 **14**／存档 **v10**。支持船内步行、唯一驾驶席、泊位附近试飞、搬运机器人与侦察机出舱归队；远征默认应用当前 StrataCave 岩层与三层背景。50 张原生素材导入检查 200/200，新增飞船／远征用例 24/24，同一 Mono 正常／弱网三进程各 28/28，Core 1048/1048；Editor 按影响合并 235/236，既有按钮主题 1 项失败留账。实际画面和本批证据见实现说明，不宣称全洞穴航行、异地降落、前台性能、IL2CPP 或双机器通过。

2026-09-22 当前候选为[独立岩层与三层背景](docs/STATIC_CAVE_BACKGROUND_EXECUTION.md)，分支 `codex/static-cave-background`，协议 **13**／存档 **v9**。用户明确允许脱离旧素材／场景；`Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity` 与同目录 `RandomCave.unity`（2026-09-23 保留 GUID 从 StrataCave 资源目录移入统一工作台，见[场景索引](docs/SCENES.md)）使用独立 AnyRuleD 规则和同源 H5 岩壁、背景算法，保留 YYGC 权威与真实坡形碰撞。原生密度统一为 8px／格，矿粒与矿光暂时隐藏、矿床玩法保留。正式远征用 `--dn-contour-static` 试用；旧风格默认保留。前台性能、IL2CPP 和双机器不宣称通过，本批证据优先于下方历史切片。 外轮廓为独立可配置的六方案，当前 HybridB／OUTLINE-0921／3px／20px／2px、岩块 4px；仅修饰表现，权威碰撞保持原坡形。

2026-09-21 当前切片为[远征营地 Demo](docs/archive/EXPEDITION_CAMP_DELIVERY.md)，协议 **12**／存档 **v8**，分支 `codex/expedition-camp-plan`。正式默认入口已切换紧凑洞穴远征；背景墙矿物、氧气货袋、设备搬运／矿工、风险撤收和原子结算已有首轮实现。旧 `Tomb` 洞口和指令圈运行链退出；以下日期记录仅代表历史切片。当前 Mono 常规／弱网四进程各 35/35，Editor 合并 213/214、远征 8/8、Core 1048/1048；按钮主题 1 项失败、全路线与前台性能等边界见本轮证据，不宣称全部产品目标完成。

2026-09-20 最新地图切片为[洞穴地图工作台](docs/archive/CAVE_WORKSHOP.md)：`CaveExploration` 已接入 gpt-image-2.5 岩层源、新 DualGrid 样式、12 种坡形及匹配权威运动、暗后壁与冷暖光、临时手采／爆破。默认碰撞行走，Tab 切换观察。正式协议 11／存档 v7 不变，正式开局和洞穴存档尚未接入；100 种子隐藏图与 96 洞室落地不代表全路线能力可达。当前实测与边界以本页链接中的新证据为准，下方为历史切片。

2026-09-20 新实验分支 `codex/cave-exploration-art` 合入 main `6b7b74c` 的手持装备，保留地图手采、矿床与恢复；协议 **11**／存档 **v7**。新增[天然洞穴技术原型](docs/archive/CAVE_EXPLORATION_ART.md)：10–13 个不规则洞室、双入口、回环与部分掩埋通路，独立 Debug 场景可 Play。**新像素美术、独立斜面块与匹配碰撞仍未完成**，当前旧图集只作技术预览；完整游戏策划未扩入本轮。以下记录均为各自历史切片，不替代本批证据。

2026-09-19 当前切片为[手采地图验收收口](docs/archive/MAP_PLAN_EXECUTION.md)，分支 `codex/map-plan-execution`：协议 10／存档 v6；钻机、无人机、自动采矿和自动物流整链已从产品代码、资源与本轮门槛删除。主 Editor 完整 196/196、Core 1048/1048、Terrain 24 向量／100 seed、ArchitectureGuard 372/12/0、Mono 启动 6/6；同一 Mono 正常网络与 `200 ms RTT + 5% loss + 25 ms jitter` 各 18/18，覆盖 Host、Client、LateJoin、Reconnect、幂等、局部刷新与真实写盘重启恢复。M6 比例、前台性能、IL2CPP、双机器和真正新机器依赖恢复仍待后续；不把这些边界写成已完成。

2026-09-17 新增[独立随机地图 Debug Bootstrap](docs/TERRAIN_DEBUG_BOOTSTRAP.md)：通过 `Dark Nights/Debug/打开随机地图 Bootstrap` 直接 Play，原始 8 房间／7 通道蓝图没有正式营地的 72 列平地覆盖；本地观察角色 WASD 穿墙飞行、近距离可调镜头、参数实时重建。原 Pinewatch 及正式 Bootstrap 保留。本批 Editor 地图 11/11、实际 Play 22/22，独立 Mono 已构建与启动；隐藏 Player 黑图不计视觉通过，画面证据来自 Editor Play。正式协议、存档、YYGC 与 M5 边界不变。

2026-09-17 最新切片为[正式随机灰松谷](docs/archive/RANDOM_PINEWATCH.md)，分支 `codex/feature-dualgrid-game-start`：默认随机模板为 `Res/Scenes/RandomPinewatch/Pinewatch.unity`，原 Pinewatch 场景保持；选择地图后台生成，正式协议 9／存档 v4，YYGC 仍锁定 `12b253c`。地图权威状态随 ObjectSession 生命周期，完整地图与表现共同门控 Ready；主角按权威格子碰撞，存档保存最终格子。AnyRules 隔离包增加有界 128 块预算补丁及哈希锁；不修改用户 YYGC master。本批结果见[证据](docs/archive/evidence/random-pinewatch-2026-09-17.json)，不把下方旧构建计数当作新批验收。

2026-09-17 Bootstrap 修复后的当前 YYGC 锁定为 `12b253c`（基于下方输入提交 `0c7cec0`），隔离修复位于 `D:/Developer/YYGC-worktrees/network-command-script-resolution`。全局命令注册表包含地形命令 Tag 3，旧三个游戏 Tag 不变；当前修复 Mono 为 `artifacts/bootstrap-registry/player-mono`，新增 Editor 2/2、启动 6/6、双进程会话 13/13 和实际菜单／开局画面通过。详见[改动账本](docs/YYGC_CHANGES.md)及[验证证据](docs/archive/evidence/bootstrap-registry-2026-09-17.json)，历史大矩阵不计入本批。

先读 [README](README.md)、[现行文档索引](docs/README.md)、[开发执行计划](docs/DEVELOPMENT.md)、[技术架构](docs/ARCHITECTURE.md) 和[联机设计](docs/MULTIPLAYER.md)。早期移植、YYGC 统一重构、Linear 世界表现与受限清理按[历史文档索引](docs/archive/README.md)追溯；旧协议、旧构建和后台容量结果不代表当前版本或前台性能已验收。正式玩法、15 类原生对象、UI、四人联机和恢复主体已有实现；前台性能由用户明确暂缓，IL2CPP／双机器仍待条件。

2026-09-16 当前切片为[主角操控与 YYGC 输入联合执行](docs/archive/HERO_INPUT_EXECUTION.md)：协议 8／存档 v3；YYGC 输入提交 `0c7cec0` 位于隔离 `D:/Developer/YYGC-worktrees/input-actions`，准备脚本已锁定。默认产品入口固定为主角操控：每个有权限的玩家首次 Ready 由服务端新建一名专属 `worker`，不占用场景现有闲置村民；重复 Ready 不增员，加载优先恢复仍带手动标记的已保存主角，重连上线生成新人，SharedCamp 恢复同一专属人物。顶部主角工具栏和营地建造、训练、招募、修缮入口暂时隐藏，快捷键继续生效；旧营地后端、工具栏与 `--dn-camp-mode` 仅为开发回归保留。另一个会话的 `D:/Developer/YYGC` master／AnyRule 工作区不能代为切换、清理或合并。新增跟进受影响 Editor／Play 20/20，累计 178 个不同用例按影响合并通过；当前 Mono 为 `artifacts/hero-input/player-mono-generated-villager-r2`，本机独立 Host＋客户端及实际 UI 捕获 39/39。历史 350／155 项与输入 Sample 34 项只保留原构建身份；不把这些结果写成前台性能、IL2CPP、双机器或 M5 全部完成。

## 范围与工作区

- 游戏名称为 Dark Nights，当前内容为灰松谷一个关卡。2–4 人合作、共享营地已确认。联机入口与托管方式的估算假设见 README。
- `../projects` 是已提交的游戏基线，`../reference projects` 是研究与素材来源。Unity 的日常导入、构建和运行必须独立于这两个目录。
- `D:\Developer\YYGC` 是用户维护的框架仓库。用户于 2026-09-12 授权必要时更新 YYGC，并于 2026-09-13 明确允许针对能力限制或 BUG 升级适配：先核实具体缺口，优先在隔离 checkout 中验证，保留用户已有改动，不代为清理或覆盖。不因当前框架限制长期保留两套游戏对象／状态系统。游戏继续使用可重现的锁定依赖；完成后必须逐项列出 YYGC 的修改文件、原因、落点与验证结果，维护 [YYGC 改动账本](docs/YYGC_CHANGES.md)。历史记录中的 UGUIManager 暂存和 IDRegistry 备份不代表当前仍有这些差异。
- 2026-09-13 已实施 YYGC 统一对象路线，分支为 `codex/yygc-unified-object-migration`：运行实体与实例状态归 YYGC ObjectInstance／业务 Behaviour，Core 只保留纯算法、只读配置和数据合同。U5 已删除旧实体、旧世界及过渡入口，不重新引入并行运行模型；U6 最终验收完成前不宣称整个迁移已交付。
- 本次无需旧数据适配：正式游戏不再要求 Godot 旧档、Unity v1 存档、协议 6／5 客户端或旧 Kind／整数身份兼容；旧入口已退出。新格式自身的保存恢复、严格校验和原子性仍必须验收。保留当前人工资产、资源 GUID 和冻结玩法证据，不自动删除用户旧存档；独立 Sample 和 YYGC 其他使用者的兼容边界另行保留。
- 框架接入通过 UPM 和锁定版本完成。实验性修正使用隔离 checkout；本机 `.deps/` 不提交，取得稳定版本后提交可重现的依赖配置与锁文件。
- 不擅自改变既有数值、布局、波次、素材字节、文字或攻击时机。联机需要改变的权限和会话语义单独记录并验证。
- 用户于 2026-09-14 明确要求使用 Linear 色彩空间。纹理与作者颜色进入线性照明和混合；Godot OpenGL Compatibility 参考的 Gamma 色差单独记录，不为逐像素追平而改成 Gamma 或在纹理乘色前反向编码。

## Git 分支命名

- 2026-09-22 起，所有新建工作分支（含临时实验及 worktree 分支）必须采用 `分类-YYYYMMDD-分支名`，例如 `ft-20260922-static-cave-background`。本仓库不使用默认的 `codex/` 前缀或其他额外层级。
- 分类按主要目的选择：`ft` 功能／地图／素材开发，`fix` 缺陷修复，`ref` 重构，`docs` 文档，`test` 测试验证，`chore` 工程维护，`exp` 探索实验；同一目的保持分类一致。
- 日期取分支创建当天的本地日期，使用八位 `YYYYMMDD`；分支名使用简短、明确的小写英文短横线名称，说明具体工作内容。创建前先检查重名，重名时增加有意义的主题限定，不覆盖已有分支。
- `main` 等长期分支保持原名；历史分支及本文历史记录中的旧命名不作为新分支模板，不自动批量改名。

<a id="execution-efficiency"></a>

## 执行效率与批量操作

### 用户明确要求 Worktree 时的低成本 Unity 流程

- 用户明确强调使用 worktree 时，除非同时要求各 worktree 独立启动 Unity、并行 Editor 验收或独立缓存，默认采用“薄 worktree 开发＋Local 单一 Unity 验收通道”。worktree 用于隔离代码、文档、配置和文本资源改动，不因任务最终可能放弃而提前支付完整 Unity 缓存成本。
- 薄 worktree 不启动 Unity，不生成、复制、硬链接或目录联接 `Library`、`Temp`、`Logs`、`obj`、构建输出和 `artifacts`；`.worktreeinclude` 不得包含这些路径。只有非 Unity 检查确实需要时才准备最小锁定依赖，不复制整套导入缓存。共享或链接同一 `Library` 给多个检出、并发 Editor 写入同一缓存均禁止。
- 在薄 worktree 先完成实现、静态检查和不依赖 Unity 导入的验证，并建立可恢复的临时 checkpoint；checkpoint 或临时分支不代表必须合并。需要 Editor 编译、PlayMode、场景／Prefab 保存重开、Player 构建或画面验收时，先合并同批候选验证项，再通过 Codex“移交到 Local”把聊天与代码状态带到本地检出，复用 Local 现有 `Library`，同一时间只运行一个 Unity 写入／构建通道。
- 移交是借用 Local 环境进行验证，不构成 merge、rebase、cherry-pick 或交付授权。通过后按原任务目标决定是否集成；未采用的实验移交回原 worktree 后归档或删除，不为保留 Unity 缓存而留下完整工作副本。移交前后检查当前分支、未提交改动和 Editor／Player 进程，不能覆盖其他会话或用户工作。
- 若候选修改 Unity Editor 版本、`Packages/manifest.json`／`packages-lock.json`、关键 `ProjectSettings`、目标平台／Scripting Backend、渲染管线或大批资源导入设置，先报告 Local 缓存失效与反复重导入风险；未经用户明确选择，不把薄 worktree 升级为第二套完整 Unity 工作区。确需独立 Unity 验收时只保留一个长期验证 worktree，并继续串行使用其中的 Editor。
- Local 中的 Unity 长任务仍按一次触发原则组合为有限批次：完整输出写日志，前台只保留任务 ID、阶段、完成标记、退出码和摘要路径；首次有界等待后退避到最多每 60 秒一次的极小状态检查，未变化不读取全量日志、不发送重复进度、不重启任务。优先使用完成通知或原子结果文件；需要完全避免模型守候时，可后台运行并在完成后由用户或后续任务恢复，但必须如实说明自动续接边界。

- 以一个可验证的功能切片或同类资源批次组织工作。执行前汇总已知输入、依赖顺序、输出和验收项；能够一起准备、一起执行的操作必须合并，避免逐文件、逐资源发起工具请求。批次保持可审查，不把整个移植合成难以定位失败的大任务。
- 已授权范围内连续完成准备、修改、生成和验证，不逐步向用户请求“继续”或重复确认。只有必须由用户补充的必要信息、超出授权范围或尚未获准的不可逆操作，才集中提出所需问题。
- 独立查询合并读取；同一任务已取得的文档、工具 schema、路径和状态按需复用。文件未变且没有新疑点时，不反复全文读取、枚举全部工具或输出完整日志；优先定向搜索、差异、摘要和失败上下文。
- 同批代码、程序集声明和可直接落盘的资源先集中写入，再统一触发所需导入／刷新，让 Unity 批量生成新增文件及目录的 `.meta`。不得每写一个文件就启动 Editor、刷新或重编译；自动导入已完成时复用结果，不叠加手动刷新。已有资产移动保留 `.meta`／GUID；不得为提速重建已有 `.meta`、随意分配 GUID 或绕过 Unity 的资源引用检查。
- 同批 Prefab、ObjectDefinition、绑定及 Addressable 条目，依赖已就绪时通过已有批量工具或一个有限的 Editor 操作顺序完成，集中保存、生成和检查。新增脚本必须先编译就绪，依赖导入结果的步骤必须等待结果；批处理暂停导入期间不得等待编译或读取尚未导入的资源，异常必须释放暂停状态。初始化仍只输出到指定空目录，批处理不放宽人工资源保护。
- “一次触发”指每个依赖已就绪的逻辑批次只主动提交一次；Unity 内部必要的导入、生成源码再编译及域重载不算重复请求。可由现有任务入口自动续接的步骤一起编排，不依赖 AI 多轮对话逐步驱动；不为凑成一次调用新建通用调度框架，也不跳过真实依赖屏障。
- 同一 Editor 的写入、生成、编译和构建串行执行，不用多个 MCP／CLI 请求竞争状态。长任务只提交一次，保留任务 ID，采用完成通知或有界等待；需轮询时通常间隔 20–30 秒，未变化则退避，单次阻塞等待不超过 60 秒。状态未变不重复取全量日志，不因等待超时重新启动仍在运行的任务。
- 按改动影响一次安排完整验证矩阵，每个后端／配置构建一次，再复用同一产物执行对应场景和基础／弱网检查。明确前置条件与失败即停规则；失败只修正并重跑受影响阶段。已通过检查只有在输入、配置或相关依赖变化，或出现新证据时才重跑，不以节省交互为由减少必要验收。
- 每阶段开始和编译／构建前检查相关磁盘剩余空间；阶段完成即清理本阶段可重建的中间编译产物、过期验证副本和不再使用的 Player 构建缓存，记录释放量与剩余空间。复用当前 Editor 导入缓存和后续验收需要的同一 Player，避免复制整套 Library；保留源码、人工资源、未保存场景备份、冻结夹具及报告。删除前核验绝对路径、链接及活动进程，不清理正在使用的编译目录；空间不足时先清理本任务可重建产物，再开始下一批。
- 用户于 2026-09-14 要求清理盘点覆盖中间过程产生的产物，包括依赖解包、生成器 bin／obj、原生调试副本、测试存档、日志、截图和报告生成的中间文件。无法清理的内容列出路径、体积、原因及保留条件即可；审批拒绝后不重试、不改工具或删父目录绕过，不重复请求许可。共享缓存、带本地修改的依赖、唯一源文件和后续验收产物单独标注；父子目录不得重复计入可释放总量。
- 中间文件因策略、权限、占用或其他原因无法当批清理时，不得散落留在源码、验证副本或正式交付目录。核验其确属可重建且不再被保留成果引用后，统一移动到该任务既有外部输出根下的 `临时待删除/YYYYMMDD-任务名/`；没有外部输出根时放入仓库 `artifacts/临时待删除/YYYYMMDD-任务名/`。保留原相对目录结构，并记录原绝对路径、目标绝对路径、体积、未清理原因和后续删除条件。最终 Player、正式证据、唯一源文件、用户成果、共享缓存及归属不明内容不得移入；“临时待删除”名称不构成自动删除授权，后续删除仍按当次范围和审批执行。
- Player 构建默认优先使用 Mono 进行快速可运行验证。未经用户明确确认，不主动生成、覆盖或验证 IL2CPP Player；需要 IL2CPP 时先报告原因、范围和预计产物，再等待确认。确认后每个受影响配置只构建一次，并复用同一 IL2CPP 产物完成对应检查；Mono 通过不代表 IL2CPP 已验收，通过状态必须分别记录。
- 批量入口返回成功／失败、完成及失败项、关键计数和日志／产物路径；详细输出落文件，需要定位时再读取。批次完成后集中检查差异、引用和输出完整性；额外触发刷新、生成、重编译或构建时说明新增输入或失败原因，不把重复调用本身当作进展。

## 代码与结构

- Editor 沿用已锁定的 `6000.4.9f1`；游戏代码兼容 C# 9 和 .NET Standard 2.1。不能把 Godot 的 C# 12／.NET 8 配置直接带入。
- 职责目录与程序集按架构文档执行。Core 不引用 Unity、Godot、GameCore、FishNet、R3、VitalRouter、文件系统或表现资源；引擎、网络与存储适配放 Runtime。
- 正式代码放 `Assets/DarkNights/Scripts`，资源放 `Assets/DarkNights/Res`；采用 Core、Runtime、View、Entry 四个运行程序集，以及隔离的 Editor/Tests。View、Entry 分别承担原方案 Presentation、Bootstrap 的职责；不改现有 Bootstrap 场景或 Sample 类型名。不要为每个小文件再建一层服务接口或一个程序集。
- 代码目录使用 Config、Logic、ViewData、Save、Network 等直观名称，具体归属见架构文档；Scripts/Res 不加入命名空间。ViewData 仅为展示副本；现有 Core/Logic 世界按统一重构阶段退出，目标业务 Behaviour／State 放 Runtime/Objects，Core/Logic 仅留纯计算。不预建空目录和占位类型。
- 文件名与主要类型一致，命名空间与职责目录一致，根命名空间 `DarkNights`。不建立无限扩张的 Manager/Utils 汇总文件。
- 手写 C# 目标 150–250 行，硬上限 300 行，包含空行与注释；一文件一个主要命名类型。按职责拆分，不压缩语句或用多个 partial 文件绕过上限。
- YYGC／MemoryPack／绑定生成器要求的类型可以 `partial`，但每个类型仍只有一份手写主体。生成输出放明确目录，记录输入和重建方式；不手改生成结果。
- 每个类、record、struct、enum、interface 添加中等详尽中文 XML summary，说明职责、状态归属以及关键生命周期／不变量。注释不逐行翻译代码。
- C# 9 使用块级 namespace、普通构造函数和显式集合初始化；不能使用文件级 namespace、required、主构造函数、C# 12 集合表达式。
- 不复制整个 Godot 数学库或建立通用引擎抽象。只迁移实际使用的坐标、数学和可恢复随机数能力。
- 沿用 YYGC 现有启动、DI、视图、资源与 UI 接口，不另造并列的 DI 容器或全局事件框架。框架本身的历史长文件不在本次全面拆分范围内。
- 联机实现先读 [YYGC能力复评](docs/archive/YYGC_REASSESSMENT.md)。优先修正并复用 Gateway/Sender/Processor、类型注册/序列化和会话 StatefulBehaviour/StateSynchronizer；游戏仅补权限、业务去重、投影、Ready、epoch和恢复。先验证可靠完整投影，测量后决定分块/拆流；局部后备网络适配必须有现有路径无法满足需求的具体证据。本地输入互斥复用 Interaction Sessions。
- 独立联机模板遵循 [LAN Sample 规范](docs/LAN_SAMPLE.md)：样板放 `Assets/Samples/LanCoop`，正式代码不反向引用；构建不覆盖样板原生资产。必要的 R3 用于状态订阅及生命周期；VitalRouter 只保留 YYGC 命令链必需的显式适配，新增业务路由／过滤器必须先说明具体必要性和调试路径。不要为模板预建 Steam、Lobby、多 transport 或房主迁移抽象。

## 权威状态与联机

- 经济、生产、单位 AI、伤害、箭矢、波次、胜负和随机数都只有一个权威写入者。状态由所属 YYGC 业务 Behaviour／实例 State 拥有；ObjectSession 组合会话能力，索引只引用对象，不另存一份状态。GameSession／WorldState 旧运行类型已删除。
- 客户端及 Host 的表现只读取冻结展示副本。StatefulBehaviour 接管权威状态时必须撤除旧状态所有者；网络 DTO、ScriptableObject 和展示副本不能再自行结算经济／HP。
- 业务命令带明确 EntityId 和参数，不能读取一个全局 SelectedIds／BuildKind 来代替请求参数。镜头、选择、悬停与建造预览属于各客户端。
- 身份从服务端连接上下文取得。请求中的 PlayerId、SenderObjectId、资源数量和伤害值都不构成授权；服务端验证共享营地权限、合法目标、范围、版本、序号和支付。
- Host 使用同一个验证与命令处理入口，保证一次输入只执行一次。客户端可以显示待确认反馈，不先结算支付或伤害。
- 共享控制使用会话级 SharedCamp / HostOnly 策略，服务端统一校验；关闭时同时限制直接命令、建造自动派工和训练等营地修改。切换增加 PolicyRevision，拒绝旧策略未执行请求，已生效任务继续；不通过转移小人的 FishNet 所有权实现。
- 模拟默认 60 Hz，倍速只在一个入口生效。暂停时网络、心跳、重连与 UI 继续运行；不用 `Time.timeScale = 0` 停掉整个服务进程。
- 稳定实体 ID、规则引用、场景放置键、YYGC Guid / Key、FishNet ObjectId、玩家连接 ID 分开。正式游戏已使用 DefinitionReference 与 GuidFirst／GuidV2；新重构不恢复旧整数兼容，独立 Sample 的 LegacyV1 单独保留。载入世界增加 epoch，拒绝旧世界命令和快照，保持当前房间控制模式。
- 快照是冻结数据；异步发送、插值、存档不能持有已归还池的状态引用。不要让 SessionScope 跨 await 或线程。
- 不默认采用锁步、回滚、ECS、并行模拟、每实体 NetworkTransform、房主迁移或专服集群。增加这些方案前给出具体需求和测量依据。

## Prefab、美术与内容

- 用户于 2026-09-20 明确指定：本项目需要 AI 生图时，使用 [imagegen-codex-provider](C:/Users/Jobscn/.codex/skills/imagegen-codex-provider/SKILL.md) 替代内置 imagegen 路径，通过已配置 provider 的 gpt-image 模型执行，遵循该技能当前模型及调用规范。这一工具选择已获授权，不再因缺少内置工具重复询问是否允许使用配置 API；具体生图仍须属于当次任务范围，评估任务不自动变成批量生图任务。
- 每批素材制作前必须评估生图必要性，记录目标原生像素尺寸、用途、共边／透明／形状精度要求、选用方式及理由。16×16／32×32 等低像素地形、DualGrid 掩码、斜面和碰撞轮廓优先采用可控的像素绘制与确定性图集工具；需要风格探索、大块岩层、远景或装饰源图时才考虑生图。不得把大图缩小、像素化滤镜或模型输出网格直接当成合格像素 tile。
- 生图源只作为可编辑美术输入，最终像素资产必须统一像素密度、调色板、透明边缘及拼接合同；按原生尺寸和实际游戏镜头检查像素团块、重复纹理、共边、材质过渡与斜面衔接。视觉斜面必须对应独立形状和权威碰撞，不能用方块圆角／阶梯冒充。新源图和派生资源保留来源及重建关系，不覆盖人工源文件。
- 洞穴参考图的差异和下一批目标见[洞穴视觉与空间目标](docs/archive/CAVE_EXPLORATION_TARGETS.md)。先完成固定洞穴样板的美术、空间与真实角色通行，再推广到随机生成；隐藏拓扑连通和旧测试图集通过不代表参考风格验收通过。
- 资源按对象／面板归组：`Res/Objects/Worker` 等目录集中所属 ObjectDefinition、Prefab、专用动画和材质；UI 同理。共用资源才放 Res/Shared，原始素材只保存一份，不因对象归组重复复制。
- Addressables 不要求游戏资源目录叫 Addressable／Addressables；Res 是项目约定，不自动注册资源。通过 Addressable 条目与分组管理加载，不使用特殊 Resources 目录存放 Addressable 资源。保留现有 AddressableAssetsData 配置位置，物理目录、分组、Address／Label 与 YYGC 定义身份分开。
- 正式对象通过 DefinitionReference 和 YYGC 定义／创建入口，由 ObjectDefinition.PrefabRef 驱动 Addressables；沿用组件绑定、注入与生成注册。检查绑定键、类型、引用及装配／池化／释放时机，不以 GetComponent、节点名或子节点索引兜底缺失绑定，不手改生成结果。详细合同见移植方案。
- 角色、建筑、工位、特效和 UI 使用原生 Prefab；Pinewatch 场景在未进入 Play 时能看到布局与外观。正式场景不得回退为一个空节点加全局创建脚本。
- Prefab、AnimationClip、场景和 Theme 等正式资源由人工维护；迁移脚本只在指定空目录输出首版样板，普通导入／构建不得覆盖美术编辑。
- 场景初始布局只有一份可编辑来源。派生关卡数据可在构建时生成，但必须能追溯到场景标记且不能反向覆盖它。
- balance/波次 JSON 保持规则唯一来源。ObjectDefinition 的共享配置保存内容映射和表现设置，不重复维护 HP、成本或实例进度。
- 角色根对齐脚底，ArtOffset、Facing、StatusAnchor、SelectionAnchor 与玩法占地分离；动画和物理碰撞不能结算游戏伤害。
- 原始 551 项素材放 Res/Art/Original，保持来源和 SHA-256；改图放 Res/Art/Custom。最近邻采样、关闭不需要的有损压缩，按适配方案转换坐标、原点和动画帧序。
- Editor 预览只产生表现，不启动网络或会话，不使用游戏随机数，不访问玩家存档。Editor API 和测试代码不得进入 Player 程序集。
- `.meta`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/` 提交；Library、用户存档、本机配置、密钥、依赖缓存不提交。Unity 场景启用文本序列化与 Visible Meta Files。

## 验证与完成

- 运行与改动相匹配的规则、场景或联机检查；不为文档修改伪造 Unity 构建通过记录。
- 联机验证包含独立进程中的 Host＋客户端。Host 单窗口、单个状态序列化测试不能替代联机验收。
- 核验并发扣款、共享工位、重发去重、非法目标、初始快照、晚加入、重连、丢包乱序、暂停、加载 epoch 和 Host 单次执行。
- 原玩法和旧档夹具是冻结证据，不用当前结果重生成来掩盖差异。旧档读取成功不再是统一重构的验收要求；仍有效的布局、规则、RNG 和时序断言迁到新对象入口，新格式完整恢复单独验收。跨引擎规则一致性与跨 GPU 画面近似分别验收。
- 美术相关变更完成 Prefab 编辑、保存、重开和运行检查；发布相关变更完成实际 Player 构建并在独立进程运行。
- 保持文档的“已完成／计划／待验证”清晰，更新执行状态、依赖和验证证据。提交前查看差异，提交当前仓库内的本次成果，不推送远端。
- Git 提交标题和正文使用中文；必要的技术名称、文件路径和提交类型前缀可以保留原文。
