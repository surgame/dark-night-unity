# Unity 与 YYGC 依赖准备

2026-09-11 核对：当前 `Game/Packages` 通过 `tools/prepare-lan-sample.ps1` 使用 YYGC 提交 `10b8f0ef6a5ed965ebd473dbcbe4a0dd795379c4` 的 `.deps/YYGC`，原框架仓库只读。另含样板程序集访问补丁和框架要求的 VitalRouter wait-all 修正版。来源、恢复方法和 SHA-256 见 [LAN Sample](LAN_SAMPLE.md) 与[依赖证据](evidence/lan-sample-dependencies.json)。正式接入计划见[移植方案](MIGRATION_PLAN.md)，旧环境操作记录保存在[评估状态](ASSESSMENT_STATUS.md)。

本文件记录 Unity 宿主的实际依赖与剩余核验项；可运行的 manifest、lock、NuGet 配置和包缓存位于 `Game/`。

## Editor、C# 与运行库

| 项目 | 已查到的事实 | 接入要求 |
|---|---|---|
| YYGC UPM | `com.tsgame.gamecore`；当前 `0.3.0-preview.1`，提交 `10b8f0e`；unity=`6000.2`，unityRelease=`35f1` | 未发布预览；沿用锁定提交和补丁，不能只按版本号假定兼容 |
| 本机 Editor | `D:\Program Files\Unity 6000.4.9f1\Editor\Unity.exe`，ProductVersion=`6000.4.9f1 (f7258d6eebbe)` | 已用于导入、编译和 Windows Player 构建探针 |
| C# | Unity 6.2 官方文档为 Roslyn / C# 9.0 | 使用块级 namespace、普通构造、显式集合初始化 |
| API Compatibility | 官方支持 .NET Standard 2.1 或 .NET Framework 4.8；默认前者 | 新代码以 .NET Standard 2.1 为边界；不能加载 net8.0 游戏程序集代替迁移 |
| Godot 输入工程 | Godot.NET.Sdk/4.7.2，net8.0，LangVersion=12 | 主构造、集合表达式、required、部分 JSON API 需要替换 |
| 发布目标 | 首版 Windows x64；Sample 已有 Mono / IL2CPP Release＋High 裁剪证据 | 正式新 DTO、Behaviour、UGUI 与存档仍需对应 Player 验收；其他平台另估 |

