# 归档与原始证据索引

归档记录保留原日期、源码、协议、Player、失败及验证身份。现行合同从[文档索引](../README.md)进入，当前待验由[DEVELOPMENT](../DEVELOPMENT.md)维护；历史待办不自动恢复暂停玩法，归档不代表完成或删除证据。

## 历史摘要

| 文档 | 用途 |
| --- | --- |
| [项目演进](PROJECT_HISTORY.md) | 已集成切片、版本演进和仍有效边界 |
| [移植与统一对象](MIGRATION_HISTORY.md) | 状态所有权、旧模型退出、冻结规则来源和U6限制 |
| [原生表现与输入](PRESENTATION_HISTORY.md) | 作者素材、Linear、视图、UI、输入和原验收边界 |
| [地图、洞穴与矿层](TERRAIN_HISTORY.md) | 路线、视觉／碰撞和矿层方案演进 |

## 近期实施记录

| 原记录 | 现行承接 |
| --- | --- |
| [Bootstrap预加载修复](BOOTSTRAP_STARTUP_OPTIMIZATION.md) | [架构资源准备](../ARCHITECTURE.md#装配能力和事务)，原计时／失败／租约证据保留 |
| [F1玩法盘点](F1_GAMEPLAY_INVENTORY.md) | [开放／暂停范围](../GROUND_GAMEPLAY_BASELINE.md)，原16项评估保留 |
| [物体图标面板](DEBUG_HUB_ICON_PANEL_20261009.md) | [Hub界面规范](../RUNTIME_DEBUG_HUB_SPEC.md)，原图标来源及Host检查保留 |
| [Hub重构](RUNTIME_DEBUG_HUB_IMPLEMENTATION_20261008.md)、[分支集成](RUNTIME_DEBUG_HUB_MERGE_20261009.md) | Hub规范／YYGC账本／执行状态；原候选、冲突及meta来源不改身份 |
| [初版手电](FLASHLIGHT_IMPLEMENTATION_20261008.md)、[可复用光效](REUSABLE_LIGHTING_20261009.md)、[灯口／后端](LIGHTING_BACKENDS_20261009.md) | [照明](../LIGHTING.md)、工具／联机／存档合同；后端导入、Shader、画面等仍在当前队列 |
| [原生矿层迁移](MINERAL_MAP_MIGRATION_IMPLEMENTATION.md) | [地形](../TERRAIN_GENERATION.md)、联机及存档；旧r12及最终Mono各自身份保留 |
| [场景快照](SCENES.md)、[Play工作台](RUNTIME_TERRAIN_TUNER.md)、[网格检查](TERRAIN_GRID_DEBUGGER.md) | [编辑工作台](../EDITOR_WORKBENCH.md)，路径／预览／只读检查按现行章节操作 |
| [日志接入](SMART_CONSOLE_INTEGRATION.md)、[Unity内存事故](UNITY_MEMORY_INCIDENT_20261007.md) | [有界日志架构](../ARCHITECTURE.md#本地照明与有界日志)、[CLI内存门控](../UNITY_CLI_WORKFLOW.md#内存门控)，原OOM与4 GiB门控历史保留 |
| [局部换区缺墙／闪底色](TERRAIN_STREAMING_FIX_20261007.md) | [装载合同](../TERRAIN_GENERATION.md#局部装载与换区)与当前编译／缓存／连续画面待验 |

## 冻结验收、失败与审查来源

- [最终弱网补测](WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md)、[航程验收](SPACE_TO_PLANET_ACCEPTANCE.md)、[交易装备验收](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md)：保留未完成组、失败及对应构建。旧Player和暂停业务不直接成为当前队列。
- [即时刷新](IMMEDIATE_TERRAIN_REFRESH_EXECUTION.md)、[Local验证](CAVE_TUNER_LOCAL_VALIDATION_20260926.md)、[隔离评审](CAVE_TUNER_ISOLATED_REVIEW.md)：原输入／可见页失败和视觉门槛保留，续接前核对现行范围。
- [业务规则审查](BUSINESS_RULE_AUDIT.md)、[Definition／Editor审查](DEFINITION_EDITOR_REVIEW.md)、[手持装备人工／回归清单](HERO_HANDHELD_TEST_PLAN.md)、[洞穴视觉目标](CAVE_EXPLORATION_TARGETS.md)：原问题和未完成检查保留，不将旧源码行号当当前状态。
- [氧气移除](OXYGEN_REMOVAL_VALIDATION.md)、[失败分析](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md)：战斗返船、弱网、矿工路线和二次航程等边界随对应业务条件处理；[退役资源来源](retired-assets/README.md)保留。

## 操作、设计与作者来源

| 主题 | 原记录 |
| --- | --- |
| 框架／对象 | [YYGC能力复评](YYGC_REASSESSMENT.md)、[覆盖映射](YYGC_UNIFIED_TEST_COVERAGE.md)、[Definition场景放置](SCENE_DEFINITIONS.md)、[导航授权](WORKSHOP_NAVIGATION_FIX_PROPOSAL.md) |
| 飞船／装备 | [可步入飞船](WALKABLE_EXPEDITION_SHIP.md)、[航程设计](SPACE_TO_PLANET_FLOW_DESIGN.md)与[实施](SPACE_TO_PLANET_IMPLEMENTATION.md)、[交易设计](SHIP_TRADE_EQUIPMENT_DESIGN.md)、[手持装备来源](HERO_HANDHELD_EQUIPMENT.md)、[会话反馈](SESSION_INPUT_FEEDBACK.md) |
| 主角／采矿 | [比例调整](HERO_MOVEMENT_SCALE.md)、[坡面运动](SLOPE_JUMP_FLOW.md)、[采矿网格](MINING_GRID_EXECUTION.md)、[统一距离](PICKAXE_UNIFIED_REACH.md) |
| 地图／制作 | [AnyRuleD评审](ANYRULED_REVIEW.md)、[地图联网](MAP_STATE_NETWORKING.md)、[Modifier](TERRAIN_GENERATION_MODIFIER_PIPELINE.md)、[生成统一](CAVE_GENERATION_ALIGNMENT.md)、[航程工作台](TUNER_JOURNEY_WORKBENCH.md) |
| 美术 | [独立背景](STATIC_CAVE_BACKGROUND_EXECUTION.md)、[矿层图集](ORE_LAYER_ART.md)、[内嵌矿素材](EMBEDDED_MINERAL_LAYER_ART.md)、[地表候选](SURFACE_ENVIRONMENT.md)、[已有三层衔接](CAVE_ENTRANCE_ART_LAYERS.md) |
| 快速进入 | [快速局原操作](QUICK_TEST_SCENES.md)、[旧F1远征页](RUNTIME_DEBUG_HUB.md)；当前操作以Player指南及工作台为准 |

独立[LAN样板](../samples/LAN_SAMPLE.md)仍有单独源码、资产及构建用途，已退出顶层；样板旧依赖／IL2CPP记录不签署正式工程的新验收。

## 报告、证据与恢复

- [09-21～27周报](reports/weekly-2026-09-27.html)、[09-28～10-04周报](reports/weekly-2026-10-04.html)、[输入架构对照](HERO_INPUT_ARCHITECTURE.html)、[输入平台对照](INPUT_PLATFORM_COMPARISON.html)保留用户成果。
- [当前证据目录](../evidence/)及[历史证据目录](evidence/)保留机器记录与原报告；内部原始产物路径是来源信息，不保证仍在该位置。
- [阶段产物清单](STAGE_CLEANUP_INVENTORY.md)、[10-05目录回执](FOLDER_ORGANIZATION_20261005.md)、[10-07精简回执](DOCUMENT_CONSOLIDATION_20261007.md)、[10-09协作约定回执](AGENTS_CONSOLIDATION_20261009.md)保留各自清单、授权边界及Git恢复基线。本轮未处理其中产物或.codex。

## 2026-10-09顶层职责整理

用户批准逐篇方案后，16份现有现行文档按唯一职责整理，新增LIGHTING合同；上方15份近期过程原文移入本目录，LAN移samples，顶层32份收敛到17份。根README只介绍项目／入口，现行索引只导航，DEVELOPMENT只维护当前源／验证／待验。场景、预览、网格和主角诊断由工作台承接，内存操作归CLI；原生矿层现行合同不再由迁移回执优先覆盖。

归档前已承接照明后端首次导入／meta／Shader／Editor／双后端及释放、局部换区编译／两项缓存／连续画面、Hub生命周期／权限／跨进程、当前联机／文件恢复及人工手感。旧Mono矩阵、暂停玩法挂起、3个旧业务失败、IL2CPP授权、前台暂缓和双机器条件均保留，未因归档核销。

整理前Git基线：`c2f613b361af14d93532b27ec5a250d3f83262c6`。15份迁移原文只增加历史提示、修正文档引用及两处失效产物入口；原日期／源码／Player／失败与测试语义保持。被精简的长期文件原文也可按该基线和原路径读取，例如：

```powershell
git show c2f613b361af14d93532b27ec5a250d3f83262c6:docs/DEVELOPMENT.md
git show c2f613b361af14d93532b27ec5a250d3f83262c6:docs/MINERAL_MAP_MIGRATION_IMPLEMENTATION.md
```

恢复时提取到独立目录，不覆盖现行内容。完整移动表、整理前哈希、引用映射和最终核验位于 `artifacts/doc-organization-20261009/`；这些资料用于本轮复查，原位保留，不计释放空间。

本轮只做文档核验：没有永久删除、源码／资产／锁定配置／YYGC修改，没有Unity测试、Play、构建或新增游戏通过数。冻结JSON、XML、图片、日志、用户成果及Player字节继续保留；链接、锚点、范围及证据哈希的实际检查以该目录verification.json为准。

最终文档核验：99份第一方Markdown／HTML的725条本地引用及70个锚点、周报53个静态／脚本来源路径、3段HTML脚本语法、16个场景路径通过；200份证据SHA-256保持，131份JSON语法通过。16份迁移正文及YYGC历史账本在扣除提示／引用变化后保持原文，周报原通过数／结果不变；Git空白及范围核对通过。顶层正文359,226字节→194,506字节，均为工作树文件字节，不计Git历史或磁盘空间回收。仅文档核验，Unity／Player运行数0。
