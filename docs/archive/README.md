# 历史文档索引

此处保存旧方案、审查、实施过程、周报与当时的验证边界；未完成的历史批次也保留原状态；它们不是当前协议或存档合同。先读[现行文档入口](../README.md)，再按日期与构建身份查阅历史材料。机器证据在 [`evidence/`](evidence/)；保留原始失败、未验收及输入哈希，不以后续通过结果覆盖。

## 评估、迁移与对象架构

- [初始评估状态](ASSESSMENT_STATUS.md)、[框架评估](FRAMEWORK_REVIEW.md)、[YYGC 能力复评](YYGC_REASSESSMENT.md)、[移植方案](MIGRATION_PLAN.md)、[Core 迁移](CORE_MIGRATION.md)
- [C 方案计划](C_REFACTOR_PLAN.md)及[实施结果](C_REFACTOR_IMPLEMENTATION.md)、[场景定义](SCENE_DEFINITIONS.md)、[对象定义审计](OBJECT_DEFINITION_AUDIT.md)
- [正式网络](FORMAL_NETWORK.md)、[恢复接入](NETWORK_RECOVERY.md)、[会话权限](SESSION_AUTHORITY.md)、[会话投影](SESSION_PROJECTION.md)

## YYGC 统一对象与 M5

- [统一重构计划](YYGC_UNIFIED_REFACTOR_PLAN.md)、[实施记录](YYGC_UNIFIED_IMPLEMENTATION.md)、[状态与恢复映射](YYGC_UNIFIED_STATE_MAP.md)、[测试覆盖](YYGC_UNIFIED_TEST_COVERAGE.md)、[性能与验收边界](YYGC_UNIFIED_PERFORMANCE.md)
- [M5 执行](M5_EXECUTION.md)、[Mono 验收](MONO_ACCEPTANCE.md)、[世界表现](M5_WORLD_PRESENTATION.md)、[UI 校准](M5_UI_CALIBRATION.md)、[结果 UI](M5_RESULT_UI.md)、[阶段清理列账](STAGE_CLEANUP_INVENTORY.md)

## 主角、原生表现与操作

- [主角输入](HERO_INPUT_EXECUTION.md)、[手持装备](HERO_HANDHELD_EQUIPMENT.md)及[测试清单](HERO_HANDHELD_TEST_PLAN.md)、[本地指令圈](LOCAL_COMMAND_RINGS.md)
- [原生美术](NATIVE_ART.md)、[对象主视图](NATIVE_OBJECT_VIEWS.md)、[效果](NATIVE_EFFECTS.md)、[UI](NATIVE_UI.md)

## 地图、洞穴与远征

- [AnyRuleD 评审](ANYRULED_REVIEW.md)、[随机灰松谷](RANDOM_PINEWATCH.md)、[地图执行](MAP_PLAN_EXECUTION.md)
- [洞穴原型](CAVE_EXPLORATION_ART.md)、[视觉与空间目标](CAVE_EXPLORATION_TARGETS.md)、[地图工作台](CAVE_WORKSHOP.md)
- [远征执行方案](EXPEDITION_CAMP_EXECUTION.md)及[交付记录](EXPEDITION_CAMP_DELIVERY.md)

## 2026-09-22～26 地形、岩层与飞船切片

- [可步入远征飞船](WALKABLE_EXPEDITION_SHIP.md)、[独立岩层与三层背景方案](STATIC_CAVE_BACKGROUND_PLAN.md)及[执行记录](STATIC_CAVE_BACKGROUND_EXECUTION.md)、[按格矿层美术候选](ORE_LAYER_ART.md)
- [地形 Modifier 与点缀层](TERRAIN_MODIFIERS_PROGRESS.md)、[局部地形即时刷新](IMMEDIATE_TERRAIN_REFRESH_EXECUTION.md)、[Cave Tuner 隔离评审](CAVE_TUNER_ISOLATED_REVIEW.md)及[本机验证](CAVE_TUNER_LOCAL_VALIDATION_20260926.md)
- [旧随机地图 Debug Bootstrap](TERRAIN_DEBUG_BOOTSTRAP.md)（`(old)` 场景回归用）

这些记录保留各自时点的“当前”措辞，仅代表当时输入。不可把旧计划、历史 Player 哈希或后台功能结果当作当前版本的完整验收。


