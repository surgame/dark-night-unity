# Dark Nights Unity 开发执行计划

2026-10-03 [矿镐吸附与挥砍统一距离](PICKAXE_UNIFIED_REACH.md)：矿镐 Definition 的 Reach 从48扩大至64（4格），移除本地额外一格预览；选取与 YYGC 权威落镐共用冻结配置。协议22／存档v15不变，本批验证结果与边界见链接及机器证据。

2026-10-02 [快速测试与场景整理验收](QUICK_TEST_SCENES.md)：复用正式 Bootstrap／Expedition 提供已着陆矿镐局，本机选择记忆、测试槽位隔离与重开基线已完成；6 场景保留原字节／GUID 迁移，16 场景分类。Editor 规则 21/21、主菜单 Play 1/1、快捷键／实际 GUI 点击 1/1 按影响合并通过；重启记忆及画面已检查。补齐取消生成代次及候选清理。协议 22／存档 v15 不变，未构建新 Player，未验多人快速局。证据适用于当前 Local 联合工作区，不替其他斜坡／天空候选宣称交付。

## 2026-10-02 Definition 与 Editor 复评后的首批改进

完整评估与最终 Review 见[复评清单](DEFINITION_EDITOR_REVIEW.md)：44 个 Definition 的能力归属、8 组运行侧缺口与顺序均已记录。先完成 E1–E6：采集装配／白名单严格校验，EditorWindow 序列化纯导航状态，搜索保留当前源与编辑树，初始化与 Play／Undo 状态刷新，事件和地形序列化句柄释放，错误矿床选择转提示。保存以最新已应用序列化值校验后写盘；临时对象／已关闭编辑器拒绝保存。Editor 专项最终 11/11、真实窗口生命周期与作者资产保持 52/52；初轮失败及修复保留报告。无运行数值、身份、协议 22／存档 v15 或 YYGC 变更，未构建 Player。运行能力 R1–R8 以及原生 GenericMenu 跨上下文守卫仍未实施，不宣称整体迁移完成；本批[机器证据](evidence/definition-editor-review-20261002.json)。

## 2026-10-02 编辑工作台聚合

常用入口集中到 `Dark Nights / 工作台`，左侧分类与搜索，右侧专用工具打开按钮或原生资产 Inspector。原配置菜单、Terrain 下的当前配置／预览／正式场景快捷菜单收进目录；初始化、安装、构建、验证及旧实验入口保留。导航不持有业务配置、不自动应用草稿；YYGC 框架、资源 GUID、协议 22／存档 v15 不变。完成[聚合评估](EDITOR_WORKBENCH_ASSESSMENT.md)后补齐 UI Toolkit 可拖动双栏、分类筛选、内嵌采集编辑／匹配和原生 Definition 浏览器；独立采集窗口复用同一面板。Local Editor 编译及最终交互探针 190/190、原生工坊重绘检查 5/5 通过，覆盖十一个页面、全部资产、筛选／搜索／选择回调、原生工坊 SharedConfigs／BehaviourTypes、目标匹配、四个专用窗口打开、幂等释放与作者资产字节保持。未构建 Player，不计作玩法或画面验收。详见[操作说明](EDITOR_WORKBENCH.md)和[机器证据](evidence/editor-workbench-20261002.json)。

## 2026-10-02 工具 Definition 采集能力

已完成自身 Definition 配置、YYGC 采集 Behaviour 装配、canonical 装备身份、目标匹配、冻结目录／指纹、动作身份与选择版本、保存恢复及原生 Definition 编辑入口。正式矿镐默认只采前景，数值保持。协议 **22**／存档 **v15**／AMP1 schema **2**，YYGC 框架无新增修改。经用户授权退出当前 Play，单一 Local Editor 完成导入和资源升级。Editor 按影响合并 62/62；单一 Mono 构建成功，正常双进程 27/27。Core 1046/1048、ArchitectureGuard 20 项既有错误均保留。弱网立即重连超时，整体未通过；未验边界及清理账见[本批说明](TOOL_DEFINITION_HARVESTING.md)和机器摘要。先前矿床开关方案已被本轮替代。

2026-10-01 地表环境候选：`ft-20261001-surface-environment` 实施完整视口天空、真实坡形天际线遮罩、局部列刷新、两层远山／稀疏云层、风化边缘与按局部地表深度的洞口明暗。游戏协议 20／AMP1 schema 2／存档 v14 不变，权威地图生成与碰撞保持。Local 导入／C# 9 编译通过，简单 Editor 专项按影响合并 **24/24**（最终相机实绘复测 7/7）；未运行真实主场景 Play、Player、独立进程联机或性能验收。配置、失败记录及人工清单见 [地表环境记录](SURFACE_ENVIRONMENT.md)，不将本批视为完整主场景／多人／性能验收。

2026-10-01 提交状态：用户授权提交当前代码，网格业务化、空 GUID 同步修复及矿镐最近表面／持续挥舞候选一并提交到 `fix-20261001-pickaxe-surface-swing`，不推送远端。下文“未提交／不提交”描述各阶段当时状态，以本条提交状态为准。本次仅核对 Git 差异、新增脚本 .meta 与依赖补丁摘要，没有新增测试、Unity 编译、Play 或构建验证；既有专项结果和功能待验边界保持原记录。

