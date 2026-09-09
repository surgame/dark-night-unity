# YYGC 技术框架评估

本页保留首轮 F01–F12 的源码盘点；最新结论与处置优先级以[YYGC 能力复评](YYGC_REASSESSMENT.md)为准。第二轮补充实际命令生成器及依赖语义探针，调整为优先修正、验证和复用 YYGC 联机链路。

评估日期：2026-09-10。结论：**可作为 Dark Nights 的 Unity 基础；M0/M1 已完成依赖导入、脚本编译、Addressables、Bootstrap 和 Windows Player 启动基线，合作玩法仍需要一层明确的服务端命令和世界同步设计。** 优先复用应用、资源、Prefab、UI 和网络基础设施，保留现有集中式模拟。

## 评估口径

来源为 `D:\Developer\YYGC`，HEAD `6c3e0ff96221ac4a9fdfe0db85bf8f2cdc8dabc9`，加当前工作区。UGUIManager 的已暂存变更和未跟踪 IDRegistry 备份均未修改；本轮额外加入 Input System 兼容接入和 Editor asmdef 引用。精确哈希与状态见[冻结证据](evidence/assessment-2026-09-10.json)。

已阅读启动、对象装配、Behaviour、DI、视图、UGUI、命令链、状态同步、序列化、存档和测试入口。以下“已确认”指源码事实；静态评估阶段没有运行 FishNet 多进程测试或性能基准，M0 之后已补做 Unity 导入、脚本编译和 Windows Player 基础构建。

| 目录口径 | C# 文件 | 物理行数 |
|---|---:|---:|
| Runtime | 175 | 25,495 |
| Editor，不含 SourceGenerators~ | 51 | 14,496 |
| Tests | 6 | 1,269 |
| 合计 | 232 | 41,260 |

包括空行和注释；Runtime 下也含 Editor-only 工具，以上不是 Player 实际代码体积。另有三个源生成器工程和六个已随包提供的生成器 DLL。上述 232 文件中 31 个超过 300 行；ObjectView 972 行，ObjectInstance 477 行，StateSynchronizer 369 行。框架规模和维护负担明显高于当前游戏的 5,447 行运行代码，首版应只使用必要能力。

## 适合复用的能力

| 能力 | 实现依据 | Dark Nights 用法 |
|---|---|---|
| 启动编排与诊断 | AppStartup、模块依赖、Ready/Failed 状态 | 内容、生成器注册、UI 和网络入口完成准备后才能开局 |
| 对象定义与装配 | ObjectDefinition 的 PrefabRef、SharedConfigs、BehaviourTypes；ObjectInstance | 内容 ID 映射到可编辑 Prefab 和少量表现 Behaviour |
| DI | Root/Session/Local 容器，注入生成器 | 注入会话服务与只读视图接口；模拟内部保持普通构造依赖 |
| 2D/3D 对象视图 | ObjectView 的 SpriteRenderer、Collider2D、Animator、锚点能力 | 采用 2D 视图，供选择、外观与状态条定位使用 |
| 美术与 UI | UGUIView、UGUIBehaviour、绑定生成器、Prefab Mode Canvas 支架 | HUD、建造面板、菜单可在 Prefab Mode 编辑 |
| 资源 | Addressables、FastInstantiator、对象定义数据库 | 小关卡先本地打包加载，不引入远程热更新 |
| 网络基础 | FishNet、ServerRpc、ObserversRpc、TargetRpc、IStateData | 连接与会话桥，保留 FishNet RPC 与类型序列化能力 |
| 状态与池生命周期 | StatefulBehaviour 的状态变化、序号、OnDespawn | 作为接口参考；需要插值的快照明确复制数据与归还时点 |
| 保存能力 | YYArchive 的模块、元数据、文件流程 | 可作为整体营地快照的存储适配；不能拆成部分成功的世界恢复 |

框架默认文档偏向 UI Toolkit，但已有完整的 UGUI 适配路径。Dark Nights 的像素 HUD 和 Prefab 美术介入更适合统一采用 UGUI；这是该游戏的局部选择，无需把框架两套 UI 全面合并。

## 关键发现与处置

### F01 · 依赖尚未形成独立构建闭环

