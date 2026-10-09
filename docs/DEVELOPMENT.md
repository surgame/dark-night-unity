# Dark Nights Unity 开发执行状态

本页统一维护当前源码身份、完成边界和待验队列。玩法范围见[地面基础玩法](GROUND_GAMEPLAY_BASELINE.md)，字段与依赖分别查长期合同；原批次过程和失败从[归档索引](archive/README.md)追溯。

## 当前输入与完成边界

截至2026-10-09，照明灯口／可切换后端源码已集成本地main，包含此前地面玩法、手电库存化和Debug Hub图标成果。游戏协议 **31**，存档 **v23**，AMP1 schema **2**；Unity锁定 **6000.4.9f1**，Linear、URP及YYGC锁定依赖保持。实际常量位于 `SessionAuthority.ProtocolVersion`、`SessionSnapshot.CurrentVersion`，依赖以manifest、packages-lock和补丁锁为准。分支切换、源码合入及静态编译不代表真实Unity导入或当前Player验收完成。

本次顶层文档整理在 `docs-20261009-document-responsibilities` 进行，只更新Markdown及既有HTML中的必要导航。本轮没有修改源码、人工资产、协议／存档／依赖锁、YYGC、Player或冻结证据，没有运行Unity编译、测试、Play或构建。

| 当前已接入的内容 | 最近证据与实际边界 |
| --- | --- |
| 地面开局、购买、主角／装备／飞船入口 | 原协议27／v21批次：51项定向Editor＋2项正式UI共53个不同用例通过；[装备证据](evidence/equipment-restore-20261007.json)。不代表后续协议31的独立Player已验 |
| 原生矿层与两层局部流 | 初轮协议25／v19的Editor、组合画面及指定Mono结果保留在[原记录](archive/MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)；当前状态、网络和文件合同已分别承接 |
| 手电进入四格库存、共用光效与目标补光 | 原批次专项10/10＋Bootstrap短时探针1/1；按影响合并33个不同用例通过，3个旧业务失败留账；[证据](evidence/reusable-light-20261009.json)。没有本批独立Player或长期性能结论 |
| Debug Hub图标物体页 | 原批次19项Host面板检查、720／480／320宽度、矿镐添加及槽位移除通过；[证据](evidence/debug-hub-icon-panel-20261009.json)。不覆盖完整生命周期与跨进程调试矩阵 |
| 有限灯口及PrivateField／Urp2D后端 | Core灯口探针12/12、三个配置静态编译及受影响架构通过；真实Unity、Shader与双后端画面仍待验；[证据](evidence/lighting-backends-20261009.json) |
| F10有界日志及中文回退 | 地面批次队列／过滤6项、中文F10探针1项、正式商店UI1项通过；[证据](evidence/ground-gameplay-baseline-20261007.json)。原两次OOM与失回执批次保留 |

以上结果各自保留源码、日期、配置、报告及Player身份，不相加成当前候选总通过数。当前没有覆盖协议31／v23及新照明后端的完整独立Player验收记录。

## 当前待验队列

