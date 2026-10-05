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

## 对用户电脑的影响

CLI 的 Editor 命令不需要鼠标键盘，也不必添加 `--focus`。编译、测试、构建仍会占用 CPU、内存和磁盘；Editor 主线程执行构建时不能同时响应其他 Editor 操作。单项目仍只有一个 Unity 写入通道。

已有 Editor 打开时优先使用 `unity command`。根命令 `unity test`／独立批处理可能启动第二个 Editor，同一路径会遇到工程锁，不能用来并行写同一份 Library。

独立 Player 的渲染验收需要图形窗口和真实相机回执。本轮脚本支持 `--background`，以 Windows 的不激活窗口方式启动；这减少窗口抢焦点，但不是无图形执行。`-nographics` 不能替代本项目的视觉 Ready 验收。

实际鼠标交互、操作系统安全弹窗、跨机器操作仍可能需要用户或 Computer Use。日常编译、测试、构建、资产 API 操作和离屏图像检查已经可以移到 CLI。测试和构建应串行运行，先保存用户正在编辑的场景。

## 证据

### 主角地面诊断记录

2026-10-05 增加 Editor 菜单，2026-10-06 按实际诊断范围更名为 `Dark Nights → Debug → 主角地面接触／跳跃调试`（全角“／”用于避免拆成两级菜单），用于分析坡顶、平台和短落差交界的地面接触与起跳资格。默认关闭；在本地 Host 或离线洞穴工作台 Play 中选择“开始记录”，在同一位置反复行走、冲刺和点跳，再选择“停止”。需要文件时单独选择“导出”。客户端没有权威运动步骤，不支持独立录制。

同日增加“打开 Profiler 浮窗”（现也可通过 `Window → Analysis → Dark Nights 主角地面接触／跳跃调试` 打开）。独立 UI Toolkit 窗口提供录制控制、10Hz实时曲线、状态统计、逐模拟步检查和最近CSV回看；曲线颜色区分脚底间隙、竖直速度、地面/空中、起跳和非起跳丢支撑。取消“跟随录制”或点击曲线只冻结检查视图，记录仍继续；关闭浮窗也不停止记录。开始与导出控制继续复用同一记录器。

输入对齐诊断schema2将脚底查询扩大到128逻辑像素，新增`jump-inputs.json`。正式本地Host按同一输入序号记录本地语义按键采样、发送、权威接受和模拟处理，使用同一单调时钟；按键采样时独立冻结显示脚底、权威脚底、快照tick、权威tick和有界碰撞格片段。最多1024个阶段事件，超过时计数但不刷日志；世界代次变化自动停止，避免旧输入关联。浮窗可显示两种脚底间隙及阶段耗时，新旧CSV均可回看。这里的显示坐标是本地采样时当前渲染对象的脚底根坐标，不是键盘设备的硬件事件时间；离线工作台仍保留运动记录，完整输入链针对正式Host。

记录来自 `TerrainHeroMotion.Tick` 的真实模拟步，包含求解前后位置、竖直速度、支撑、跳跃输入与消费、脚底间隙、格形状和地图版本。最多7200步，1倍速约2分钟；停止、达到上限或退出Play只冻结内存，不自动导出。记录由Editor SessionState跨脚本重载保留；成功开始新一轮会清空上一轮内存、按键事件、回看快照、统计、选中步与导出路径，并恢复跟随录制。已经导出的磁盘文件保留；浮窗重开不自动载入旧导出，点击“最近记录”才回看本轮最近的导出。载入失败时保留原视图，避免混用两轮数据。导出仅保存当时快照，不会停止正在进行的录制；输出在 `artifacts/hero-ground-trace/<时间与短ID>/`：`summary.json`、`steps.csv`、`terrain.json`、`jump-inputs.json`。附近65列×23行的碰撞格在开始时冻结；录制期间尽量只行走和点跳，地图版本变化会记录在CSV中。

同日根据 `20261005-162841-800-5574ba` 的失败记录修复Host本地主角操控：按下时显示脚底接地，但权威间隙35.98逻辑像素，显示横向落后15.83像素，跳跃发送等待22.64ms。`HostHeroProjection`复用正式Actor投影映射，每帧仅冻结本地主角展示值，验证epoch、槽位与控制租约；位置、高度、动作和支撑不再等待全世界10Hz投影。游戏侧顺序明确为许可刷新(-1300)、主角输入(-1200)、既有YYGC模拟(-1000)、个体显示(-800)、UI上下文(-700)，镜头随后跟随本帧显示。跳跃按下绕过30Hz发送等待，持续输入和心跳仍节流；记录器继续读取真正网络副本，避免把即时展示值误标为Replica。接地容差、跳跃规则、YYGC、协议与存档未改；远端玩家仍走原插值显示，客户端网络延迟未纳入本次修复。Editor主角35/35、表现28/28、诊断6/6，共69个不同用例通过；最终编译0错误，五个执行层级的脚本导入及默认顺序已核对，原183步诊断记录保留。未构建Player或执行独立进程联机验收，最终坡沿手感由用户实测。证据在 `artifacts/hero-ground-fix-20261005/`。

“未起跳丢支撑”和“近地空中”是诊断标记，需要结合轨迹和实际格形状判断，不自动归类为BUG。记录器只读、不调整跳跃、碰撞或土狼时间，编译到Player时完全排除。当前已通过脚本编译、3项Editor记录器检查及一次CSV导出检查；合成地形对照不是截图位置的实证。诊断证据保存在 `artifacts/slope-contact-analysis-20261005/`。

原始 JSON 保存在 `artifacts/mineral-map-migration-20261005/`：`unity-cli-tools.json`、`cli-recompile-r*.json`、`cli-native-tests-*.json`、`cli-composite-*.json`、`cli-capture-scene-r4.json`、`cli-build-*-job.json` 与 `cli-build-*-result.json`。这些结果对应本次环境，不能替代未来升级后的核验。
