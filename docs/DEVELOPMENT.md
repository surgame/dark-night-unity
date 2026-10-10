# Dark Nights Unity 开发执行状态

本页统一维护当前源码身份、完成边界和待验队列。玩法范围见[地面基础玩法](GROUND_GAMEPLAY_BASELINE.md)，字段与依赖分别查长期合同；原批次过程和失败从[归档索引](archive/README.md)追溯。

## 当前输入与完成边界

2026-10-10，本轮提交与主分支集成候选汇总工作区的持久光照配置、临时调试草稿／Undo、场景光照引用修复、矿层跳跃闪隐修复及1 GiB内存门控调整。6个游戏侧C#程序集静态编译全部通过，0错误、18警告；新增资产与meta配对、GUID和JSON完整性核对通过。未触发Unity刷新、额外测试、Play或Player构建，不扩大此前定向验证结论；真实Unity／Shader及人工验收待项继续保留。[本轮核对](evidence/commit-main-20261010.json)。

截至2026-10-09，照明灯口／可切换后端源码已集成本地main，包含此前地面玩法、手电库存化和Debug Hub图标成果。游戏协议 **31**，存档 **v23**，AMP1 schema **2**；Unity锁定 **6000.4.9f1**，Linear、URP及YYGC锁定依赖保持。实际常量位于 `SessionAuthority.ProtocolVersion`、`SessionSnapshot.CurrentVersion`，依赖以manifest、packages-lock和补丁锁为准。分支切换、源码合入及静态编译不代表真实Unity导入或当前Player验收完成。

本次顶层文档整理在 `docs-20261009-document-responsibilities` 进行，只更新Markdown及既有HTML中的必要导航。本轮没有修改源码、人工资产、协议／存档／依赖锁、YYGC、Player或冻结证据，没有运行Unity编译、测试、Play或构建。

持久光照配置重构在 `ref-20261009-light-profile-config` 开发：Definition 可选引用 LightProfile 并逐项覆盖，Profile 为空时读取内置默认值；光效由预设模板或场景的默认模板自动挂载。Odin Addressables 抽屉接管 Profile 字段的问题已通过实际属性树检查定位，面板改挂到 Overrides 字段；当前实际抽屉链选中自定义面板，原始 None 的 Definition 可只读解析为 LightEffect 模板、14格照距、1.35强度及2.2格补光。源码自动导入已有编译产物且未发现C#错误；这两项只读检查不代表GUI拖动、原生资产迁移、测试或Play验收。该批次未迁移默认场景资源，原检查提交余量4.45 GiB低于8 GiB门槛；31个制作回归用例已编写但未执行。旧错误、源检查点与精确待验范围见[本批记录](evidence/light-profile-refactor-20261009.json)。

2026-10-10 在同一工作分支优化逐项覆盖交互：工坊与光照调试台移除覆盖复选框，全部参数可直接编辑，实际修改自动记录该项覆盖并显示蓝点；点蓝点或参数行右键恢复后继续跟随当前预设／内置默认值。覆盖位与数值一起进入原生 Undo，换预设保留其他覆盖，输入相同值不产生新覆盖。源码、制作说明及受影响回归用例已更新；本批系统提交余量6.30 GiB低于8 GiB停止线，未触发静态编译、Unity刷新／编译、测试、Play或构建，GUI与测试保持待验。[本批边界](evidence/light-override-ux-20261010.json)不替代上批验收。

滑条整次拖动的 Undo／Redo 后续修正已写入游戏侧源码：保存拖动起点组，释放时合并属性树追加的原生记录；工坊在属性树结束当帧处理后收尾，不再把仍持有捕获的 Ignore 当作松手，调试台过滤其他指针／控件的捕获变化。追加原生记录、连续两次拖动、切换资产和跨帧向左拖动的回归已更新。当前仍仅完成源码与差异核对，本批提交余量6.28 GiB低于8 GiB门槛，未触发静态编译、Unity刷新／编译、测试、Play或构建；实际工坊与调试台拖动验收待完成。[修正边界](evidence/light-slider-undo-20261010.json)。

