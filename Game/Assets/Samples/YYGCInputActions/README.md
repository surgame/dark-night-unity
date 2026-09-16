# Input Actions Sample

在 Package Manager 选择 Game Core → Samples → Input Actions → Import，打开导入目录 `Content/InputActions.unity` 并进入 Play。示例不依赖 Dark Nights、不联网，也不读取游戏存档。

操作：A/D 移动，空格跳跃，左键使用演示工具。状态行显示按下／松开次数及当前按住状态。Switch mode 切换两个互斥动作组；Open modal 阻止游戏动作，关闭后恢复。UI 点击不应计入世界使用次数。

模态页的 Rebind Jump 等待一个新按键，Escape 或 Cancel rebind 取消。关页也取消未完成改键。回主界面 Save bindings 保存、Load bindings 重载；设置位于 `Application.persistentDataPath/yy_input_sample_settings.json`，与默认产品文件隔离。切换模式后可以分别改两个模式的 Jump。

## 开发者入口

- 输入与按钮状态：`Runtime/InputActionsSample.cs`。
- 通用 API 与兼容合同：包内 `Documentation~/INPUT_ACTIONS.md`。
- 改键／文件／输入路由回归：`Tests/InputRebindingTests.cs`、`InputSettingsTests.cs`、`InputRoutingTests.cs`。
- 真实场景回归：`Tests/InputSampleSceneTests.cs`；使用官方 InputTestFixture，位于 PlayMode 测试页，需要 Unity Test Framework。
- 空目录初建工具：`Editor/InputActionsSampleBuilder.cs`，只用于维护首版资产，不会在正常导入或构建中运行。已有 Content 目录会拒绝覆盖。

`PlayerInput` 使用 InvokeCSharpEvents；动作引用、UI 模块和场景对象已序列化。正式业务应通过 YYGC 命令与权威对象能力执行，本示例的矩形角色只展示本地输入。

## 验证状态

2026-09-16：实际动作资产、场景、UGUI 引用和材质已导入、保存并重开。Unity 6000.4.9f1／Input System 1.19.0 下，34 个不同 PlayMode 用例按影响合并通过：服务与文件 33 项，真实场景 1 项。原生 UGUI 点击不会触发世界使用；模态／失焦阻塞、改键完成／取消、真实保存重载和重复启停均有回归。

场景测试使用虚拟键鼠及焦点消息；末次场景复跑禁用音频，避免本机音频设备切换干扰。两张真实渲染截图已检查。本 Sample 未单独构建独立 Player，不把宿主游戏的 Mono 结果算作 Sample 的独立发布验收。

在 PlayMode 运行 `YYGC.InputActions.Tests`，或用 Editor CLI 的 `-runTests -testPlatform PlayMode -assemblyNames YYGC.InputActions.Tests`。可额外传入 `-yyInputSampleEvidence <绝对目录>` 保存场景截图。测试使用独立临时设置文件，不覆盖示例或游戏的用户设置。