| 项目 | 仍需完成的检查 | 前置条件／原始入口 |
| --- | --- | --- |
| 照明后端导入 | 新脚本／HLSL首次导入及meta、真实Unity／Shader编译、`LightingBackendTests`、模板provider持久化复核 | 同一Local Editor、编译及内存门控；[后端原记录](archive/LIGHTING_BACKENDS_20261009.md) |
| 双后端画面与释放 | Bootstrap中Private→URP→Private，地图更换、非游戏相机、失败后切换、灯／纹理／遮挡退休和真实组合画面 | 上项通过后短时探针；私有墙内补光／反射与URP能力分别验，不能视作逐像素同效果 |
| 局部换区修复 | 最终源码Unity编译、两项新增缓存回归、扩展组合用例；原位置、横向／深入连续换区的缺墙及闪底色复验 | [诊断与停止记录](archive/TERRAIN_STREAMING_FIX_20261007.md)、[证据](evidence/terrain-streaming-fix-20261007.json)；不借用别批GPU探针或启动成功核销画面 |
| Debug Hub边界 | 注册代次／同名旧句柄／失败页、关闭Domain Reload；动作中移除、批量上限、普通Client及旧epoch／库存版本／重复序号拒绝 | [原人工清单](archive/RUNTIME_DEBUG_HUB_IMPLEMENTATION_20261008.md#关键人工测试项)；已验基本图标操作保留，其余逐项核销 |
| 当前联机与存档 | 同一新Mono的2人／4人、正常／弱网，支付竞争、Host单次执行、权限、Ready、晚加入／重连、暂停、epoch；装备／手电／两层地图真实写盘、加载和重启恢复 | 按当前开放玩法套用[联机验收](MULTIPLAYER.md#联机验收重点)及[存档检查](SAVE_FORMAT.md#验证要求)，每个配置构建一次复用 |
| 操作与可见性 | 正常速度下主角坡沿／跳跃、改键、洞室通行；照明方向／身体可读性、厚墙／坡形、挖墙／爆破／换区更新及多光源 | [照明人工检查](LIGHTING.md#人工检查)、[工作台诊断](EDITOR_WORKBENCH.md#主角地面诊断)；不以后台或离屏小样签署最终手感 |

监控入口为 `tools/ground-baseline/watch_memory.py`。本机停止线为Editor私有8 GiB、可用RAM6 GiB、系统提交余量8 GiB；构建另留预计分配余量。此前4 GiB门控下的样本仍按原批次保存。门控或编译失败后停止本任务后续批次，不结束其他应用；流程见[Unity CLI](UNITY_CLI_WORKFLOW.md#内存门控)。

## 历史失败与挂起项

- 可复用照明扩展回归仍有3个旧用例失败：`DeathRemovesPossessionWithoutAnOrphanedPlayerIndex`依赖暂停的StartNight刷怪，`ShopAndSaleUseLeasePositionAndAtomicState`依赖暂停出售，`ShopReservesFirstPickaxeBudget`依赖未实现预算规则。原失败和前后复验保留，不将全部回归写成通过。[原记录](archive/REUSABLE_LIGHTING_20261009.md)。
- 2026-10-05最终Mono为 `artifacts/map-state/player-mono-20261005-083543-43792d9e/DarkNights.exe`，身份在 `artifacts/weak-network-completion-20261005/final-player-source-manifest.json`。原16组矩阵未完成：四人采矿弱网38/38、两人采矿弱网中断，其余不能搬用旧产物计数。该Player早于主角输入／Host展示及后续玩法／照明变更；[补测过程](archive/WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md)继续保留，不能作为当前候选的验收产物。
- 矿工完整路线、设备物流、出售／返航结算、二次完整太空航程及旧营地全局矩阵随对应玩法恢复再安排。挂起不等于已通过或删除失败，不因旧清单自动恢复业务。
- 前台性能按用户暂缓边界保持待验；后台容量、启动成功和隐藏小样不替代。IL2CPP主动生成／覆盖／验证需用户明确后端授权；双机器LAN需要实际第二台设备，本机多进程不替代。
- 手持装备旧批次的未完成人工／池化／联机项目、工具能力弱网重连失败、地形工作台可见页失败和美术来源均保留在[归档索引](archive/README.md)，继续前须核对当前适用范围和输入身份。

## 执行与文档维护

只重跑失败、受新输入影响或有新证据的阶段。每批先检查磁盘及内存，复用Local的单一Editor和当前缓存，串行导入、生成、编译及构建；保留任务ID、完成标记、退出码和原始报告。历史成功不能替代当前源／配置／Player身份。产物按[工作区保全流程](WORKSPACE.md#阶段产物盘点与保全)处理。

完成状态只在本页更新；稳定规则进所属合同，实际操作进指南，实施与失败进归档。顶层整理映射、Git恢复基线和文档核验见[整理记录](archive/README.md#2026-10-09顶层职责整理)。
