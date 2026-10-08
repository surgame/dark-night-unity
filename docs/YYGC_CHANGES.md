# YYGC 修改授权与改动账本

## 2026-10-09：按用户请求集成现成 Debug Hub 候选

将获批候选的九项 Debugging 源码／资源（具体名单沿用下方2026-10-08表）接入 Local `.deps/YYGC-grid-business`；没有修改其源码行为。追加下表9个自动元数据文件到原Debugging补丁。基线fee1864及既有网格补丁不变，新的overlay SHA-256为 `64490b4dabd9249aba2483709df9aafad93d96ee3e39f978cc7ba9b24b0d8a4b`。

| 文件 | 原因 | 落点与验证 |
| --- | --- | --- |
| `Runtime/Debugging/Resources.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/Resources/YYGC.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/Resources/YYGC/Debugging.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/Resources/YYGC/Debugging/RuntimeDebugHub.uss.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/Resources/YYGC/Debugging/RuntimeDebugHub.uxml.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/RuntimeDebugHubView.cs.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/RuntimeDebugPanelDescriptor.cs.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/RuntimeDebugPanelRegistration.cs.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |
| `Runtime/Debugging/RuntimeDebugPanelRegistry.cs.meta` | Unity首次导入生成；保存GUID及原导入设置 | Local锁定依赖及Debugging补丁；候选逐项一致 |

18个源码／元数据与候选一致，Editor7、Development5、Release5个程序集静态编译均0错误，协议30／存档v22／AMP1 schema2。未运行合并版Play、测试或Player。用户YYGC主工作区既有RuntimeDebugHub.cs修改保留，未切换或覆盖。移除旧游戏worktree前保全原依赖副本与正式证据；过程见[集成记录](RUNTIME_DEBUG_HUB_MERGE_20261009.md)。

## 2026-10-08：Runtime Debug Hub UITK 重构候选

按本聊天的具体 Debugging 规范和“完成重构、只做静态编译”要求实施。落点仅为 `ref-20261008-runtime-debug-hub-uitk` 的隔离 `.deps/YYGC-grid-business`，未修改／切换用户 YYGC master、Local依赖或其已有 RuntimeDebugHub.cs 工作。没有升级框架提交／包版本，没有改动其他子系统。新工具资源的 .meta 留待正常Unity导入。

| 框架相对文件 | 原因与最终行为 | 验证 |
| --- | --- | --- |
| `Runtime/Debugging/RuntimeDebugHub.cs` | IMGUI退出；稳定注册、F1、UITK文档、单一Interaction租约及异常收尾 | 三种条件静态编译通过；运行待验 |
| `Runtime/Debugging/IRuntimeDebugPanel.cs` | Draw改为保留视图、激活取消、停用及释放；游戏旧提供者同步迁移 | 游戏全部调用者编译通过 |
| `Runtime/Debugging/RuntimeDebugPanelContext.cs` | GUIStyle退出；只提供关闭与有界反馈 | 静态编译通过 |
| `Runtime/Debugging/RuntimeDebugPanelDescriptor.cs` | 稳定ID、标题、排序、有限正数参考尺寸 | 静态编译通过 |
| `Runtime/Debugging/RuntimeDebugPanelRegistration.cs` | 懒创建、失败可重试、每次激活CTS及幂等释放 | 静态编译通过；生命周期待验 |
| `Runtime/Debugging/RuntimeDebugPanelRegistry.cs` | ID判重、排序和注册对象代次比较 | 静态编译通过；注册行为待验 |
| `Runtime/Debugging/RuntimeDebugHubView.cs` | 沿用UIPanel/DI绑定；共享导航、内容、拖动与尺寸 | 静态编译通过；真实画面待验 |
| `Runtime/Debugging/Resources/YYGC/Debugging/RuntimeDebugHub.uxml` | 通用窗口模板、可滚动内容和状态 | XML／控件源码检查通过；Unity导入待验 |
| 同目录 `RuntimeDebugHub.uss` | Flex及有作用域主题、输入、普通／危险按钮 | 子集源码检查通过；Unity导入待验 |

可重建来源是 `tools/debug-hub/yygc-debug-hub.patch` 与同目录锁。基线仍为 `fee18645c997ed7529c4592917de6c412033c84e`，既有网格补丁SHA `58efc66c8e5b38caf2db939db1bf1fe9597b701b2485a29f36ef34d576b44bbb` 不变；新增Debugging补丁SHA为 `081456b98c116b352addd1d852414c59acf3d26aa9928a212936af6eb599b67a`。干净基线重建9/9文件一致，准备入口幂等通过。原网格准备脚本在完成原补丁后串行应用Debugging overlay，拒绝锁不匹配和未知文件差异。

最终编译：Editor七程序集、Development五程序集、Release五程序集均0错误；源码输入哈希已核对。候选协议29／存档v21／AMP1 schema2；没有Unity导入、Play、测试执行、Player或联机验收，不借用其他切片的通过数。详见[实现记录](RUNTIME_DEBUG_HUB_IMPLEMENTATION_20261008.md)与[静态证据](evidence/runtime-debug-hub-20261008.json)。

## 2026-10-06：获批地形调试入口整合

用户明确批准本次两个 AnyRuleD Editor 文件、对应检查、隔离候选及可重现补丁接入。修改落在游戏已锁定的隔离检出 `.deps/YYGC-grid-business`，保留其已有修改；没有切换、清理或写入用户 `D:/Developer/YYGC` 主仓库。

| 框架相对文件 | 原因与最终行为 | 验证 |
| --- | --- | --- |
| `AnyRuleD~/Packages/com.tsgame.anyrules/Editor/Debug/GridDebugWindow.cs` | 将旧 Tools 菜单迁到 `YY/AnyRuleD/网格调试器`；列出存活的已绑定地图、复用同一窗口，支持子对象选择、Scene Shift 定位、格属性和四角查询；退休后撤销借用引用，窗口不拥有地图 | 实际菜单唯一；真实两层宿主输入／四角／切换／只读／退休检查1/1；Bootstrap Host 两层和退出 Play 检查通过 |
| `AnyRuleD~/Packages/com.tsgame.anyrules/Editor/Workbench/AnyRuleDWorkbench.cs` | 保留编辑态制作画布；Play 时选择当前地图并打开同一网格调试器；原显式导出与目录身份检查保留 | 真实 Host 中运行页面绑定前景，并列出前景／矿层；不写源资产 |

游戏侧只在 Editor 给 `TerrainPreview`、`MineralLayerView` 绑定原生 `GridDebugView`，新回归位于 `TerrainGridDebugIntegrationTests.cs`。框架补丁仍以 `fee18645c997ed7529c4592917de6c412033c84e` 为基线，`tools/grid-business/yygc.patch` SHA-256为 `58efc66c8e5b38caf2db939db1bf1fe9597b701b2485a29f36ef34d576b44bbb`；既有18项保留，追加两个Editor文件，最终干净基线重建20/20与当前源码一致，准备脚本幂等通过。

最终编译完成且无错误，架构732文件／16自测／0命中。首轮新测试断言编译错误、SceneHandle排序失败及Play退出时短暂残留的退休控制器均保留记录并修复；最终退休回归通过。后台Scene未绘制的尝试保留；随后受控聚焦Editor，真实Scene Shift事件在缩放宿主中选中(88,-71)，600×800窗口截图通过布局核对。未构建Player、升级YYGC或更改规则／渲染算法；游戏协议25／存档v19／AMP1 schema2不变。本批记录见[地形检查说明](TERRAIN_GRID_DEBUGGER.md)及 `artifacts/terrain-debug-integration-20261006/`。

## 2026-10-05 原生矿层游戏侧接线

本批没有修改YYGC、AnyRules、`.deps`源码／补丁或升级锁定版本。原生前景 `MapWireMessage` 与独立 `MineralMapWireMessage`／序列化，以及 `NativeMapTransport<TWire>` 位于游戏Runtime，复用现有MapInterestService、MapProtocol及ChunkReplicaStateMachine。记录的373项框架文件哈希复核0项变化；当前限制及验证见[实现记录](MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)。

## 2026-10-03：获批 Workshop 导航修正

用户在本聊天明确批准两个工坊文件、新导航回归、隔离验证及可重建依赖接入。落点为 detached 薄检出 `D:/Developer/YYGC-worktrees/workshop-navigation-20261003`，检查点 `0d461a29294a3932479f12db8800976b3261914a`。用户 `D:/Developer/YYGC` master 与其已有 RuntimeDebugHub.cs 修改未变；没有新建游戏分支、没有推送。

| 框架相对文件 | 原因／最终行为 | 结果 |
| --- | --- | --- |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.cs` | 切目标与焦点不自动补配置，只刷新编辑树；原能力编辑、显式同步和保存仍同步 | 完整／缺失配置导航内容与 dirty 保持；原生目标转交、窗口复用通过 |
| `Editor/Objects/Definition/Workshop/WorkshopConfigService.cs` | 真实配置变化才 SetDirty，forceRefresh 只触发视图回调 | 强制刷新不标脏；显式同步、Undo／Redo、保存重开通过 |
| `Tests/IdRegistry/ObjectDefinitionWorkshopNavigationTests.cs` | 新增导航只读、刷新、显式同步与持久夹具保存重开回归 | Local Editor 框架3/3，原生接入2/2 |
| 同上 `.cs.meta` | Local Unity 导入生成的测试脚本元数据 | 保留原GUID并接入补丁，未手配GUID |

游戏锁定基线 `fee18645c997ed7529c4592917de6c412033c84e` 不变，更新 `tools/grid-business/yygc.patch` 与 `dependency.lock.json`；补丁SHA-256为 `7b16e941cd7cc119e22b531678584103c697c6b5456b637658cdd0915a039811`。既有14项补丁保留，新修正4项；干净基线重建18/18与本机源码一致，准备脚本幂等通过。没有改 manifest、包版本、运行协议或存档。

框架UPM测试默认未启用，本次由 `tools/workbench-navigation/prepare-probe.ps1` 从依赖唯一源产生临时镜像，编入现有游戏测试程序集验证。镜像、持久夹具及备用Git索引已按工程规则集中保留于 `artifacts/待清理/20261003-workbench-aggregation/`；无永久删除。游戏本批不同Editor用例合并16/16，作者资产436项保持。新增测试首轮命名空间冲突及旧焦点采样失败保留；没有Player、IL2CPP或联机验收。见[机器证据](archive/evidence/workbench-aggregation-20261003.json)与[授权执行记录](archive/WORKSHOP_NAVIGATION_FIX_PROPOSAL.md)。

