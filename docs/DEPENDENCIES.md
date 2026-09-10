# Unity 与 YYGC 依赖准备

2026-09-11 增补：当前 `Game/Packages` 通过 `tools/prepare-lan-sample.ps1` 使用 YYGC 提交 `10b8f0ef6a5ed965ebd473dbcbe4a0dd795379c4` 的 `.deps/YYGC`，原框架仓库只读。另含样板程序集访问补丁和框架要求的 VitalRouter wait-all 修正版。来源、恢复方法和 SHA-256 见 [LAN Sample](LAN_SAMPLE.md) 与 [依赖证据](evidence/lan-sample-dependencies.json)。下文旧工作区与本地路径描述保留原评估／M0 时点，不代替当前 manifest。

本文件记录 Unity 宿主的实际依赖与剩余核验项；可运行的 manifest、lock、NuGet 配置和包缓存位于 `Game/`。

## Editor、C# 与运行库

| 项目 | 已查到的事实 | 接入要求 |
|---|---|---|
| YYGC UPM | `com.tsgame.gamecore`；原评估包内为 `1.0.0`，现已更正为开发版本 `0.2.3`（未发布）；unity=`6000.2`，unityRelease=`35f1` | 历史最新标签为 `v0.2.2`；正式锁定前需核对版本约束与宿主兼容；不能擅自改低最低 Unity 版本 |
| 本机 Editor | `D:\Program Files\Unity 6000.4.9f1\Editor\Unity.exe`，ProductVersion=`6000.4.9f1 (f7258d6eebbe)` | 已用于导入、编译和 Windows Player 构建探针 |
| C# | Unity 6.2 官方文档为 Roslyn / C# 9.0 | 使用块级 namespace、普通构造、显式集合初始化 |
| API Compatibility | 官方支持 .NET Standard 2.1 或 .NET Framework 4.8；默认前者 | 新代码以 .NET Standard 2.1 为边界；不能加载 net8.0 游戏程序集代替迁移 |
| Godot 输入工程 | Godot.NET.Sdk/4.7.2，net8.0，LangVersion=12 | 主构造、集合表达式、required、部分 JSON API 需要替换 |
| 发布目标 | 首版按 Windows x64 估算 | 至少完成 Mono Player；尽早补 IL2CPP/MemoryPack 生成注册探针；其他平台另估 |

