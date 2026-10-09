# 归档文档索引

2026-10-07 按用户要求再次精简：早期过程与重复快照改为主题摘要，部分旧实现证据移出工作树。先读[现行文档](../README.md)；归档中的日期、协议、构建和通过数仅代表原批次，未完成项不因整理变成通过。

## 历史摘要

| 文档 | 保留内容 |
| --- | --- |
| [项目演进](PROJECT_HISTORY.md) | 已集成切片、版本演进和仍有效的边界 |
| [移植与统一对象](MIGRATION_HISTORY.md) | 状态所有权、旧模型退出、冻结规则来源及U6限制 |
| [原生表现与输入](PRESENTATION_HISTORY.md) | 作者资源、Linear、主视图、UI、输入和M5边界 |
| [地图、洞穴与矿层](TERRAIN_HISTORY.md) | 地图路线、视觉／碰撞约束和原生矿层替代关系 |

## 仍需接续的验收与审查

- [最终弱网补测](WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md)、[航程验收](SPACE_TO_PLANET_ACCEPTANCE.md)、[交易装备验收](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md)：保留未完成组、构建身份和失败边界。
- [即时刷新实施](IMMEDIATE_TERRAIN_REFRESH_EXECUTION.md)、[Local验证](CAVE_TUNER_LOCAL_VALIDATION_20260926.md)、[隔离修复](CAVE_TUNER_ISOLATED_REVIEW.md)：按实际输入续接，不能沿用旧通过数。
- [业务规则审查](BUSINESS_RULE_AUDIT.md)、[Definition与Editor审查](DEFINITION_EDITOR_REVIEW.md)、[手持装备人工／回归清单](HERO_HANDHELD_TEST_PLAN.md)、[洞穴视觉与空间目标](CAVE_EXPLORATION_TARGETS.md)。
- [氧气移除范围与验证](OXYGEN_REMOVAL_VALIDATION.md)、[历史失败分析](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md)：保留战斗返船、弱网和二次航程边界。

## 仍有操作、设计或来源用途的专题

| 主题 | 文档 |
| --- | --- |
| 框架与真实对象 | [YYGC能力复评](YYGC_REASSESSMENT.md)、[回归覆盖映射](YYGC_UNIFIED_TEST_COVERAGE.md)、[Definition场景放置](SCENE_DEFINITIONS.md)、[导航授权记录](WORKSHOP_NAVIGATION_FIX_PROPOSAL.md) |
| 航程与装备 | [可步入飞船](WALKABLE_EXPEDITION_SHIP.md)、[航程设计](SPACE_TO_PLANET_FLOW_DESIGN.md)与[实现](SPACE_TO_PLANET_IMPLEMENTATION.md)、[交易设计](SHIP_TRADE_EQUIPMENT_DESIGN.md)、[手持装备与素材](HERO_HANDHELD_EQUIPMENT.md)、[会话反馈](SESSION_INPUT_FEEDBACK.md) |
| 主角和采矿 | [主角比例](HERO_MOVEMENT_SCALE.md)、[坡面运动](SLOPE_JUMP_FLOW.md)、[采矿网格](MINING_GRID_EXECUTION.md)、[统一触及距离](PICKAXE_UNIFIED_REACH.md) |
| 地图和制作 | [AnyRuleD评审](ANYRULED_REVIEW.md)、[地图联网](MAP_STATE_NETWORKING.md)、[Modifier管线](TERRAIN_GENERATION_MODIFIER_PIPELINE.md)、[生成统一](CAVE_GENERATION_ALIGNMENT.md)、[航程工作台](TUNER_JOURNEY_WORKBENCH.md) |
| 美术来源 | [独立背景实施](STATIC_CAVE_BACKGROUND_EXECUTION.md)、[矿层素材](ORE_LAYER_ART.md)、[内嵌矿素材合同](EMBEDDED_MINERAL_LAYER_ART.md)、[地表候选](SURFACE_ENVIRONMENT.md)、[现有三层衔接](CAVE_ENTRANCE_ART_LAYERS.md) |
| 快速进入 | [快速场景操作](QUICK_TEST_SCENES.md)、[F1调试页](RUNTIME_DEBUG_HUB.md) |

## 报告、证据与恢复

- [09-21～27周报](reports/weekly-2026-09-27.html)、[09-28～10-04周报](reports/weekly-2026-10-04.html)、[输入架构对照](HERO_INPUT_ARCHITECTURE.html)、[输入平台对照](INPUT_PLATFORM_COMPARISON.html)保留为用户成果。
- [阶段产物清单](STAGE_CLEANUP_INVENTORY.md)、[10-05目录整理回执](FOLDER_ORGANIZATION_20261005.md)保留产物归属与处理边界；待清理目录未随本次文档整理处理。
- [历史证据目录](evidence/)仅保留仍有来源、框架、操作或未完成验收用途的记录；[当前证据](../README.md#历史与证据)继续单列。机器记录内的原始产物路径是来源信息，不保证旧输出仍在该处。
- [10-07整理回执](DOCUMENT_CONSOLIDATION_20261007.md)记录整合映射、移除范围、检查及Git恢复入口。
- [10-09协作约定整理回执](AGENTS_CONSOLIDATION_20261009.md)记录AGENTS的20段历史承接、100项约束整理映射及原文恢复；仅文档更新，不新增游戏验收。