2026-10-02 工具 Definition 采集重构：**框架修改文件为 0**。新增游戏 MiningToolBehaviour 复用现有 RequireConfig／Inject、ObjectInstanceFactory／PreparedObjectDefinition、LocalObjectInstanceInitializer 和会话权限；无独立 DI、对象或状态框架。矿镐 Prefab 保留 GUID 补齐原生装配；新增炸弹定义身份使用 Unity meta 与 DefinitionIdentityAuthoring.AdoptCopiedAsset。真实装配与恢复验收见[本批说明](TOOL_DEFINITION_HARVESTING.md)。

## 2026-10-01 矿镐最近表面与持续挥舞

本批 YYGC／AnyRuleD 框架新增源码改动：**无**。游戏复用现有 ARDMap 只读查询、YYGC ActorState 事务与生成 CopyFrom；直线选表面和落镐时机属于游戏。`tools/grid-business/dependency.lock.json` 仅将游戏协议同步为 20，框架补丁摘要仍为下方 GUID 修复的 `60b9f681dab1194d10e27637d9f55ff1dad62b8c105203120d5afbaad0aab00b`。按用户要求未编译、测试或构建，见 [开发记录](DEVELOPMENT.md)。

## 2026-10-01 采矿业务状态空占用者协议修复

改动落点仍为 `.deps/YYGC-grid-business` 的隔离检出，基线 `fee18645c997ed7529c4592917de6c412033c84e`；没有修改或切换用户 `D:/Developer/YYGC` master。现有补丁已加入本次修复及回归，当前 SHA-256 为 **60b9f681dab1194d10e27637d9f55ff1dad62b8c105203120d5afbaad0aab00b**，对应 `tools/grid-business/dependency.lock.json`。当前检出的 `git apply --reverse --check` 通过；未建立新检出验证整套依赖恢复。

| 框架相对文件 | 原因与修改 | 本批验证 |
| --- | --- | --- |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/Protocol/MapProtocol.cs | Occupant 是可选值；以原十六字节槽的全零编码未占用，读回 default。其他 GUID 维持严格必填合同 | 修复前复现异常；修复后协议与发布链路 28/28 |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/Tests/Protocol/ProtocolTests.cs | 新增 Snapshot／Delta 的空／非空占用者往返及必填 GUID 严格性共五项 | 5/5 |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/Tests/Protocol/NetworkTests.cs | 新增 ARDMap 扣血后的发布、受损基线晚加入／重连及清格检查 | 1/1 |

专项回归共六项，修复前 3/6 通过，修复后均通过；已有相关用例同时通过。此为 .NET 协议检查，不代表 Unity 实际左键、独立进程联机、Player、弱网或完整业务化候选验收。游戏协议 19、地图 schema 2、存档 v14 和包路径不变；当前候选此前不可编码空占用者，本次修正没有改变记录宽度或放宽必填身份。证据见 [本轮摘要](archive/evidence/mining-guid-20261001.json)。

## 2026-09-30 网格业务化隔离源码候选（未验证）

用户授权实施但无需验证。基线 fee18645c997ed7529c4592917de6c412033c84e；修改落点为游戏仓库忽略目录 `.deps/YYGC-grid-business` 的 detached worktree。没有切换、合并、提交或覆盖用户 D:/Developer/YYGC master；其 Runtime/Debugging/RuntimeDebugHub.cs 本地修改仍保留于原处，候选依赖只使用锁定基线，不自动携带该未提交修改。

可复现来源为 `tools/grid-business/yygc.patch` 与 `dependency.lock.json`。本日初始候选补丁 SHA-256 为 **9ef9b7e6f4c5c0486aad909c867f2c924345107fd9ae4da721e25f1ee7285f63**，当前摘要以本页 2026-10-01 记录与锁文件为准。准备入口 `tools/grid-business/prepare-dependency.ps1` 只建立隔离检出和应用补丁；不编译、不启动 Unity、不清理源仓库，对不匹配的已有本地修改拒绝覆盖。准备脚本本身未运行验收。

| 框架相对文件 | 修改原因 | 验证 |
| --- | --- | --- |
| Runtime/Objects/NetworkStates/SessionStateChange.cs | 对象候选全部 Validate 后才调用唯一外部安装回调，再 Install／Notify；不引用 AnyRuleD 或游戏 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules/Runtime/Core/Contracts/IGridCellIdentitySource.cs | 无框架依赖的目标内容实例版本只读能力，耐久变化不使合作目标过期 | NOT_RUN |
| 同上 .cs.meta | 现有 Editor 自动导入生成，非人工指定 GUID；随补丁保留 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules/Runtime/Core/Business/GridBusinessStateStore.cs | 允许经过校验的网络副本构造冻结 GridBusinessSample；不增加权威写入口 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules/Runtime/Core/Business/GridBusinessCatalog.cs | 暴露业务状态边界校验供独立协议接收层使用 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules/Runtime/Core/Grid/ARDMap.cs | 同批清格后同类型重放也发布 Logic 变化，内容重置不会被当成无操作 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/Protocol/MapProtocol.cs | schema 2 格记录传输可选业务偏差和内容版本，校验 Unknown／Empty 的规范编码 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/Protocol/MapInterestService.cs | 权限过滤后读取业务字段，HP-only 变化也发布 delta，不空闲扫全图 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/Protocol/ChunkReplicaStateMachine.cs | 每次字典交换同时安装格子、业务字段和内容版本，按冻结目录校验 HP | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/Protocol/ProtocolLimits.cs | schema 2；有界 8 MiB 窗口容纳全图受损基线 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking/package.json | 独立协议候选包升级 0.3.0-preview.1 | NOT_RUN |
| AnyRuleD~/Packages/com.tsgame.anyrules.networking.fishnet/package.json | 传输适配包同升候选版本并引用新协议包，未改变认证或 transport | NOT_RUN |

游戏配置、奖励、矿袋、设备与矿床行为没有迁入基础框架。基础 Core／Compiler／Networking 仍不引用 YYGC；未直接复用完整检查点与 Outbox 适配器作为逐击采矿路径。游戏 manifest 和 packages-lock 改为同一相对路径候选检出，可能触发已有 Local Editor 重导入；没有创建第二套 Unity 缓存。

没有测试、故障注入、工作台保存重开、Player、跨进程联机或性能证据。源码实现不代表原子性、完整恢复、生成绑定或编译已经通过。回退时保留用户存档、原目录和候选源码；按基线恢复对应源码与依赖配置，不通过删除 Library、整目录清理或重生成证据来冒充回退。

## 2026-09-29：RuntimeDebugHub 仅保留 F1

用户明确要求暂时移除反引号唤出。实际框架 `D:/Developer/YYGC` 开始时为干净的 `master`／`fee18645c997ed7529c4592917de6c412033c84e`；本批不切分支、不提交框架、不改依赖配置，仅保留下列定向源码修改。游戏变更与复现方式见[调试页说明](archive/RUNTIME_DEBUG_HUB.md)。

| YYGC 修改文件 | 原因与落点 | 验证 |
|---|---|---|
| `Runtime/Debugging/RuntimeDebugHub.cs` | 新旧输入系统均只读取 F1；标题移除反引号提示。 | 仅源码差异检查；按用户要求未运行测试、编译或 Play 验证。 |

框架原文件 SHA-256：`191F4FD40D09BF81BB333007DD7FC486077D265EC67AADBB5625E60831330242`；修改后落盘 SHA-256：`AE23D39554C5AF3E4F68027F0E3E9C053EC6BD1EA39A18264D263E1116F0DBB6`。精确补丁保存在 `tools/runtime-debug-hub/F1Only.patch`，受保护的重放脚本为 `Apply-F1Only.ps1`；不覆盖其他框架修改。未新增第二套 Unity 工作区。

## 2026-09-29：飞船商店交互修复

YYGC 框架修改文件：**无**。实际依赖仍为 `D:/Developer/YYGC` 的 `fee18645c997ed7529c4592917de6c412033c84e`，本轮读取时工作区干净。缺口位于游戏创建 UI Toolkit 面板时遗漏主题和字体配置，沿用已有 AssetProvider、UIManager／UIPanel 与 Interaction Session 即可修复，无须修改框架。游戏内修改与 Editor／Mono 验证见[验收记录](archive/SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md)。

## 2026-09-29：飞船交易与装备候选

本批 **YYGC 框架仓库修改文件：无**。飞船服务使用现有 ObjectDefinition／`IConfigData` 与 `DefinitionReference`；UI 使用现有 `UIManager`／`UIPanel` 及绑定扩展；权威命令、对象状态和联机投影只修改游戏仓库。游戏候选协议 17／存档 v12，当前 Editor 快速测试 10/10 通过；框架包修改、独立进程与新机器依赖恢复均未在本批执行。

## 2026-09-27：框架分支合入 master

用户授权将游戏实际使用的 YYGC 改动合入 `master`。`D:/Developer/YYGC` 活动检出中暂存的八个文件与 `fix-20260926-bootstrap-startup`（`094e722`、`19d8f5b`）逐字节一致，以合并提交 **`0a9eec7`** 记录，工作区文件无变化；`CHANGELOG.md` 补记后 `master` 快进到 **`fee1864`**，原 `4939af2` 之后的地图联网拆包、局部地形刷新、地形复核、生成注册范围及启动修复共 24 个提交全部合入。活动检出已切到 `master`（同一提交）。AnyRules 暂无旧数据，拆包不做数据迁移。未推送远端。

游戏 `manifest.json` 仍为 `file:D:/Developer/YYGC`；当前构建对应的框架源码即 `fee1864`，但本机包路径不等于全新机器可重现的依赖恢复。

## 2026-09-26：航程验收恢复池化单例注册

实际 Editor 回归发现 `YYInteractionSessionService` 第二轮初始化后 `Instance` 为空。当前 `D:/Developer/YYGC` 缺少游戏仓库已有的 `RestoreSingletonOnPooledReentry.patch`：首次初始化标记保留，离场却清空单例，导致重入跳过注册。

先在已空闲且干净的 `D:/Developer/YYGC-worktrees/bootstrap-startup` 核验并应用现有补丁，隔离提交 **`19d8f5b`**；确认目标文件与隔离基线 SHA-256 相同后，只向活动包应用该文件。保留七文件启动修复和其他既有改动，不切换用户框架分支。

