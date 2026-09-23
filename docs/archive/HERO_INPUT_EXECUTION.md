# 主角操控与 YYGC 输入改进联合执行

## 2026-09-16 镜头跟随抖动快速修复

主角镜头此前在 Update 直接使用低频冻结快照的 X，而角色由展示时间线插值，两者位置不同步。现在通过已有 SessionEntityViews 引用，在 LateUpdate 跟随角色本帧显示位置；Ready、连接代次、epoch 和镜头输入阻塞检查继续生效。Focus 同步刷新视差背景与环境，先限制镜头边界再计算背景位置，避免背景沿用上一帧镜头。

Unity 6000.4.9f1 批处理编译及 EntityPresentationTests **28/28** 通过；架构守卫 **316 文件／12 自测／0 错误**，git diff --check 通过。日志与测试报告保留在 `artifacts/camera-follow/editor.log`、`artifacts/camera-follow/tests.xml`。本批仅修改三个表现／接线脚本，未更改 YYGC、协议、资源或玩法；没有重建 Player，未进行实际前台画面复核，不更新既有 Mono 或前台性能验收结论。

阶段开始与完成 C／D 可用空间均约 7.58／31.94 GiB；复用现有 Unity 导入缓存，保留本批日志与测试报告供追溯，未生成 Player 或独立构建副本，清理释放量为 0，不清理此前列账的保留目录。

## 原主角输入批次记录

2026-09-16，本批主角操控与 YYGC 输入改进已合并落地。游戏在 `codex/hero-input`；框架在隔离 `codex/input-actions` 提交 `0c7cec0`。默认人物现修正为玩家上线时新建专属村民，旧入口及遮挡画面的顶部工具栏保持隐藏；当前 Mono 为 `artifacts/hero-input/player-mono-generated-villager-r2`。本文不签署仍待条件的前台性能、IL2CPP、双机器 LAN 或整个 M5。

## 默认村民生成跟进

`SetReadyCommand` 继续携带 `RequestHero`，但服务端不再遍历闲置友军。每个首次上线且有权限的连接通过正式 `ObjectSession` 生命周期在酒馆出生点创建一名新 `worker`，随后在同一权威事务中接管；不扣招募资源、不触发招募冷却，也不受现有闲置村民顺序影响。四个玩家槽位均可各获新人。重复 Ready 复用当前占用；断线释放旧人物，新的连接代次 Ready 时生成新人；HostOnly 撤回来宾权限，回到 SharedCamp 时按连接记录的 ID 接回同一人物而不重复增员。加载后只有对应对象仍带存档中的手动主角标记时才可按记录 ID 恢复，否则只在其他已保存手动主角中分配或新建，不能因 EntityId 碰巧相同而占用普通闲置村民。

默认 UI 已隐藏截图中会遮挡画面的整块顶部主角工具栏（状态文字、三格按钮与改键按钮），并隐藏营地建造、训练、招募和修缮按钮；1／2／3、滚轮及移动／使用快捷键保持在主角动作组，等待分配期间也不会短暂开放旧营地操作。旧 `CampInput`、工具栏、营地命令和服务端权限合同没有删除，只通过 `--dn-camp-mode` 和显式验收驱动保留，方便现有回归与后续决定是否重新开放；这不是并行状态模型。

- 受影响 Editor／Play **20/20**：HeroControl 9、HeroRecovery 5、SessionClientReady 4、UnifiedSlicePlay 2；新增三个不同用例后，游戏累计为 **178 个不同用例按影响合并通过**，不是一次完整 178 项运行。四槽生成、场景村民不被占用、重复 Ready、重连新人、加载 ID 碰撞和策略恢复均有明确断言。
- 架构守卫 **316 文件／12 自测／0 错误**；PowerShell 验收脚本语法、C# 300 行上限和差异空白检查通过。YYGC、规则、美术、协议与存档格式未改变。
- 新 Mono `artifacts/hero-input/player-mono-generated-villager-r2/DarkNights.exe` 构建成功，**414 文件／199383292 字节**；独立 Host＋客户端和实际 UI 捕获 **39/39**。运行结果明确验证来宾 Ready 新增一个 Actor、重连再次新增且旧人物保持自动控制、加载与 SharedCamp 恢复可继续控制。结果在 `artifacts/hero-input/network-20260916-202640-009/result.json`；`hero-default.png` 已复核顶部框仍未出现。
- 机器摘要见[默认村民生成证据](evidence/generated-villager-2026-09-16.json)。历史 350／155／43 项、输入 Sample 34 项、完整弱网、三夜和容量矩阵没有重跑，继续绑定原产物。
- 新跟进没有再次执行清理：此前同阶段删除已被 `blocked by policy`，按约定不重试或换工具绕过。13 项微型测试运行的两份日志、r3 Player／日志／捕获，以及加载 ID 校验前的首个生成村民 Player／日志／捕获，共 8 个目标、856 个文件、404616700 字节，已补入[清理清单](STAGE_CLEANUP_INVENTORY.md)；本轮释放 **0 字节**。

