# YYGC 修改授权与改动账本

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

YYGC 分段提交：`13cd0b9` 红测，`85275e3` 稀疏 Delta，`0684b9d` 变化集与副本，`1ec2b55` 拆包，`d4687c9` 诊断，`f8ebc76` 样板初稿，`b0e2387` 独立多进程和弱网收口，`b239df6` 按需 canonical 对比，`1501025` 迁移类型的显式线缆身份别名，`cb2ec5f`/`00b7c14`/`e07e9a9` 远程诊断和窗口。Game 的主 YYGC 依赖仍锁旧提交 `12b253c`，故 `tools/lan-framework-patch/MapTypeWireIdentityAlias.patch` 在隔离 `.deps/YYGC-unified` 复现同一别名 API；`tools/prepare-map-type-identity.ps1` 校验来源与修改前后哈希。初次旧 Player 混连的类型表拒绝记录保留，别名仅用于已搬移且内容不变的 `TerrainEditCommand`，其余类型仍严格握手；最终锁的正反向混连各 15/15。`b239df6` Player 的正常／弱网三进程各 28/28、地图编辑各 15/15 保留旧构建身份；最终锁重新构建的地图循环含远程桥 17/17。迁移说明在 YYGC `AnyRuleD~/Documentation~/MAP_STATE_NETWORKING.md`；游戏接入与待验边界见[地图联网重构](MAP_STATE_NETWORKING.md)。回退时切回旧锁与旧三包 manifest、恢复旧游戏接线，不删除存档或原素材。尚未运行 IL2CPP、双机器或前台性能验证。

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

本批 **YYGC／AnyRules 修改文件为 0**；继续锁定 YYGC `12b253c`，未触碰用户框架工作区、UPM 路径或锁文件。新材质调用纯 Core 算法，权威状态与命令仍使用 ObjectInstance／ObjectSession／TerrainMapAuthority。发现的地图边界刷新问题修正在游戏侧 `TerrainReplicaSource` 指纹和 `TerrainPreview` 边界裁剪，没有给框架加入临时回退路径。验证和已知边界见 [执行记录](STATIC_CAVE_BACKGROUND_EXECUTION.md)。

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

详见[正式随机灰松谷](archive/RANDOM_PINEWATCH.md)及[本批证据](archive/evidence/random-pinewatch-2026-09-17.json)。未将独立生成器或旧游戏历史大矩阵计入新构建验收。

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

游戏侧切片与验收见[联合执行文档](archive/HERO_INPUT_EXECUTION.md)，输入详细结果见[机器证据](archive/evidence/hero-input-framework.json)。保留原 `StartInteractiveRebind` 原生返回类型；旧调用者提前结束仍须先 Cancel 再 Dispose。新增管理入口用于界面生命周期，不重写 Unity 的设备或按钮状态机。



<a id="unified-u6-performance"></a>

## 2026-09-13：U6 装配校验热点修正

框架提交 `745f3d2c844a66389e39bb84cd878d5d80f77962`，先在 `.deps/YYGC-unified` 验证；确认用户仓库干净、master 仍为 `8faf74f` 后本地 fetch 并快进 `D:\Developer\YYGC`，未推送。游戏准备脚本锁定新完整提交，六份既有补丁及友元文件保留。

| 文件 | 原因与实际修改 | 落点与验证 |
|---|---|---|
| `Runtime/Objects/Runner/ObjectAssemblyValidation.cs` | 每次客户端完整投影都重新反射 Behaviour 的配置及组件绑定声明，256 次校验微测量均值 101.64 ms。按类型缓存不变声明，不保存 Definition、配置／组件实例或成功结果；实际配置、工厂、能力和绑定仍逐次验证 | 隔离 checkout 与用户仓库同路径；最终 138/138 Editor／Play，56.68 秒，包括预热后删除配置、清空／重复绑定的拒绝回归。缓存声明后的两次微测量为 16.66／9.76 ms；不据此宣称 Player 帧率通过 |

类型解析缓存候选没有显示明确收益，已撤回，`BehaviourTypeResolver.cs` 无最终差异；该候选的 139 项回归不增加当前通过数。本次无新增 YYGC 文件或 `.meta`。游戏 `a7bb926`／本框架提交的正式 Mono 完整矩阵 347 项通过，Sample 基础／弱网各 30 项通过。后续容量积压修正只落在游戏投影编码及验收工具中：`4e3798f`／同框架通过 144 项 Editor／Play、正式 Mono 350 项与另一次 240 秒容量 21 项。Sample 代码及共用框架未变，沿用其已有 60 项证据。收尾再次核对用户仓库干净且 HEAD 仍为 `745f3d2`，未推送；本次没有追加 YYGC 或 FishNet 修改。前台性能由用户暂缓，详情见[性能切片](archive/YYGC_UNIFIED_PERFORMANCE.md)。

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

分批验证覆盖原 95 项及新增 16 项 Editor／Play 用例，失败的测试驱动已修复并复测；独立 Mono 装配 14/14，Sample 四进程基础 30/30。详情见[U1 证据](archive/evidence/yygc-unified-u1.json)与[实施记录](archive/YYGC_UNIFIED_IMPLEMENTATION.md)。没有进行 IL2CPP 或双机器 LAN；U2–U6 尚未完成。

## 2026-09-13：统一对象架构的升级适配授权（仅规划）

用户明确允许在 YYGC 存在能力限制或 BUG 时升级适配；正式游戏后续采用一套 YYGC 对象／状态模型，不要求旧数据兼容。具体前置能力、阶段门槛和交付要求见 [YYGC 统一重构计划](archive/YYGC_UNIFIED_REFACTOR_PLAN.md)。先在隔离 checkout 核实和验证，保留用户已有改动，游戏仍锁定可重现依赖；该授权不要求每项必要修正重复确认。

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