| YYGC 修改文件 | 原因与落点 | 当前验证 |
|---|---|---|
| `Runtime/Objects/Singletons/SingletonBehaviours.cs` | 两类单例在每轮 `InitializeCore` 注册实例，首次初始化钩子仍只执行一次。 | Local Unity 编译通过；实际 `InteractionSingletonRegistersAgainAfterPooledDespawn` 连续三轮重入通过。 |

补丁仍由 `tools/lan-framework-patch/RestoreSingletonOnPooledReentry.patch` 和既有准备脚本管理；不另复制一份实现。前后源码哈希及工作区状态见 `artifacts/space-planet-flow/singleton-source-proof.json`，本批测试见 `editor-walkway-and-framework.json`。Player 和后续联机结果以航程验收报告为准。

## 2026-09-26：Bootstrap 蓝屏启动修复

用户报告 `UGUI Runtime` 缺少 Canvas、`Network State Types` 缺少独立 Sample 的 `CampState`，授权修复并通知航程验收聊天。当前包直接引用 `D:/Developer/YYGC`，基线 `0b1fad6` 已按生成目标程序集隔离注册，但运行时仍扫描所有已加载程序集；该包也缺少旧隔离包曾有的 Unity 空值补丁。

修复先在 `D:/Developer/YYGC-worktrees/bootstrap-startup` 的 `fix-20260926-bootstrap-startup` 完成，提交 **`094e722`**，再以精确补丁应用到当前包的七个文件。未切换用户 YYGC 分支，未覆盖已有 `ARDRenderController.cs` 修改，未更换 manifest、lock、渲染或平台设置。游戏保存 `tools/bootstrap-startup/YYGCStartup.patch`、前后源码 SHA-256 与补丁锁；`Apply-YYGCStartup.ps1` 仅接受完整基线或已应用状态，不覆盖未知或部分修改。当前仍是项目既有的本机包引用；补丁可重放不等于已经验证全新机器的完整依赖恢复。

| YYGC 修改文件 | 原因与落点 | 验证 |
|---|---|---|
| `Runtime/UI/UGUI/UGUIRuntimeStartupModule.cs` | Canvas、CanvasScaler、UGUIManager 使用 Unity `== null` 判断，避免 `??` 漏掉缺失组件托管壳。 | 修复前临时根节点复现原异常；修复后补组件、重复初始化、销毁后重建通过。 |
| `Editor/GeneratedRegistryScope.cs` | 提取与类型筛选同源的稳定程序集名称列表。 | 正式范围包含游戏 Runtime，排除独立 LAN Sample。 |
| `Editor/Objects/NetworkStates/StateDataInterfaceGenerator.cs` | 正式状态生成器把作者引用范围写入注册器，不手改生成输出。 | 原有 Tag 10–18 保持，航程状态 Tag 19；Sample 不写入正式表。 |
| `Editor/NetworkCommands/NetworkCommandInterfaceGenerator.cs` | 命令生成器同步写入范围，避免状态通过后命令校验继续阻断启动。 | 正式命令启动校验通过，原命令 Tag 保持。 |
| `Runtime/Middlewares/GenericTypeSerializer/GeneratedGenericTypeRegistryCatalog.cs` | 注册器同时保存范围并随 SubsystemRegistration 清空；无范围或旧生成器保持严格校验。 | 缺少范围时不跳过校验，测试结束恢复全局注册状态。 |
| `Runtime/Objects/NetworkStates/StateDataTypeStartupModule.cs` | 只要求当前生成注册器范围内的状态，仍拒绝正式类型漏注册。 | 故意移除 ActorState 仍失败；独立 CampState 不阻止正式启动。 |
| `Runtime/NetworkCommands/NetworkCommandStartupModule.cs` | 命令启动使用相同范围合同。 | 实际 Bootstrap 全部启动模块进入 Ready。 |

Unity **6000.4.9f1** 当前 Local 统一编译完成；`tools/bootstrap-startup/StartupRegression.cs` **7/7**。实际 Bootstrap Play 主菜单显示，点击“开始守夜”后 Host 建立、太空飞船／人物／目的地入口显示，两阶段错误日志均 **0**。已退出 Play，留在 Bootstrap。正式证据为 [摘要](archive/evidence/bootstrap-startup-20260926.json) 与 `artifacts/bootstrap-startup-fix/` 下诊断 JSON、`bootstrap-menu.png`、`newgame.png`；这些为保留证据。未构建本轮 Player，未宣称完整航程、独立进程联机或 IL2CPP 通过。

本批只生成上述源码、补丁与正式证据，复用 Local Library，无独立 Unity 缓存或可移出的中间构建产物；未删除或移动其他任务文件。阶段完成 D 盘约 25.5 GB 可用，C 盘约 6.7 GB 可用。

## 2026-09-25：生成更新分派器的友元程序集访问

原因：Unity 当前 `com.tsgame.gamecore` 包路径指向用户 YYGC 工作区 `D:/Developer/YYGC`，该检出缺少游戏已有的 `SampleAssemblyAccess.cs` 宿主补丁。BehaviourRegistry 为 `DarkNights.Samples.LanCoop.Runtime`、`DarkNights.Runtime` 和 `DarkNights.View` 生成的分派器需要访问 `CoreBehaviour` 的 internal 更新标志与索引；没有友元声明时触发 CS1061。先从 YYGC `01289e0f92ebcab3671cd03ab8e11751e627602f` 建隔离分支 `fix-20260925-lan-dispatcher-access` 核对补丁落点与原工作区状态，再将现有两文件补丁原样补入活动包，并由当前 Unity Editor 编译验证；未改生成器、包路径、manifest／lock 或 AnyRules 文件，也未提交 YYGC 工作区。

| YYGC 修改文件 | 原因与落点 | 验证 |
|---|---|---|
| `Runtime/NetworkCommands/SampleAssemblyAccess.cs` | 加入既有宿主补丁中的 `InternalsVisibleTo` 声明，授权 LAN Sample、正式 Runtime、View 访问更新分派所需内部状态。 | 当前 Unity 6000.4.9f1 刷新编译后，三个程序集生成代码的 CS1061 均消失。 |
| `Runtime/NetworkCommands/SampleAssemblyAccess.cs.meta` | 沿用游戏仓库补丁的 GUID，保持资源身份一致。 | 与 `tools/lan-framework-patch/SampleAssemblyAccess.cs.meta` 字节一致。 |

编译边界：本次 Unity 编译发现 `TerrainModifierInstaller.cs(62,50)` 缺少 `DarkNights.Core.Logic.Terrain` 命名空间导入，已补上；本轮尚未取得补丁后的 Unity 编译结果，也没有运行 PlayMode 或 Player 测试。活动 `D:/Developer/YYGC` 文件目前是未提交本地补丁；锁定 `.deps/YYGC-unified` 的 `prepare-lan-sample.ps1` 路径与本次活动包路径不同，切换依赖时应继续使用对应的锁定准备流程。

## 2026-09-25：局部地形源输入与即时刷新候选

隔离 AnyRuleD 检出从 `e07e9a9e3e36cdbae1e0d39ec07aea555e95fbad` 建立 `ft-20260925-immediate-terrain-refresh`；完整改动以提交 `6b85403630c07d886811e5f18494c15c056eb2ef` 推送到同名远端分支。按用户要求，`D:/Developer/YYGC` 主检出已从 `master` 切换到该分支；推送后确认工作树干净，再由 Git 安全移除隔离工作树 `D:/Developer/YYGC-worktrees/immediate-terrain-refresh`。游戏同名候选分支将读取新增的源输入 API，但 `Packages/manifest.json`、`packages-lock.json` 及 `source-lock-map-state.json` 目前仍锁定 `e07e9a9`，因此未宣称游戏 Unity 编译通过。

| YYGC / AnyRuleD 修改文件 | 原因与落点 | 已验证 / 边界 |
|---|---|---|
| `com.tsgame.anyrules/Runtime/Core/Grid/MapInputBatch.cs`、`Grid/ARDMap.Input.cs` | 冻结格变化、完整快照和生命周期输入；对显式只读源地图执行整批验证、原位安装、输入代次及提交游标校验。 |
| `com.tsgame.anyrules/Runtime/Core/Contracts/WorldDescriptor.cs`、`Grid/GridChangeSet.cs`、`Invalidation/GridDependency.cs` | 增加源提交变化原因与依赖记录，使精确格/范围失效可沿既有地图通知链传递。 |
| `com.tsgame.anyrules/Runtime/Core/AssemblyInfo.cs` | 允许独立 Core 测试程序集覆盖内部原子安装 API。 |
| `com.tsgame.anyrules/Runtime/Unity/Facade/MapOptions.cs`、`Facade/ARDMapController.cs`、`Scheduling/ARDRenderController.cs`、`Collision/GridCollisionProjection.cs`、`Debug/GridDebugController.cs` | 接入只读源地图创建、主线程安装和渲染/碰撞范围失效；调试摘要记录来源变化类型。 |
| `com.tsgame.anyrules.networking/Protocol/ChunkReplicaStateMachine.cs` | 每个已完成网络提交包含 session、stream generation、snapshot chunks 和 cell coordinates 的冻结通知；不改 AMP1 V1 wire。 |
| `TestHosts/CoreTests/AnyRules.Core.Tests.csproj`、`MapInputBatchTests.cs`、`SourceInputInstallTests.cs` | 覆盖冻结值、快照、增量、生命周期、权限、非法批次拒绝及同一源提交内的快照加增量原子安装。 |

验证：`dotnet test AnyRuleD~/TestHosts/CoreTests/AnyRules.Core.Tests.csproj --nologo --no-restore` 为 138/138 通过。Unity 包导入/编译、Editor 场景及游戏 Player 未运行；完整文件差异与待验条件见[局部地形即时刷新执行记录](archive/IMMEDIATE_TERRAIN_REFRESH_EXECUTION.md)。

## 2026-09-24：AnyRuleD 地图联网拆包与稀疏增量

隔离检出 `D:/Developer/YYGC-worktrees/map-state-networking` 从 YYGC `4939af2` 建立 `ref-20260924-map-state-networking`；用户维护的 `D:/Developer/YYGC` 主检出未切换或覆盖。游戏从 `ft-20260922-terrain-modifiers` 的 `e204c1d` 同名新分支接入。YYGC 当前包代码锁定 `e07e9a9e3e36cdbae1e0d39ec07aea555e95fbad`，由 `tools/map-framework-patch/source-lock-map-state.json` 校验四包 462 个文件。旧 `aa450a7` 加补丁的准备脚本保留为 `tools/prepare-map-packages-legacy.ps1`。