前台手感／性能和整体 UI 视觉签署仍不在本次自动捕获范围；未构建 IL2CPP，不替代双机器 LAN 或整个 M5 验收。

## 续接收尾：平台坐标与基本本地验证

原任务中断前新增的回归已证明平台画面与碰撞不一致：第一个平台的可见顶面为地面下 11 像素，权威高度却为地面上 12 像素。本轮统一为 Unity 向上为正，场景平台顶面置于地面上 12／24／36 像素，布局导出使用 `point.y - groundY`；Prefab 图形中心下移 1 像素，使可见顶面精确落在标记高度。初建入口同步修正。通过显式一次性 Editor 操作保存、重开现有 Prefab／场景，原 GUID、16 个旧摆放身份和旧规则不变。

- 本轮 Editor／Play **17/17**：平台顶面对齐、冻结布局、主角运动／战斗／恢复及开关域重载的实际 Play。加上此前按影响保留的结果，共 **173 个不同用例按影响合并通过**，不称为本轮完整 173 项运行。
- 架构 **316 文件／12 自测／0 错误**。YYGC、导入 Sample 和九份宿主补丁未修改，复用其既有证据。
- 由于 View、场景和 Prefab 输入变化，新增一次 Mono 构建到空目录；同一产物启动 **6/6**、本机独立 Host＋客户端主角检查 **37/37**，合计 **43/43**。三张实际截图已复核。按用户要求未重跑完整弱网、三夜或容量矩阵。
- 平台阶段 Player：`artifacts/hero-input/player-mono-platform/DarkNights.exe`，**414 文件／199376158 字节**，包含当时调试副本；现已由本文开头的默认人物 Player 替代。构建前 6172 个已有输入和测试前后的 Player 字节不变；构建新增的两份 Addressables `link.xml`／meta 单列为生成输出，合计清单 6174 项，不冒充构建前输入。
- 该阶段六个源码／资源差异及证据见[平台修正验收](evidence/hero-input-platform-2026-09-16.json)，完整产物清单见[新 Player 文件](evidence/hero-input-player-platform-files-2026-09-16.json)。下方 350／155 项矩阵属于前三次构建，不能算作当时第四次构建重新通过。
- 旧 `player-mono` 清理整条命令被自动审批以 `blocked by policy` 拒绝，未执行、未重试，该阶段释放 **0 字节**；平台阶段 Player、旧产物及证据的保留条件见[续接清理记录](evidence/hero-input-platform-cleanup-2026-09-16.json)。此前实际释放 381.25 MiB 的记录保留。

前台手感／性能和整体 UI 视觉签署仍不在本次基本本地验证内；未构建 IL2CPP，不替代双机器 LAN 或整个 M5 验收。

## 范围与保留合同