## 2026-10-01 矿镐最近表面与持续挥舞源码候选

用户确认此前空 GUID 修复后可采集，授权扩大范围、直线优先近墙、按住持续挥舞并在落镐结算，明确无需测试。本批工作分支 `fix-20261001-pickaxe-surface-swing`，保留开始时已有的整批业务化未提交候选，不覆盖或提交其他会话修改，不推送远端。

- WorldSession 的 HandheldConfig 新增矿镐独立参数：PickaxeReach=48（3 格）、PickaxeHandHeight=36（主角四倍显示后的手部高度）、PickaxeImpactFraction=0.6。原 0.48 秒周期、伤害 10、手枪／炸弹 HandHeight 和共享 WorkReach 不改。
- Core RayCell 对有限射线与格矩形、真实坡形半平面做精确裁剪；Runtime 在有界射线区域找最早交点，不使用粗步进跳过细小坡形。基岩、不可采材质、Unknown 和地图外区域均参与阻挡，不搜索它们背后的可采格。
- 本地高亮及权威落镐共用查询。鼠标是方向，最近表面是实际目标；空格矿床只有射线可达且位于前景阻挡之前才可选。提示额外预览一格超距表面，不增加权威作用距离。持续状态提示不按每次空挥发送错误消息。
- 持镐按下／按住均启动动作，采集失败也空挥；每轮开始冻结输入目标与角度，在 60% 进度消耗一次命中门闩，落镐时按当前权威位置验证原目标、内容版本、最近表面及最终容量。外层被其他人挖掉时当前轮空挥，下一轮才重新选后方；不能一镐连续穿透多格。
- 输入继续更新供下一轮使用，短按缓存同时冻结目标和角度；展示在当前轮使用冻结角度，将抬落镐曲线叠加到瞄准方向。普通松键完成当前轮，换装、菜单、登船、死亡、暂停、权限撤销和输入超时取消待命中轮次。
- 新临时字段只归 ActorState，MemoryPackIgnore 保证不进入线缆／存档，YYGC 生成 CopyFrom 仍按值复制；没有第二份状态字典或手改生成代码。保存时不记录待命中目标，恢复保留既有动作冷却但不补伤害。

游戏协议升至 **20** 以隔离行为合同；地图 AMP1 schema **2**／存档结构 **v14** 不变。新增参数参与原有装备指纹，旧配置的 v14 存档可能因指纹不符被拒绝，保留原档，不做自动迁移。依赖锁只同步 game_protocol，YYGC 源码和框架补丁本批无新增改动；之前协议 19 的 GUID 28/28 专项测试不能作为本批证据。

按用户要求未执行测试、lint、编译、Unity 刷新、Play、Player 构建或联机；只查看源码与差异。本批没有新增脚本资产，不需要人工生成 .meta。没有主动生成验证日志、截图、存档、编译或构建中间产物，无待清理产物归档。Unity 如自动导入，不计作本批编译通过。

待验：真实鼠标各方向及坡形／角点、手部与镐头画面对齐、三格边界、近墙优先与基岩阻挡、空挥／短按／长按／松键、单轮一次伤害及多人移除目标、移动后重新校验、菜单／暂停／超时取消、容量最后一击、存档动作恢复与配置指纹、独立 Host＋客户端及弱网。本批未改旧测试夹具，其中一 Tick 即期待伤害／无效目标不播放动作的旧断言尚需按新落镐语义调整；不宣称旧矩阵适配完毕。

## 2026-10-01 采矿后空占用者 GUID 同步修复

用户报告点击左键采矿即抛出 `An unassigned GUID is not a valid identity`。定位到隔离依赖的 `MapProtocol.WriteCell`：受损格存在业务偏差，但 `GridBusinessState.Occupant=default` 合法表示未占用；schema 2 错把该可选字段交给必填身份编解码，发布失败后 `SessionNetwork.Fail` 断开会话。

只修正占用者的编解码：维持原有十六字节宽度，全零表示未占用，非零仍使用 canonical GUID。世界、材质等必填身份继续拒绝全零，`StableGuid` 本体与玩法数值不变；游戏协议 19／地图 schema 2／存档 v14 不变。修复落在当前 manifest 引用的 `.deps/YYGC-grid-business`，同步更新 `tools/grid-business/yygc.patch` 和摘要锁，没有修改用户 YYGC master。

新增六项专项回归。修复前 **3 失败／3 通过**，未占用的 Snapshot、Delta 以及真实 ARDMap 扣血后 `MapInterestService.Publish` 均复现原堆栈；修复后协议与发布链路 **28/28 通过**，包含受损格增量、晚加入、重连、清格和必填身份拒绝空值。这里的晚加入／重连是同进程协议状态机检查，不是独立 Player 联机。证据见 [本轮摘要](evidence/mining-guid-20261001.json)，原始 TRX／日志保存在 `artifacts/mining-guid-20261001/`。

