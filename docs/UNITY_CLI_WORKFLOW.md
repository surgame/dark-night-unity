# Unity CLI 环境与后台开发流程

本机已经具备可用的 Unity CLI。本次矿层迁移用它连接当前打开的 Editor，在不发送鼠标键盘操作、不请求前台焦点的情况下完成编译、Editor 测试、截图及 Mono 构建。日常开发优先使用这条通道，用户可以继续操作其他程序。

## 已核验环境

2026-10-05 的本机结果：

| 项目 | 实际结果 |
| --- | --- |
| CLI | `1.0.0-beta.12`，Windows x64；检查时为该 beta 通道最新版本，没有升级 |
| 可执行入口 | `C:/Users/Jobscn/AppData/Local/Microsoft/WindowsApps/unity.exe`，已在 PATH |
| Editor | 项目 `Game`，`6000.4.9f1`，连接端口 `7800` |
| Pipeline | 工程原有 `com.unity.pipeline 0.6.0-exp.1`，没有新增或升级依赖 |
| 连接 | `unity status --json` 返回一个就绪实例 |
| 能力目录 | `unity command list --json` 返回 149 项命令 |
| 本轮操作 | 全部使用 CLI 和文件工具；Computer Use 调用次数为 0 |

官方文档说明，控制已打开的 Editor 需要 Pipeline；CLI 提供 JSON 输出及退出码，当前仍属于实验性工具。本机已有所需组件，不需要再安装第二套 Unity MCP。[Unity CLI 文档](https://docs.unity.com/en-us/unity-cli/unity-cli)

## 能完成的工作

| 工作 | 本机验证状态 | 采用方式 |
| --- | --- | --- |
| 查看实例、版本、命令与参数 | 已执行 | `status`、`version`、`command list` |
| 触发导入和编译并读取错误 | 已执行，0 错误、0 警告 | `recompile` |
| 执行有限 Editor C# 操作 | 已执行 | `command eval`；复用游戏已有 Editor API |
| Editor 测试和机器可读结果 | 已执行 | `command run_tests`、`command test_status` |
| Mono Player 构建 | 已执行 | CLI 调用现有 `TerrainValidationRunner`，保留 Addressables 构建与配置恢复步骤 |
| Scene View 截图 | 已执行，960×540 PNG | `command capture_scene_view`；保存路径受 authoring root 限制 |
| Play、停止、Game View 截图 | 命令已暴露，未单独计作本机验收 | `editor_play`、`editor_stop`、`capture_game_view` |
| 场景、Prefab、资产、Animator 操作 | 命令已暴露，按任务逐项验证 | 使用结构化命令或 Editor API，利用提供的 dry run／Undo |
| 独立 Host 与客户端联机检查 | 已有游戏脚本 | Python 启动相同 Player、发送真实游戏输入、读报告；CLI 负责生成 Player |

命令可用不表示任意复杂动作都已经验收。截图证明通道能输出像素；游戏美术通过仍需检查对应候选的完整组合画面。

## 常用命令

在仓库根目录运行 PowerShell：

```powershell
$dnProject = Join-Path (Get-Location) 'Game'
unity status --json
unity recompile --project-path $dnProject --timeout 60 --json
unity command --caller plugin --skill unity-cli eval --project-path $dnProject --code 'return new { playing = UnityEditor.EditorApplication.isPlaying, compiling = UnityEditor.EditorApplication.isCompiling };' --json
unity command --caller plugin --skill unity-cli run_tests --project-path $dnProject --mode editor --filter DarkNights.Tests.MineralBehaviourTests --async_tests true --json
unity command --caller plugin --skill unity-cli test_status --project-path $dnProject --json
```

`run_tests` 的测试名筛选是子串匹配，不是正则表达式。要提交一组明确的不同测试，沿用游戏的有限批次请求文件，再显式调用 `DarkNights.Editor.TerrainValidationRunner.ProcessQueued()`。这样不依赖 Editor 在后台持续触发空闲轮询。

本机 CLI 的 `--caller`／`--skill` 标签参数放在 `command` 与具体子命令之间；放在 `eval` 后会被当作未知参数。2026-10-05 目录整理已用上述顺序完成只读检查与测试夹具归档，没有改变依赖或启动新 Editor。

长构建使用 `command eval ... --detach --json`，保存返回的 job ID，随后通过 `unity job status ID --project-path $dnProject --json` 读取完成状态。提交成功不等于构建完成；必须检查 job 状态和游戏构建摘要。编译后的域重载可能让下一次调用返回 Connection reset；先核对请求文件是否已消费、构建是否已开始，再恢复同一已排队请求，不能重写请求或重复构建。不要因等待超时重复启动构建。

本机 `capture_scene_view --save_path Temp/example.png` 实际输出到 `Assets/Temp/example.png`。它不接受 `..` 或项目外的绝对路径。截图应使用本任务专用目录，结束后按项目规则归档其 PNG 和自动生成的 `.meta`；不要把临时图片当作正式资产提交。

## 有限批次与依赖屏障

本节承接[协作约定](../AGENTS.md#execution-efficiency)中的操作细节，不改变授权、资源保护或验收范围。

1. 先列出本批输入、依赖顺序、输出和验收项，合并同类代码、程序集声明与文本资源。每阶段及编译／构建前检查磁盘；每批Play／测试／构建前按[内存门控](UNITY_MEMORY_INCIDENT_20261007.md)检查三项指标，确认独立监控和短时探针。构建另外预留余量。
2. 统一等待必要导入和新增脚本编译，再执行同批Prefab、ObjectDefinition、绑定与Addressable条目操作，集中保存／生成／核对。自动导入已完成时复用结果，不重复刷新；已有资产移动携带`.meta`，新增meta交给Unity，不重建已有GUID或绕过引用检查。
3. 暂停导入期间不等待编译、不读取未导入资源，异常必须释放暂停状态；首版初始化仅输出到指定空目录，批处理不得覆盖人工Prefab、场景、动画或Theme。
4. 每个依赖就绪的逻辑批次主动提交一次；Unity内部必要导入、生成后编译和域重载不算重复请求。沿用现有有限入口续接依赖步骤，不跳过屏障，也不新建通用调度框架。同一Editor的写入、生成、编译和构建串行，不用MCP／CLI请求竞争状态。
5. 长任务保存job／请求身份，完整输出写日志。优先完成通知或原子结果文件；首次有界等待，需轮询时通常20–30秒检查，状态不变即退避，稳定长任务最多每60秒做一次小状态检查，单次阻塞不超过60秒。不重复读取全日志、发送未变进度或因超时重启任务。后台运行须说明后续由用户／后续任务恢复的边界，不承诺没有执行入口的自动续接。
6. 失去回执先检查崩溃、日志反馈、请求消费和任务是否运行；不直接重发。按影响安排完整验证矩阵，每个获批后端／配置构建一次并复用同一产物；失败即停，只重跑失败或受修复影响阶段，输入／配置／依赖或证据未变不重跑已通过项。

批次摘要至少包含任务ID、阶段、完成标记、退出码、完成／失败项、不同用例计数和日志／产物路径；详细输出需要定位时再读。结束集中核对差异、引用与输出完整性，额外刷新／生成／编译／构建说明新增输入或失败原因。提交成功、进程启动和调度完成都不等于游戏验收通过。

阶段产物按[盘点与保全流程](WORKSPACE.md#阶段产物盘点与保全)处理；Player默认Mono，主动生成、覆盖或验证IL2CPP须用户先明确确认原因、范围和预计产物，获批后一次构建复用并单独记录后端结论。

## 薄 worktree 与 Local 验证

用户明确要求worktree时，默认仅隔离代码、文档、配置和文本资源；除非同时明确要求独立Editor、并行验收或独立缓存，不提前支付完整Unity缓存成本。

- 薄worktree不启动Unity，不生成、复制、硬链接或目录联接`Library`、`Temp`、`Logs`、`obj`、构建输出或`artifacts`；`.worktreeinclude`排除这些路径。仅非Unity检查确实需要时准备最小锁定依赖。不共享／链接同一Library给多个检出，不并发写入同一缓存。
- 先完成实现、静态检查和非导入验证，建立可恢复checkpoint；需要Editor编译、PlayMode、场景／Prefab保存重开、Player或画面时，先组合本批候选，再通过Codex“移交到Local”复用Local现有Library和单一Unity通道。checkpoint或临时分支不代表候选必须采用。
- 移交前后核对分支、未提交修改及Editor／Player进程，不覆盖其他聊天或用户工作。移交仅借环境验证，不构成merge、rebase、cherry-pick或交付授权；是否集成按原任务目标判断。
- Editor版本、manifest／packages-lock、关键ProjectSettings、平台／Scripting Backend、渲染管线或大批资源导入设置变化，先报告Local缓存失效／反复导入风险；未经用户明确选择不升级为第二套完整Unity工作区。确需独立验收只保留一个长期验证worktree，仍串行使用Editor。
- 弃用实验先移交回原worktree，再依据授权和可恢复性归档或移除源码检出；不为保留Unity缓存长期留完整副本。移除工作树不授权删除其中产物，缓存、存档、报告和用户成果仍按保全规则核对；不能用删除工作树父目录绕过保护。

Local长任务采用上节有限批次规则。此流程不授权修改YYGC源码／补丁／锁定版本，所需具体授权仍遵循[YYGC约束](../AGENTS.md#yygc-授权与锁定依赖)。

## 对用户电脑的影响

CLI 的 Editor 命令不需要鼠标键盘，也不必添加 `--focus`。编译、测试、构建仍会占用 CPU、内存和磁盘；Editor 主线程执行构建时不能同时响应其他 Editor 操作。单项目仍只有一个 Unity 写入通道。

已有 Editor 打开时优先使用 `unity command`。根命令 `unity test`／独立批处理可能启动第二个 Editor，同一路径会遇到工程锁，不能用来并行写同一份 Library。

独立 Player 的渲染验收需要图形窗口和真实相机回执。本轮脚本支持 `--background`，以 Windows 的不激活窗口方式启动；这减少窗口抢焦点，但不是无图形执行。`-nographics` 不能替代本项目的视觉 Ready 验收。

实际鼠标交互、操作系统安全弹窗、跨机器操作仍可能需要用户或 Computer Use。日常编译、测试、构建、资产 API 操作和离屏图像检查已经可以移到 CLI。测试和构建应串行运行，先保存用户正在编辑的场景。

## 证据

### 主角地面诊断记录

2026-10-05 增加 Editor 菜单，2026-10-06 按实际诊断范围更名为 `Dark Nights → Debug → 主角地面接触／跳跃调试`（全角“／”用于避免拆成两级菜单），用于分析坡顶、平台和短落差交界的地面接触与起跳资格。默认关闭；在本地 Host 或离线洞穴工作台 Play 中选择“开始记录”，在同一位置反复行走、冲刺和点跳，再选择“停止”。需要文件时单独选择“导出”。客户端没有权威运动步骤，不支持独立录制。

同日增加“打开 Profiler 浮窗”（现也可通过 `Window → Analysis → Dark Nights 主角地面接触／跳跃调试` 打开）。独立 UI Toolkit 窗口提供录制控制、10Hz实时曲线、状态统计、逐模拟步检查和最近CSV回看；曲线颜色区分脚底间隙、竖直速度、地面/空中、起跳和非起跳丢支撑。取消“跟随录制”或点击曲线只冻结检查视图，记录仍继续；关闭浮窗也不停止记录。开始与导出控制继续复用同一记录器。

2026-10-06 将“主角移动 8×（快速跑图）”开关统一放进该浮窗，移除原独立菜单。它用于快速到达检查位置；只增加房主权威主角的横向移动速度（含冲刺），不改变跳跃高度、敌人速度或游戏时间倍率。开关继续保存为本机 EditorPrefs，停止 Play 后才能设置，下次 Play 生效；检查正常移动与跳跃手感时关闭。

本次通过 Unity CLI 完成 Editor 编译（无错误）、实际附着浮窗的开关保存与启动倍率读取检查，并恢复检查前的本机设置。640px 宽窗口中新增控件布局未越界；编译后的菜单注册核对确认独立加速菜单退出、六个地面接触／跳跃诊断入口保留。本批未进入 Play 或构建 Player，不新增正常手感与联机通过结论。

输入对齐诊断schema2将脚底查询扩大到128逻辑像素，新增`jump-inputs.json`。正式本地Host按同一输入序号记录本地语义按键采样、发送、权威接受和模拟处理，使用同一单调时钟；按键采样时独立冻结显示脚底、权威脚底、快照tick、权威tick和有界碰撞格片段。最多1024个阶段事件，超过时计数但不刷日志；世界代次变化自动停止，避免旧输入关联。浮窗可显示两种脚底间隙及阶段耗时，新旧CSV均可回看。这里的显示坐标是本地采样时当前渲染对象的脚底根坐标，不是键盘设备的硬件事件时间；离线工作台仍保留运动记录，完整输入链针对正式Host。

记录来自 `TerrainHeroMotion.Tick` 的真实模拟步，包含求解前后位置、竖直速度、支撑、跳跃输入与消费、脚底间隙、格形状和地图版本。最多7200步，1倍速约2分钟；停止、达到上限或退出Play只冻结内存，不自动导出。记录由Editor SessionState跨脚本重载保留；成功开始新一轮会清空上一轮内存、按键事件、回看快照、统计、选中步与导出路径，并恢复跟随录制。已经导出的磁盘文件保留；浮窗重开不自动载入旧导出，点击“最近记录”才回看本轮最近的导出。载入失败时保留原视图，避免混用两轮数据。导出仅保存当时快照，不会停止正在进行的录制；输出在 `artifacts/hero-ground-trace/<时间与短ID>/`：`summary.json`、`steps.csv`、`terrain.json`、`jump-inputs.json`。附近65列×23行的碰撞格在开始时冻结；录制期间尽量只行走和点跳，地图版本变化会记录在CSV中。

同日根据 `20261005-162841-800-5574ba` 的失败记录修复Host本地主角操控：按下时显示脚底接地，但权威间隙35.98逻辑像素，显示横向落后15.83像素，跳跃发送等待22.64ms。`HostHeroProjection`复用正式Actor投影映射，每帧仅冻结本地主角展示值，验证epoch、槽位与控制租约；位置、高度、动作和支撑不再等待全世界10Hz投影。游戏侧顺序明确为许可刷新(-1300)、主角输入(-1200)、既有YYGC模拟(-1000)、个体显示(-800)、UI上下文(-700)，镜头随后跟随本帧显示。跳跃按下绕过30Hz发送等待，持续输入和心跳仍节流；记录器继续读取真正网络副本，避免把即时展示值误标为Replica。接地容差、跳跃规则、YYGC、协议与存档未改；远端玩家仍走原插值显示，客户端网络延迟未纳入本次修复。Editor主角35/35、表现28/28、诊断6/6，共69个不同用例通过；最终编译0错误，五个执行层级的脚本导入及默认顺序已核对，原183步诊断记录保留。未构建Player或执行独立进程联机验收，最终坡沿手感由用户实测。证据在 `artifacts/hero-ground-fix-20261005/`。

“未起跳丢支撑”和“近地空中”是诊断标记，需要结合轨迹和实际格形状判断，不自动归类为BUG。记录器只读、不调整跳跃、碰撞或土狼时间，编译到Player时完全排除。当前已通过脚本编译、3项Editor记录器检查及一次CSV导出检查；合成地形对照不是截图位置的实证。诊断证据保存在 `artifacts/slope-contact-analysis-20261005/`。

原始 JSON 保存在 `artifacts/mineral-map-migration-20261005/`：`unity-cli-tools.json`、`cli-recompile-r*.json`、`cli-native-tests-*.json`、`cli-composite-*.json`、`cli-capture-scene-r4.json`、`cli-build-*-job.json` 与 `cli-build-*-result.json`。这些结果对应本次环境，不能替代未来升级后的核验。