- 输入：保留 `GameCore.PlayerInputs`、`YYInputRebindingService`、`YYInputSettingsStore`、`YYInputSettingsData` 与现有公开调用；复用 Unity InputAction 以及 YYGC Interaction Sessions，不建立同义的 Profile／玩家输入运行模型。
- 操控：保留 `CampInput` 和旧营地指令，将单位旧自动决策提取为可装配能力；新增主角输入／控制能力，共用 ActorState、移动、战斗与工作，不出现第二套实体。
- 键位：A/D 左右，空格跳跃，S 下穿单向平台，左键使用背包当前道具，数字键／滚轮切换道具；W 预留向上，不用于跳跃。产品入口固定为主角动作组；显式开发旧模式仍与主角互斥，菜单、背包和改键由交互通道协调。
- 主角由服务端独占分配；每位首次 Ready 且有权限的玩家默认新建一名专属村民，同一角色最多一个控制者。现有 SharedCamp／HostOnly 权限仍适用，控制权不依赖 FishNet 单位所有权。重复 Ready 和策略恢复不重复增员；断线、失去权限或角色死亡释放控制，真正重连生成新人。
- 连续输入与一次性按钮变化分开处理；服务端验证连接、epoch、策略、角色控制权、输入序号和范围，过期输入归零。Host 使用相同入口。
- 使用道具时固定角色、选择版本／道具、目标和序号；背包状态、冷却与消耗由服务端所属能力维护。首版提供可以实际使用的基础道具栏，不把拾取、掉落、制作、交易等未要求玩法写成已有功能。
- 新增纵向运动、着地和单向平台合同；旧地面单位保持原有运动、数值和攻击时序。空中战斗、箭矢目标、表现锚点、快照和保存一起调整。未提供的跳跃专用素材不伪造为现有动画。
- 当前 Linear、Unity 6000.4.9f1、C# 9／.NET Standard 2.1、人工资产和 GUID、单份场景布局继续保留。

## 并发工作隔离

游戏起点 `38d1f95`；输入起点采用游戏锁定的 YYGC `745f3d2`。用户 YYGC 当前工作区 `56afcad` 正有其他任务引入插件，包含 README 和 AnyRule 文档／工具未提交修改。

- 游戏在 `codex/hero-input` 工作。
- YYGC 在 `D:/Developer/YYGC-worktrees/input-actions` 的 `codex/input-actions` 工作，提交只包含本批输入、Sample、测试和文档。
- 不切换、重置或提交 `D:/Developer/YYGC` 当前工作区，不触碰另一个任务的插件目录、生成文件或未提交修改；不竞争其 Unity 项目。
- 测试使用当前游戏的独立宿主及 `.deps`，不复制整套 Library。框架完成后使用明确提交锁定，输入提交保留在 YYGC 仓库中供插件分支合流；不把未验证插件顺带带入游戏。
- 若同名文件有新增并发修改，先核对内容和基线，按具体冲突处理，不覆盖整个文件或清理他人目录。

## 已核实的输入缺口

| 项目 | 当前证据 | 本批处理 |
|---|---|---|
| 改键异常 | 先 Disable，再创建操作；组合根等错误可留下禁用状态 | 修改前验证、异常恢复与释放 |
| 改键结束 | Dispose 不等于 Cancel，旧 wasEnabled 不代表当前模式仍允许输入 | 保留低层 API，增加受生命周期管理的入口；旧、新入口共用实现 |
| 加载原子性 | Unity 默认先清除覆盖，再解析 JSON | 候选验证，失败保留当前绑定 |
| 文件保存 | 直接 WriteAllText 覆盖，Version 未校验 | 原子替换与版本校验，沿用现有文件名／字段 |
| 查重范围 | 当前 API 正确执行全资产路径查询 | 保持默认语义，增加按动作组／设备组过滤能力 |
| 模式与 UI | 已有 Interaction Sessions，但没有动作与会话的统一接线 | 增加薄适配，复用通道、会话句柄和 Unity 动作 |

这些工具主要在设置操作时使用，没有已证实的性能瓶颈。只对新增稳定运行路径设定无持续分配的目标，最终以测量记录为准。

## 批次与依赖顺序

| 批次 | 输入与工作 | 输出 | 状态 |
|---|---|---|---|
| E0 | 核对仓库、并发任务、磁盘、冻结合同 | 本文、隔离分支和起点记录 | 完成 |
| E1 | 修复 YYGC 输入缺陷，补生命周期和动作接线 | 输入代码、针对失败路径的回归、保留 API 的说明 | 完成；33 项服务／文件回归通过 |
| E2 | 与输入代码一起准备 Sample 脚本，编译后批量生成场景 | 可导入 Input Actions Sample、原生场景、操作／接入文档 | 完成；实际场景回归与截图通过 |
| E3 | 提取旧控制能力，新增主角运动、角色占用和道具意图 | 同一 ActorState 的两种控制模式、服务器验证 | 完成；基础主角 37 项、真实弱网 35 项通过 |
| E4 | 接入原生 UI、输入动作、表现、平台、网络和完整恢复 | 默认主角模式、开发旧模式及匹配的数据版本和内容摘要 | 实现及原生资源导入完成；协议 8／存档 v3；产品旧营地入口暂时隐藏 |
| E5 | 完整 Editor／Play；按新增输入分批构建 Mono 并复用 | 下方验证矩阵、日志、截图和构建产物 | 完成；178 个不同用例按影响合并通过；历史产物 350／155／43 项，当前 Mono 39/39 |
| E6 | 复核差异、资源引用、并发目录和清理 | YYGC 改动账本、依赖锁定、中文提交及交付记录 | 复核、归档及本批清理完成；两仓库中文提交、不推送 |