官方依据于 2026-09-10读取：[C# 编译器与语言版本](https://docs.unity3d.com/6000.2/Documentation/Manual/csharp-compiler.html)、[API 兼容级别](https://docs.unity3d.com/6000.2/Documentation/Manual/dotnet-profile-support.html)。文档也指出 init/record 需要正确的 IsExternalInit 类型，Unity 自身序列化不支持把 record 当作序列化类型。网络 DTO 的 MemoryPack 支持与 Unity Inspector 序列化是不同机制。

2026-09-10 的 [YYGC 版本补齐](<D:/Developer/YYGC/Documentation~/VERSIONING.md>)核对了 17 个本地历史标签：包内版本全部遗留为 `1.0.0`，`v0.2.2` 的提交说明还写着 `v0.2.1`。本次仅更正后续开发包元数据并补文档，历史标签不移动；`0.2.3` 尚未打标签或完成发布验收。数值下调可能影响其他旧宿主的版本约束 / asmdef Version Defines，不能按补丁号推定完全兼容。

当前 manifest / lock 中 YYGC 都是 `file:D:/Developer/YYGC`，因此本轮没有把它改成 registry 版本。GUID / Key、旧 ID deprecated、在线开关及迁移仍按[框架执行方案](<D:/Developer/YYGC/Documentation~/ID_REGISTRY_REFACTOR_PLAN.md>)待实施，不改变本仓库已有 DefinitionId 接入约定，也不代表旧项目回归或网络验收已完成。

## 第三方依赖清单

YYGC 的 package.json 未声明 dependencies。以下是从 asmdef 与源码提取的实际依赖，版本和宿主安装来源目前均未得到完整锁定。

| 依赖 | 证据／用途 | M0 接入结果 |
|---|---|---|
| FishNet | FishNet.Runtime、NetworkBehaviour、RPC、自定义 serializer | UPM Git `4.7.2`，编译通过；已建立空的 `NetworkManager`/`DefaultPrefabObjects` 基线，实际网络 Prefab 注册待 M2 |
| UniTask | UniTask、UniTask.Addressables、Editor 引用 | UPM Git `2.5.11`，编译通过 |
| Addressables / ResourceManager | PrefabRef、FastInstantiator、定义数据库 | Unity 包 `2.10.1`，编译和 Player 构建通过 |
| Input System | Runtime asmdef、重绑定与设置存储 | Unity 包 `1.19.0`，项目 activeInputHandler=1；YYGC UGUI 启动与 Editor 创建器已切换 `InputSystemUIInputModule` |
| URP / Core RP | Runtime asmdef、框架 renderer/shader | Unity 包 `17.4.0`，Windows Player 构建通过 |
| UGUI / TextMeshPro | Unity.ugui、Unity.TextMeshPro | Unity 包 `2.0.0` 及宿主内置 TMP，编译通过 |
| R3 | 状态流、UI；测试引用 R3.Unity/Editor | UPM Git `1.3.1` + NuGet `R3 1.3.1`，编译通过 |
| VitalRouter | 命令路由、过滤器、CommandPool | NuGet `2.7.1`，已还原到 `Assets/Packages` |
| MemoryPack | 网络与存档 serializer、Tests 的 MemoryPack.Core.dll | UPM Git `1.21.4` + NuGet `MemoryPack/Core/Generator 1.21.4`，编译通过 |
| ZLinq | 对象视图和状态集合 | UPM Git `1.5.6` + NuGet `1.5.6`，编译通过 |
| Odin Inspector | 多处属性，Editor asmdef 的 Addressables 模块 | 使用本机已有插件 payload，YYGC Editor/Runtime 编译通过；提交前需确认插件授权 |
| DOTween | LightBlockControl/LightBlockRenderer 的 `DG.Tweening` | 使用本机已有 `DOTween.dll`，编译通过 |

`YY.Pools.Collections`、YYSingleton、GenericTypePool 是框架内源码，不列作缺失的外部包。选择不使用某项功能也不会自动解除其在主程序集内的编译依赖；需要时只做有边界的程序集隔离。

M0/M1 已按上述来源安装依赖并完成编译、Addressables 内容构建和 Player 探针。`com.tsgame.gamecore` 保留为 `file:D:/Developer/YYGC`，这是当前机器上的开发引用；换机器时需改为对应本地路径。Odin Inspector 与 DOTween 是本机插件，不属于 NuGet，不应从 NuGet 版本替代。

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

1. 记录框架评估 HEAD 和工作区差异。当前未提交 UGUI API 不应被误写成 HEAD 已提供的接口。
2. 当前按用户要求直接使用 `file:D:/Developer/YYGC`，因此 YYGC 工作区修改会即时反映到 DNights；提交前仍需明确框架 commit 与工作区差异。
3. 修正构建必需的 Editor 隔离、兼容类型和依赖声明；保留修正清单和独立提交，避免混进游戏规则变更。
4. 取得可重现的框架包版本后，锁定包来源／commit；提交 `Packages/manifest.json`、`Packages/packages-lock.json` 和 `ProjectSettings/ProjectVersion.txt`。没有已核实远端地址时不编造 Git URL。
5. 当前已用 Bootstrap 基线验证 Editor 编译、Addressables 内容和 Windows Player 构建；StateData/NetworkCommand 生成器已生成空注册入口，本地 ObjectView、网络 DTO、AOT 与 Domain Reload 重复启动仍属于下一阶段。

## JSON、存档与素材

Core 不持有 JSON 库或磁盘依赖。保留现有 JSON 的 snake_case、浮点与 64 位随机状态语义，解析与严格字段校验在 Runtime 的内容／存档入口完成。

现有 Content 使用 System.Collections.Immutable，不能默认它随 Unity 的 API profile 一并提供。迁移时可用只读接口和防御性复制保留定义不可变性；若保留该库，则明确锁定兼容版本，避免向外暴露可修改的共享集合。

M0 选择 Unity 可用且版本固定的 JSON 库；优先复用宿主已验证依赖，否则验证兼容的 System.Text.Json 版本及生成／裁剪路径。`JsonNamingPolicy.SnakeCaseLower` 来自现有 .NET 8 使用方式，不能默认可用；可采用显式字段映射。换库不能跳过缺字段、非法 enum、非有限值和双向关系校验。

551 项原素材共 2,010,712 字节（约 1.92 MiB），当前规模无需为它们额外引入 LFS。新大文件进入前按实际体积评估。原始素材、帧序、原点与来源清单保留，Unity 额外生成的 .meta 正常纳入 Git。

## M0/M1 退出条件

M0/M1 基础导入已完成；命令生成器旧命名空间、接口 MemoryPack formatter 闭环和状态初始 null 的首次发布仍必须逐项核实/修正，见[复评 R01–R03](YYGC_REASSESSMENT.md)。

- Editor 补丁、API profile、依赖版本、框架 commit 和生成器都可重现。
- 全新目录导入成功，Runtime 没有 Editor 类型泄漏。
- 生成器样例、网络 DTO 往返与类型注册可用，正式 Player 构建并能启动。
- Addressables 的本地基线 Prefab 和 AppStartup 根已可构建并由 Player 启动；具体游戏 Prefab、UGUI 内容和对象定义仍待补齐。
- 未提交用户代码的差异已明确处理为“仍留在原仓库”或后续已提交的新基线，不暗中混入依赖。

未满足这些条件时继续做独立的 Core/协议设计，但不宣称 Unity 集成或构建已经通过。
