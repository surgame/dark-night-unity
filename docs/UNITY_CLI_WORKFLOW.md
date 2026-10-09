# Unity CLI与单Editor执行流程

本页管理Local单一Unity通道、有限批次、内存门控和测试／构建续接。稳定资源／权限边界见[AGENTS](../AGENTS.md)，当前输入和结果见[执行状态](DEVELOPMENT.md)，实际工具操作见[工作台](EDITOR_WORKBENCH.md)。文档整理不启动Unity或签署新Player通过。

## 环境与通道

Game使用锁定Editor及Pipeline，来源见[依赖说明](DEPENDENCIES.md)。本机2026-10-05已核验CLI 1.0.0-beta.12、WindowsApps/unity.exe、Pipeline 0.6.0-exp.1及一个就绪Editor；这是原核验身份，不声明CLI永远最新或端口／命令数不变。每批先读实际status／版本和必要参数，不重复枚举全能力目录。

| 工作 | 通道与边界 |
| --- | --- |
| 状态／版本／参数 | CLI只读状态；不因文档整理启动另一个Editor |
| 导入／编译 | 同一Editor串行，读取明确错误／完成回执 |
| 资产／场景／Prefab | 现有Editor API及结构化入口，保留Undo／作者来源／GUID，初始化只写指定空目录 |
| Editor用例 | 有限请求文件与TerrainValidationRunner或已核验run_tests接口 |
| Player | 现有GamePlayerBuild／验证Runner生成受影响Mono配置一次，完整产物复用 |
| 跨进程联机 | 现有游戏Python／PowerShell驱动向独立Host＋Client发送真实输入，原始报告落任务目录 |
| 截图 | 真实相机／Scene输出需实际检查像素；离屏探针不代替完整场景 |

CLI不需要鼠标键盘或--focus，但Editor主线程的同步构建仍阻塞其他操作。已打开Editor优先command通道；根unity test／独立batchmode可能再启Editor，不能并行写同一Library。Player --background减少抢焦点但仍需图形和真实Ready，-nographics不替代视觉验收。

## 常用命令

以下为已核验通道的示例，按当前CLI实际参数检查；编译／测试须先通过后续门控。

```powershell
$dnProject = Join-Path (Get-Location) 'Game'
unity status --json
unity recompile --project-path $dnProject --timeout 60 --json
unity command --caller plugin --skill unity-cli eval --project-path $dnProject --code 'return new { playing = UnityEditor.EditorApplication.isPlaying, compiling = UnityEditor.EditorApplication.isCompiling };' --json
unity command --caller plugin --skill unity-cli run_tests --project-path $dnProject --mode editor --filter DarkNights.Tests.MineralBehaviourTests --async_tests true --json
unity command --caller plugin --skill unity-cli test_status --project-path $dnProject --json
```

caller／skill位于command和具体子命令之间。run_tests的名称过滤是子串匹配，明确一批不同用例时沿现有请求文件，再调用 `DarkNights.Editor.TerrainValidationRunner.ProcessQueued()`，不依赖后台空闲轮询。

长构建使用command eval的--detach，保存job ID，用unity job status读取完成。Connection reset／域重载／超时先查请求是否消费、任务是否已开始和产物摘要，再续接原请求，不重写文件或重复触发。调度成功、进程开始和等待结束不等于构建／验收通过。

已核验capture_scene_view的Temp相对路径实际落Assets/Temp，不接受..或项目外绝对路径。截图使用本任务指定目录，结束保全PNG和Unity生成meta；不把临时截图提交为作者资源。

## 内存门控