脚本与程序集批量准备后只触发一次相应导入；需要脚本编译结果的场景生成随后串行执行。Sample 场景只向指定空目录初建，普通导入／构建不覆盖人工资源。

## 输入 Sample 交付合同

- 位于 YYGC `Samples~/InputActions`，通过 package samples 条目导入；使用独立命名空间／程序集，不引用 Dark Nights。
- 提供实际序列化的场景与动作资产；能观察移动、跳跃／使用按钮按下持续松开、UI 阻塞、模式切换、改键、取消、保存和重载。
- 场景无需联网或读取玩家游戏存档；示例输入设置使用独立文件名，界面显示实际动作和状态，不要求使用者理解内部实现才能操作。
- 文档列出导入与打开步骤、键位、改键流程、原有 API 与新增入口的关系、作用域释放、异常处理和验证边界。

## 验证矩阵与失败即停

1. 静态：C#／程序集依赖、手写文件长度、公开 API 兼容、样板与正式游戏隔离、资源 GUID、引用与内容摘要。
2. YYGC：非法／组合绑定、创建失败、取消／释放／界面关闭、重复启动、模式失效后结束改键、上下文查重、坏 JSON／未知版本、真实文件写入失败保留旧数据。
3. 输入与 UI：短按与多模拟步只执行一次；按住切模式、UI 点击与拖拽不穿透、失焦和设备丢失归零；重复进入 Play 不重复订阅。记录新增输入运行路径的分配与耗时，不能用后台容量结果声称前台性能达标。
4. 游戏规则：旧模式冻结规则继续通过；两种控制不会同时推进；跳跃、落地、平台下穿、喷气与燃料、道具选择与工作／攻击范围由权威状态决定。
5. 恢复：空中运动、道具及其计时完整保存／恢复；失败加载保留旧世界；加载 epoch 后旧控制与输入不生效。
6. 独立 Mono：Host＋客户端，角色争抢、非法目标、重发、暂停、策略切换、晚加入、断线／重连与弱网；复用同一产物执行既有相关矩阵和新增主角场景。
7. 原生场景／Prefab：保存、重开、运行；实际截图复核。Sample 也必须在真实场景运行，不能只交脚本或生成入口。

编译失败停止场景生成；引用／存档／权威检查失败停止 Player 验收。只重跑修复所影响的阶段。Mono 与 IL2CPP 分开记录，本批没有主动生成 IL2CPP 的授权；双机器 LAN、原有前台性能及 M5 未完成状态保持明确。

## 产物与清理记录

本批报告集中在 `artifacts/hero-input`，提交的摘要放 `docs/evidence`。阶段开始 D 盘剩余约 37.8 GiB、C 盘约 6.5 GiB；编译／构建前重新检查。只清理本批可重建、未被进程占用的已核验绝对路径，保留源码、人工资源、报告及验收 Player；既往被拒绝的清理不重试。

## 已实施方案

`ActorBehaviour` 仍拥有唯一 `ActorState`，共用行动时钟后只选择一个控制分支。`AutomaticActorControlBehaviour : IAutomaticActorControl` 按原顺序承接自动工作、训练、寻路和战斗；`HeroControlBehaviour`、`HeroMotionBehaviour`、`HeroInventoryBehaviour` 分别处理玩家意图、纵向运动和三格道具。能力通过原 ObjectDefinition 装配，三个友军职业可接管，敌军只装配自动控制。