## 2026-09-26～10-05 航程、装备与地形过程

- [航程设计](SPACE_TO_PLANET_FLOW_DESIGN.md)、[实现](SPACE_TO_PLANET_IMPLEMENTATION.md)、[验收](SPACE_TO_PLANET_ACCEPTANCE.md)、[交易设计](SHIP_TRADE_EQUIPMENT_DESIGN.md)与[验收](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md)。
- [主角比例](HERO_MOVEMENT_SCALE.md)、[坡面运动](SLOPE_JUMP_FLOW.md)、[矿镐网格](MINING_GRID_EXECUTION.md)、[触及范围](PICKAXE_UNIFIED_REACH.md)、[会话输入反馈](SESSION_INPUT_FEEDBACK.md)。
- [地图联网重构](MAP_STATE_NETWORKING.md)、[Modifier 管线](TERRAIN_GENERATION_MODIFIER_PIPELINE.md)、[生成入口统一](CAVE_GENERATION_ALIGNMENT.md)、[旧地表环境候选](SURFACE_ENVIRONMENT.md)、[现有三层衔接](CAVE_ENTRANCE_ART_LAYERS.md)。
- [氧气移除评估](OXYGEN_REMOVAL_ASSESSMENT.md)、[执行](OXYGEN_REMOVAL_EXECUTION.md)、[验证](OXYGEN_REMOVAL_VALIDATION.md)与[历史失败](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md)。

## 工作台、审查与快速测试过程

- [业务审查](BUSINESS_RULE_AUDIT.md)、[Definition／Editor 复评](DEFINITION_EDITOR_REVIEW.md)、[工作台聚合评估](EDITOR_WORKBENCH_ASSESSMENT.md)、[导航修正提案](WORKSHOP_NAVIGATION_FIX_PROPOSAL.md)。
- [快速测试评估](QUICK_TEST_SCENES_ASSESSMENT.md)与[实现／操作记录](QUICK_TEST_SCENES.md)、[F1 调试入口](RUNTIME_DEBUG_HUB.md)、[星球与航程工作台](TUNER_JOURNEY_WORKBENCH.md)。
- [输入架构对照](HERO_INPUT_ARCHITECTURE.html)、[输入平台对照](INPUT_PLATFORM_COMPARISON.html)，按原制作时点阅读。

## 内嵌矿方案与当前补测过程

- [旧逐床状态方案](EMBEDDED_MINERAL_LAYER_PLAN.md)、[审查](EMBEDDED_MINERAL_LAYER_REVIEW.md)、[素材合同](EMBEDDED_MINERAL_LAYER_ART.md)、[协议24检查点](EMBEDDED_MINERAL_LAYER_IMPLEMENTATION.md)。
- [原生地图迁移执行方案](MINERAL_MAP_MIGRATION_PLAN.md)；现行实现见[当前矿层记录](../MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)。
- [周报弱网补测过程](WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md)：最终 Player 矩阵尚未完成，原始失败与中断状态保留。

## 周报与整理快照

- [2026-09-21～27 周报](reports/weekly-2026-09-27.html)、[2026-09-28～10-04 周报](reports/weekly-2026-10-04.html)。
- [原根 README 时间线](PROJECT_CHANGELOG_20261005.md)、[开发计划历史](DEVELOPMENT_HISTORY_20261005.md)。
- [架构历史](ARCHITECTURE_HISTORY_20261005.md)、[联机历史](MULTIPLAYER_HISTORY_20261005.md)、[存档历史](SAVE_FORMAT_HISTORY_20261005.md)。
- [场景整理历史](SCENE_ORGANIZATION_HISTORY_20261005.md)、[地图历史](TERRAIN_GENERATION_HISTORY_20261005.md)、[工作台历史](EDITOR_WORKBENCH_HISTORY_20261005.md)。
- [2026-10-05 文件夹与文档整理回执](FOLDER_ORGANIZATION_20261005.md)：保留边界、归档路径与核验结果。

历史证据集中在 [evidence/](evidence/)，原始结果字节及哈希保持。归档位置变化见整理回执；JSON 内的原产物路径属于历史来源，不据此重新写证据。

本批整理核验见[2026-10-05机器回执](evidence/folder-organization-20261005.json)。