官方依据于 2026-09-10读取：[C# 编译器与语言版本](https://docs.unity3d.com/6000.2/Documentation/Manual/csharp-compiler.html)、[API 兼容级别](https://docs.unity3d.com/6000.2/Documentation/Manual/dotnet-profile-support.html)。文档也指出 init/record 需要正确的 IsExternalInit 类型，Unity 自身序列化不支持把 record 当作序列化类型。网络 DTO 的 MemoryPack 支持与 Unity Inspector 序列化是不同机制。

早期包内遗留 `1.0.0`，后经 `0.2.3` 调整到当前预览版本；历史与发布约定见 [YYGC 版本管理](<D:/Developer/YYGC/Documentation~/VERSIONING.md>)。不能根据旧评估中的包版本推断当前接口状态。

当前 manifest 使用 `file:../../.deps/YYGC`，准备脚本校验上述提交。GUID / Key、旧 ID 兼容和显式迁移已在锁定框架中实现，接法见[定义身份指南](<D:/Developer/YYGC/Documentation~/DEFINITION_IDENTITY.md>)。正式新资源用 DefinitionReference 和正式 Key；首个网络切片保留 Sample 的 LegacyV1 wire，网络定义具备有效旧 ID。V2 为单独的协议切换，不在本轮设计中默认开启。

## 第三方依赖清单

YYGC 的 package.json 未声明 dependencies。以下依赖由 Unity 宿主显式提供；UPM / NuGet 清单和 Sample 的补丁、DLL 证据共同构成当前可重现输入。

| 依赖 | 证据／用途 | M0 接入结果 |
|---|---|---|
| FishNet | FishNet.Runtime、NetworkBehaviour、RPC、自定义 serializer | UPM Git `4.7.2`；Sample 原生网络 Prefab 与多进程通过，正式注册待 M2 |
| UniTask | UniTask、UniTask.Addressables、Editor 引用 | UPM Git `2.5.11`，编译通过 |
| Addressables / ResourceManager | PrefabRef、FastInstantiator、定义数据库 | Unity 包 `2.10.1`，编译和 Player 构建通过 |
| Input System | Runtime asmdef、重绑定与设置存储 | Unity 包 `1.19.0`，项目 activeInputHandler=1；YYGC UGUI 启动与 Editor 创建器已切换 `InputSystemUIInputModule` |
| URP / Core RP | Runtime asmdef、框架 renderer/shader | Unity 包 `17.4.0`，Windows Player 构建通过 |
| UGUI / TextMeshPro | Unity.ugui、Unity.TextMeshPro | Unity 包 `2.0.0` 及宿主内置 TMP，编译通过 |
| R3 | 状态流、UI；测试引用 R3.Unity/Editor | UPM Git `1.3.1` + NuGet `R3 1.3.1`，编译通过 |
| VitalRouter | 命令路由、过滤器、CommandPool | 基于 `2.7.1` 固定源码构建 wait-all 修正版；NuGet 恢复后须核对 DLL，不能悄悄换回原版 |
| MemoryPack | 网络与存档 serializer、Tests 的 MemoryPack.Core.dll | UPM Git `1.21.4` + NuGet `MemoryPack/Core/Generator 1.21.4`，编译通过 |
| ZLinq | 对象视图和状态集合 | UPM Git `1.5.6` + NuGet `1.5.6`，编译通过 |
| Odin Inspector | 多处属性，Editor asmdef 的 Addressables 模块 | 使用本机已有插件 payload，YYGC Editor/Runtime 编译通过；提交前需确认插件授权 |
| DOTween | LightBlockControl/LightBlockRenderer 的 `DG.Tweening` | 使用本机已有 `DOTween.dll`，编译通过 |

`YY.Pools.Collections`、YYSingleton、GenericTypePool 是框架内源码，不列作缺失的外部包。选择不使用某项功能也不会自动解除其在主程序集内的编译依赖；需要时只做有边界的程序集隔离。

环境基线已完成编译、Addressables 内容构建和 Player 启动。Sample 在当前依赖组合中另完成 Mono / IL2CPP 及多进程验证；正式 Bootstrap 的新组合仍需 M0 重验。另一台开发机通过准备脚本建立同一相对 `.deps/YYGC`，不修改 manifest 为个人绝对路径。Odin Inspector 与 DOTween 是已有插件，不属于 NuGet。

## 已随框架提供的生成器

| DLL | 同仓库源码工程 | 备注 |
|---|---|---|
| Editor/Plugins/YYGC.ViewBinding.Generator.dll | 有 | 视图与 UGUI 绑定；Roslyn 4.3.0 / netstandard2.0 / C# 9 |
| Editor/Plugins/YYGC.DependencyInjection.Generator.dll | 有 | DI 生成；同上，另有 CodeFix 依赖 |
| Editor/Plugins/YYGC.BehaviourRegistry.Generator.dll | 有 | Behaviour 注册；Roslyn 4.3.0 / netstandard2.0 / C# 9 |
| Runtime/NetworkCommands/SourceGenerators/YYGame.NetworkCommand.Generator.dll | 未找到 | 冻结 DLL 与 .meta；取得可重建来源后再升级 |
| Runtime/Objects/NetworkStates/SourceGenerators/YYGame.StateDataNoMemPack.Generator.dll | 未找到 | 不能从名字推断完整 MemoryPack/AOT 支持，需编译与往返探针 |
| Runtime/YYPlugins/YYSingleton/SourceGenerators/GenInstance/YYSingletonInstanceGenerator.dll | 未找到 | 同上 |

精确 SHA-256 位于[证据](evidence/assessment-2026-09-10.json)的 `bundled_dlls`。生成器 DLL 的 RoslynAnalyzer 标签、平台导入设置和程序集作用域也属于构建输入。

ViewBinding 与 DI 工程的 AfterBuild 会复制 DLL 回框架目录；本轮没有运行这些构建。后续仅在隔离 checkout 构建，避免覆盖用户工作区。纯 Core 不引用 GameCore，必须确认框架生成器不会向 Core 注入引擎代码。

## 包接入策略

1. 保持 YYGC `10b8f0e`、Editor `6000.4.9f1` 与现有包版本；新机器先运行准备脚本，不修改原框架工作区。
2. 新增正式 Runtime / Presentation Behaviour 后，验证生成器对内部成员的访问；当前 `SampleAssemblyAccess.cs` 只授权 Sample Runtime，不能直接当作正式程序集补丁。必要修正在隔离 checkout 中完成并记录输入。
3. 为正式命令、状态的具体类型保留可用于 IL2CPP 的注册入口，校验集合复制和归池。类型 Tag、Behaviour 顺序和定义目录进入握手摘要，正式表不引用 Sample 的测试 ID。
4. 保留 VitalRouter 修正的源码版本、补丁和 DLL 哈希；普通 NuGet 恢复可能换回原版，依赖预检应能识别。UPM lock 单独不代表完整输入。
5. 正式 AppStartup 使用现有资源；把 `DarkNightsEnvironmentSetup.BuildAddressablesContent()` 与会保存场景／Prefab 的 `Initialize()` 分离，再接入日常构建。
6. 提交 manifest / lock、ProjectVersion、`.meta` 及准备脚本。今后采用已核实的稳定包来源时整体更新并重验，不编造远端 URL，不把 `.deps` 缓存提交。

## JSON、存档与素材

Core 不持有 JSON 库或磁盘依赖。保留现有 JSON 的 snake_case、浮点与 64 位随机状态语义，解析与严格字段校验在 Runtime 的内容／存档入口完成。

现有 Content 使用 System.Collections.Immutable，不能默认它随 Unity 的 API profile 一并提供。迁移时可用只读接口和防御性复制保留定义不可变性；若保留该库，则明确锁定兼容版本，避免向外暴露可修改的共享集合。

M0 选择 Unity 可用且版本固定的 JSON 库；优先复用宿主已验证依赖，否则验证兼容的 System.Text.Json 版本及生成／裁剪路径。`JsonNamingPolicy.SnakeCaseLower` 来自现有 .NET 8 使用方式，不能默认可用；可采用显式字段映射。换库不能跳过缺字段、非法 enum、非有限值和双向关系校验。

551 项原素材共 2,010,712 字节（约 1.92 MiB），当前规模无需为它们额外引入 LFS。新大文件进入前按实际体积评估。原始素材、帧序、原点与来源清单保留，Unity 额外生成的 .meta 正常纳入 Git。

## 正式接入收口的退出条件

基础环境与 Sample 已完成；[复评 R01–R03](YYGC_REASSESSMENT.md)保留旧问题来源。正式接入需验证本次新增程序集、类型与宿主组合，不重复宣称旧问题仍未修复。

- Editor 补丁、API profile、依赖版本、框架 commit 和生成器都可重现。
- 全新目录导入成功，Runtime 没有 Editor 类型泄漏。
- 正式生成器样例、集合 DTO 往返与类型注册可用，Mono / IL2CPP Player 构建并能启动。
- Addressables 的本地基线 Prefab 和 AppStartup 根已可构建并由 Player 启动；具体游戏 Prefab、UGUI 内容和对象定义仍待补齐。
- 正式构建不会执行会保存资源的环境初始化；框架补丁来源完整，用户工作区差异不暗中混入依赖。

未满足这些条件时继续做独立的 Core/协议设计，但不宣称 Unity 集成或构建已经通过。