`Gameplay.inputactions` 保留 `Player/Move`、`Jump`、`Attack`、`Crouch` 等 Unity 原生名称；Attack 表示使用当前道具。`GameInputActions` 接 YYGC Interaction Sessions，`HeroPlayerController` 只采样及提交。默认主角模式不显示顶部工具栏，也没有 Tab／道具栏营地切换入口，按键仍直接驱动动作；自动验收旧营地模式显式传入 `--dn-camp-mode`，或由 `SessionAutomation` 的 `hero-mode` 操作调用同一模式切换入口。

主角连续输入使用新增 `HeroInputCommand`，仍走 Gateway/Sender/Processor。变化最多 30 Hz、静止 10 Hz 保活，500 ms 无新输入归零；连接代次、控制租约、epoch、策略版本、单调序号及时间窗同时验证。离散道具请求带选择版本和目标，不用客户端伤害值。Host 与客户端使用相同权威入口；不为每个输入包发送回执或强制发布完整世界。

Pinewatch 保留原 16 个摆放及人工 GUID，另加三个原生单向平台；主角 UI、动作资产与平台均已保存重开。高度进入命中距离、箭矢目标和展示。新规则只增加 `hero_control` 八个字段，原 196 项规则不变。无跳跃专用动画；三格提供职业武器、工作工具、可装备喷气背包，尚无拾取／制作／交易系统。

协议升级为 8，保存格式为 v3。位置高度、垂直速度、平台支撑／下穿计时、手动状态、道具选择及喷气装备／燃料完整持久化；玩家连接、占用、租约和按钮不保存。加载后由新 epoch Ready 只接回仍带手动主角标记的保存对象；连接记录的 ID 仅用于验证对应对象，不能把同 ID 的普通闲置村民当成主角，缺少可恢复对象时才新建。旧 v1／v2 文件保留并明确拒绝。

## YYGC 与 Sample 当前证据

输入提交为 `0c7cec00b7a7f9cec0287bb56d0af9fc45c9d143`，已在 `codex/input-actions`；游戏准备脚本锁定该提交。`.deps/YYGC-unified` 原有七份 tracked 补丁及两个友元文件的字节在切换前后核对一致，准备脚本精确校验通过。用户 `D:/Developer/YYGC` 的 master／插件工作区保持原状。Git 的 LF／CRLF 转换单独核对，不作为输入实现差异。

框架 Sample 位于 `Samples~/InputActions/Content/InputActions.unity`；游戏中已导入到 `Game/Assets/Samples/YYGCInputActions`，44 个文件与隔离框架版本逐文件校验。Package Manager samples 条目、原生 PlayerInput、UGUI 与 InputActionReference 均已序列化，初建工具不在普通导入／构建时覆盖资源。

框架 34 个不同 PlayMode 用例按影响合并通过：33 个服务／文件用例，末次真实场景 1/1；不是单次 34/34 全绿运行。场景使用官方 InputTestFixture 虚拟键鼠驱动原生 UI，验证点击隔离、模式／模态／焦点、改键、实际保存重载及重复启停；两张真实截图已复核。Sample 没有单独构建 Player。

路由微测量：10,000 次 Refresh 与全动作 CanRead，2 动作 2.4844 ms、32 动作 75.8918 ms；GC.Alloc 记录器校准命中 4096 字节对象的一次分配，测量循环均无分配。仅代表路由内核，不代表 UI、网络、完整帧或前台性能。详细结果见[框架证据](evidence/hero-input-framework.json)。

## 验收中发现与修正