调试台「临时编辑、确认保存」切片已完成源码接线：单灯复用既有覆盖合同叠加到选中的运行手电，场景配置使用独立副本，灯口先调整运行实例／只读 Stage 旁的临时预览；各资产仅从明确确认入口写回。调试台临时 Undo／Redo 独立且有界，工坊保留原生资产历史；关闭、换目标、实例回收、换世界和退出 Play 撤除临时效果。保存只合并改动字段，未另选预设时跟随工坊的最新引用。既有窗口回归已改为核对草稿及来源隔离，但未执行。本批预检提交余量7.979 GiB仍低于8 GiB门槛，未主动触发编译、测试、Play或构建。Unity 自动生成四个新脚本的 meta 并编译过中间候选，期间的接口未齐编译错误已保留；最终源码晚于已加载程序集，完整候选编译及实际调参／保存检查仍待完成。待测操作及预期见[人工检查](LIGHTING.md#人工检查)，[本批证据](evidence/light-runtime-draft-20261010.json)记录明确边界。

2026-10-10 启动缺少场景光照配置已修复资源：前述重构新增必填引用但遗漏迁移，正式 Expedition 和 RandomCampRegression 均未绑定。现经单一 Editor API 创建设备／场景光照资产及 meta，并保存两处引用；重新加载校验两处配置有效，手电仍为可选 None 预设，Definition／Prefab 字节和场景布局保留。迁移源码增加独立修复入口、有效配置复用和局部续接，启动错误追加关卡路径及修复指引。预检提交余量4.765 GiB低于8 GiB门槛，未主动编译、测试或 Play；最终修复源码编译及 Bootstrap 实际启动仍待验，不能将资源引用校验写成启动通过。[本批证据](evidence/lighting-startup-fix-20261010.json)。

2026-10-10 原位置跳跃导致矿层闪隐已在游戏侧修复：同世界订阅换代保留原矿层宿主及重叠页面，复用前景局部装卸；Ready仅取当前矿层基线／实绘，撤权、断线、换世界仍立即撤除。Unity编译通过，扩展的矿层组合回归1/1通过，覆盖往返换区、旧页复用、采空像素和撤权；原会话独立检查点恢复后在同位置连续跳跃3次，410个实绘采样中宿主保持一致、整层缺失及可见页无输出均为0。协议／存档／锁定依赖和作者资产未改；该结果只覆盖本机Editor定向范围，不代表Player、弱网或此前全部换区待验项通过。失败夹具诊断、修正及新候选身份见[本批证据](evidence/ore-jump-fix-20261010.json)。

| 当前已接入的内容 | 最近证据与实际边界 |
| --- | --- |
| 地面开局、购买、主角／装备／飞船入口 | 原协议27／v21批次：51项定向Editor＋2项正式UI共53个不同用例通过；[装备证据](evidence/equipment-restore-20261007.json)。不代表后续协议31的独立Player已验 |
| 原生矿层与两层局部流 | 初轮协议25／v19的Editor、组合画面及指定Mono结果保留在[原记录](archive/MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)；当前状态、网络和文件合同已分别承接 |
| 手电进入四格库存、共用光效与目标补光 | 原批次专项10/10＋Bootstrap短时探针1/1；按影响合并33个不同用例通过，3个旧业务失败留账；[证据](evidence/reusable-light-20261009.json)。没有本批独立Player或长期性能结论 |
| Debug Hub图标物体页 | 原批次19项Host面板检查、720／480／320宽度、矿镐添加及槽位移除通过；[证据](evidence/debug-hub-icon-panel-20261009.json)。不覆盖完整生命周期与跨进程调试矩阵 |
| 有限灯口及PrivateField／Urp2D后端 | Core灯口探针12/12、三个配置静态编译及受影响架构通过；真实Unity、Shader与双后端画面仍待验；[证据](evidence/lighting-backends-20261009.json) |
| 持久光照配置与自动装配 | 可选预设／内置默认值、Definition逐项覆盖、自动挂载及资产Undo源码接线完成；场景配置和两关卡引用已补齐并只读校验，最终编译、手电完整迁移及运行仍待验；[配置证据](evidence/light-profile-refactor-20261009.json)、[启动修复](evidence/lighting-startup-fix-20261010.json)。旧测试数不代表新候选 |
| F10有界日志及中文回退 | 地面批次队列／过滤6项、中文F10探针1项、正式商店UI1项通过；[证据](evidence/ground-gameplay-baseline-20261007.json)。原两次OOM与失回执批次保留 |

以上结果各自保留源码、日期、配置、报告及Player身份，不相加成当前候选总通过数。当前没有覆盖协议31／v23及新照明后端的完整独立Player验收记录。

## 当前待验队列

| 项目 | 仍需完成的检查 | 前置条件／原始入口 |
| --- | --- | --- |
| 照明后端导入 | 新脚本／HLSL首次导入及meta、真实Unity／Shader编译、`LightingBackendTests`、模板provider持久化复核 | 同一Local Editor、编译及内存门控；[后端原记录](archive/LIGHTING_BACKENDS_20261009.md) |
| 持久配置和Undo／Redo | 当前候选Unity编译，显式原生资产迁移；`LightProfileTests`、`LightInlineConfigurationTests`、`FlashlightDebugUndoTests`、`FlashlightDebugWindowTests`；Definition工坊直接编辑、蓝点及右键单项恢复、整次拖动、换预设保留覆盖、保存／重开、原生菜单／快捷键、挂点预览和自动释放 | 新批次先满足现行内存门控，串行复用Local；不构建Player；[操作合同](LIGHTING.md#调试窗口持久化撤销与重做)、[配置证据](evidence/light-profile-refactor-20261009.json)及[覆盖交互](evidence/light-override-ux-20261010.json) |
| 双后端画面与释放 | Bootstrap中Private→URP→Private，地图更换、非游戏相机、失败后切换、灯／纹理／遮挡退休和真实组合画面 | 上项通过后短时探针；私有墙内补光／反射与URP能力分别验，不能视作逐像素同效果 |
| 局部换区修复 | 最终源码Unity编译、两项新增缓存回归、扩展组合用例；原位置、横向／深入连续换区的缺墙及闪底色复验 | [诊断与停止记录](archive/TERRAIN_STREAMING_FIX_20261007.md)、[证据](evidence/terrain-streaming-fix-20261007.json)；不借用别批GPU探针或启动成功核销画面 |
| Debug Hub边界 | 注册代次／同名旧句柄／失败页、关闭Domain Reload；动作中移除、批量上限、普通Client及旧epoch／库存版本／重复序号拒绝 | [原人工清单](archive/RUNTIME_DEBUG_HUB_IMPLEMENTATION_20261008.md#关键人工测试项)；已验基本图标操作保留，其余逐项核销 |
| 当前联机与存档 | 同一新Mono的2人／4人、正常／弱网，支付竞争、Host单次执行、权限、Ready、晚加入／重连、暂停、epoch；装备／手电／两层地图真实写盘、加载和重启恢复 | 按当前开放玩法套用[联机验收](MULTIPLAYER.md#联机验收重点)及[存档检查](SAVE_FORMAT.md#验证要求)，每个配置构建一次复用 |
| 操作与可见性 | 正常速度下主角坡沿／跳跃、改键、洞室通行；照明方向／身体可读性、厚墙／坡形、挖墙／爆破／换区更新及多光源 | [照明人工检查](LIGHTING.md#人工检查)、[工作台诊断](EDITOR_WORKBENCH.md#主角地面诊断)；不以后台或离屏小样签署最终手感 |

2026-10-10用户要求放宽为避免运行时内存耗尽，现已同步规则与 `tools/ground-baseline/watch_memory.py`：Editor私有内存仅记录，不设固定上限；可用RAM或系统提交余量低于1 GiB停止，构建另留预计分配余量。单次预检只读，即使越界也不退出已有Play；持续监控仍仅停止本项目Play。静态编译入口复用同一判定。此前4／8 GiB门控下的样本及未执行项仍按原批次保存，不因放宽规则改写为通过。门控或编译失败后停止本任务后续批次，不结束其他应用；流程见[Unity CLI](UNITY_CLI_WORKFLOW.md#内存门控)。

## 历史失败与挂起项

- 可复用照明扩展回归仍有3个旧用例失败：`DeathRemovesPossessionWithoutAnOrphanedPlayerIndex`依赖暂停的StartNight刷怪，`ShopAndSaleUseLeasePositionAndAtomicState`依赖暂停出售，`ShopReservesFirstPickaxeBudget`依赖未实现预算规则。原失败和前后复验保留，不将全部回归写成通过。[原记录](archive/REUSABLE_LIGHTING_20261009.md)。
- 2026-10-05最终Mono为 `artifacts/map-state/player-mono-20261005-083543-43792d9e/DarkNights.exe`，身份在 `artifacts/weak-network-completion-20261005/final-player-source-manifest.json`。原16组矩阵未完成：四人采矿弱网38/38、两人采矿弱网中断，其余不能搬用旧产物计数。该Player早于主角输入／Host展示及后续玩法／照明变更；[补测过程](archive/WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md)继续保留，不能作为当前候选的验收产物。
- 矿工完整路线、设备物流、出售／返航结算、二次完整太空航程及旧营地全局矩阵随对应玩法恢复再安排。挂起不等于已通过或删除失败，不因旧清单自动恢复业务。
- 前台性能按用户暂缓边界保持待验；后台容量、启动成功和隐藏小样不替代。IL2CPP主动生成／覆盖／验证需用户明确后端授权；双机器LAN需要实际第二台设备，本机多进程不替代。
- 手持装备旧批次的未完成人工／池化／联机项目、工具能力弱网重连失败、地形工作台可见页失败和美术来源均保留在[归档索引](archive/README.md)，继续前须核对当前适用范围和输入身份。

## 执行与文档维护

只重跑失败、受新输入影响或有新证据的阶段。每批先检查磁盘及内存，复用Local的单一Editor和当前缓存，串行导入、生成、编译及构建；保留任务ID、完成标记、退出码和原始报告。历史成功不能替代当前源／配置／Player身份。产物按[工作区保全流程](WORKSPACE.md#阶段产物盘点与保全)处理。

完成状态只在本页更新；稳定规则进所属合同，实际操作进指南，实施与失败进归档。顶层整理映射、Git恢复基线和文档核验见[整理记录](archive/README.md#2026-10-09顶层职责整理)。