没有启动、刷新或控制正在运行的 Unity，没有执行实际左键、Unity 编译、Player 构建、跨进程或弱网验证。已有失败会话需在源码重新编译后重新开局。保持现有未提交业务化候选和其他修改，不提交或推送本批混合工作区；完整候选仍按下方待验项验收。专项 .NET 编译中间产物统一保留到 `artifacts/待清理/20261001-mining-guid/`，清单记录路径、体积及空间，不计作释放磁盘。

## 2026-10-01 开始守夜地图目录修复

修复点击“开始守夜”时 `ARDMap` 抛出 `Use the map's immutable gameplay catalog` 的源码接线问题：`SessionTerrainNetwork` 冻结采集规则后复用 `rules.Business.Gameplay`，不再重复加载同一作者资产。`SessionTerrain` 的启动与恢复候选统一持有冻结规则目录，`TerrainMapAuthority` 创建地图时直接使用业务目录所属的 `Tiles`。洞穴工作台同样只冻结一次并共享目录实例。保持 AnyRuleD 的实例一致性检查，不修改框架依赖、配置或协议。按用户要求未运行编译、测试、Play、构建或联机验证，运行结果待确认；修复保留在已有未提交候选中。

## 2026-10-01 编译错误修复

`TerrainGameplayBehaviour` 的池重入钩子改为当前 YYGC `PooledBehaviour.OnSpawn`，退池时清空规则引用；`MiningInputTests` 从 WorldSession Definition 的共享配置读取矿镐伤害，避免访问 Runtime 内部属性。复用 Local `6000.4.9f1` Editor 完成导入／编译，最终 `isCompiling=false`、`isUpdating=false`、`scriptCompilationFailed=false`，Runtime 与 Tests 程序集已更新。仅确认本次编译通过，未运行测试、Play、Player 构建或联机验证，不替代下方源码候选的功能验收。修复保留在已有未提交候选中。

## 2026-09-30 网格业务化源码候选

实施基线为已合并 main 的 `9084698`。本轮按用户明确要求实施但不验证：没有主动运行测试、lint、构建、Play、联机或工作台应用操作；Editor 因源码和包路径变化自行导入不作为验收。本批源码保持未提交，不自动再合并或推送。

### 状态归属与实现

- WorldSession ObjectDefinition 新增 `TerrainProfileConfig` 与 `TerrainGameplayBehaviour`。Profile 直接保存在现有 Definition 的共享配置中，没有为形式统一再创建一个独立 Profile 资产或地图 ObjectInstance。
- `SessionTerrain` 管理唯一地图生命周期，`TerrainMapAuthority` 内的 ARDMap／GridBusinessStateStore 唯一拥有格耐久。材质按稳定 Key 绑定，最大耐久从原生 GameplayDefinition 或其未绑定时的 TerrainDefinition 回退编译而来；没有第二份手写最大 HP 表。
- 除基岩外的非空材质都可以作用；Protected／SoftRock 保留为生成标签，不再阻止矿镐。保留原生材质已有最大耐久，新增矿镐默认伤害 10；冷却、距离、地图和素材不改。普通岩体默认没有经济产出；现有 cargo 只支持 iron/gold，其他资源配置明确拒绝，不把任意资源错误计为铁。
- 前景目标与背景矿床使用明确类型、实体 ID 和内容实例版本。只有更换或重置格内容才改变内容版本；耐久变化不会中断合作采集。恢复重新绑定地图代次，不依赖旧代次中的内容版本。
- 岩壁扣血到零才清格与发奖；矿床新增单次采集耐久，完成后扣实际储量并重置下一次耐久。完成前预检容量，无产出岩体不受矿袋满限制。搬运矿工也按矿镐伤害命中矿床，不再直接绕过耐久取矿。
- 正式爆炸改为有界目标的耐久伤害。工作台拆填仍是明确的编辑操作，并重置业务组件；带奖励的旧 Tag 3 后提交回调被拒绝，正式产出必须走主角业务事务，避免扣血一次就发一次矿。
- YYGC 提交器增加校验完成后的唯一外部安装回调；对象候选先全部校验，地图内核自身验证、分配和原子安装完成后再安装对象状态。地图通知延迟到对象安装后；此为代码实现，不代表跨系统原子性已通过故障验证。

### 同步、保存与编辑器

- 游戏协议 19、AMP1 schema 2、世界存档 v14。分块帧增加可选业务偏差与内容实例版本，客户端通过一次分块字典交换安装瓦片、业务值和版本；授权外目标仍是 Unknown，撤权清除旧投影。
- 传输窗口上限提升为有界 8 MiB，以容纳全图受损基线；独立网络包与 FishNet 包候选版本 0.3.0-preview.1。不每次命中复制整图，也不创建逐格网络对象。
- 存档保存最终格、稀疏业务记录、材质 GUID 和规则指纹；矿床采集进度进入既有快照／投影。恢复候选先检查材质、耐久、配置和代次，再交给原有世界切换。v13 不自动适配，不删除用户旧存档。
- HUD 提示显示当前／最大耐久和容量阻止原因。本轮没有新增裂纹素材；ProceduralRock 裂纹渲染以及将原生受损阶段贯穿游戏表现输入仍未完成，不能将 HP 同步当成裂纹画面完成。
- 菜单 `Dark Nights / Terrain / 网格业务配置工作台` 提供 Profile 绑定、原生规则瓦片编辑入口、耐久草稿、工具与矿床设置、独立单格试采。应用先检查源冲突，显式编译到新输出路径，再更新 Definition 引用；不覆盖旧编译目录和人工素材，不自动批量覆盖场景。
- 单格试采只使用冻结目录与离线 ARDMap，不写正式世界、背包或存档，也不是正式鼠标／联机验证。