- 全量 Editor 172 项首轮通过 170 项：新增武器测试在逐步相减的浮点尾数处提前一帧断言，调整为原时序允许的一个固定步量化范围；另一项受本机 FMOD 输出设备初始化干扰。相关 3 项复跑通过，未改变攻击前摇或掩盖日志错误。
- 新增 Mono 主角驱动修正了四处测试合同：营地命令对已接管角色返回 InvalidRequest；场景 Transform 到像素的 float 尾数使用 0.001 像素容差且同时检查支撑与垂直速度；加载后用新 epoch Ready 判完成，因为旧 epoch 回执可被正常丢弃；重连后的回执按 epoch／连接代次区分，不与旧连接高序号比较。失败报告保留，不计入最终通过。
- 首次真实截图发现旧 HUD 提示“空格继续”以及帮助页旧键位。当前提示改为点击继续、按模式显示操作；帮助页通过明确的 ControlsGuide 绑定，在打开时展示当前跳跃绑定。原首版 UI 几何和冻结文字夹具保留，未重新生成原生面板。该新增输入触发第二次 Mono 构建，完整主矩阵使用此同一产物。
- 后续画面复核调整了新增 Hero 道具栏的底部偏移 185→300，保留地面角色与普通跳跃的可见空间；只修改新 Prefab 和其初建工具，因此第三次构建只复验受影响的启动、主角、局部输入和画面。没有改原有 UI 布局。
- 弱网权限断言在来宾尚未收到新策略时发出请求，服务端正确返回 PolicyChanged。测试增加来宾观察到新 PolicyRevision 的屏障后再检查 PermissionDenied；未放宽权限规则。
- **保留观察**：首次弱网喷气飞行等待超时，当时未记录完整轨迹，具体原因没有定位。添加轨迹后两轮同条件飞行分别达到 99.76、90.42 像素，最终完整弱网 35 项通过；没有修改游戏代码、降低原 `>90` 门槛或延长旧输入有效期。原失败、两份轨迹及策略断言修正均保留，不能将这次未复现观察描述成已定位修复的代码 BUG。

## 平台修正前的验证与产物身份

完整证据见[机器记录](evidence/hero-input-2026-09-16.json)。以下均为本批实际运行，历史协议 7 成绩不计入。

| 范围 | 结果与边界 |
|---|---|
| 纯算法／配置 | Core 1043 项通过；原 196 项规则及完整旧 JSON 内容不变，仅增 8 项主角参数 |
| 架构 | 316 个正式手写 C#、12 项守卫自测、0 错误 |
| 游戏 Editor／Play | 172 个不同用例按影响合并通过：首轮 170/172，相关 3/3、UI／实际 Play 10/10；后两批使用 `-noaudio` 隔离本机音频设备问题 |
| YYGC 输入／实际 Sample | 34 个不同 PlayMode 用例按影响合并通过；见上方独立证据，未单独构建 Sample Player |
| 第二次 Mono 构建的完整矩阵 | 350/350：启动 6、会话 13、并发 13、活跃恢复 14、四人恢复 24、9 组弱网各 24、双分辨率 12、战斗 9、普通资源三夜 22、容量 21 |
| 第三次 Mono 构建的相关复验 | 155/155：启动 6、主角基础 37、主角弱网 35、指令圈基础／弱网各 22、终局 UI 21、双分辨率各 6；其中启动／双分辨率 18 项属于主矩阵覆盖的重复复验 |
| 原生画面 | 实际主角 3 张、Sample 2 张、终局 4 张、双分辨率昼夜／构图／菜单 14 张，共 23 张原图已复核 |
| 资源与依赖 | 551 项原素材字节、1415 个既有 GUID、16 个旧摆放身份保留；44 个导入 Sample 文件与框架源一致；9 个宿主补丁／友元文件哈希保留 |

主矩阵真实 UDP 扰动为 RTT 0／100／200 ms × 丢包 0／1／5%，单向抖动 25 ms；同机四进程。最终主角弱网为 RTT 200 ms、丢包 5%、抖动 25 ms，实际丢弃 73 包、重排 273 包；指令圈弱网实际丢弃 36 包、重排 220 包。容量检查为 256 实体／1024 箭矢、30 秒后台观察，只证明功能与容量，不代表前台帧率。

本批共构建三次 Mono，每次都有新增输入，未构建 IL2CPP：

1. 初次实现，414 文件／199374976 字节；真实截图暴露旧提示后被替代。
2. 提示修正，414 文件／199376158 字节；完整 350 项主矩阵使用此产物。
3. 新道具栏位置修正，**413 文件／199130911 字节**；当时交付使用此产物，相关 155 项通过。续接任务的第四次构建已替代它，见本文开头。

第三次构建路径：`artifacts/hero-input/player-mono/DarkNights.exe`。其 6174 个输入（含游戏 Assets／Packages／ProjectSettings 和有效 YYGC／FishNet 依赖）及 413 个 Player 文件哈希在当时验收后保持不变，完整清单见[Player 文件证据](evidence/hero-input-player-files-2026-09-16.json)。第二、三次构建的 Core／Runtime／View／Entry 四个游戏 DLL 字节一致；6174 个输入仅新道具栏 Prefab 与初建工具两处不同。Entry SHA-256 为 `16e08f4296435dafce4fcf14e000e3fdd778f8be02821c13679a83b417ace71d`。平台及默认分配历史构建身份以各自证据为准；当前 Player 身份见本文开头和默认村民生成证据。