**已确认：** [package.json](<D:/Developer/YYGC/package.json>) 未声明 dependencies；[Runtime asmdef](<D:/Developer/YYGC/GameCore.Runtime.asmdef>) 和 [Editor asmdef](<D:/Developer/YYGC/Editor/GameCore.Editor.asmdef>) 直接依赖 FishNet、UniTask、Addressables、Input System、URP、UGUI/TMP 和 Odin 的 Addressables 适配。源码另使用 R3、VitalRouter、MemoryPack、ZLinq、DOTween。

包声明 `6000.2` / `35f1`；M0 实际使用 `6000.4.9f1` 完成导入、脚本编译和 Windows Player 构建。最低受支持补丁仍需和 YYGC 正式 commit 一起确认，不能只凭一次本地构建扩大兼容性结论。

**处置：M0 基础已完成，联机和干净机器复现仍待验收。** 依赖版本与来源已写入 manifest/lock 和 NuGet 配置；详细清单见[依赖文档](DEPENDENCIES.md)。

### F02 · Runtime 与 Editor 隔离存在编译风险

**已确认：** 以下 using 未包在 `UNITY_EDITOR` 条件内，而所属主程序集可进入 Player：

- [StateSynchronizer.cs:11](<D:/Developer/YYGC/Runtime/Objects/NetworkStates/StateSynchronizer.cs:11>)。
- [ObjectView.cs:4](<D:/Developer/YYGC/Runtime/Objects/Views/ObjectView.cs:4>)。
- [LocalObjectInstanceInitializer.cs:1](<D:/Developer/YYGC/Runtime/Objects/Runner/LocalObjectInstanceInitializer.cs:1>)。
- SODatabaseUpdater、WorldGeneratorSetting 也存在同类引用，需要一并扫描。

方法体有 Editor 宏并不能保护文件顶部的无条件 using。YYTabs 则有单独 Editor-only asmdef，不能把所有 Runtime 目录下的 UnityEditor 字样都判为错误。

另有 [IsExternalInit.cs:6](<D:/Developer/YYGC/Runtime/Utils/IsExternalInit.cs:6>) 将兼容类型定义在 `Runtime.Utils`；编译器识别的是 `System.Runtime.CompilerServices.IsExternalInit`。现有文件本身不能提供所需兼容类型，是否由其他锁定依赖提供仍需验证。

**处置：M0 高优先级构建风险。** 在隔离框架 checkout 做最小修正，实际验证 Player 和生成器。不把这一静态发现伪称为已复现的构建日志。

### F03 · 发送器所有权没有覆盖游戏操作权限

**已确认：** [NetworkCommandSender.cs:50](<D:/Developer/YYGC/Runtime/NetworkCommands/NetworkCommandSender.cs:50>) 使用 `RequireOwnership = true`；[NetworkCommandProcessor.cs:12](<D:/Developer/YYGC/Runtime/NetworkCommands/NetworkCommandProcessor.cs:12>) 收到连接后，经第 42 行发布 command，未将真实连接身份传入业务处理上下文。连接只在广播分支中用于寻找发送器。

这能约束从哪个网络对象发起 RPC，但未回答请求者能否操作 command 内的单位、建造类型或会话权限。共享营地也需要验证玩家已入局、单位属于友军、目标可用、支付成功和请求未重复。

**处置：M0/M2 必做。** 优先在 YYGC Processor 中保留可信连接上下文，并为 Gateway 增加可选择的权威命令策略；游戏只实现营地权限、业务去重和规则调用。`CampCommandEndpoint` 若保留名称，仅表示薄业务适配，不默认新建 RPC/发送器/路由栈。具体边界见复评 R04。

### F04 · 本地优先路由需要与权威模拟隔离

**已确认：** [NetworkCommandGateway.cs:19](<D:/Developer/YYGC/Runtime/NetworkCommands/NetworkCommandGateway.cs:19>) 先执行本地 `next`，再发送网络；第 99 行在服务器启动时直接返回，Host 依赖之前的本地发布执行业务。

该模式适合本地响应或预测。若把 Dark Nights 原有支付、生成建筑和伤害处理直接挂上去，普通客户端也可能先修改局面；Host 则需要另一套隐含路径。