### 依赖与执行状态

框架改动在忽略的 `.deps/YYGC-grid-business` 独立 detached 检出中，基线 fee18645c997ed7529c4592917de6c412033c84e；用户的 D:/Developer/YYGC master 及其 RuntimeDebugHub 本地修改保持原状。可重建来源为 `tools/grid-business/dependency.lock.json`、`yygc.patch` 和 `prepare-dependency.ps1`。游戏 manifest／lock 使用同一个相对路径检出，可能触发 Local Editor 重导入；不建立第二套 Unity Library。

| 阶段 | 本批状态 |
| --- | --- |
| P0 基线与状态边界 | 基线已保存，源码所有者已调整 |
| P1 事务 | 提交顺序源码已修改；故障注入 NOT_RUN |
| P2 配置装配 | Definition 配置、冻结、指纹与入口源码已落盘；生成绑定／编译未验 |
| P3 权威采集 | 岩壁、矿床、爆破和设备采集源码已落盘；实际作用未验 |
| P4 同步恢复 | schema、业务投影和保存合同已修改；晚加入、重连与重启未验 |
| P5 工作台 | 编辑、冲突检查、独立试采源码已落盘；打开、应用、保存重开未验 |
| P6 验收 | 用户要求跳过，全部 NOT_RUN |

既有 MiningInputTests 按部分耐久／非基岩可采语义更新，但未运行；其他旧清格、保护区、协议和快照夹具没有在本批全面迁移，不声明整个旧测试集合适配完毕。前台性能、IL2CPP、双机器、着陆平台被挖后的流程均未验。尚无本批 Player。

本轮没有主动产生构建、测试存档、截图或临时验证报告。隔离依赖是当前 manifest 仍引用的源码，不作为待清理缓存移动；没有删除或归档共享 Library、旧 Player 或其他对话产物。

2026-09-30 [业务规则和组件职责审查](BUSINESS_RULE_AUDIT.md)：按用户要求先整理，扫描生产源码并列出119组规则及16项职责／一致性问题，区分默认远征、条件能力、旧营地、残留入口和技术门禁。重点为缺氧全员死亡自动结算、结算后远征阶段4与Journey.Landed不一致、背包／设备损失、警戒与货物耦合、职业参数被远征AI覆盖、终端表现参与授权和自动写第10槽。审查包含当前未提交采矿候选，读取到协议19／存档v14，不代表该候选完成验收；文件指纹见[证据](evidence/business-rule-audit-20260930.json)。本轮未改生产代码、未启动Unity或构建；是否保留各规则尚未裁决，不能宣称业务已经干净。

2026-09-30 [矿镐网格与采集](MINING_GRID_EXECUTION.md)跟进显示验证：首轮按要求仅实施源码，随后用户报告持镐仍不可见并授权验证。已修复远征主角 HUD 根被隐藏、明确网格全视口；同一 Local Editor 完成导入／编译及购镐、航行、着陆、出舱后的显示生命周期 **14/14**。专项 Editor **13/13**，与首次批次按影响合并 **28/30**；两项旧手枪测试缺少装备初始化仍失败，不扩修。协议 **18**／存档 **v13**。本次落地区未找到合法可达采矿格，实际单格清除与奖励、独立进程联机、事务异常及保存恢复仍待验证；未构建 Mono／IL2CPP，不沿用旧 Player。

2026-09-30 [地形生成阶段 Modifier](TERRAIN_GENERATION_MODIFIER_PIPELINE.md)：入口步道迁为 `[SerializeReference]` 接口配置，正式航程与工作台冻结同一有序步骤，Odin 编辑共用。后续授权验收发现并修复 29 个生成组合无安全步道的问题及空格矿床锚点误滤；当前生成器 v4，Core 1903/1903、Editor 181/181、Mono 构建成功。再次授权后复用同一协议 17／存档 v13 Mono，六组正常／弱网、双人／四人及房主／来宾驾驶矩阵累计 945/945，18 份稳定阶段存档经过实际进程重启恢复和哈希复核；原始失败轮次及脚本修复均保留，见[联机摘要](evidence/terrain-modifier-network-20260930.json)。未启动 Unity、未改生产代码或 YYGC；不同目的地竞争、各阶段全新晚加入、原生 UI、全路线、旧指纹存档、性能、IL2CPP 和双机器等剩余边界见文末清单。