每批Play／测试／构建先查磁盘、目标Editor私有内存、可用物理内存和系统提交余量。32 GiB本机阈值由[AGENTS](../AGENTS.md#内存与故障门控)及 `tools/ground-baseline/watch_memory.py` 的LIMITS维护：Editor达到8 GiB、RAM低于6 GiB、提交余量低于8 GiB停止。构建另预留阶段预计分配量；不足记录待验，不重复尝试或结束其他应用。

先通过unity status核对当前PID和Game项目，用独立监控先单次采样，再覆盖一批短时验证。示例中PID必须来自本批status，输出为本任务新目录：

```powershell
python tools/ground-baseline/watch_memory.py --pid <本批EditorPID> --project Game --output artifacts/<任务>/memory-preflight --once
python tools/ground-baseline/watch_memory.py --pid <本批EditorPID> --project Game --output artifacts/<任务>/memory --seconds 1800
```

监控每秒采样，越界写breach.json并请求停止本项目Play，随后禁止新批次；不会杀其他应用。响应依赖Editor，同步BuildPipeline可能不响应停止Play，所以启动前余量和短探针是必要前置。

F10先用有界队列／过滤用例、一条中文日志和三次开关验证，不重造日志风暴；通过后才扩展一批UI或其他场景。失去测试回执先排查崩溃、日志反馈和请求执行状态，不能直接重发。原2026-10-07两次OOM、历史4 GiB停止线和采样见[事故记录](archive/UNITY_MEMORY_INCIDENT_20261007.md)；当前门控为8 GiB，不改写原批次结论。

## 有限批次与依赖屏障

本节承接[执行效率](../AGENTS.md#execution-efficiency)的实际步骤：

1. 汇总同类输入、依赖顺序、输出与验收范围；每阶段检查磁盘，每批Play／测试／构建检查内存。保留原场景草稿及人工资源，必要时先保存用户正在编辑内容。
2. 集中写本批代码／程序集／文本资源，等待必要导入与编译，再执行依赖它们的Prefab／Definition／绑定／Addressables操作。自动导入已完成不额外刷新，已有资产移动携带meta，新meta交Unity。
3. 暂停导入时不等待编译／读取未导入资产，异常释放暂停。初始化仅指定空目录；批量调用不放宽人工Prefab、动画、Theme和场景保护。
4. 每个依赖就绪逻辑批次主动触发一次，Unity内部必要导入／生成编译／域重载不算重复请求。同一Editor写入、生成、编译、构建串行，不跳过屏障，不建通用调度框架。
5. 保存job／请求身份及完整日志，优先原子完成结果。首次有界等待，通常20–30秒检查，未变化退避；稳定长任务最多每60秒一次小状态，单次阻塞不超过60秒。不刷全日志、重复进度或因超时重启；说明真实后台续接边界。
6. 失败停后续批次，按新输入／失败／影响复验。每个获批配置构建一次并复用，不用旧来源通过数替代；没有新变化或疑点不重复完整矩阵。

批次摘要包含任务ID、阶段、完成标记、退出码、完成／失败项、不同用例计数、源／配置／Player身份和日志／产物路径。详细输出落文件；结束集中核对差异、引用和完整性，额外刷新／生成／编译说明新增输入或失败原因。

## Player与独立进程验收

默认Mono，主动生成／覆盖／验证IL2CPP先取得明确后端范围授权，获批后每受影响配置一次构建并分别记录。新构建使用空输出目录，保留完整Data／UnityPlayer／Addressables。现有构建方法参数按源码和任务请求核对，不用历史默认输出覆盖原Player。

联机覆盖当前玩法的2人／4人、正常／弱网、权限、支付竞争、Host单次执行、Ready／重连、暂停和epoch，见[联机矩阵](MULTIPLAYER.md#联机验收重点)。只验证当前已开放功能，旧营地／NPC／航程脚本不自动成为当前发布矩阵。

开发驱动明确传当前PlayerPath及新任务报告目录；test-game-delivery等旧脚本默认可能指历史artifacts，必须先核对阶段和来源。续跑只重跑未完成／受影响项，原退出码、DLL／内容身份、失败及中断保持。测试只写隔离槽位，不访问玩家存档；图片尺寸或窗口启动不能代替视觉检查。

前台性能按用户暂缓，后台帧时不签署前台通过。解除暂缓后按驱动ForegroundRole与操作系统前台证据检查；双机器需要真实设备，本机四进程不代替。冻结夹具不按当前结果重生成。

## 薄 worktree 与 Local 验证

用户要求worktree时，默认隔离源码／文档／配置及文本资源，在Local单一验收。薄树不启动Unity，不生成／复制／硬链接／联接Library、Temp、Logs、obj、构建输出或artifacts，worktreeinclude排除；仅静态检查需要时准备最小锁定依赖。

先完成实现／静态检查和可恢复checkpoint，再把同批候选移交Local复用已有Library。移交前后核对分支、未提交修改、Editor／Player及草稿；不覆盖其他会话，不把checkpoint／移交当merge、rebase、cherry-pick或交付授权。

Editor、manifest／lock、关键ProjectSettings、平台后端、管线或大批导入变化先报告缓存失效风险；未经用户明确选择不升级第二套完整Workspace。需要独立验收只保留一个长期验证树，仍串行Editor。弃用实验先移回原树，再按授权与可恢复性归档；工作树移除不授权删除其中缓存、存档、失败、用户成果或Player。

此流程不授权修改YYGC源码／补丁／锁定版本，具体范围仍按[框架约束](../AGENTS.md#yygc-授权与锁定依赖)。产物保全见[WORKSPACE](WORKSPACE.md#阶段产物盘点与保全)。
