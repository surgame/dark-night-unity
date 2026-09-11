# 评估状态与验证边界

## 2026-09-11 开始正式移植

已实施配置、AppStartup 接入、资源构建防覆盖、架构守卫以及权威规则／旧档核心，详见[当前执行状态](DEVELOPMENT.md#implementation-progress)、[首批证据](evidence/migration-start-2026-09-11.json)和[核心迁移证据](evidence/core-migration-2026-09-11.json)。后续各节保留历史时点，不代表新一批的状态；正式场景、对象绑定、灰松谷表现和正式联机仍未完成。

## 2026-09-11 ObjectDefinition 身份模式切换

当前正式 Dark Nights 项目已切换到 `GuidFirst`／`GuidV2`：数据库关闭在线 ID 服务，正式 Definition 的 `Id` 全为 `0`、旧 ID 别名与旧 ID 映射为空；Windows 构建启用 `YYGC_GUID_DEFINITION_WIRE_V2`。Editor／Runtime 均有硬失败守卫，不再给旧 ID 赋值、通过旧 ID 查询或接受 LegacyV1 正式定义。YYGC 框架中的 deprecated `ObjectDefinition.Id` 字段仍保留给其他旧项目的序列化兼容，用户维护的 YYGC 仓库未修改；独立 LAN Sample 继续作为 LegacyV1 对照。旧 v1 存档导入是玩法迁移，不属于本次切断的定义身份兼容。

本次切换的 32/32 Editor 测试、Core 编译与回归、Mono／IL2CPP 各 6/6 独立启动检查记录在[正式 GuidV2 身份切换证据](evidence/formal-object-contracts-guid-v2-2026-09-11.json)。此前的[正式对象接入证据](evidence/formal-object-contracts-2026-09-11.json)保留为 LegacyCompatible／LegacyV1 历史基线，不覆盖或重生成。

## 2026-09-11 正式移植设计更新

本次按当前工作区更新[移植方案](MIGRATION_PLAN.md)、[架构](ARCHITECTURE.md)、[联机合同](MULTIPLAYER.md)和[执行计划](DEVELOPMENT.md)，同步 README、依赖、开发入口及协作约定。工作范围为设计文档，未开始正式游戏代码迁移。

实际核对：Godot HEAD 为 `91cb09ff0894f26134f07fd544f1273d9fe7ffaa`；Unity 设计输入为 `4432d75`；YYGC 当前 HEAD 为 `10b8f0ef6a5ed965ebd473dbcbe4a0dd795379c4` / `0.3.0-preview.1`。读取了当前项目版本、包引用、真实命令／状态链、定义身份入口、Godot 规则及旧档约束，并检查 Sample 的已提交验证摘要。设计开始时三个仓库均无工作区差异。

原设计阶段曾明确：共享控制默认开启，房主可切 HostOnly；关闭时同时禁止来宾直接下令和建造自动派工等间接操作。策略版本与世界 epoch 分开，已执行任务继续，单人走同一权威入口。正式四程序集不引用 Sample；新定义使用 GUID / Key，首个网络切片暂保留 LegacyV1。该身份方案已由上方正式 GuidFirst／GuidV2 切换取代。

从源码确认的接入待办：样板友元访问补丁不覆盖正式程序集；当前 Addressables 构建入口会执行环境初始化并保存资源；正式投影需要从标量扩展为有界冻结集合。这些工作尚未实施，列入 M0 / M2。既有 LAN / IL2CPP 结果仍是历史证据，本次未重新运行 Unity、Godot 或游戏测试。

本次文档核验：9 份修改后的 Markdown 均可按 UTF-8 读取，77 个本地链接有效，代码围栏配对；六阶段基础工作量加总为 20–33 人日。`git diff --check` 通过，Game / tools / 冻结证据均无改动；Godot 与 YYGC 的 HEAD 和干净工作区状态保持不变。

下文保留此前环境、框架和 Sample 的执行记录；其中“M0/M1 环境”是历史命名，当前阶段以执行计划为准。

2026-09-11 增补：独立 [LAN Sample](LAN_SAMPLE.md) 已完成。25 项真实 Core 断言、15 项 VitalRouter 修正回归、Unity 编译与 Windows Mono Player 构建通过；四个 Player 的基础和真实 UDP 弱网各 30 项断言通过。另已完成 Windows x64 IL2CPP Release＋High 裁剪构建，IL2CPP 四进程基础／弱网也各通过 30 项；弱网实测 622 包、33 丢弃、49 次重排。原生场景重开、ObjectView 绑定和 Prefab 副本编辑／保存／重开通过，Mono 图形 Player 画面已检查。证据在 `docs/evidence/lan-sample-*.json`。双机器 LAN、Steam、正式玩法 AOT、长期负载未验证；下文保留原评估时点。

日期：2026-09-10。本文件保留 YYGC 静态评估与 Unity／合作联机方案的历史边界；M0/M1 执行结果已追加在下方。

## YYGC ID 重构方案与版本补齐

2026-09-10 已在 `D:\Developer\YYGC` 写入[定义 ID 重构执行方案](<D:/Developer/YYGC/Documentation~/ID_REGISTRY_REFACTOR_PLAN.md>)、[版本核对与发布约定](<D:/Developer/YYGC/Documentation~/VERSIONING.md>)和 CHANGELOG；包内版本由遗留 `1.0.0` 更正为 `0.2.3`，明确为未发布开发版本。历史标签保持原样，未补造缺少独立证据的 `v0.2.1`。

本次方案基线为 `14a8b9a44bc826eb0736b364db21e625bfbcf14b`。检查开始时的 UGUI / Input System 工作区改动在编写期间已由其他操作纳入该提交，本次不重复提交那些改动。原评估中的 `6c3e0ff` 和暂存 / 备份描述继续作为历史记录；本次快照未发现历史备份目录，不创建、清理或恢复它。

方案约定保留旧 int 字段、公开签名、序列化引用及默认 V1 wire；deprecated 常驻显示与可选 `Obsolete` 警告分开，避免破坏 warnings-as-errors 旧构建。GUID-only 内容、池化判等、旧档历史映射和双进程兼容均有独立门槛。

本轮没有实施 GUID、开关、代码 deprecated 标注、迁移或 Unity 测试；完成的是文档、JSON / 版本记录和改动范围核对。源码哈希与检查边界见[本次 audit](<D:/Developer/YYGC/Documentation~/Evidence/id-registry-plan-audit-2026-09-10.json>)。Dark Nights 的定义库仍为空，不能代替真实旧项目的兼容夹具。

## M0/M1 执行增补

在评估后已对 `DNights` 执行 Unity 6000.4.9f1 导入：

- `Packages/manifest.json`、`packages-lock.json` 已锁定本地 YYGC、NuGetForUnity、FishNet 4.7.2、UniTask 2.5.11、R3 1.3.1、MemoryPack 1.21.4、ZLinq 1.5.6 与 Addressables 2.10.1。
- `Assets/NuGet.config`、`Assets/packages.config` 和 `Assets/Packages` 已就位；NuGet 包缓存不再被 Git 忽略，以避免新 checkout 在脚本编译前缺少 R3/MemoryPack 核心 DLL。
- 从本机已有插件补齐 Odin Inspector（含 Addressables 模块）和 DOTween，YYGC 的 Runtime/Editor 程序集均可编译。
- YYGC 自动创建了 6 个空的全局 ScriptableObject 配置资产；`ObjectDefinitionDatabase` 保持空列表，不带入参考工程的对象定义。
- 已创建 `AppStartupSettings`、`GameCore`、`NetworkManager`、`DefaultPrefabObjects`、Addressables 默认组和 `Bootstrap` 首场景；启动场景已放在 Build Settings 第一位。
- 已执行 StateData/NetworkCommand 生成器；当前没有项目专属类型，因此生成的是空但可编译的注册入口。
- 为适配项目已启用的 Input System，YYGC 的 UGUI 启动和两个 Editor 创建器改用 `InputSystemUIInputModule`，Editor asmdef 补充 `Unity.InputSystem` 引用。
- Unity 编译、Addressables 内容构建、Windows Player 构建和最终 Player 冒烟均通过；Player 日志达到 `AppStartup` Ready，且无 Input System 异常。联机、完整玩法、AOT 和真实多进程资源流程仍未验收。

YYGC 原有暂存的 `UGUIManager.cs` 和未跟踪 IDRegistry 备份保持原状；本轮仅新增上述 Input System 兼容改动，仍未提交 YYGC。

## 第二轮快速复评

新增[YYGC能力复评](YYGC_REASSESSMENT.md)与[冻结证据](evidence/yygc-network-review-2026-09-10.json)。用户补充联机尚未正式生产使用；设计改为优先修正并验证YYGC完整命令/会话状态链，游戏补营地合同，分块和拆流由测量触发。

本轮实际执行：用真实命令生成器DLL对当前/旧命名空间分别运行Roslyn，输出0/1份文件；R3 1.3.0空初值订阅收到[2]，已有初值对照收到[1,2]；MemoryPack 1.21.4具体类型往返得到42，未注册接口调用抛出异常。工具源码和依赖锁文件已保存。它们是隔离生成器和依赖语义观察，未运行Unity、FishNet RPC或完整框架测试；M0/M1 环境基线已完成，M2–M5仍未完成。

补充盘点了Interaction Sessions、工厂Local/Network分流、会话Behaviour的复用位置；这些静态复评仍以 YYGC/Godot 只读为口径。随后执行阶段只对 YYGC 加入 Input System 兼容改动，未改 Godot。下文的文档数量、链接数量和全部测试未执行等记录是首轮历史口径；第二轮新增工具的运行范围以本节和复评报告为准。

第二轮验证：依赖锁定恢复及探针运行成功，原始551项素材哈希通过；10组源码/仓库/生成器/素材证据与首轮冻结值一致。随后 M0/M1 已验收 `Game/` 宿主（ProjectVersion 为 6000.4.9f1），因此旧的“只读取、未修改宿主”描述仅适用于 M0 之前的评估阶段。

## 本轮完成

- 创建 `unity-projects`，初始化独立Git `main`分支。
- 读取YYGC启动、对象、DI、视图、UGUI、命令、状态、序列化、存档与测试实现，并对照框架文档。
- 记录YYGC HEAD＋工作区状态；用户的UGUIManager暂存变更与IDRegistry备份保留。
- 统计框架232份C#／41,260物理行（不含SourceGenerators~），并记录六个随包提供的生成器DLL及导入设置哈希。
- 核对Godot基线115份运行C#／5,447行，以及可移植规则和需要重建的表现层。
- 重新核验551项原素材SHA-256，通过；记录balance、waves、Pinewatch布局和旧档夹具哈希。
- 读取本机一套Unity.exe版本，核对Unity6.2官方C#／API profile说明。
- 确认2–4人合作共享营地，形成服务端权限、同步、晚加入、重连、暂停／倍速与保存建议。
- 建立架构、迁移、美术接入、难度／执行计划、AGENTS与人工接手指南。
- 建立 Addressables、AppStartup、FishNet NetworkManager 和 Bootstrap 场景基线；执行两个空类型注册源生成器。
- 在本地 YYGC 中修正 UGUI/Input System 组件和 Editor asmdef，使 Player 启动不再触发 `StandaloneInputModule` 与 active Input System 冲突。

机器可复核数据见[冻结证据](evidence/assessment-2026-09-10.json)。采集器排除被忽略的生成器bin/obj缓存；目录物理代码量与Player实际程序集体积不同。

## 本轮没有执行

| 项目 | 当前状态／原因 |
|---|---|
| Unity工程导入／Play／Player构建 | M0/M1 已完成导入、脚本编译、Addressables 构建、Windows Player 构建和启动冒烟；编辑器 Play、场景功能仍未完整验收 |
| YYGC测试与生成器重建 | StateData/NetworkCommand 注册源已在宿主中生成；YYGC 全套测试与生成器工程重建仍未执行 |
| FishNet Host＋独立客户端 | 未执行；没有构建出的Unity宿主 |
| 旧档／完整三夜Unity规则回归 | 未执行；Core尚未迁移 |
| Unity Prefab制作与美术人工流程 | 未执行；文档为设计合同 |
| Unity性能／带宽／弱网测试 | 未执行；频率和缓存值只是待测起点 |
| 当前Godot游戏全套测试 | 未重跑；133游戏检查／27架构自测来自其既有报告 |
| YYGC或Godot代码修改／提交 | YYGC 仅有本轮 Input System 兼容改动，未提交；Godot 未修改；用户原有 YYGC 工作区改动保持不变 |

## 已区分的事实与推断

StateSynchronizer确实通过OnSpawnServer＋TargetRpc实现了单对象首次快照；“没有初始快照”不是本次结论。需要新建的是世界切点、Ready、epoch和合作业务的一致性流程。

Runtime中的无条件UnityEditor引用、IsExternalInit命名空间、缺少包依赖声明属于已看到的源码事实；Player具体报错和依赖补齐后的行为仍需实测。广播类型路由、初次Owner注册和Domain Reload关闭后的重复filter列为探针，不伪装成已复现故障。

框架README的性能描述未经过本轮基准验证。源码中的byte[]序列化和状态变化广播说明需要测量分配与消息量，不能直接宣称已证明性能不达标。

## 本仓库完成检查

已核验10份Markdown的UTF-8与62个本地链接（含源码行号），没有失效链接或项目用语问题。阶段加总24–40人日，加入25%余量后为30–50人日；证据规模与文档一致。

采集器对相同行数的文件按路径稳定排序，使用默认输出连续重跑两次，10组核心报告数据均与冻结证据完全相同；原素材551项哈希通过。源仓库HEAD与Git差异不变，21项YYGC关键文件和17项Godot证据文件哈希一致。六个生成器DLL及其六份.meta均已记录。

Git忽略规则覆盖 Library、Temp、Obj、Logs、Build、artifacts、本机依赖与 UserSettings；NuGet `Assets/Packages`、README、AGENTS、Unity.meta、Packages 锁文件、ProjectSettings 与冻结证据保持可版本化。实际 Unity 开发从 M1 的环境基线继续；生成注册表目前为空，双进程、序列化、AOT 和玩法对象仍属于后续验收。

新增实施结果需附实际命令和证据，在此更新状态。不要把计划中的Prefab、API、守卫或测试写成已经存在。