| YYGC / AnyRuleD 修改文件或目录 | 原因与落点 | 已验证 / 边界 |
|---|---|---|
| `com.tsgame.anyrules/Runtime/Core/Contracts/WorldDescriptor.cs`、`Runtime/Core/Grid/ARDMap.cs` | 新增 `IGridChangeSource`，权威提交时发布已有变化集；仍由地图对象唯一写入。 | .NET 130/130；Game Editor 编译通过。 |
| 旧 `com.tsgame.anyrules.yygc.fishnet/Protocol/*` → `com.tsgame.anyrules.networking/Protocol/*` | 协议、发布器与只读副本移出 YYGC/FishNet；`MapInterestService` 只为变化格编码 Delta，保留 AMP1 V1 帧、完整 Snapshot、连接权限及版本边界；`ChunkReplicaStateMachine` 一次解码并原子通知范围。原 `.meta` 随移动保留。`MapInterestService` 增加按实际授权投影计算 canonical SHA。 | 单格 1 record、负坐标、跨块原子、新旧 V1 向量、背压、不同权限投影 .NET 用例通过；真实新旧 Player 双向各 15/15。 |
| 旧 `com.tsgame.anyrules.yygc.fishnet/Runtime/FishNetMapTransport.cs` → `com.tsgame.anyrules.networking.fishnet/Runtime/` | 可靠传输、连接清理与实时重试；仅依赖 FishNet 和地图协议，编辑意图不经此状态流。原 `.meta` 保留；安全停止后 `Pump` 空操作。 | Game Editor 编译通过；无 YYGC Mono Host/16 客户端及 Dedicated/4 客户端通过，真实 UDP 三档通过。 |
| 旧 `com.tsgame.anyrules.yygc.fishnet/Runtime/TerrainEditCommand.cs` → `com.tsgame.anyrules.yygc/Runtime/` | 命令保留 YYGC 认证、路由、权限及业务去重路径；原 `.meta` 保留。 | Game 的 Runtime/Entry/Tests 引用更新后编译通过；最终锁 Mono 地图编辑循环正常及真实弱网各 15/15。 |
| 两个新包的 `package.json`、`README.md`、asmdef 与 `.meta` | UPM 将基础、纯协议、FishNet 与 YYGC 分离；FishNet 最低依赖兼容 YYGC 宿主 4.6.12，游戏实际仍为 4.7.2。新资源 `.meta` 由 Unity 导入生成。 | `verify-package-layout.py`：15 个程序集、433 个唯一 GUID；四包锁 462/462。 |
| `MapStateDiagnostics.cs`、`MapStateDebuggerWindow.cs`、`MapStateFeatureWindow.cs`、`MapStateDebugBridge.cs` 及 FishNet 诊断接线 | YY 菜单、有界 2048 事件、只读业务贡献者、安全停止、按需本机和每 peer 的 canonical 对比；开发版显式令牌回环桥，正式 Player 不注册端点；窗口增加远程读取、peer 比较和故障档脚本入口。新桥脚本 `.meta` 为 Unity 生成并原样提交。 | Matched/Different/Incomplete 及不同权限投影 .NET 测试通过；Game Editor 编译、菜单开窗；最终锁 Host/Client 的桥摘要匹配和错误令牌拒绝 2/2。窗口按钮人工交互及不同权限双 Player 未验。 |
| `Samples~/StandaloneNetwork/*`、`Tools~/MapStateTests/*`、`tools/prepare-standalone-map-host.py` | 无 YYGC 独立 FishNet 样板、协议/进程测试入口、真实 UDP 弱网档位；输出隔离 runId。 | 宿主 5/5 文件且不装 YYGC；协议 runner 有 TRX/JSON，Mono Host 0/1/2/4/8/16、Dedicated 1/4，TypicalWeak/Severe/Blackout 四客户端通过；证据在 YYGC `AnyRuleD~/Evidence/MapState/map-state-20260924-summary.json`。 |

YYGC 分段提交：`13cd0b9` 红测，`85275e3` 稀疏 Delta，`0684b9d` 变化集与副本，`1ec2b55` 拆包，`d4687c9` 诊断，`f8ebc76` 样板初稿，`b0e2387` 独立多进程和弱网收口，`b239df6` 按需 canonical 对比，`1501025` 迁移类型的显式线缆身份别名，`cb2ec5f`/`00b7c14`/`e07e9a9` 远程诊断和窗口。Game 的主 YYGC 依赖仍锁旧提交 `12b253c`，故 `tools/lan-framework-patch/MapTypeWireIdentityAlias.patch` 在隔离 `.deps/YYGC-unified` 复现同一别名 API；`tools/prepare-map-type-identity.ps1` 校验来源与修改前后哈希。初次旧 Player 混连的类型表拒绝记录保留，别名仅用于已搬移且内容不变的 `TerrainEditCommand`，其余类型仍严格握手；最终锁的正反向混连各 15/15。`b239df6` Player 的正常／弱网三进程各 28/28、地图编辑各 15/15 保留旧构建身份；最终锁重新构建的地图循环含远程桥 17/17。迁移说明在 YYGC `AnyRuleD~/Documentation~/MAP_STATE_NETWORKING.md`；游戏接入与待验边界见[地图联网重构](archive/MAP_STATE_NETWORKING.md)。回退时切回旧锁与旧三包 manifest、恢复旧游戏接线，不删除存档或原素材。尚未运行 IL2CPP、双机器或前台性能验证。

## 2026-09-23：编辑器顶部菜单归属

仅调整 Editor 菜单注册，不修改运行时框架身份、`com.tsgame.gamecore` 包名或 `GameCore.*` 程序集名。正式游戏的菜单声明集中在 `Game/Assets/DarkNights/Scripts/Editor/DarkNightsMenu.cs`，环境初始化／校验／Addressables 从 `YY/Dark Nights` 归入 `Dark Nights`；调用仍转发原工具。LAN 示例因独立程序集仍在自己的 `SampleBuilder.cs` 注册。框架菜单声明集中在 YYGC `Editor/YYMenu.cs`，保留窗口、注册器的原方法与自动初始化；`Assets/Create` 和 `GameObject` 上下文菜单不挪动。导入的输入示例与框架源示例均改为 `YY/Samples`，不再创建 `GameCore` 顶级菜单。

| YYGC 隔离包修改文件 | 原因／落点 |
|---|---|
| `Editor/YYMenu.cs`、`Editor/YYMenu.cs.meta` | 集中注册对象套件、定义、命令、状态和工具窗口的顶部菜单；`YY/Objects` 只保留一次打开套件／工坊。 |
| `Editor/AppStartup/AppStartupSettingsWindow.cs`、`Editor/ObjectDefinitionViewer/ObjectDefinitionViewer.cs`、`Editor/Objects/ObjectManagerSuite.cs`、`Editor/Objects/Singletons/ObjectSingletonEditorWindow.cs` | 移除分散的对象套件菜单特性，保留窗口方法。 |
| `Editor/Objects/Definition/DefinitionLookupWindow.cs`、`DefinitionMigrationWindow.cs`、`ObjectArchetypeManagerWindow.cs`、`ObjectDefinitionWorkshopWindow.cs` | 定义与原型入口改由 `YYMenu` 注册，原窗口逻辑不变。 |
| `Editor/NetworkCommands/NetworkCommandInterfaceGenerator.cs`、`NetworkCommandDependencyValidator.cs`、`Editor/Objects/NetworkStates/StateDataInterfaceGenerator.cs`、`StateDataRegistryUpdater.cs` | 菜单特性迁移；原自动发现、生成和构建前校验仍直接调用原方法。 |
| `Runtime/YYPlugins/YYToolkits/Databases/SODatabaseUpdater.cs`、`Runtime/YYPlugins/YYToolkits/MapGenerator/Editor/WorldGeneratorEditorWindow.cs` | 两个 YYGC 工具入口改由 `YYMenu` 注册。 |
| `Samples~/InputActions/Editor/InputActionsSampleBuilder.cs` | 包内待导入示例与游戏中已导入副本保持相同的 `YY/Samples` 路径。 |

游戏仓库以 `tools/lan-framework-patch/YYEditorMenus.patch` 重放上述 YYGC 源码差异，`tools/prepare-lan-sample.ps1` 对已有隔离包及从 `0c7cec0` 重放的薄目录都进行了差异哈希和反向补丁校验。AnyRuleD 不属于 GameCore：其 `Editor/Workbench/AnyRuleDWorkbench.cs` 和 `Editor/Import/TileImportWizard.cs` 从 `YY/AnyRuleD` 移到 `Tools/AnyRules`，由 `tools/map-framework-patch/EditorMenus.patch` 与 `source-lock.json` 的两个文件哈希记录；未改其他地形文件。

Unity 6000.4.9f1 本机 Editor 重新编译通过；菜单枚举确认 `GameCore/*`、`YY/Dark Nights/*`、`YY/AnyRuleD/*` 均已消失。YYGC 现有锁定目录和从旧提交重放的薄目录各通过重复准备检查。AnyRuleD 菜单补丁在隔离源上应用并匹配两项更新哈希；当前 AnyRules 缓存另有 `TerrainEditBusinessHandler.cs`、`TerrainEditCommand.cs` 两项既存哈希漂移，原有 `TerrainEditCommandPayload.patch` 在全新源上的应用也失败，因此没有宣称完整 442 文件重放通过，也没有覆盖这两项文件。未运行 Player 或联机检查。

本任务两个可重建的隔离验证目录删除被执行策略拒绝，已核对绝对路径和无链接后移到 `artifacts/临时待删除/20260923-editor-menus/`，同卷移动不计入释放空间。原 `artifacts/menu-package-repro` 为 28,987,065 字节，现为 `artifacts/临时待删除/20260923-editor-menus/menu-package-repro`；原 `artifacts/menu-anyrules-repro` 为 2,659,699 字节，现为同目录下 `menu-anyrules-repro`。两者仅含本任务的克隆／解包及补丁验证输入，不是正式成果；策略允许且确认无需复核时才可另行删除，不自动清理。

## 2026-09-22：独立岩层与外轮廓候选