2026-09-29 [洞穴生成统一与通路阻断退出](CAVE_GENERATION_ALIGNMENT.md)：正式航程、Tuner、ReferenceChamber 和 RandomCave 复用完整生成器，移除塌方与竖井岩棚回填。2026-09-30 当前工作台的固定地图来源与保存入口已退出，原人工资产保留。用户要求本次不验证；此前自动批次 59 通过／5 失败不代表本次改动通过，全路线未验收，不构建 Player。

2026-09-29 [远征调试页与 F1 唤出](RUNTIME_DEBUG_HUB.md)：原远征按钮块移入 RuntimeDebugHub“远征”页，正式驾驶台改为靠近后 E 交互；YYGC 唤出键仅保留 F1，Smart Console 仍为 F10。按用户要求不测试、不构建。

2026-09-29 [会话消息、滚轮与返航恢复](SESSION_INPUT_FEEDBACK.md)：消息与横幅统一进入 Smart Console；滚轮退出装备选择；修复全员倒下结算后主角零生命值导致无法移动的明确代码缺陷。一般性切回卡住按用户要求等待进一步现象，不宣称已解决。

2026-09-29 [主角比例与移动调整](HERO_MOVEMENT_SCALE.md)：`fix-20260929-hero-movement-scale`；独立主角步速、冲刺、3 格显示、匹配碰撞、位移动画与镜头。协议 17／存档 v13，旧档保留，新开局使用新规则。用户明确无需测试；本批未运行测试或构建，不宣称实际手感/通行通过。