**处置：M2 必做。** 本地只更新选择、待确认预览；Host 和远端请求都进入同一权威处理器一次。共享库存和建筑生成只由服务端结算。

### F05 · 首次快照已实现，但粒度是单对象

**已确认：** [StateSynchronizer.cs:286](<D:/Developer/YYGC/Runtime/Objects/NetworkStates/StateSynchronizer.cs:286>) 在 `OnSpawnServer(connection)` 收集 Behaviour 当前状态，第 311 行以 TargetRpc 发送首次快照。因此不能说框架没有晚加入状态。

文件顶部仍写 SyncList，实际路径为 **TargetRpc 初始快照＋ObserversRpc 后续状态**。后续变化发送完整 `IStateData`，并非自动按字段计算差量。`Skip(1)` 避免订阅时立即重复发初始值。

**待验证：** 初始快照与不可靠更新的交错、Definition 准备时序，以及多个对象和经济／波次之间的一致切点。`TryInitialize` 在第 172 行先设完成标记，DefinitionId 为 0 时初始化提前退出，第 177 行仍发布完成事件；异常或零 ID 的恢复需要测试。未在这条对象链中看到完整营地的 snapshot barrier、epoch 或入局 Ready 协议。

**处置：M2/M4。** 优先把整个营地投影冻结成一个会话 Behaviour 状态，复用现有 TargetRpc/ObserversRpc；游戏补原子副本应用、Ready 和 epoch。复评 R03 另确认初始 null 时 `Where(non-null).Skip(1)` 会吞掉第一次真实变化，需要先修正。分块与拆流由测量决定。

### F06 · 框架 InstanceId 不适合充当营地实体 ID

**已确认：** [StateSynchronizer.cs:193](<D:/Developer/YYGC/Runtime/Objects/NetworkStates/StateSynchronizer.cs:193>) 对同一 Owner 使用 `Player_{ClientId}`；无 Owner 的服务端为 `Object_{ObjectId}`，客户端回退为 `ClientObject_{ObjectId}`。[LocalObjectInstanceInitializer.cs:26](<D:/Developer/YYGC/Runtime/Objects/Runner/LocalObjectInstanceInitializer.cs:26>) 则用 `LocalInstance_{DefinitionId}`。

同 Owner 的多个单位、同 Definition 的多个本地建筑不会得到唯一 InstanceId，两端字符串也未必相同。已检查实现中主要用于实例属性／日志，不能据此断言现有框架已发生字典覆盖。

**处置：M1/M2。** 保留游戏 `EntityId`，按 `(epoch, EntityId)` 绑定世界与视图。连接 ID、FishNet ObjectId、YYGC DefinitionId 各自使用，不复用其字符串当存档身份。

### F07 · SessionScope 只适合受控同步片段

**已确认：** [SessionScope.cs:19](<D:/Developer/YYGC/Runtime/Objects/Runner/DI/SessionScope.cs:19>) 是一个静态 Stack，没有线程或异步上下文隔离；注释称“线程安全”与实现不符。[ObjectInstanceFactory.cs:42](<D:/Developer/YYGC/Runtime/Objects/Runner/ObjectInstanceFactory.cs:42>) 等待资源之后才进入装配，而 ObjectInstance 从 `SessionScope.Current` 取得父容器。

**风险：** 多个 await 交错时，跨 await 持有 scope 会把实例装入错误容器。对于单线程同步装配，这个模式仍可用。

**处置：M0/M2。** 游戏一次只运行一个权威对局；明确传递会话容器，资源等待结束后仅在同步初始化片段进入 scope。需要扩展时只补工厂的容器传递点，不引入多战局调度框架。

### F08 · 旧状态归池影响插值与异步发送

**已确认：** [StatefulBehaviour.cs:77](<D:/Developer/YYGC/Runtime/Objects/NetworkStates/StatefulBehaviour.cs:77>) 在状态变化后归还前一状态；第 158 行按不可靠 Sequence 丢弃旧包，OnDespawn 重置序号。`Network` 为空的本地初始化对象不能直接依赖其 `IsServer/IsOwner` 属性。

**风险：** UI、插值缓存或异步发送若保存原 State 引用，随后可能读到被池复用的数据。record 的浅复制／集合成员也不能自动提供深度不可变快照。

