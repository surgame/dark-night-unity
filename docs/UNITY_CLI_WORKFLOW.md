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
unity command eval --project-path $dnProject --code 'return new { playing = UnityEditor.EditorApplication.isPlaying, compiling = UnityEditor.EditorApplication.isCompiling };' --json
unity command run_tests --project-path $dnProject --mode editor --filter DarkNights.Tests.MineralBehaviourTests --async_tests true --json
unity command test_status --project-path $dnProject --json
```

`run_tests` 的测试名筛选是子串匹配，不是正则表达式。要提交一组明确的不同测试，沿用游戏的有限批次请求文件，再显式调用 `DarkNights.Editor.TerrainValidationRunner.ProcessQueued()`。这样不依赖 Editor 在后台持续触发空闲轮询。

长构建使用 `command eval ... --detach --json`，保存返回的 job ID，随后通过 `unity job status ID --project-path $dnProject --json` 读取完成状态。提交成功不等于构建完成；必须检查 job 状态和游戏构建摘要。编译后的域重载可能让下一次调用返回 Connection reset；先核对请求文件是否已消费、构建是否已开始，再恢复同一已排队请求，不能重写请求或重复构建。不要因等待超时重复启动构建。

本机 `capture_scene_view --save_path Temp/example.png` 实际输出到 `Assets/Temp/example.png`。它不接受 `..` 或项目外的绝对路径。截图应使用本任务专用目录，结束后按项目规则归档其 PNG 和自动生成的 `.meta`；不要把临时图片当作正式资产提交。

## 对用户电脑的影响

CLI 的 Editor 命令不需要鼠标键盘，也不必添加 `--focus`。编译、测试、构建仍会占用 CPU、内存和磁盘；Editor 主线程执行构建时不能同时响应其他 Editor 操作。单项目仍只有一个 Unity 写入通道。

已有 Editor 打开时优先使用 `unity command`。根命令 `unity test`／独立批处理可能启动第二个 Editor，同一路径会遇到工程锁，不能用来并行写同一份 Library。

独立 Player 的渲染验收需要图形窗口和真实相机回执。本轮脚本支持 `--background`，以 Windows 的不激活窗口方式启动；这减少窗口抢焦点，但不是无图形执行。`-nographics` 不能替代本项目的视觉 Ready 验收。

实际鼠标交互、操作系统安全弹窗、跨机器操作仍可能需要用户或 Computer Use。日常编译、测试、构建、资产 API 操作和离屏图像检查已经可以移到 CLI。测试和构建应串行运行，先保存用户正在编辑的场景。

## 证据

原始 JSON 保存在 `artifacts/mineral-map-migration-20261005/`：`unity-cli-tools.json`、`cli-recompile-r*.json`、`cli-native-tests-*.json`、`cli-composite-*.json`、`cli-capture-scene-r4.json`、`cli-build-*-job.json` 与 `cli-build-*-result.json`。这些结果对应本次环境，不能替代未来升级后的核验。