2026-09-29 飞船商店交互修复：`fix-20260929-ship-shop-interaction`，协议 17／存档 v12。已修复不可见商店与输入回收，补做正式 Editor Play、三分辨率画面及 Mono 双进程交易／重连／真实重启恢复；通过项、首次失败原因与剩余人工范围见[本批验收](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md#2026-09-29-按-e-卡住的修复与基本回归)。

2026-09-29 [飞船交易、冲刺与背包改版](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md)在 `ft-20260929-ship-trade-equipment` 形成源码候选：YYGC 子模块定义、角色唯一装备状态、可信交易事务及 UI Toolkit／R3 展示已接入；协议 **17**／存档 **v12**。自动与人工验收状态见清单，不沿用协议 16／v11 的旧结果。前置方案保留在[设计](SHIP_TRADE_EQUIPMENT_DESIGN.md)。

2026-09-28 [Smart Console 与游戏日志接入](SMART_CONSOLE_INTEGRATION.md)：Bootstrap 装配控制台并订阅 Unity 日志；客户端可见消息与横幅、局部 HUD 状态接入既有 YYLogger。远征寻路停滞改为含单位和目标的服务端 Gameplay 诊断，不再弹通用玩家提示。实际 Unity 验证状态见接入文档。

2026-09-28 原地着陆源码候选已移除星球降落的唯一泊位限制，并同步快照校验、提示与回归用例；游戏协议 **16**／存档 **v11**。新规则的实际验证以[航程验收清单](SPACE_TO_PLANET_ACCEPTANCE.md)为准。

2026-09-28 正式会话运行对象已按用途挂到 `EntityViews` 的独立父节点：角色按类型归组，矿床、投射物池及其他效果分开，便于在 Hierarchy 中查找 `worker`。静态差异检查通过；当前 Unity Editor 正在使用本工程，本批尚未单独执行编译或 Play 画面验收，见[场景索引](SCENES.md)。

2026-09-28 [输入平台实施前后对照](INPUT_PLATFORM_COMPARISON.html)对应的代码边界已在 `ref-20260928-input-boundary` 调整：主角与营地从 `GameInputActions` 读取当帧语义值，帮助面板提供通用改键和恢复默认；未修改 YYGC、协议、存档或 Unity 动作资产。按用户要求本批不执行 Unity 编译、Play 或联机回归，运行状态待验证。

2026-09-26 [太空到星球流程](SPACE_TO_PLANET_IMPLEMENTATION.md)在 `ft-20260926-space-planet-flow` 继续验收，基线 `31aaecf`，已接入启动修复 `4f270dc`。协议 **15**／存档 **v11**，Local 编译及 Mono r2 构建通过；用户反馈的落地问题已改为配置缓降、自动安全着陆、释放驾驶席和开门。测试、实际 Play、联机、构建和环境阻塞分别记入[验收清单](SPACE_TO_PLANET_ACCEPTANCE.md)，不再沿用首轮免验收状态。

2026-09-26 [运行时洞穴工作台](RUNTIME_TERRAIN_TUNER.md)已将 Cave Wall Tuner 接入新版固定／随机工作台左栏，五个主 Tab、岩壁／背景子页、拆填草稿与自适应 UI。受影响 Unity 检查按最新结果合并 35/35，通过两场景实际 Play 与小窗口画面检查；既有架构守卫 10 项和下条地形可见页 2 项继续留账，本批不宣称全项目验收通过。

2026-09-26 当前地图入口与接入核验见[场景索引](SCENES.md)：正式远征、新版固定／随机工作台共用 StrataCave。最新代码已默认开启即时前景并捕获 LocalV2，不能继续按下条早期记录判断“未接入”。已有 Local Unity 实测 32 通过／2 失败，完整视觉、前台性能及新 Player 联机仍未验收；本次仅整理旧场景与入口，不扩大为刷新算法修复。

2026-09-25 [局部地形即时刷新](archive/IMMEDIATE_TERRAIN_REFRESH_EXECUTION.md)在 `ft-20260925-immediate-terrain-refresh` 形成隔离候选：AnyRuleD Core 138/138；LocalV2 独立全图/分页遮罩、岩粒和最终 RGBA 对照 10 组通过。默认 LegacyV1 保持不变，V1/V2 画面门槛、Unity 导入/编译、运行时与性能仍未验；不得称填拆延迟已解决。游戏包锁尚未切到候选 AnyRuleD API，完整阶段状态与依赖风险见执行记录。

2026-09-24 [地图联网重构](MAP_STATE_NETWORKING.md)已在 `ref-20260924-map-state-networking` 完成本轮代码与 Mono 收口；YYGC 可选协议和 FishNet 包锁定 `e07e9a9`，AMP1 V1 帧结构不变。最终 Mono 的地图循环与新旧双向混连已通过；完整三进程正常／弱网矩阵保留 `b239df6` 构建身份，不能冒称最终锁重跑。阶段状态、实际结果与未验项见 `D:/Downloads/AnyRuleD_MapSync/PROGRESS.md`。

2026-09-22 [可步入远征飞船](archive/WALKABLE_EXPEDITION_SHIP.md)已接入正式远征，协议 14／存档 v10；本批 Mono 正常／弱网三进程各 28/28、Core 1048/1048，Editor 按影响合并 235/236（按钮主题 1 项失败留账）。完整边界和证据见切片记录；下方为先前版本的阶段记录，不将其旧“当前”表述用作本批结论。

2026-09-22 [独立洞穴候选实施](archive/STATIC_CAVE_BACKGROUND_EXECUTION.md)已增加同源三层背景、初始轮廓持久化／基线同步和后台分页缓存，协议 13／存档 v9。按用户对首轮画面的反馈，范围扩大为独立原生岩壁、柔光、角色比例与新场景，保留 AnyRuleD 能力；矿粒和矿光暂时隐藏。固定样板先于随机／正式集成验收；P5 前台目标硬件性能及最终美术签署仍待验，不自动切换正式默认风格。

2026-09-21 远征首轮实现与验收见[交付记录](archive/EXPEDITION_CAMP_DELIVERY.md)，分支 `codex/expedition-camp-plan`，协议 **12**／存档 **v8**。E0–E5 的 Demo 主循环已有实现，E6 按本轮快速验收收口：Editor 合并 213/214，1 项通用按钮主题失败单列；Mono 常规／弱网四进程各 35/35；固定浅层往返、矿工交货、部署撤收与原子结算有新证据。原[执行方案](archive/EXPEDITION_CAMP_EXECUTION.md)的全路线、完整故障矩阵、前台性能及正式美术仍不是已通过状态。

本文件下文保留原移植里程碑与历史证据。当前远征阶段以[本轮交付记录](archive/EXPEDITION_CAMP_DELIVERY.md)为准，旧协议／存档和原营地回归计数不再重复列在入口顶部。

<a id="implementation-progress"></a>

## 历史移植实施进展

原主角输入与 M5 的历史切片见[主角与输入联合执行](archive/HERO_INPUT_EXECUTION.md)和 [M5 剩余验收](archive/M5_EXECUTION.md)。以下批次保留历史推进过程，不代表最新版本仍缺相应功能。

第十二批最新状态（2026-09-12）：原生箭矢／浮字／残骸／声音与消息、环境火把／七灯位／月亮／萤火／地表／阴影、角色插值和放置范围线已接入。规则 1338/1338、Editor 53/53、鼠标 13/13、战斗 Play 7/7 通过；新协议 4 Mono 启动 6/6、双进程操作 13/13 通过，详见[原生效果与环境](archive/NATIVE_EFFECTS.md)。画面对照、三夜流程、M4 恢复及完整网络矩阵继续执行，M3–M5 尚未整体完成。

第十一批最新状态（2026-09-12）：五个原生 UGUI 面板、15 类头像、选择／镜头／小地图、状态叠层及工厂建造预览已接入；真实鼠标 13/13、两分辨率 162 控件及原生资源 5/5 检查通过，规则 1316 项、守卫 181 文件通过。Mono Player 实际构建一次，启动 6/6、独立双进程 13/13 通过，详见[原生 UI 与操作](archive/NATIVE_UI.md)。后续继续完整表现和恢复，不将本批作为 M2–M5 整体完成。

第十批最新状态（2026-09-12）：551项冻结素材、15类原生外观、32段动画、场景布局预览和正式副本到对象工厂的接线已实施；583个关键帧采样、冻结布局与Editor Host启停重连通过。UI操作、完整环境／效果／音频及本批Player验证仍待完成，见[原生外观实施](archive/NATIVE_ART.md)。以下第九批及更早段落保留历史边界，后续继续A3/B。

第九批最新状态：M2 A2 已接通正式网络与 Entry，Mono Host＋独立客户端12项检查通过；详见[正式网络接线](archive/FORMAL_NETWORK.md)。下述第八批状态为先前实现边界；当前继续A3可操作表现与M3完整资源，M2整体尚未退出。

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

本批验证记录在 [首批移植证据](archive/evidence/migration-start-2026-09-11.json)。配置宿主可用于验证依赖与启动，**尚不能游玩灰松谷**。LevelDefinition 仍只含 JSON 中的身份、seed 和波次；后续第三批已将布局保存在独立的唯一可编辑场景来源，启动仍不能从 JSON 缺省出零坐标世界。

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

详见[核心迁移记录](archive/CORE_MIGRATION.md)与[冻结证据](archive/evidence/core-migration-2026-09-11.json)。独立复跑新增 `dotnet run --project tools/CoreRegression -- .`；Editor 仍使用上方正式测试程序集入口。Godot 原目录、用户 YYGC 仓库、规则 JSON 和美术未修改。

第三批建立实际 `DarkNights.View`，以 `LevelLayoutAuthoring` 和场景放置组件保存灰松谷边界及 4 个建筑、5 个资源点、7 个友方单位。一次性 Editor 入口只向指定空目录生成首版 `Pinewatch.unity`；日常验证只读场景。2026-09-15 放置组件已收缩为 `ScenePlacement`：正式 Prefab 直接位于三个分组，按 sibling 顺序导出冻结 `LevelLayout`，稳定放置身份自动生成且不要求人工录入。Core 布局校验同时覆盖建筑边界、建筑重叠、资源点遮盖及类别变体约束，错误不会创建部分世界。

验证：结构守卫 83 个手写文件、10 项自测通过；C#9／netstandard2.1 编译零错误／警告；独立回归 1104 项通过；Unity 重编译无错误，完整 Editor 程序集 29/29 通过，其中布局场景逐项对照冻结夹具并创建出相同初始实体顺序。未运行 Player、PlayMode、对象表现或联机检查；本批场景只有可编辑玩法标记和 Gizmo，不把它写成正式可玩或美术完成。证据见[布局迁移记录](archive/evidence/pinewatch-layout-2026-09-11.json)。

第四批（历史基线，已被后续身份切换取代）完成 M0 正式接入探针：离线定义目录冻结为 `DNights`／LegacyCompatible，Worker ContentId 经 DefinitionReference 指向本地定义，WorldSession 网络定义固定 LegacyV1 ID `930001` 并进入 FishNet spawn 列表；两个 Prefab 均注册 Addressables。正式 Runtime 获得窄范围生成器友元访问，SetReadyCommand、SessionStatusState 和 WorldSessionBehaviour 的具体注册已生成并由 AppStartup 强校验。一次性工具只创建空目录首版，日常构建只验证，不重写正式对象。

上述第四批验证：隔离 YYGC `10b8f0e` 准备可重现；结构守卫 92 个手写文件、10 项自测通过；C#9／netstandard2.1 编译零错误／警告；独立核心回归 1104 项通过；Unity 重编译无错误，完整 Editor 程序集 32/32 通过。Windows Mono 与 IL2CPP Player 各构建一次并在独立进程完成 6/6 启动检查，正式对象／命令／状态／Behaviour 注册只出现一次且 Editor／Tests 程序集未进入 Player。Worker、WorldSession 四个资产及 Pinewatch 场景的构建前后 SHA-256 一致。该记录中的身份设置是历史 LegacyV1 基线，见[历史正式对象接入记录](archive/evidence/formal-object-contracts-2026-09-11.json)。

第五批切断正式 ObjectDefinition 的旧整数兼容：数据库固定为 `GuidFirst`、在线 ID 服务关闭且无 `LegacyIdMap`；Worker／WorldSession 的 `Id` 均为 `0`、旧 ID 别名为空；正式 Windows 构建开启 `YYGC_GUID_DEFINITION_WIRE_V2`。正式 Editor／Runtime 校验会拒绝 `LegacyCompatible`、旧 ID、旧 ID 别名、旧映射或 LegacyV1；独立 LAN Sample 与 YYGC 用户仓库保持不变，旧 v1 存档导入仍作为独立的玩法迁移边界保留。

验证：结构守卫 92 个手写文件、Core 编译零错误／警告、独立核心回归 1104 项通过；Unity Editor 测试 32/32 通过；Windows Mono 与 IL2CPP Player 均在独立进程完成 6/6 启动检查，正式日志均包含 `identity=GuidFirst wire=GuidV2`，Editor／Tests 程序集未进入 Player。切换后的数据库与两个 Definition 资产哈希分别为 `0ec20eded0912c30852db60a99b03c3ab77a27a7057da30e08a085cd1282d10b`、`83f71cb728b42d10de75295369ebc889e91e4659ed978cd4ff7e7e77b36b8714`、`c24b46a54049efd47a6b7f161fc84b05d1238f56ba8fdc7b5db392dee71278e5`；证据见[正式 GuidV2 身份切换记录](archive/evidence/formal-object-contracts-guid-v2-2026-09-11.json)。

第六批补齐 M1 独立存储切片：新档固定 `dark-nights.world` v1，引用 Core 的随机算法标识，以实际不可变目录和布局计算独立二进制规范化 SHA-256；世界字段不含相机、选择或房间控制策略。旧 v1 保持独立显式导入，恢复先经过现有完整字段／关系校验，只返回新 GameSession。文件适配提供 0–9 槽位、严格 UTF-8 与 4 MB 上限、取消检查、同目录临时文件 `Flush(true)` 后 Move／Replace；失败保留原档，同实例操作串行。遵循方案的 JSON／文件路线，未接 YYArchive 模块或 UI。

验证：独立回归 **1164/1164**（新增存档 60 项），架构守卫 **97 个手写文件、10 项自测**通过；Unity 本批导入及修正算法常量后的编译均无错误，完整 Editor 程序集 **34/34**通过。覆盖真实文件锁导致提交失败、损坏／超大输入、取消、并发保存、新旧格式隔离及新格式恢复后继续 20 秒的冻结结果。实际 .NET 存档在 Unity 加载后 JSON 字段值精确一致，两个摘要一致；浮点文本位数不同不作为字节一致保证。本批未构建或运行 Mono／IL2CPP Player、联机或美术验收。正式资产、配置和旧夹具未改写；详见[存档合同](SAVE_FORMAT.md)及[第六批证据](archive/evidence/world-save-2026-09-11.json)。

第七批实现 `Runtime/Session`：权威实例自行创建并独占 GameSession；Host 和来宾均提交冻结的显式参数，在创建线程按接受顺序处理。每连接最多 16 条待处理、全局最多 64 条，保留最近 64 条完成结果；重复请求返回原回执，改参重发／窗口外旧序号不能再次支付。连接替换增加代次并清除 Ready，执行点复查当前连接、epoch、策略与权限。SharedCamp／HostOnly 限制所有营地修改及自动派工；房主控制时间与 BeginLoad。加载票据由服务端持有，取消／失败保留旧世界，成功恢复完整世界后增加 epoch、清空 Ready／去重并保持房间策略。

验证：独立回归 **1260/1260**（新增会话 96 项），架构守卫 **109 个手写文件、10 项自测**零错误；Unity 两次批量编译无错误，最终完整 Editor 程序集 **37/37**通过。采集 12 秒、住宅施工和训练与直接 Core 的完整快照一致；队列争抢不足资源、工位独占、部分训练支付、策略切换、非法输入、重连旧请求及加载失败／取消均有检查；加载冻结旧档后继续 20 秒对照原结果。没有运行 Player、PlayMode、多进程或弱网检查，没有修改正式资源或冻结夹具。第二次编译的新增输入为显式保序去重及补充队列／部分成功场景；详见[会话业务合同](archive/SESSION_AUTHORITY.md)和[第七批证据](archive/evidence/session-authority-2026-09-11.json)。

第八批执行 M5 路线的 A1：实现全部实体／HUD 冻结展示、箭矢稳定展示身份、WorldReplica 完整帧替换与连接／发布版本过滤，以及 SessionClock 的有界追帧和余量保留。独立回归 **1316/1316**（新增 56 项），架构守卫 **123 文件／10 自测／0 错误**；一次 Unity 批量编译无错误，完整 Editor **40/40**。没有修改规则、夹具或正式资源，没有构建 Player 或运行联机。实现合同及边界见[展示副本与时钟](archive/SESSION_PROJECTION.md)，实测摘要见[第八批证据](archive/evidence/session-projection-2026-09-11.json)。

后续批次已完成 M3 表现与 M4 存档恢复主体及正式四进程／弱网分批检查；当前余项以 [M5 收尾清单](archive/M5_EXECUTION.md#closeout) 为准。M1 存档产品已并入 M4；IL2CPP 和双机器分别取得对应前置条件后实测。

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
- 现行实现：普通 C# 权威核心＋YYGC 会话对象／命令／状态链＋本地 Prefab / UGUI；单人走同一入口。2026-09-13 已选择后续迁为 YYGC 唯一对象模型，见本页顶部；未完成迁移前不改写现行实现状态。
- 默认建议：SharedCamp；保留 HostOnly 开关，房主控制时间／存档；不提前分配私人单位。
- 已有实证：`6000.4.9f1` 环境基线，YYGC `10b8f0e` 的 Sample Mono / IL2CPP 四进程与弱网；正式少量定义、DTO 与生成注册已通过双后端启动，正式玩法联机仍必须重验。
- M0/M1 历史锁定：正式生成注册／访问、JSON 库、GUID / Key 映射与 GuidFirst／GuidV2 网络定义、规则及随机兼容；当时保留旧 v1 导入。新的统一重构不再要求旧档／旧协议兼容，仍验证新格式恢复和冻结玩法语义。
- M2 测量决定：完整投影频率、插值缓冲、载荷上限、是否分块或拆流以及性能预算。
- M0 双后端探针已收口；M1 规则／存储基础已验证；M2 正式网络、可见原生对象及 UI 操作已接入，仍需完整网络矩阵。文件编排、完整表现和交付继续执行；新增 YYGC 必要修正逐项记录于账本，不覆盖用户框架工作区。