**处置：M2。** 本地视图使用普通 PooledBehaviour／绑定组件；发送与插值保存明确拥有的冻结 DTO。需要池化时写清释放时点并做生命周期测试。

### F09 · 不能依据“零 GC”描述推断发送成本

**已确认：** [GenericTypeSerializer.cs:26](<D:/Developer/YYGC/Runtime/Middlewares/GenericTypeSerializer/GenericTypeSerializer.cs:26>) 调用返回 `byte[]` 的 MemoryPack Serialize；每次 Behaviour 变化可立即触发广播，没有游戏级快照频率或批量预算。

**处置：M2/M5。** 首版以 60 Hz 规则推进、10–20 Hz 展示快照为独立参数，记录消息数、字节数、分配与帧时。先做简单批量 DTO，测量后再考虑差量编码、池或压缩，不为小关卡重写序列化框架。

### F10 · 重启与广播路径需要补真实测试

**已确认：** Gateway ResetRuntimeState 清发送器和 `_initialized`，没有在该方法移除 LocalInput filters；[CommandRouters.cs](<D:/Developer/YYGC/Runtime/NetworkCommands/CommandRouters.cs>) 的 Router 是静态 readonly。客户端广播用 `PublishAsync(INetworkCommand)`，服务端用 `PublishTo` 保留具体类型；[INetworkCommand.cs:16](<D:/Developer/YYGC/Runtime/NetworkCommands/INetworkCommand.cs:16>) 的发布也没有等待异步结果。

**待验证：** 关闭 Domain Reload 后重复 Play 是否叠加 filter，初始 Owner spawn 是否触发发送器注册，具体类型 handler 是否收到广播，以及异步发布与池归还顺序。尚未取得所需 VitalRouter/FishNet 版本，因此不把这些全部判为确定运行错误。

**处置：M0/M2。** 用最小探针验证；游戏业务入口避免依赖这些隐含假设。若仍启用相关框架模块，重复启停测试必须保留。

### F11 · 类型注册与生成器是协议和构建输入

**已确认：** GenericTypeRegistry 以整型 ID 注册类型，启动模块验证生成注册。三个 Editor 生成器有源码，网络命令、StateData、Singleton 的三个 DLL 未在该仓库找到对应源码工程。详见[依赖清单](DEPENDENCIES.md)。

**处置：M0/M2。** 锁定 DLL/hash、.meta、生成注册表及包版本。连接先校验协议与注册表摘要；未兼容的客户端在反序列化业务数据前被拒绝。不运行 IDRegistry 服务或改写其数据库来完成本次评估。

### F12 · 保存模块隔离与世界原子恢复需要区分

**已确认：** [YYArchiveService.cs:230](<D:/Developer/YYGC/Runtime/YYPlugins/YYArchive/YYArchiveService.cs:230>) 逐模块 Restore，异常后继续其他模块，测试也覆盖这一行为。它有助于隔离无关模块，但经济与实体互相引用时会形成部分恢复。

**处置：M1/M4。** 整个营地作为一个事务：先解析和校验全部关系，在临时世界恢复成功后一次替换。首版继续保留 JSON 语义与严格校验；若接 YYArchive，将整体快照封装为一个模块，不分别恢复经济、单位和建筑。

## 测试证据与维护建议

当前 Tests 主要覆盖双轨池化、DI、UI 绑定和 YYArchive。未在已检查测试中找到 Host＋独立客户端、晚加入／重连、带身份业务命令、丢包恢复和 Player 构建证据。两轮均未运行这些测试；第二轮只补了独立依赖语义和真实命令生成器探针。

不需要先把 YYGC 改成“理想框架”再做游戏。优先处理 F01/F02 的编译与依赖、验证 F07/F10 的实际使用路径；游戏侧实现 F03/F04/F05/F06 所需的会话合同。F08/F09 按测量和生命周期约束落实。大型 Editor 工具、3D 地形、全部对象池和 UI Toolkit 的全面重构暂不进入该关卡工程。

下一步顺序和每阶段出口见[开发执行计划](DEVELOPMENT.md)，推荐架构见[技术架构](ARCHITECTURE.md)。