本批 **YYGC／AnyRules 修改文件为 0**；继续锁定 YYGC `12b253c`，未触碰用户框架工作区、UPM 路径或锁文件。新材质调用纯 Core 算法，权威状态与命令仍使用 ObjectInstance／ObjectSession／TerrainMapAuthority。发现的地图边界刷新问题修正在游戏侧 `TerrainReplicaSource` 指纹和 `TerrainPreview` 边界裁剪，没有给框架加入临时回退路径。验证和已知边界见 [执行记录](archive/STATIC_CAVE_BACKGROUND_EXECUTION.md)。

## 2026-09-20：手持装备复用框架，隔离恢复本机依赖漂移

本批 **没有修改 YYGC／AnyRules 框架源文件，没有新增框架补丁**。YYGC 继续锁定 `12b253c`；AnyRules 继续基于 `aa450a7` 加原三份补丁。新装备使用现有 ObjectInstance、IConfigData、状态同步、输入命令链和 R3 生命周期；不新增业务 Router。

首轮编译发现 `.deps/AnyRules` 的 `TerrainEditCommand.cs` 与 `TerrainEditBusinessHandler.cs` 偏离已提交的源码哈希锁，导致 ExpectedRevision 缺失。原缓存及 `D:/Developer/YYGC` 用户仓库原样保留。在 `.deps/AnyRules-locked-aa450a7` 从原提交解包并应用既有补丁，442 个文件哈希全部匹配；然后 Unity 批处理编译退出 0。

| 本仓库修改文件 | 原因／落点 | 本批验证 |
|---|---|---|
| `tools/prepare-map-packages.ps1` | 使用新隔离目录及专用解包 zip，保留旧缓存；提交和补丁锁不变 | 442 文件哈希一致 |
| `Game/Packages/manifest.json` | 三个 AnyRules 包指向新锁定目录 | Unity 实际解析并编译 |
| `Game/Packages/packages-lock.json` | 同步本地包路径 | Unity 最终编译退出 0 |

游戏协议升为 10／存档 v5，与框架版本升级无关。[本批证据](archive/evidence/hero-handheld-2026-09-20.json)保留漂移哈希、失败原因和成功编译记录；未运行 Play、Player 或联机验收，不计入历史通过矩阵。

## 2026-09-17：正式随机地图有界区块预算

本批不修改 `D:/Developer/YYGC` master，YYGC 主包仍锁定 `12b253c`。AnyRules 仍基于 `aa450a7`，只在游戏隔离 `.deps/AnyRules` 应用三份受版本管理的补丁。原 64 块、每轴六块上限不足以发送 320×192 的正式地图（负坐标对齐后 70 块），具体缺口已由 Editor 回归复现。

| 文件／落点 | 原因与内容 | 验证 |
|---|---|---|
| `com.tsgame.anyrules.yygc.fishnet/Protocol/ProtocolLimits.cs` | 最大块数 64→128；其余单包与缓存边界保持 | 全图 61,440 格流回归与正式 Player 联机 |
| `com.tsgame.anyrules.yygc.fishnet/Protocol/MapInterestService.cs` | 每轴最大十块，分配前检查相交范围加 halo 后实际块数不得超过预算 | 全图订阅、静态门闩、晚加入与加载 |
| `tools/map-framework-patch/PlayableMapChunkBudget.patch` | 固化以上两个框架文件差异 | 从源提交全新解包、三份补丁应用通过 |
| `tools/map-framework-patch/source-lock.json` | 更新受影响两个文件哈希，其余保持 | 442 个包文件一致 |
| `tools/prepare-map-packages.ps1` | 纳入第三份补丁、换行规范化；完整哈希已匹配时幂等返回，避免补丁重叠上下文误判 | 准备脚本与干净复现通过 |

详见[正式随机灰松谷](archive/TERRAIN_HISTORY.md)及[本批证据](archive/evidence/random-pinewatch-2026-09-17.json)。未将独立生成器或旧游戏历史大矩阵计入新构建验收。

## 2026-09-17：Bootstrap 地形命令漏注册修复

AnyRuleD 的 `TerrainEditCommand.cs` 同时包含命令 record 和结果结构体，Unity `MonoScript.GetClass()` 实际返回 `TerrainEditResult`。旧生成器把任意非空类型直接当作脚本类型，导致发现菜单也跳过真实命令；Bootstrap 的必需 Network Commands 模块校验失败，中止后续场景与 UI 启动。

框架修复位于隔离工作区 `D:/Developer/YYGC-worktrees/network-command-script-resolution`，分支 `codex/network-command-script-resolution`，提交 **`12b253c6bdd262feb860ab905b9e56e940ec9c40`**，仅基于原输入提交 `0c7cec0`，没有合入用户 master 的其他改动。

| 修改文件／落点 | 原因与内容 | 验证 |
|---|---|---|
| YYGC `Editor/NetworkCommands/NetworkCommandInterfaceGenerator.cs` | 仅接受有效命令类型的 `GetClass()` 结果，否则从受支持命令中按脚本名查找唯一匹配；避免结果结构体遮蔽命令 | 实际包脚本解析回归通过 |
| 游戏 `tools/prepare-lan-sample.ps1` | 将可重现依赖锁到 `12b253c` | 隔离依赖完整补丁检查通过 |
| 游戏 `tools/lan-framework-patch/ExcludeSampleFromGlobalRegistry.patch` | 随新基线更新补丁 blob 身份，原 Sample 排除语义不变 | 准备脚本精确差异验证通过 |
| 游戏全局 `NetworkCommandInterfaceGenerateRegistry.asset` 和 `INetworkCommand.generated.cs` | 使用框架发现／生成菜单加入地形命令 Tag 3；原 Tag 0/1/2 不变，不手改生成源码 | 已安装命令完整性与稳定编号回归通过 |

本批不修改 AnyRuleD 包、场景、Prefab、人工资源、玩法协议 8 或存档 v3。新的注册表摘要会区分包含地形命令的构建，不将旧 Player 混作本批联机客户端。详细验收与产物见 [Bootstrap 修复证据](archive/evidence/bootstrap-registry-2026-09-17.json)。未验证 IL2CPP、双机器 LAN 或前台性能，M5 状态不变。

<a id="hero-input"></a>

## 2026-09-16：输入封装、主角接线与 Input Actions Sample

用户要求保持原命名、对比缺陷后合并实施，并指出 YYGC 另一个会话正在引入插件。本批只在 `D:/Developer/YYGC-worktrees/input-actions` 的 `codex/input-actions` 修改，输入基线 `745f3d2`，提交 **`0c7cec00b7a7f9cec0287bb56d0af9fc45c9d143`**。用户 `D:/Developer/YYGC` 的 master `56afcad` 及 AnyRule 未提交文件保持原状；不切换或合并那个工作区，也未推送。

下表逐项列出本提交的 **54 个文件**，路径相对隔离框架根目录。所有文件同时存在于游戏锁定的 `.deps/YYGC-unified`；44 个 Sample 文件另导入到游戏 `Assets/Samples/YYGCInputActions`，与隔离框架源逐文件核对。原有七份 tracked 补丁及两个友元文件在依赖更新前后字节一致，准备脚本精确校验通过。UPM manifest／lock 的本地路径保持不变，完整提交由 `tools/prepare-lan-sample.ps1` 锁定。