第二次构建的全部玩法／网络证据按影响复用；**没有宣称第三次产物重新执行过全部 350 项**。旧报告保留运行时的 `player-mono` 路径，具体身份以对应完整文件清单为准。被替代的两个 Player 已在归档和逐文件核验后清理，三个构建日志、清单、成功／失败报告继续保留。

## 复验与示例入口

- 游戏：Unity 打开 `Game`，进入 Pinewatch 或运行当前完整 Player。当前玩家入口直接进入主角操作且不显示顶部工具栏，详见[玩家说明](../PLAYER_GUIDE.md)。按 3 选择喷气背包后左键装备，空中按住空格喷气。
- 输入示例：Package Manager → Game Core → Samples → Input Actions → Import，打开 `Content/InputActions.unity`。游戏已导入副本为 `Game/Assets/Samples/YYGCInputActions/Content/InputActions.unity`；同级上层 README 提供完整操作与维护说明。
- 框架源码与 API：`D:/Developer/YYGC-worktrees/input-actions/Documentation~/INPUT_ACTIONS.md`。保留原方法名与原生返回类型；新增 `YYInputActionService` 及 `YYInputRebindingHandle` 的用途、迁移成本和限制已对比列出，全部 54 文件见[YYGC 改动账本](../YYGC_CHANGES.md#hero-input)。

已有资产不运行初建工具。确有源码／资源输入变化时，Mono 构建入口为 `DarkNights.Editor.GamePlayerBuild.MonoToEmptyDirectory`，`-darkNightsOutput` 指向新的空目录；测试使用 `DarkNights.Tests`，框架 Sample 使用 `YYGC.InputActions.Tests` 的 PlayMode。

```powershell
pwsh -NoProfile -File tools/prepare-lan-sample.ps1
$player = (Resolve-Path 'artifacts/hero-input/player-mono-generated-villager-r2/DarkNights.exe').Path
pwsh -NoProfile -File tools/test-game-hero.ps1 -PlayerPath $player -Port 28600 -Capture
pwsh -NoProfile -File tools/test-game-hero-network.ps1 -PlayerPath $player -Port 28610
pwsh -NoProfile -File tools/test-game-hero-network.ps1 -PlayerPath $player -Port 28630 -CommandRings
```

各脚本只使用独立验收槽位；保持同一 Editor 的写入、编译和构建串行。全量回归入口仍为 `tools/test-game-delivery.ps1 -PlayerPath <完整路径>`，没有新增输入时复用已有结果。

## 清理与保留边界

本批清理前核验绝对路径、父子重解析链接和活动进程；仅删除两个已被替代的 Player、一次性资源写入器／CLI 包装、过期 PID 及已查看的合成拼图，实际移除 **399769079 字节，约 381.25 MiB** 的文件内容。清理后 C／D 剩余约 5.94／37.20 GiB；盘符变化包含其他进程活动，不当作本任务释放量。

平台阶段 Player、前三次构建的完整清单、原始截图、XML、日志、成功／失败报告与隔离存档保留；本批目录、相关矩阵目录和共享依赖已列入[保留清单](evidence/hero-input-retained-2026-09-16.json)，父子目录体积不可重复相加。`.deps/YYGC-unified` 的宿主补丁与 `D:/Developer/YYGC` 插件工作区未清理。历史已拒绝的 `ArchitectureGuard/bin/obj`、`WinPlayerBuildProgram`、U6 source／archives 和旧调试目录没有重试，继续按[历史交接](STAGE_CLEANUP_INVENTORY.md)保留。

当前三格道具是最小可用能力示例，没有拾取／制作／交易和新跳跃动画。高延迟操控仍受权威往返影响，未做客户端预测；超过 60 服务端 tick 的旧输入拒绝，30 tick 无新输入归零。前台手感／性能、IL2CPP、双机器 LAN 与 M5 最终验收仍在各自边界内，没有以本批自动化通过替代。
