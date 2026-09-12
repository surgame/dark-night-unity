# Dark Nights Unity 开发执行计划

2026-09-12 C 重构已实施，按用户要求收尾并先交付架构验收。Core 1361、最终 Editor 95、Play 生命周期 20、Mono 启动 6 和双进程 13 项通过；容量检查未签署通过，Ready 修复后的完整弱网矩阵及性能对比留待下次。实际合同、此前通过记录和待办见[C 实施记录](C_REFACTOR_IMPLEMENTATION.md)。M5 既有待验收项保持，下文历史批次的当时边界保留。

2026-09-12 后续重构规划：[C 方案执行计划](C_REFACTOR_PLAN.md)已完成源码核对、分批安排、具体文件清单与影响评估，**尚未实施**。目标为 ObjectsV2 会话对象拥有权威生命周期、Core 保留个体状态和规则、Local 个体 Behaviour 接管表现职责；本批不包含装备/Buff 新玩法。没有新增 Unity/Core/Player 验证结果，不改变下述已实现功能与 M5 未完成状态。

2026-09-12 编辑器维护：YYGC Workshop 已改为名称／Key／原资产文件名三行，平铺与树形统一 60 像素行高；有效旧 ID 仅作辅助标记，搜索和排序适配 GUID／Key。Unity `6000.4.9f1` 编译与 47 项 UI 检查通过，实际窗口关闭重开后 29 个定义行均保留文件名。框架锁定更新至 `516f76c`，逐文件修改及边界见[YYGC 账本](YYGC_CHANGES.md#workshop-display)和[本批证据](evidence/workshop-display-2026-09-12.json)。本批不改变下述 M5 游戏验收状态。

2026-09-12 独立美术流程试验：[Blender 像素角色脚手架](../experiments/blender-pixel-crew/README.md)完成共用骨架、模块切换、序列帧及 MCP 接入的技术验证；用户认为外观与参考图仍有明显差距，**美术风格验证未通过**。该试验独立于 Game，不计入 M3/M5 完成状态。

2026-09-12 2D 对照试验：[Aseprite 像素角色与换装](../experiments/aseprite-pixel-crew/README.md)已生成可编辑的 64×64／13 图层源文件、空闲／行走／跳跃共 24 帧及 H5 预览。头部／上衣／背包共 18 种组合，432 帧原生合成比对、原生编辑保存重开及浏览器交互检查通过。**美术风格待用户评审**；Cel 为独立副本，不宣称任意服饰自动适配全部动作。试验未接入 Game，不改变 M3/M5 状态。

当前状态（2026-09-12 验收续跑）：环境恢复后，源码 `e567f8c` 的干净 Mono 构建成功；活跃施工／训练／在飞箭矢恢复 13/13、并发 13/13、九组四进程弱网各 22/22、三夜 17/17、容量上限 13/13 均通过。Core 1355、Editor 55 与架构守卫因游戏输入未变而复用，2,457 个构建输入前后哈希一致。

M5 尚未完成：画面复核发现字体、禁用文字状态和新增菜单控件布局需校准；工作集全零、分位数样本窗口与隐藏窗口使性能证据仍不足。已归档新 Mono 的 408 个文件及完整哈希，IL2CPP 与第二台 Windows 主机仍待外部条件。详见 [Mono 验收记录](MONO_ACCEPTANCE.md)与[M5 后续清单](M5_EXECUTION.md#mono-acceptance)。下方保留历史实施批次及当时边界。

2026-09-12 集成进展：协议 5 的十槽位存档、同房间重开、120 秒恢复凭据已接通；九组四进程弱网各 22/22、并发 13/13、三夜 17/17、容量上限四端 13/13 通过。Core 1355、Editor 55、鼠标 13 项通过；五页 UI 圆角与状态边框已校准。正在执行干净目录 Mono、普通 Player 性能与完整画面对照；IL2CPP 和双机器仍单独待验收。

2026-09-12 增补：M3 正式 Mono 四进程三夜通关 13/13，34 个击杀与四端胜利一致；图形战斗晚加入 9/9。效果、环境、音频和插值主体已接通；完整画面对照、M4 存档恢复与 M5 矩阵仍待完成，详见 [原生效果](NATIVE_EFFECTS.md)。

后续执行主入口：[从当前实现连续推进到 M5](M5_EXECUTION.md)。现有阶段和历史证据保留；执行收敛为 M2 可玩闭环 → M3 完整关卡 → M4 会话恢复 → M5 交付四批，集中准备、导入和验证，复用已通过结果。

计划更新：2026-09-12，配合[移植方案](MIGRATION_PLAN.md)。**M0–M4 的规则、原生表现、正式四人和恢复主体已实现并完成上述分批验证；M3 画面对照与 M5 干净交付／性能／外部条件继续收口。** 下文统一使用 M0 表示环境与正式接入收口、M1 表示规则核心；历史记录中的“M0/M1 环境完成”不代表本表 M1 完成。

Sample 使用独立四个运行程序集、Editor、原生资源和复跑脚本；YYGC 锁定 `10b8f0e` 加窄范围友元程序集补丁，VitalRouter 修正从固定源构建。Mono 和 Windows x64 IL2CPP Release＋High 裁剪均已实际构建，每种后端的基础与弱网各 30 项多进程断言见 Sample 文档及 `docs/evidence/lan-sample-*.json`。下文正式玩法预算和退出条件保持有效，不能以测试营地代替完整游戏验收。

原方案更新时只修改设计，没有运行新的 Unity / Godot 测试；本次实际实施与检查见下方进展。Sample 已覆盖的旧生成器、首状态和命令路由问题不再作为从零研究任务；正式程序集与 AppStartup / Addressables 的小探针现已独立验收，实体集合投影、命令处理与游戏权限仍需正式联机验收。先可靠完整投影，测量后再做分块或拆流。

2026-09-11 目录复审要求已写入[移植方案](MIGRATION_PLAN.md)与[技术架构](ARCHITECTURE.md)：正式代码位于 Scripts（Core、Runtime、View、Entry），资源位于 Res，按对象／面板归组；Addressables 无游戏素材目录命名要求，保留现有配置目录。目录已按实际功能落地，Worker 与 WorldSession 首批定义／Prefab／绑定已建立，其余对象、UI 与美术目录仍随功能实施。

<a id="implementation-progress"></a>

## 当前实施进展

第十二批最新状态（2026-09-12）：原生箭矢／浮字／残骸／声音与消息、环境火把／七灯位／月亮／萤火／地表／阴影、角色插值和放置范围线已接入。规则 1338/1338、Editor 53/53、鼠标 13/13、战斗 Play 7/7 通过；新协议 4 Mono 启动 6/6、双进程操作 13/13 通过，详见[原生效果与环境](NATIVE_EFFECTS.md)。画面对照、三夜流程、M4 恢复及完整网络矩阵继续执行，M3–M5 尚未整体完成。

第十一批最新状态（2026-09-12）：五个原生 UGUI 面板、15 类头像、选择／镜头／小地图、状态叠层及工厂建造预览已接入；真实鼠标 13/13、两分辨率 162 控件及原生资源 5/5 检查通过，规则 1316 项、守卫 181 文件通过。Mono Player 实际构建一次，启动 6/6、独立双进程 13/13 通过，详见[原生 UI 与操作](NATIVE_UI.md)。后续继续完整表现和恢复，不将本批作为 M2–M5 整体完成。

第十批最新状态（2026-09-12）：551项冻结素材、15类原生外观、32段动画、场景布局预览和正式副本到对象工厂的接线已实施；583个关键帧采样、冻结布局与Editor Host启停重连通过。UI操作、完整环境／效果／音频及本批Player验证仍待完成，见[原生外观实施](NATIVE_ART.md)。以下第九批及更早段落保留历史边界，后续继续A3/B。

第九批最新状态：M2 A2 已接通正式网络与 Entry，Mono Host＋独立客户端12项检查通过；详见[正式网络接线](FORMAL_NETWORK.md)。下述第八批状态为先前实现边界；当前继续A3可操作表现与M3完整资源，M2整体尚未退出。

2026-09-11 截至第八批：**M0 正式接入退出条件已完成；M1 规则与存储基础已实现；M2 权威业务层、冻结展示副本和真实时间累积已通过独立及 Editor 回归。** 新增 SessionProjector／WorldReplica 覆盖全部实体及 HUD、发布次序和连接／epoch 隔离，SessionClock 以未缩放时间驱动 60 Hz。尚未接入 Bootstrap、Unity Update 或 YYGC 网络回调；wire／内容握手／真实 Ready、存档文件编排及 UI 待完成。M0 双后端探针与本批内存投影回归不代表可玩会话或正式联机。

| 内容 | 第八批时的历史实现（最新状态见上方第十一批） |
|---|---|
| 代码与资源分离 | Scripts/Core、Runtime、View、Entry、Editor/Tests；Res/Config 保存原始 balance.json、pinewatch.json，Res/Scenes/Pinewatch 保存可编辑布局场景；Res/Objects 已按对象建立 Worker 与 WorldSession，不预建其余实体表现或 UI 空类型 |
| 只读配置 | Core/Config 为普通 C#9 不可变类，构造时复制集合；Runtime 显式映射 JSON，保留 196 项原始规则值和 64 位 seed |
| 依赖 | 显式锁定已有 Newtonsoft UPM 3.2.2（DLL 13.0.2），不增加第二份 JSON DLL；Core 不引用解析库 |
| 启动 | GameContentStartupModule 接入现有 AppStartup，Addressables 并行加载两份文本并释放句柄；同时验证正式定义、内容映射、Behaviour 工厂与命令／状态注册，全部成功才注册 GameCatalog |
| 正式对象 | Worker 与 WorldSession 均使用 GuidFirst 的 GUID／Key 定义、旧 ID 为 `0`、无旧 ID 别名；分别接入 Addressable Prefab、ObjectInstance／ObjectView／initializer、StateSynchronizer 与生成的 WorldSessionBehaviour |
| wire 合同 | SetReadyCommand 与 SessionStatusState 已由 MemoryPack／YYGC 生成注册，两个首批 Tag 均冻结为 `0`；尚未把会话业务层接到网络处理器和状态发布器；内存实体集合投影已实现，未生成 wire |
| 权威会话基础 | SessionAuthority 独占 GameSession，可信适配签发连接能力；统一队列执行 SharedCamp／HostOnly、时间与加载权限，保序去重与有界结果窗口；已通过真实规则回归，网络身份认证／握手、恢复凭据及实际投影 Ready 尚未接入 |
| 资源保护 | 环境工具保留 GUID 移入正式 Editor 目录；Initialize 遇已有目标立即拒绝；BuildAddressablesContent 仅验证并构建，不调用初始化。配置注册只保存本批配置与分组 |
| 守卫 | tools/ArchitectureGuard 检查源码结构、项目依赖、Core 禁用 API 与 Editor/Tests 隔离；tools/CoreBuild 以 C#9 / netstandard2.1 独立编译实际 Core 源码 |
| 新存档与文件 | GameSaveJson 独立格式 v1；GameSaveStore 固定槽位与原子替换。SessionAuthority 已验证房主 BeginLoad 票据、失败保留世界、成功切 epoch／Ready 并保持策略；文件任务编排、保存命令与 UI 尚未接入 |

构建会临时开启 Addressables Build Layout 诊断并恢复原设置，避免首次构建的可选报告弹窗阻塞自动任务。构建串行执行，结束时恢复后端、裁剪及预加载选项，并原样还原调用前的 ProjectSettings 文件，避免 Unity 把临时构建配置留在磁盘中。正式 Mono 首次运行暴露 Sample 类型被 AppStartup 完整性检查误认为漏注册，已在隔离依赖补齐与生成器一致的排除规则；失败日志保留，详见[依赖修正](DEPENDENCIES.md#正式配置解析依赖)。

本批已通过 22 项 Editor 检查、Core 独立编译、10 项守卫自测，以及 Mono／IL2CPP 各 5 项独立启动检查。397 个构建前已有资源与 `.meta` 无非预期改写；Unity 构建期间可能生成 Addressables `link.xml`，验证后按既有工作区约定清理，不将该生成物作为正式内容源。

本批验证记录在 [首批移植证据](evidence/migration-start-2026-09-11.json)。配置宿主可用于验证依赖与启动，**尚不能游玩灰松谷**。LevelDefinition 仍只含 JSON 中的身份、seed 和波次；后续第三批已将布局保存在独立的唯一可编辑场景来源，启动仍不能从 JSON 缺省出零坐标世界。

复跑入口（仓库根目录，已打开正确的 Game Editor）：

```powershell
dotnet build tools/CoreBuild/CoreBuild.csproj
dotnet run --project tools/ArchitectureGuard -- .
unity command run_tests --mode editor --filter DarkNights.Tests --filter_type assembly --project-path Game --detach
unity command menu --path 'Dark Nights/Build/Windows Mono' --project-path Game --detach
pwsh -NoProfile -File tools/test-game-startup.ps1 -Backend mono
unity command menu --path 'Dark Nights/Build/Windows IL2CPP' --project-path Game --detach
pwsh -NoProfile -File tools/test-game-startup.ps1 -Backend il2cpp
```

先完成导入／编译，再注册配置和运行测试；修改源文件后需确认编译产物已更新，不能仅以 `isCompiling=false` 判断最新代码已加载。每个 detached 请求等待其任务 ID 完成后再执行下一步。首次新建配置条目使用 `Dark Nights/Content/Register Initial Configuration`；首次新建布局场景使用 `Dark Nights/Content/Create Initial Pinewatch Layout`；首批正式对象先执行 YYGC 的 NetworkCommand 与 StateData 注册生成菜单，再执行 `Dark Nights/Content/Create Initial Formal Objects`。三个初始化入口都只允许指定输出为空或不含受保护资产，不属于日常构建步骤，也不从 Godot 目录导入或覆盖现有内容。

第二批已落实 `Core/Logic` 的经济、生产、施工、训练、AI、伤害、箭矢、三夜夜袭和胜负；`GameSession` 不持有选择或镜头，业务操作使用显式实体 ID。`Core/Save` 为深度冻结记录，`Runtime/Save` 显式解析旧 v1 JSON，校验成功后只返回新世界，不触碰调用方当前营地。布局通过独立 `LevelLayout` 必需参数注入，测试夹具不进入正式内容加载。

验证：C#9／netstandard2.1 实际编译零错误／警告；架构守卫 78 个手写文件、10 项自测通过；独立回归 1102 项通过，其中 70 项规则／旧档／输入检查及 1032 项随机检查。正常策略、无人照料、旧档恢复及继续 20 秒均与冻结结果一致。Unity Editor 首次 28 项中 27 项通过，发现 Mono 浮点中间值精度差异；固定 binary32 舍入后，重跑受影响的 6 组核心检查全部通过，原 22 项环境／配置结果复用。未重跑 Player 构建或联机；Sample 历史结果不作为本批核心的 Player 证据。

详见[核心迁移记录](CORE_MIGRATION.md)与[冻结证据](evidence/core-migration-2026-09-11.json)。独立复跑新增 `dotnet run --project tools/CoreRegression -- .`；Editor 仍使用上方正式测试程序集入口。Godot 原目录、用户 YYGC 仓库、规则 JSON 和美术未修改。

第三批建立实际 `DarkNights.View`，以 `LevelLayoutAuthoring` 和 `LevelPlacementMarker` 保存灰松谷边界及 4 个建筑、5 个资源点、7 个友方单位。一次性 Editor 入口只向指定空目录生成首版 `Pinewatch.unity`；日常验证只读场景，保存重开后按显式 SpawnOrder 导出冻结 `LevelLayout`。Core 布局校验同时补齐建筑边界、建筑重叠、资源点遮盖及类别变体约束，错误不会创建部分世界。

验证：结构守卫 83 个手写文件、10 项自测通过；C#9／netstandard2.1 编译零错误／警告；独立回归 1104 项通过；Unity 重编译无错误，完整 Editor 程序集 29/29 通过，其中布局场景逐项对照冻结夹具并创建出相同初始实体顺序。未运行 Player、PlayMode、对象表现或联机检查；本批场景只有可编辑玩法标记和 Gizmo，不把它写成正式可玩或美术完成。证据见[布局迁移记录](evidence/pinewatch-layout-2026-09-11.json)。

第四批（历史基线，已被后续身份切换取代）完成 M0 正式接入探针：离线定义目录冻结为 `DNights`／LegacyCompatible，Worker ContentId 经 DefinitionReference 指向本地定义，WorldSession 网络定义固定 LegacyV1 ID `930001` 并进入 FishNet spawn 列表；两个 Prefab 均注册 Addressables。正式 Runtime 获得窄范围生成器友元访问，SetReadyCommand、SessionStatusState 和 WorldSessionBehaviour 的具体注册已生成并由 AppStartup 强校验。一次性工具只创建空目录首版，日常构建只验证，不重写正式对象。

上述第四批验证：隔离 YYGC `10b8f0e` 准备可重现；结构守卫 92 个手写文件、10 项自测通过；C#9／netstandard2.1 编译零错误／警告；独立核心回归 1104 项通过；Unity 重编译无错误，完整 Editor 程序集 32/32 通过。Windows Mono 与 IL2CPP Player 各构建一次并在独立进程完成 6/6 启动检查，正式对象／命令／状态／Behaviour 注册只出现一次且 Editor／Tests 程序集未进入 Player。Worker、WorldSession 四个资产及 Pinewatch 场景的构建前后 SHA-256 一致。该记录中的身份设置是历史 LegacyV1 基线，见[历史正式对象接入记录](evidence/formal-object-contracts-2026-09-11.json)。

第五批切断正式 ObjectDefinition 的旧整数兼容：数据库固定为 `GuidFirst`、在线 ID 服务关闭且无 `LegacyIdMap`；Worker／WorldSession 的 `Id` 均为 `0`、旧 ID 别名为空；正式 Windows 构建开启 `YYGC_GUID_DEFINITION_WIRE_V2`。正式 Editor／Runtime 校验会拒绝 `LegacyCompatible`、旧 ID、旧 ID 别名、旧映射或 LegacyV1；独立 LAN Sample 与 YYGC 用户仓库保持不变，旧 v1 存档导入仍作为独立的玩法迁移边界保留。

验证：结构守卫 92 个手写文件、Core 编译零错误／警告、独立核心回归 1104 项通过；Unity Editor 测试 32/32 通过；Windows Mono 与 IL2CPP Player 均在独立进程完成 6/6 启动检查，正式日志均包含 `identity=GuidFirst wire=GuidV2`，Editor／Tests 程序集未进入 Player。切换后的数据库与两个 Definition 资产哈希分别为 `0ec20eded0912c30852db60a99b03c3ab77a27a7057da30e08a085cd1282d10b`、`83f71cb728b42d10de75295369ebc889e91e4659ed978cd4ff7e7e77b36b8714`、`c24b46a54049efd47a6b7f161fc84b05d1238f56ba8fdc7b5db392dee71278e5`；证据见[正式 GuidV2 身份切换记录](evidence/formal-object-contracts-guid-v2-2026-09-11.json)。

第六批补齐 M1 独立存储切片：新档固定 `dark-nights.world` v1，引用 Core 的随机算法标识，以实际不可变目录和布局计算独立二进制规范化 SHA-256；世界字段不含相机、选择或房间控制策略。旧 v1 保持独立显式导入，恢复先经过现有完整字段／关系校验，只返回新 GameSession。文件适配提供 0–9 槽位、严格 UTF-8 与 4 MB 上限、取消检查、同目录临时文件 `Flush(true)` 后 Move／Replace；失败保留原档，同实例操作串行。遵循方案的 JSON／文件路线，未接 YYArchive 模块或 UI。

验证：独立回归 **1164/1164**（新增存档 60 项），架构守卫 **97 个手写文件、10 项自测**通过；Unity 本批导入及修正算法常量后的编译均无错误，完整 Editor 程序集 **34/34**通过。覆盖真实文件锁导致提交失败、损坏／超大输入、取消、并发保存、新旧格式隔离及新格式恢复后继续 20 秒的冻结结果。实际 .NET 存档在 Unity 加载后 JSON 字段值精确一致，两个摘要一致；浮点文本位数不同不作为字节一致保证。本批未构建或运行 Mono／IL2CPP Player、联机或美术验收。正式资产、配置和旧夹具未改写；详见[存档合同](SAVE_FORMAT.md)及[第六批证据](evidence/world-save-2026-09-11.json)。

第七批实现 `Runtime/Session`：权威实例自行创建并独占 GameSession；Host 和来宾均提交冻结的显式参数，在创建线程按接受顺序处理。每连接最多 16 条待处理、全局最多 64 条，保留最近 64 条完成结果；重复请求返回原回执，改参重发／窗口外旧序号不能再次支付。连接替换增加代次并清除 Ready，执行点复查当前连接、epoch、策略与权限。SharedCamp／HostOnly 限制所有营地修改及自动派工；房主控制时间与 BeginLoad。加载票据由服务端持有，取消／失败保留旧世界，成功恢复完整世界后增加 epoch、清空 Ready／去重并保持房间策略。

验证：独立回归 **1260/1260**（新增会话 96 项），架构守卫 **109 个手写文件、10 项自测**零错误；Unity 两次批量编译无错误，最终完整 Editor 程序集 **37/37**通过。采集 12 秒、住宅施工和训练与直接 Core 的完整快照一致；队列争抢不足资源、工位独占、部分训练支付、策略切换、非法输入、重连旧请求及加载失败／取消均有检查；加载冻结旧档后继续 20 秒对照原结果。没有运行 Player、PlayMode、多进程或弱网检查，没有修改正式资源或冻结夹具。第二次编译的新增输入为显式保序去重及补充队列／部分成功场景；详见[会话业务合同](SESSION_AUTHORITY.md)和[第七批证据](evidence/session-authority-2026-09-11.json)。

第八批执行 M5 路线的 A1：实现全部实体／HUD 冻结展示、箭矢稳定展示身份、WorldReplica 完整帧替换与连接／发布版本过滤，以及 SessionClock 的有界追帧和余量保留。独立回归 **1316/1316**（新增 56 项），架构守卫 **123 文件／10 自测／0 错误**；一次 Unity 批量编译无错误，完整 Editor **40/40**。没有修改规则、夹具或正式资源，没有构建 Player 或运行联机。实现合同及边界见[展示副本与时钟](SESSION_PROJECTION.md)，实测摘要见[第八批证据](evidence/session-projection-2026-09-11.json)。

后续批次已完成 M3 表现与 M4 存档恢复主体及正式四进程／弱网分批检查；当前余项以 [M5 收尾清单](M5_EXECUTION.md#closeout) 为准。M1 存档产品已并入 M4；IL2CPP 和双机器分别取得对应前置条件后实测。

## 工作量与难度

2026-09-11 实施准备补充：官方 Unity CLI／Pipeline MCP 已接入，完成 Editor 编译、协议调用和域重载复查；隔离 YYGC 增加 Sample 全局注册排除补丁。新增依赖后重建 Mono／IL2CPP，两个后端的四进程基础与弱网检查共 120 项通过。此项在当时不代表 M0 正式 AppStartup／Addressables 接入完成；当前 M0 已由上方第四批另行验收。版本、警告与历史证据见[依赖说明](DEPENDENCIES.md#官方-unity-mcp-开发工具)。

以下为基于现有环境与 Sample 的**剩余工作暂估**，以一名熟悉 C#/Unity、能够调试 FishNet 的开发者为基准；现有规则、素材和测试可使用，无新增美术、地图或经济设计。一个人日包含实现、调试与相应验收；尚未通过实际移植速度验证。

| 阶段 | 工作内容 | 退出条件 | 人日 |
|---|---|---|---:|
| M0 正式接入收口 | 已完成：锁定环境、正式四程序集与守卫、生成访问／具体注册、初始化与构建分离、JSON 依赖、双后端小探针 | 已通过；后续新增正式类型仍需维持相同生成与 Player 检查 | 0 |
| M1 可移植核心收尾 | 已完成 C#9 规则、显式操作、RNG、冻结旧档、新格式与原子文件适配；剩余存档 UI 与会话接入，和 M4 协调验收 | 存储层已验证失败保留文件／当前世界；产品入口接入后验证授权及世界切换 | 1–2 |
| M2 双进程联机切片 | 工人／建筑等少量正式 Prefab；Host＋独立客户端；身份、去重、SharedCamp／HostOnly；可靠完整实体投影 | 双方独立选择，移动／采集／建造一致；单人共用入口；关闭共享控制后无越权、无双扣 | 4–6 |
| M3 完整关卡与美术流程 | 15类外观、环境、HUD、菜单、小地图、音效、动画、布局／预览工具 | Unity可完整玩三夜；Prefab可编辑、保存重开；固定画面对照 | 5–8 |
| M4 会话完整性 | 2–4人、晚加入、重连、暂停／倍速权限、存档与加载 epoch、策略切换、Host 退出与清理 | 网络与恢复矩阵通过；没有幽灵实体、重复交易、旧策略越权或旧消息污染 | 3–5 |
| M5 集成与交付验证 | 四进程／双机器、网络扰动、性能测量、干净构建、Player及文档 | 一套可运行构建和可复现报告；人工／自动边界明确 | 3–5 |
| **剩余基础合计** |  |  | **16–26** |

增加约 25% 的实体集合投影、恢复与跨引擎表现余量后，暂按 **20–33 人日，约 4–7 工作周**安排。早期 30–50 人日是环境和样板尚未建立时的全量估算；本次重估扣除已经实际验收的 M0 和大部分 M1，不表示正式游戏已经通过联机或可玩验收。M2 的投影测量完成后再校正。

Unity 单机适配为中等难度，主要在54个表现文件对应的场景／HUD／动画；联机为中高难度，主要在身份、共享事务、初始快照和重连。代码体量较小减少玩法分析成本，但不会消除这些生命周期工作。

## 每批实施的执行方式

各阶段遵循 [AGENTS 的执行效率约束](../AGENTS.md#execution-efficiency)：**集中准备 → 一次提交批次 → 等待依赖就绪 → 汇总验证**。优先使用已有 CLI／MCP、Editor 入口及复跑脚本，减少模型与工具之间逐项往返。本节为执行规范，实际已实施范围以下方状态和证据为准。

例如新增一批脚本和素材，应先集中写入文件与程序集声明，再统一触发导入，让 Unity 为新增文件／目录补齐 `.meta` 并完成编译；不能每个文件分别刷新。需要引用新组件的 Prefab／ObjectDefinition 在编译就绪后作为下一批集中创建、绑定、注册及保存，最后统一检查 GUID、缺失引用和生成结果。已有工具能够处理整条依赖链时一次提交即可；遇到必须等待的编译或导入结果，按屏障分批，不强行并行。

提交长任务后保留同一个任务 ID，按完成事件或有界等待取得结果。构建前列出本次受影响的后端、配置和检查；同一产物复用到对应基础、弱网及恢复检查，不为每项断言重新构建。失败保留日志及已完成结果，只重跑受影响部分；最终集中报告通过项、未完成项与证据路径。美术保存重开、实际 Player 和独立进程等既有验收条件不变。

## 阶段顺序与交付物

已有环境包含 `ProjectSettings/ProjectVersion.txt`、包 manifest/lock、NuGet 依赖、全局空配置、AppStartup/Addressables、GameCore/NetworkManager Prefab、Bootstrap 和空正式注册源。当前 YYGC commit 已锁定为 `10b8f0e`，由准备脚本建立隔离 `.deps/YYGC`，不直接依赖用户工作区的未提交状态。

M0 不重建环境：新增正式程序集后验证 Behaviour 生成器的内部访问（现有友元补丁仅覆盖 Sample）；保留 VitalRouter wait-all 修正和完整恢复输入；验证 GUID / Key 内容映射与 GuidV2 网络定义；将 `BuildAddressablesContent → Initialize` 的调用拆开，构建只读取已维护资源。实现全局注册与会话启停时确保只有一个活动网络管理器，Sample 不叠加加载。

目录调整按需实施，保留现有资产 GUID，检查定义数据库、Addressable 条目与硬编码路径；不为套目录改名 Bootstrap 场景或 Sample。首批正式对象将 Definition 与 Prefab 同目录维护，验证绑定键／类型／引用、装配顺序及池化释放；同时检查正式生成注册、资源依赖和构建前后资产哈希。M3 继续执行修改 Prefab、保存、重开及运行检查，不能以绑定表存在代替验收。

M1先引入只读内容与Core程序集，建立禁止Unity/Godot/YYGC等跨层引用的守卫；为所有手写类型添加职责注释。保留原夹具与SHA-256，在构造接口变化时仅调整测试适配器，不改固定结果。

M2 是正式架构的主要出口：在独立 Host / 客户端中使用真正的灰松谷规则和冻结布局，先完成工人移动、采集与住宅建造；双方独立选择。按服务端接受顺序处理冲突，Host 也只执行一次。默认共享控制，并验证切 HostOnly 后来宾直接命令、建造派工和训练都被拒绝，已生效任务继续。

会话投影从 Sample 的标量扩展到正式实体集合，需检查深复制、归池、具体类型注册、回执／快照先后和完整投影上限。测量 10 Hz 起点的字节数、带宽、序列化分配及插值，再决定是否分块或拆流。少量正式 Prefab 此后继续使用；早期切片不以 IMGUI 样板替代最终 UGUI。

M3扩展完整场景。每增加一个角色／建筑／工位，同时补齐Definition映射、外观、动作、选择锚点与对应检查。美术验收包括实际修改、保存和重开，不只检查资源文件存在。

M4处理坏网络和会话恢复，并冻结联机协议／新存档格式。先修正确性再增加性能优化；不在此阶段加入房主迁移或多地图。

M5从干净目录／锁定依赖构建，验证实际Player，不把Editor Play通过或单纯生成包文件当作可发行。报告记录操作系统、Editor、框架commit、依赖、硬件、进程数、网络参数和未完成项。

## 验收矩阵

以下是正式游戏的实施要求；环境和 Sample 仅已覆盖其中相应的小样板路径。正式游戏不会因为历史检查名称相同就被标为通过。

| 类别 | 最低场景 | 必须观察到的结果 |
|---|---|---|
| 构建 | 正式 AppStartup 干净导入＋Mono / IL2CPP Player；正式 DTO 和定义 | 无 Editor 泄漏、缺包、丢失生成注册或资源；构建前后正式场景／Prefab 无意外改写 |
| 核心规则 | 固定布局、1×／2×、暂停、正常策略和无人照料 | 状态、胜负、耗时和原夹具在明确容差内一致 |
| 旧档 | v1加载、继续20秒、坏字段、坏关系、过大文件 | 有效档恢复；坏档不替换当前世界；64位随机状态准确 |
| 双进程 | Host＋1个独立客户端 | 选择独立；同一命令只处理一次；显示最终一致 |
| 四人 | Host＋3个独立客户端 | 共享库存、训练队列、工位和单位冲突可解释，无重复扣款 |
| 权限 | 非房主控制时间/加载、伪造实体、敌军、NaN、超大列表、重发 | 拒绝或明确部分失败；不越权改变模拟 |
| 控制模式 | SharedCamp→HostOnly→SharedCamp；队列旧请求、自动派工、训练、暂停时切换、加载与晚加入 | 当前策略统一生效；已执行任务继续，未执行旧策略请求不扣款；加载不扩大权限 |
| 晚加入 | 第二夜、施工中、训练中、在飞箭矢 | 同一版本世界，无重算伤害、重复开局或资源跳回 |
| 弱网络 | 建议覆盖RTT 0/100/200ms、抖动约25ms、丢包0/1/5%及乱序 | 关键状态不丢、运动最终收敛、结果无重复、缓存有界 |
| 暂停／倍速 | 暂停时加入与下令、1×↔2× | 心跳与UI继续；权限正确；Speed仅应用一次 |
| 恢复／重开 | 保存后变化、加载、旧epoch包、断线后重连 | 世界原子替换，旧消息无效，连接代次和实体身份分离 |
| 生命周期 | 多次连接／退出／重开、Domain Reload关闭后多次Play | 无重复filter、订阅、Update、失效缓存和遗留视图 |
| Host退出 | 正常退出和断连 | 其他玩家明确返回菜单，不无限等待新房主 |
| 美术 | 修改位置、帧、原点、主题后保存重开 | Prefab/场景编辑保留；预览无存档、模拟或网络副作用 |
| UI与画面 | 1280×800、1600×900、昼／夜、转职、施工、残骸 | 素材、脚底、分层、文本与操作反馈符合基线 |
| 性能 | 正常关卡高峰、四连接、持续完整三夜 | 记录模拟步时、帧时p95、带宽、分配及快照缓存；无持续增长 |
| 交付 | 双机器局域网＋独立Player＋干净项目构建 | 不依赖Godot、参考目录、旧Library或用户本机绝对资源路径 |

网络扰动数值是验收用例输入，不是已验证的网络能力声明。性能目标先保持当前窗口的可用60FPS体验，具体预算在M2取得Unity实测后冻结；原Godot测量不能作为Unity性能结论。

## 最值得优先消除的不确定性

| 不确定性 | 影响 | 收敛方式 |
|---|---|---|
| 新程序集的生成访问与正式 AppStartup / Addressables 组合 | Sample 可运行但正式 Player 未必可用 | M0 以正式 asmdef、定义和少量 DTO 验证 Mono / IL2CPP；保留补丁与依赖哈希 |
| 当前资源构建入口会执行环境初始化 | 构建可能保存正式场景／Prefab | M0 分离初始化与只读构建，验证资产前后哈希 |
| 游戏权限、去重与控制模式边界 | 共享关闭后仍通过自动派工控制单位 | M2 统一校验入口，双进程验证模式切换与交易 |
| 原RNG、浮点边界与旧档兼容 | 内容结果或存档继续运行发生变化 | 固定seed和旧档20秒逐字段比较 |
| 实体集合投影、池化和高峰载荷 | 旧副本被改写、晚加入不一致、可靠队列积压 | M2 冻结集合与生命周期检查，测量有效上限；按需分块，不预建增量日志 |
| 原生动画／字体／灯光差异 | 美术可用但观感偏离 | 先校准工人、分层弓箭手和一组HUD，再扩展 |

## 首版之外的成本

| 额外目标 | 粗略追加量 | 条件 |
|---|---:|---|
| 公网房间／邀请与一种中继或平台服务 | 约5–12人日 | 取决于平台SDK、账号、NAT、鉴权与服务运维；不含服务费用 |
| 独立专服进程与一种部署方式 | 约5–10人日 | 需去掉图形/UI启动依赖、验证headless资源与服务生命周期；不含集群平台 |
| 房主无缝迁移 | 约10–20人日以上 | 全状态接管、随机数、时钟、连接重建和故障分歧，需单独设计 |
| 各自营地／PVP或新增地图 | 重新估算 | 这会改变内容、权限、平衡和同步范围 |

直连方案需要可达IP和端口；FishNet本身的存在不自动提供公网房间或NAT穿透。只有网络入口确定后才能锁定相应transport、平台SDK和成本。

## 当前决策记录

- 沿用范围：2–4 人、一个共享营地、灰松谷三夜；Windows、房主主持、LAN 直连为首版假设。
- 采用：普通 C# 权威核心＋YYGC 会话对象／命令／状态链＋本地 Prefab / UGUI；单人走同一入口。
- 默认建议：SharedCamp；保留 HostOnly 开关，房主控制时间／存档；不提前分配私人单位。
- 已有实证：`6000.4.9f1` 环境基线，YYGC `10b8f0e` 的 Sample Mono / IL2CPP 四进程与弱网；正式少量定义、DTO 与生成注册已通过双后端启动，正式玩法联机仍必须重验。
- M0/M1 锁定：正式生成注册／访问、JSON 库、GUID / Key 映射与 GuidFirst／GuidV2 网络定义、规则及随机兼容；旧 v1 存档导入单独保留。
- M2 测量决定：完整投影频率、插值缓冲、载荷上限、是否分块或拆流以及性能预算。
- M0 双后端探针已收口；M1 规则／存储基础已验证；M2 正式网络、可见原生对象及 UI 操作已接入，仍需完整网络矩阵。文件编排、完整表现和交付继续执行；新增 YYGC 必要修正逐项记录于账本，不覆盖用户框架工作区。