| 文件 | 修改原因与内容 | 验证 |
|---|---|---|
| `Documentation~/INPUT_ACTIONS.md` | 接入、旧新方案对比、API 与生命周期、限制和测量说明 | 按实际实现及证据核对 |
| `Documentation~/INPUT_ACTIONS_VALIDATION.json` | 归档 34 个不同用例的来源与校准后路由测量 | 输入 XML 按影响合并生成 |
| `Runtime/PlayerInputs/YYInputActionService.cs` | 新增动作到 Interaction Sessions 的薄接线、即时取消及同帧 Button 隔离 | 模式／模态／失效／重入取消与路由微测量 |
| `Runtime/PlayerInputs/YYInputActionService.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Runtime/PlayerInputs/YYInputRebindingHandle.cs` | 新增界面拥有的改键句柄，Dispose 取消，拒绝同动作重复拥有 | 取消／释放／重复启动／模式许可回归 |
| `Runtime/PlayerInputs/YYInputRebindingHandle.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Runtime/PlayerInputs/YYInputRebindingService.cs` | 保留原签名；修改前校验、异常恢复、托管入口及可选查重范围 | 原 API、组合绑定、异常、查重回归 |
| `Runtime/PlayerInputs/YYInputSettingsData.cs` | 保留 v1 字段与默认值，补职责注释 | 既有字段与保存恢复回归 |
| `Runtime/PlayerInputs/YYInputSettingsStore.cs` | 保留路径与 API；严格信封、候选验证、失败回滚、原子刷盘替换 | 坏 JSON／未知版本／真实文件锁／清除失败回归 |
| `Samples~/InputActions/Content.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/Demo.mat` | 示例矩形的独立材质 | 实际渲染截图 |
| `Samples~/InputActions/Content/Demo.mat.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/InputActions.inputactions` | 原生 Player／Camp／UI 动作资产 | 移动跳跃、模式、UGUI 与改键回归 |
| `Samples~/InputActions/Content/InputActions.inputactions.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/InputActions.unity` | 实际序列化场景，包含角色、PlayerInput、UGUI 和模态页 | 保存重开、实际场景 1/1 |
| `Samples~/InputActions/Content/InputActions.unity.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/Pixel.png` | 75 字节白色像素，独立示例图形来源 | 实际渲染截图 |
| `Samples~/InputActions/Content/Pixel.png.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Cancel.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Cancel.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Click.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Click.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Navigate.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Navigate.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Point.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Point.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_ScrollWheel.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_ScrollWheel.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Content/UI_Submit.asset` | 原生 InputActionReference，连接 UGUI 的对应动作 | UGUI 点击、模态和改键场景回归 |
| `Samples~/InputActions/Content/UI_Submit.asset.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Editor.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Editor/InputActionsSampleBuilder.cs` | 仅空目录初建场景与原生 UI 引用，普通导入不执行 | 初建、保存、重开与运行 |
| `Samples~/InputActions/Editor/InputActionsSampleBuilder.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Editor/YYGC.InputActions.Editor.asmdef` | 隔离 Sample 的运行、Editor 或测试程序集，不引用 Dark Nights | Unity 编译和相应场景／测试 |
| `Samples~/InputActions/Editor/YYGC.InputActions.Editor.asmdef.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/README.md` | 用户操作、导入、维护入口与真实验收范围 | 按已交付场景核对 |
| `Samples~/InputActions/README.md.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Runtime.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Runtime/InputActionsSample.cs` | 独立本地移动／跳跃／使用、模式／模态、改键与保存演示 | 实际场景 1/1、两张渲染截图 |
| `Samples~/InputActions/Runtime/InputActionsSample.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Runtime/YYGC.InputActions.asmdef` | 隔离 Sample 的运行、Editor 或测试程序集，不引用 Dark Nights | Unity 编译和相应场景／测试 |
| `Samples~/InputActions/Runtime/YYGC.InputActions.asmdef.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputRebindingTests.cs` | 旧 API、错误索引、托管取消、回调和查重边界 | 对应 PlayMode 用例通过 |
| `Samples~/InputActions/Tests/InputRebindingTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputRoutingTests.cs` | 模式、通道、同帧泄漏、重入与校准分配测量 | 对应 PlayMode 用例通过 |
| `Samples~/InputActions/Tests/InputRoutingTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputSampleSceneTests.cs` | 官方 InputTestFixture 驱动实际场景、鼠标 UI、焦点、改键重载 | 最终场景 1/1，通过截图复核 |
| `Samples~/InputActions/Tests/InputSampleSceneTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/InputSettingsTests.cs` | 真实文件和原生资产上的原子设置／坏输入边界 | 对应 PlayMode 用例通过 |
| `Samples~/InputActions/Tests/InputSettingsTests.cs.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `Samples~/InputActions/Tests/YYGC.InputActions.Tests.asmdef` | 隔离 Sample 的运行、Editor 或测试程序集，不引用 Dark Nights | Unity 编译和相应场景／测试 |
| `Samples~/InputActions/Tests/YYGC.InputActions.Tests.asmdef.meta` | Unity 自动生成的新资源／目录元数据，保留 GUID | 导入、引用与源／导入副本逐文件校验 |
| `package.json` | 注册 Input Actions Sample 导入入口 | UPM 导入及实际场景检查 |

Unity 6000.4.9f1／Input System 1.19.0 下 **34 个不同 PlayMode 用例按影响合并通过**（服务／文件 33 项、最终真实场景 1 项），并非一次全绿 34 项运行。末次场景复跑禁用音频；虚拟键鼠驱动原生 UGUI，两张真实截图已检查。路由内核 10,000 次 Refresh＋全部 CanRead：2 动作 2.4844 ms、32 动作 75.8918 ms；校准后 GC.Alloc 未检测到分配。该数字不覆盖 UI、网络或前台帧率。Sample 未单独构建 Player，游戏 Mono 不能替代其独立发布验收。

游戏侧切片与验收见[联合执行文档](archive/PRESENTATION_HISTORY.md)，输入详细结果见[机器证据](archive/evidence/hero-input-framework.json)。保留原 `StartInteractiveRebind` 原生返回类型；旧调用者提前结束仍须先 Cancel 再 Dispose。新增管理入口用于界面生命周期，不重写 Unity 的设备或按钮状态机。



<a id="unified-u6-performance"></a>

## 2026-09-13：U6 装配校验热点修正

框架提交 `745f3d2c844a66389e39bb84cd878d5d80f77962`，先在 `.deps/YYGC-unified` 验证；确认用户仓库干净、master 仍为 `8faf74f` 后本地 fetch 并快进 `D:\Developer\YYGC`，未推送。游戏准备脚本锁定新完整提交，六份既有补丁及友元文件保留。

| 文件 | 原因与实际修改 | 落点与验证 |
|---|---|---|
| `Runtime/Objects/Runner/ObjectAssemblyValidation.cs` | 每次客户端完整投影都重新反射 Behaviour 的配置及组件绑定声明，256 次校验微测量均值 101.64 ms。按类型缓存不变声明，不保存 Definition、配置／组件实例或成功结果；实际配置、工厂、能力和绑定仍逐次验证 | 隔离 checkout 与用户仓库同路径；最终 138/138 Editor／Play，56.68 秒，包括预热后删除配置、清空／重复绑定的拒绝回归。缓存声明后的两次微测量为 16.66／9.76 ms；不据此宣称 Player 帧率通过 |

类型解析缓存候选没有显示明确收益，已撤回，`BehaviourTypeResolver.cs` 无最终差异；该候选的 139 项回归不增加当前通过数。本次无新增 YYGC 文件或 `.meta`。游戏 `a7bb926`／本框架提交的正式 Mono 完整矩阵 347 项通过，Sample 基础／弱网各 30 项通过。后续容量积压修正只落在游戏投影编码及验收工具中：`4e3798f`／同框架通过 144 项 Editor／Play、正式 Mono 350 项与另一次 240 秒容量 21 项。Sample 代码及共用框架未变，沿用其已有 60 项证据。收尾再次核对用户仓库干净且 HEAD 仍为 `745f3d2`，未推送；本次没有追加 YYGC 或 FishNet 修改。前台性能由用户暂缓，详情见[性能切片](archive/MIGRATION_HISTORY.md)。

<a id="unified-u5"></a>

## 2026-09-13：U5 已完成资源的异步等待修正

框架提交为 `8faf74f03d9eac4e025d6fe0f81f0c26a9eac9d4`。先在 `.deps/YYGC-unified` 验证；确认用户仓库干净、仍在 `master` 且 HEAD 为 `0305eb7` 后，本地 fetch 并快进 `D:\Developer\YYGC` 至同一提交，没有推送。游戏准备脚本锁定完整提交；UPM manifest／lock 的隔离路径不变，六文件 Sample／UI／单例补丁及友元文件通过精确校验并保留。

| 文件 | 具体缺口与修正 | 落点与验证 |
|---|---|---|
| `Runtime/Utils/FastInstantiator.cs` | 后台 Editor 中，Addressables 句柄已完成，但 `handle.Task` 仍等待 ResourceManager 的延迟完成回调，导致会话预加载停滞。AcquireComponentAsync 改为现有 UniTask.Addressables 的 `handle.ToUniTask`，已完成句柄直接返回；取消时不由适配器自动释放，继续由原租约异常路径唯一释放，组件访问回主线程 | 隔离与用户仓库同一路径；26/26 会话测试 3.07 秒、整批 134/134 Editor／Play 54.02 秒，含真实 Worker 工厂、取消、两种域重载和三夜。U6 已从无旧 Library 的源码目录构建 Mono，同产物 347 项自动检查通过 |

本次没有新增 YYGC 文件、程序集引用或 `.meta`。Sample 未调用本次修改的 AcquireComponentAsync／PrepareAsync 路径，其既有 API 未改；不重复构建未受影响的 Sample。先前尝试仅更改 Task 续接上下文仍会阻塞，失败与取消记录保留，不作为修正通过证据。

U6 未新增框架修改；2026-09-13 收尾再次核对用户 YYGC 工作区干净且 HEAD 为上述完整提交。最终 Mono 使用游戏 `9e69a76`，通过活跃恢复、四人重开、九组弱网、三夜及容量功能；报告见 [U6 证据](archive/evidence/yygc-unified-u6.json)。容量性能尚未签署，IL2CPP／双机器未验收，不将功能结果扩展为框架全平台或性能保证。

<a id="unified-u2"></a>

## 2026-09-13：U2 网络会话装配与跨对象提交

框架提交为 `0305eb74bbc2677a3d9025f684d8ded16481be4a`，在 `.deps/YYGC-unified` 验证。用户仓库 `D:\Developer\YYGC` 在同步前及 fetch 后均检查为干净、HEAD 为 `ddce2ff`，随后仅执行本地快进；当前具有相同提交，没有推送。下表每个文件均落在隔离和用户仓库的同一路径。

| 文件 | 原因与修改 | 实际验证 |
|---|---|---|
| `Runtime/Objects/NetworkStates/SessionStateChange.cs` | 新增复制／验证／安装／通知／释放的同步批量状态提交；异常通知继续处理其余状态 | 真实支付、同一通知读取多个最终 State、异常订阅和撤权重入用例通过 |
| `Runtime/Objects/NetworkStates/SessionStateChange.cs.meta` | Unity 自动生成的新脚本元数据 | Editor 导入、Mono 两配置通过 |
| `Runtime/Objects/NetworkStates/StatefulBehaviour.cs` | 状态引用与通知时点分开；权威和只读副本均可准备批量候选，保留池的所有权 | 原状态／池回归、新事务及副本失败重试、Mono 装配与 Sample 通过 |
| `Runtime/Objects/NetworkStates/StateSynchronizer.cs` | Spawn 前绑定显式可信会话，可延迟激活；停止后释放上下文引用 | 真实 Play、正式 Host＋两客户端、原 Sample 四进程通过 |
| `Runtime/Objects/NetworkStates/StateDataTypeStartupModule.cs` | 运行校验仅收集生成器支持的 StateDataAttribute 类型，修复本地测试状态误阻断启动 | 先复现 Bootstrap 失败，修正后两种域重载重复 Play 通过；带标记状态仍严格检查 |
| `Runtime/Objects/Runner/ObjectInstanceFactory.cs` | 显式会话参数贯通网络／本地初始化器；await 后验证生命周期 | 正式网络会话首次创建及复用原 Sample 通过 |
| `Runtime/Objects/Runner/ObjectDefinitionLoader.cs` | 公开持久初始化器所持有的原实例用于准备阶段预检 | 精确场景原实例接管、重开及保存恢复通过 |
| `Runtime/Objects/Runner/ObjectInstance.cs` | 批量通知期间拒绝装配、激活、退休及释放 | 同步通知退休兄弟对象被拒绝，所有 State 和支付仍完整提交 |
| `Runtime/Objects/Runner/ObjectSessionContext.cs` | 批量通知期间拒绝激活／退休上下文 | 撤权重入用例、退出和换 epoch 通过 |
| `Documentation~/OBJECT_SESSION_LIFECYCLE.md` | 记录批量提交、网络上下文、瞬时池引用和注册范围 | 与实际 API 及验收边界核对 |

U2 分批覆盖 126 个不同 Editor／Play 用例，无未解决失败；Mono 装配 14/14，正式三进程切片 26/26，独立四进程 Sample 30/30。只构建正式 Mono 和 Sample Mono 各一次，复验复用产物。游戏准备脚本锁定完整提交；manifest／packages-lock 保持同一隔离路径。既有六文件 Sample／UI／单例补丁完整保留，启动排除 patch 仅更新新基线的上下文和 blob 哈希。详见 [U2 证据](archive/evidence/yygc-unified-u2.json)。完整玩法、最终弱网／性能、IL2CPP 及双机器 LAN 不属于本阶段通过范围。

## 2026-09-13：U1 显式会话状态与对象装配

已提交 `ddce2ffdf422c8c9cb8e872fb5f20053cdedcda6`。先在 `.deps/YYGC-unified`／`codex/dark-nights-unified-objects` 验证；再次确认 `D:\Developer\YYGC` 工作区干净且仍在 `ccd61e0` 后，将用户仓库快进到该提交。没有推送。原 `.deps/YYGC` 的修改和缓存保留；游戏依赖改用 `.deps/YYGC-unified`，准备脚本锁定完整提交并精确校验既有四份补丁及友元文件。

下面 20 个文件均落在该框架提交，用户仓库具有相同路径。新 `.meta` 由 Unity 生成，没有重新分配已有资产 GUID。

| 文件 | 修改原因与结果 | 验证 |
|---|---|---|
| `Runtime/Objects/Behaviours/BehaviourContext.cs` | 携带显式会话，不靠跨 await 的 SessionScope 找依赖 | 换会话注入、真实工厂通过 |
| `Runtime/Objects/NetworkStates/IStatefulBehaviour.cs` | 增加 Session 同步模式，区分状态权限和发送粒度 | 本地权威／只读副本通过 |
| `Runtime/Objects/NetworkStates/StateSynchronizer.cs` | 会话托管的个体 State 不占网络索引、不逐个发包 | 既有状态回归、Sample 四进程通过 |
| `Runtime/Objects/NetworkStates/StatefulBehaviour.cs` | 动态状态权限、深复制入口、失效 scope、异常回滚和退休中的回调清理 | Editor 状态用例及真实 Play 通过 |
| `Runtime/Objects/Runner/IObjectSessionInitializer.cs` | 显式会话装配合同，返回实际 ObjectInstance | Loader 原实例与工厂通过 |
| `Runtime/Objects/Runner/IObjectSessionInitializer.cs.meta` | 新接口的 Unity 元数据 | 导入及 Mono 构建通过 |
| `Runtime/Objects/Runner/LocalObjectInstanceInitializer.cs` | 接入显式会话与延迟激活 | 场景、动态实例通过 |
| `Runtime/Objects/Runner/ObjectAssemblyValidation.cs` | Editor／Player 共用能力、配置、生成工厂和绑定检查 | 真实 Mono 负例全部被拒绝 |
| `Runtime/Objects/Runner/ObjectAssemblyValidation.cs.meta` | 新校验器元数据 | 导入及 Mono 构建通过 |
| `Runtime/Objects/Runner/ObjectDefinitionLoader.cs` | BindSession 接管同一个场景实例 | Prefab 连接、位置和实例引用保持 |
| `Runtime/Objects/Runner/ObjectInstance.cs` | 准备／激活／退休／释放；换会话注入；拒绝清理失败的 Behaviour 复用 | 重入、换定义、失败清理、两种重载 Play 通过 |
| `Runtime/Objects/Runner/ObjectInstanceFactory.cs` | 预加载返回同步装配租约 | Addressables、冷加载取消、Mono 通过 |
| `Runtime/Objects/Runner/ObjectSessionContext.cs` | 可信动态权限、主线程会话及取消／退休 | 撤权、未激活、退出加载用例通过 |
| `Runtime/Objects/Runner/ObjectSessionContext.cs.meta` | 新上下文元数据 | 导入及 Mono 构建通过 |
| `Runtime/Objects/Runner/PreparedObjectDefinition.cs` | 预加载后同步创建，错误回收未激活对象，拒绝加载后修改 Prefab 引用 | 真实 Worker Prefab 通过 |
| `Runtime/Objects/Runner/PreparedObjectDefinition.cs.meta` | 新工厂租约元数据 | 导入及 Mono 构建通过 |
| `Runtime/Utils/ComponentAssetLease.cs` | 每次加载拥有独立 Addressables 引用 | 冷加载取消、释放后创建拒绝通过 |
| `Runtime/Utils/ComponentAssetLease.cs.meta` | 新资源租约元数据 | 导入及 Mono 构建通过 |
| `Runtime/Utils/FastInstantiator.cs` | AcquireComponentAsync 的取消／失败释放，不清理其他租约 | Editor 与独立 Mono 通过 |
| `Documentation~/OBJECT_SESSION_LIFECYCLE.md` | 记录 API 时序、权限、租约和状态快照边界 | 与实际实现核对 |

宿主额外修改 `tools/lan-framework-patch/SampleAssemblyAccess.cs`，为真实 `DarkNights.Tests` 生成调度器增加友元访问。该文件继续作为游戏的锁定补丁，不混入通用框架提交。旧六文件补丁没有被清理或重复计为本轮框架修改。

分批验证覆盖原 95 项及新增 16 项 Editor／Play 用例，失败的测试驱动已修复并复测；独立 Mono 装配 14/14，Sample 四进程基础 30/30。详情见[U1 证据](archive/evidence/yygc-unified-u1.json)与[实施记录](archive/MIGRATION_HISTORY.md)。没有进行 IL2CPP 或双机器 LAN；U2–U6 尚未完成。

## 2026-09-13：统一对象架构的升级适配授权（仅规划）

用户明确允许在 YYGC 存在能力限制或 BUG 时升级适配；正式游戏后续采用一套 YYGC 对象／状态模型，不要求旧数据兼容。具体前置能力、阶段门槛和交付要求见 [YYGC 统一重构计划](archive/MIGRATION_HISTORY.md)。先在隔离 checkout 核实和验证，保留用户已有改动，游戏仍锁定可重现依赖；该授权不要求每项必要修正重复确认。

本次只读核对：用户 YYGC 仓库与隔离依赖均位于 `ccd61e01f15332b1197cfa5ee72af8777c4a0b49`，用户仓库状态为空；`.deps/YYGC` 的既有补丁差异保留。**本次 YYGC 修改文件数为 0，未创建框架提交、未升级依赖、未进行新 Unity／Player 验证。** 新计划中的状态权限、显式会话装配、同步创建和严格校验是待实施项，不能计入下方已实施账本。后续每阶段须逐文件补充原因、隔离／用户仓库落点、提交和验证结果。

<a id="scene-definitions"></a>

## 2026-09-13：Definition 场景入口

实际缺口：Loader 缺少统一公开定义引用，拖拽工具只写旧整数 ID、单例判重也使用旧 ID；现有分类不能表达单位和自然资源点。先修改隔离 `.deps/YYGC`，Unity `6000.4.9f1` 编译完成；确认用户 YYGC 工作区干净且 HEAD 为 `516f76c` 后，快进到 `ccd61e01f15332b1197cfa5ee72af8777c4a0b49`。没有推送远端。游戏准备脚本锁定此完整提交，manifest／packages-lock 的隔离路径不变；既有六文件运行补丁完整保留，准备脚本精确校验通过。

以下是本次 YYGC 的全部修改，路径相对于 `D:\Developer\YYGC`，隔离依赖具有相同提交。**仅编译完成，测试回归按用户要求待确认。**

| 文件 | 原因与实际修改 | 验证状态 |
|---|---|---|
| `Runtime/Objects/Runner/ObjectDefinitionLoader.cs` | 公开 DefinitionReference／ResolveDefinition；EditorConfigure 统一写 GUID 与初始化器，检测 PrefabRef 一致性；激活时拒绝未解析定义 | 编译完成；运行生命周期待回归 |
| `Editor/Objects/Runner/ObjectDefinitionDragHandler.cs` | 拖拽改用 GUID 配置入口，单例按解析后的 Definition 判重；公开 SceneObjectCreated 编辑器扩展事件 | 编译完成；实际拖拽和单例冲突待回归 |
| `Runtime/Objects/Types/ObjectType.cs` | 追加 Unit、Scenery_ResourceNode、World_Session、World_Connection，保留所有旧枚举值 | 编译完成；17 个游戏定义已配置 |
| `Runtime/Objects/Types/TypeCategory.cs` | 末尾追加 Unit 分类并补充职责注释，不重排旧值 | 编译完成；序列化回归待确认 |

本批没有新增 YYGC 文件或重建 `.meta`。游戏侧 16 个场景放置引用迁移和静态差异核对见 [实施记录](archive/SCENE_DEFINITIONS.md)。

2026-09-12 用户授权：必要时可以更新 YYGC，并将这项约定加入项目知识；完成后必须一一列出 YYGC 改动。此授权允许为实际接入缺口修正框架，保留用户已有修改、隔离验证和锁定依赖的要求仍有效。`AGENTS.md` 已同步该约定。

| 本次变更 | 具体证据与原因 | 修改落点 | 当前验证 |
|---|---|---|---|
| 池化重入时重新登记本地／网络单例 Instance | 关闭 Domain Reload 再进入 Play，`CampInput.Initialize` 因 Interaction Sessions 服务为空而使正式会话模块失败；单例离场清空 Instance，但池化重入跳过首次 Initialize | 新增 `RestoreSingletonOnPooledReentry.patch`，将单例引用登记移至每次执行的 InitializeCore，首次 OnSingletonInitialize 仍只调用一次；同时覆盖两种单例基类，并接入准备脚本 | Editor 55/55；常规重载三次会话 9/9，关闭 Domain Reload 的连续两次 Play 各三次会话 10/10；协议 5 Mono 九组四进程恢复均 22/22 |
| 修正 UGUI 根组件的 Unity 空引用判断 | 空场景启动实际抛出 `MissingComponentException: Canvas`；`GetComponent<T>() ?? AddComponent<T>()` 未识别 Unity 的空组件包装对象 | 新增可复现补丁 `tools/lan-framework-patch/FixUguiRootUnityNull.patch`，修改隔离依赖 `UGUIRuntimeStartupModule` 中 Manager、Canvas、CanvasScaler 的三个判断；准备脚本验证并应用补丁，用户 YYGC 仓库未改动 | 修复后 Editor 实际创建五个正式面板和一个菜单 Interaction Session；字体及后续生命周期另行验证 |
| 为 `DarkNights.View` 增加 `InternalsVisibleTo` | UGUI Behaviour 的 YYGC 生成更新分派器读取 `CoreBehaviour._updateFlags`，Unity 编译报 CS1061；既有 Runtime／Sample 已有相同授权 | 本仓库 `tools/lan-framework-patch/SampleAssemblyAccess.cs`，经核对旧文件等于已提交基线后更新 `.deps/YYGC/Runtime/NetworkCommands/SampleAssemblyAccess.cs`；用户维护的 YYGC 仓库尚未改动 | 依赖准备脚本通过；Unity 编译通过；原生 UGUI 首版资源创建完成，运行与生命周期验收继续执行 |

后两项已随原生 UI 批次 Mono 实际构建、启动 6/6、独立 Host＋客户端 13/13 和 Editor 鼠标 13/13 验证，见[实际证据](archive/evidence/native-ui-2026-09-12.json)。用户维护的 `D:\Developer\YYGC` 未改动，修正位于可重现补丁与隔离依赖。

<a id="workshop-display"></a>

## 2026-09-12：Workshop 定义展示

原因：工坊平铺和树形均以 `[{Id}]` 开头、仅按旧 ID 排序并搜索，正式 GuidFirst 定义的零值无法区分对象，也不能按 Key 查找。用户确认原有资产文件名必须保留，因此采用名称／Key／文件名三行；名称 13 号加粗、Key 11 号偏蓝灰、文件名 10 号较淡，行高统一 60。长 Key 和文件名按宽度从中间省略，完整值保留在提示／复制和搜索中；有效旧 ID 只作为辅助标记。

落点：先在 `.deps/YYGC` 验证，提交 `516f76c4fe062fa82384f7b91ac46c453abbe80d`，再确认用户 YYGC 工作区干净且 HEAD 仍为 `10b8f0e`，将 `D:\Developer\YYGC` 的 `master` 快进至同一提交。没有推送远端。游戏通过 `tools/prepare-lan-sample.ps1` 锁定该完整提交；manifest／packages-lock 的隔离包路径未改变，既有运行补丁保留并通过精确校验。

以下为该 YYGC 提交的全部 10 个文件，路径相对于 `D:\Developer\YYGC`；隔离依赖包含相同提交。

| 文件 | 原因与修改 | 验证 |
|---|---|---|
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionDisplay.cs` | 新增只读名称、身份及原文件名映射；按 Key／GUID／别名搜索，按名称及稳定身份排序 | 缺名称、零旧 ID、长 Key、GUID 与旧号搜索；实际窗口名称顺序通过 |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionDisplay.cs.meta` | Unity 导入生成的新脚本元数据 | 原样提交并同步；没有手工指定 GUID |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionLabels.cs` | 新增共用三行视图、有效旧 ID 标记、完整提示与右键复制；复用时重置文件名和身份 | 两种栏宽／两种视图、完整 Key 复制、行复用通过 |
| `Editor/Objects/Definition/Workshop/WorkshopDefinitionLabels.cs.meta` | Unity 导入生成的新脚本元数据 | 原样提交并同步；没有手工指定 GUID |
| `Editor/Objects/Definition/Workshop/WorkshopListPresenter.cs` | 平铺和树形共用三行视图及 60 像素行高；改名时整体隐藏／恢复三行 | 220／400 像素栏宽布局与搜索、F2／取消、文件名保留通过 |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.uss` | 名称、Key、文件名的字号、配色、间距与省略；定义浅色主题对应颜色 | 当前深色 Editor 中实际布局无重叠和越界；浅色主题视觉复核未执行 |
| `Editor/Objects/Definition/Workshop/WorkshopAssetService.cs` | 去除按旧整数排序，使用共用名称比较器 | 实际 29 个定义名称顺序通过 |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.cs` | 接入完整身份／文件名搜索，保存、项目变化和撤销后刷新列表并保持目标选择 | 实际窗口按大写完整 Key 搜索通过；关闭重开后 29 行均含文件名 |
| `Editor/Objects/Definition/Workshop/WorkshopInspectorPresenter.cs` | 基本属性显示可选中复制、自动换行的 Key／GUID；零旧 ID 不显示 | 实际 Worker 属性区绘制及窗口重开通过，Console 无错误 |
| `Documentation~/DEFINITION_IDENTITY.md` | 记录三行展示、长文本、旧号、搜索与身份制作边界 | 与本次实现和用户保留文件名的要求核对 |

验证：Unity `6000.4.9f1` 导入／编译通过，UI 检查 **47/47**；实际窗口搜索、名称排序与重开检查通过；2,457 个游戏资源、meta、Packages 和 ProjectSettings 输入哈希不变。当前依赖准备与全新隔离 clone 准备均通过。首次导入生成两份新 meta；用户提出三行要求后，仅对新增输入涉及的三份源码／样式统一再导入一次。未新增 Player、PlayMode 或联机验证，不改变 M5 状态。详见[本批证据](archive/evidence/workshop-display-2026-09-12.json)。

已有的 Sample 注册排除、启动验证排除及 Sample／Runtime 友元声明是此前已提交补丁，本次没有改变这些行为。每次后续修复在本表新增独立行；最终交付逐项列出真实改动及各自通过／待验证状态，不把用户原有修改算作本次成果。
## 2026-09-16／17 地图包接入（独立切片）

地图生成与可破坏地形合同见 [地图切片](TERRAIN_GENERATION.md)。主 YYGC 仍锁定 0c7cec0；新增三个独立包从 aa450a7168d5800b0899e216ed484892e44fb40a 提取到 .deps/AnyRules，不修改或切换用户 D:/Developer/YYGC master 工作区。

| 修改文件 | 原因与落点 | 验证 |
|---|---|---|
| AnyRuleD~/Packages/com.tsgame.anyrules.yygc.fishnet/Protocol/MapInterestService.cs | 现有 Publish 每次扫描兴趣格；追加可选内容／权限版本提供者及扫描计数，两版本不变时直接返回。Subscribe/Revoke 清除缓存，默认不传参数保留原轮询语义。游戏用 tools/map-framework-patch/IdleMapPublication.patch 锁定，准备脚本重复运行只校验／应用一次。 | 新地图 Editor 回归已通过静止 1,000 次不扫描、实际修改、晚加入与权限撤回；双进程结果见地图完成记录。 |
| AnyRuleD~/Packages/com.tsgame.anyrules.yygc.fishnet/Runtime/FishNetMapTransport.cs | 弱网实际暴露每帧计数的 120 次 Pump 超时过早触发重同步；改为实时时钟 10 Hz 推进，12 秒窗口、最多三次。复用断线清理清单以去除逐帧列表分配。落点为 tools/map-framework-patch/RealtimeMapRetry.patch。 | 同一 Mono 真实 200 ms RTT／5% 丢包／25 ms 抖动，破坏、去重、重连及静态发布检查通过；不视为双机器或前台性能验收。 |

两份补丁只存在于宿主隔离解包目录，未写入用户框架仓库。准备脚本规范化这两个修改文件的换行、校验全部 442 项源文件；全新归档解包、应用补丁和哈希对比已通过。三个包的源码身份均来自 aa450a7，现有 YYGC 0c7cec0 的对象／输入补丁不变。

未修改 AnyRuleD 核心地图、规则编译器或渲染后端。游戏直接使用 ARDMap 局部事务，以 ObjectSessionContext 控制写权限；不把 TerrainEditBusinessHandler 的整图检查点事务当作高频采矿实现。当前补丁是宿主锁定适配，不宣称已合并 YYGC 主分支或发布新版框架。

## 2026-09-19：地图遗漏修复的隔离 AnyRules 宿主补丁

本批没有修改 `D:\Developer\YYGC` 用户仓库，也没有创建或推送 YYGC 提交。为满足地图方案 v1.1 的权威幂等合同，补丁只落在本仓库 `tools/map-framework-patch` 和解包后的 `.deps/AnyRules`，由 `tools/prepare-map-packages.ps1` 应用并锁定。

| 文件 | 原因与落点 | 验证 |
|---|---|---|
| `tools/map-framework-patch/TerrainEditCommandPayload.patch` | 移除客户端 `ExpectedRevision` 字段及其重置／序列化输入；旧兼容处理改为读取宿主 `CommitId`，并为 TerrainEditBusinessHandler 暴露提交代次，使游戏服务端只按当前权威地图提交 | 隔离 Unity 编译前源码检查；`prepare-map-packages.ps1` 通过，442 项源文件校验 |
| `.deps/AnyRules/.../TerrainEditCommand.cs` | 应用上述 payload 合同，客户端不再提交地图 revision | 与 patch 内容和 source-lock SHA-256 一致 |
| `.deps/AnyRules/.../TerrainEditBusinessHandler.cs` | 提供宿主读取的 `CommitId`，保留既有 Tag 3 注册与处理入口 | 与 patch 内容和 source-lock SHA-256 一致 |
| `tools/map-framework-patch/source-lock.json` | 更新两个隔离文件的 SHA-256，保持解包可重现 | `prepare-map-packages.ps1` 442/442 通过 |
| `tools/prepare-map-packages.ps1` | 把补丁纳入一次性解包／校验流程，避免依赖本机用户框架工作区 | 既有用户工作区未写入；远端 clone 可用性仍是 P2 待核查 |

这不是 YYGC master 的上游合并，也不代表远端框架已发布新版本；游戏仍通过隔离包和锁文件使用该适配。游戏侧地形路由、ActorState 炸药库存、矿床手采对象和正式资产改动均记录在当前分支，不计入 YYGC 文件变更；钻机链已从本轮产品代码与验收中删除。

`prepare-lan-sample.ps1` 的远端恢复路径已补齐：新环境从基线 `0c7cec00b7a7f9cec0287bb56d0af9fc45c9d143` 克隆，再应用 `tools/lan-framework-patch/NetworkCommandInterfaceGenerator.patch` 及既有 LAN 补丁，得到与 `12b253c6bdd262feb860ab905b9e56e940ec9c40` 等价的源码。使用临时空 checkout 的本地克隆模拟已通过并回收；当前环境的 GitHub `ls-remote` 未在限时内返回，所以公网可达性仍待新机器确认。未修改 `D:\Developer\YYGC` master，也未推送新提交。

该 NetworkCommand 补丁另外锁定 SHA-256 `2D86D92A3EBE1B4DD37650C206B00D969F93850C3648D56293E4DC01927AB17E`；准备脚本在应用前拒绝带有未预期 tracked 修改的基线 checkout，避免覆盖用户已有改动。
