# 文件夹与文档整理回执

2026-10-05，继续原聊天最后一项“整理所有文件夹、弃用内容移入待清理、旧文档和过程归archive、更新索引”。当前分支 `ft-20261004-embedded-minerals`，未创建新分支、合并或推送。

## 整理结果

归档32份方案／审查／实施过程、2份输入对照HTML、2份周报，历史机器证据114个文件集中到 `docs/archive/evidence/`。现行根README、文档索引、开发计划、架构／联机／存档、场景和工作台去掉重复的旧版本时间线，整理前全文保留8份历史快照。弱网补测过程另归archive，保留尚未完成的最终矩阵状态。

新增[工作区用途](../WORKSPACE.md)、[工具索引](../../tools/README.md)、[实验来源索引](../../experiments/README.md)。现行[开发计划](../DEVELOPMENT.md)明确区分当前最终Player与历史构建；本次整理没有重跑游戏矩阵或新增游戏通过计数。

可确认不用的18项工具编译／Python缓存、4项测试资产／meta及2个归档后空目录移入统一目录：`D:/Developer/MiniGames/Dark Nights/unity-projects/artifacts/待清理/20261005-folder-organization/`。共311个文件、50,353,724字节（48.02MiB），全部逐文件哈希保持。该目录中的 `清单.md`、`preservation.json`记录原／新绝对路径、任务、体积、原因与后续条件。没有永久删除，释放空间为0。

## 保留与边界

- 当前Editor的营地场景有未保存改动，保持原场景和dirty状态；此前工作区源码、场景修改与新增输入绑定保留。
- 全部Player仍关联报告、DLL身份或失败复现，保留原路径；未完成的弱网矩阵目录、隔离存档和正式日志继续保留，没有重新启动原矩阵。
- 退役场景仍被菜单、场景合同或测试引用，营地场景仍参与专项回归与显式构建，未移动。矿床Prefab／Definition仍承担配置与作者素材引用。
- 三个experiments目录保留唯一编辑源、正式图集来源或用户成果；第三方组件、当前Library、依赖及外部工作区均保留。
- YYGC源码、补丁、锁定版本及包配置没有修改。本次没有触碰 `.codex`、外部基线或其他聊天工作树。

## 文档迁移目录

下面列原／新仓库相对路径。历史正文的日期、计数和失败结论保持；可导航链接按新位置更新。机器证据的字节与哈希完全保持，其JSON内原始产物路径继续表示历史来源。

| 原路径 | 新路径 |
| --- | --- |
| `docs/BUSINESS_RULE_AUDIT.md` | [docs/archive/BUSINESS_RULE_AUDIT.md](BUSINESS_RULE_AUDIT.md) |
| `docs/CAVE_ENTRANCE_ART_LAYERS.md` | [docs/archive/CAVE_ENTRANCE_ART_LAYERS.md](CAVE_ENTRANCE_ART_LAYERS.md) |
| `docs/CAVE_GENERATION_ALIGNMENT.md` | [docs/archive/CAVE_GENERATION_ALIGNMENT.md](CAVE_GENERATION_ALIGNMENT.md) |
| `docs/DEFINITION_EDITOR_REVIEW.md` | [docs/archive/DEFINITION_EDITOR_REVIEW.md](DEFINITION_EDITOR_REVIEW.md) |
| `docs/EDITOR_WORKBENCH_ASSESSMENT.md` | [docs/archive/EDITOR_WORKBENCH_ASSESSMENT.md](EDITOR_WORKBENCH_ASSESSMENT.md) |
| `docs/EMBEDDED_MINERAL_LAYER_ART.md` | [docs/archive/EMBEDDED_MINERAL_LAYER_ART.md](EMBEDDED_MINERAL_LAYER_ART.md) |
| `docs/EMBEDDED_MINERAL_LAYER_IMPLEMENTATION.md` | [docs/archive/EMBEDDED_MINERAL_LAYER_IMPLEMENTATION.md](EMBEDDED_MINERAL_LAYER_IMPLEMENTATION.md) |
| `docs/EMBEDDED_MINERAL_LAYER_PLAN.md` | [docs/archive/EMBEDDED_MINERAL_LAYER_PLAN.md](EMBEDDED_MINERAL_LAYER_PLAN.md) |
| `docs/EMBEDDED_MINERAL_LAYER_REVIEW.md` | [docs/archive/EMBEDDED_MINERAL_LAYER_REVIEW.md](EMBEDDED_MINERAL_LAYER_REVIEW.md) |
| `docs/HERO_MOVEMENT_SCALE.md` | [docs/archive/HERO_MOVEMENT_SCALE.md](HERO_MOVEMENT_SCALE.md) |
| `docs/MAP_STATE_NETWORKING.md` | [docs/archive/MAP_STATE_NETWORKING.md](MAP_STATE_NETWORKING.md) |
| `docs/MINERAL_MAP_MIGRATION_PLAN.md` | [docs/archive/MINERAL_MAP_MIGRATION_PLAN.md](MINERAL_MAP_MIGRATION_PLAN.md) |
| `docs/MINING_GRID_EXECUTION.md` | [docs/archive/MINING_GRID_EXECUTION.md](MINING_GRID_EXECUTION.md) |
| `docs/OXYGEN_REMOVAL_ASSESSMENT.md` | [docs/archive/OXYGEN_REMOVAL_ASSESSMENT.md](OXYGEN_REMOVAL_ASSESSMENT.md) |
| `docs/OXYGEN_REMOVAL_EXECUTION.md` | [docs/archive/OXYGEN_REMOVAL_EXECUTION.md](OXYGEN_REMOVAL_EXECUTION.md) |
| `docs/OXYGEN_REMOVAL_FAILURE_ANALYSIS.md` | [docs/archive/OXYGEN_REMOVAL_FAILURE_ANALYSIS.md](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md) |
| `docs/OXYGEN_REMOVAL_VALIDATION.md` | [docs/archive/OXYGEN_REMOVAL_VALIDATION.md](OXYGEN_REMOVAL_VALIDATION.md) |
| `docs/PICKAXE_UNIFIED_REACH.md` | [docs/archive/PICKAXE_UNIFIED_REACH.md](PICKAXE_UNIFIED_REACH.md) |
| `docs/QUICK_TEST_SCENES.md` | [docs/archive/QUICK_TEST_SCENES.md](QUICK_TEST_SCENES.md) |
| `docs/QUICK_TEST_SCENES_ASSESSMENT.md` | [docs/archive/QUICK_TEST_SCENES_ASSESSMENT.md](QUICK_TEST_SCENES_ASSESSMENT.md) |
| `docs/RUNTIME_DEBUG_HUB.md` | [docs/archive/RUNTIME_DEBUG_HUB.md](RUNTIME_DEBUG_HUB.md) |
| `docs/SESSION_INPUT_FEEDBACK.md` | [docs/archive/SESSION_INPUT_FEEDBACK.md](SESSION_INPUT_FEEDBACK.md) |
| `docs/SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md` | [docs/archive/SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md) |
| `docs/SHIP_TRADE_EQUIPMENT_DESIGN.md` | [docs/archive/SHIP_TRADE_EQUIPMENT_DESIGN.md](SHIP_TRADE_EQUIPMENT_DESIGN.md) |
| `docs/SLOPE_JUMP_FLOW.md` | [docs/archive/SLOPE_JUMP_FLOW.md](SLOPE_JUMP_FLOW.md) |
| `docs/SPACE_TO_PLANET_ACCEPTANCE.md` | [docs/archive/SPACE_TO_PLANET_ACCEPTANCE.md](SPACE_TO_PLANET_ACCEPTANCE.md) |
| `docs/SPACE_TO_PLANET_FLOW_DESIGN.md` | [docs/archive/SPACE_TO_PLANET_FLOW_DESIGN.md](SPACE_TO_PLANET_FLOW_DESIGN.md) |
| `docs/SPACE_TO_PLANET_IMPLEMENTATION.md` | [docs/archive/SPACE_TO_PLANET_IMPLEMENTATION.md](SPACE_TO_PLANET_IMPLEMENTATION.md) |
| `docs/SURFACE_ENVIRONMENT.md` | [docs/archive/SURFACE_ENVIRONMENT.md](SURFACE_ENVIRONMENT.md) |
| `docs/TERRAIN_GENERATION_MODIFIER_PIPELINE.md` | [docs/archive/TERRAIN_GENERATION_MODIFIER_PIPELINE.md](TERRAIN_GENERATION_MODIFIER_PIPELINE.md) |
| `docs/TUNER_JOURNEY_WORKBENCH.md` | [docs/archive/TUNER_JOURNEY_WORKBENCH.md](TUNER_JOURNEY_WORKBENCH.md) |
| `docs/WORKSHOP_NAVIGATION_FIX_PROPOSAL.md` | [docs/archive/WORKSHOP_NAVIGATION_FIX_PROPOSAL.md](WORKSHOP_NAVIGATION_FIX_PROPOSAL.md) |
| `docs/HERO_INPUT_ARCHITECTURE.html` | [docs/archive/HERO_INPUT_ARCHITECTURE.html](HERO_INPUT_ARCHITECTURE.html) |
| `docs/INPUT_PLATFORM_COMPARISON.html` | [docs/archive/INPUT_PLATFORM_COMPARISON.html](INPUT_PLATFORM_COMPARISON.html) |
| `docs/reports/weekly-2026-09-27.html` | [docs/archive/reports/weekly-2026-09-27.html](reports/weekly-2026-09-27.html) |
| `docs/reports/weekly-2026-10-04.html` | [docs/archive/reports/weekly-2026-10-04.html](reports/weekly-2026-10-04.html) |
| `docs/WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md` | [弱网补测过程](WEAK_NETWORK_VALIDATION_COMPLETION_20261005.md) |

完整98项文档／证据位置映射、8份快照来源、盘点和操作回执保存在 `artifacts/folder-organization-20261005/`。历史证据本身没有被合并重写，旧失败不以新结果覆盖。

## 核验

历史机器证据和待清理文件逐文件SHA-256保持；111项既有Git证据blob与迁移前一致，冻结换行／原始诊断规则已同步；已移动的临时源路径退出工作区。正式源码、作者资源、场景、Prefab、meta、包配置及项目设置与本次任务开始时对照。6096项已跟踪非文档文件核验：6095项内容与任务开始时一致，`.gitattributes`仅将冻结XML／构建诊断的路径规则随证据迁入archive。已迁移证据按目标位置核验；当前19份根目录Markdown均纳入索引。文档／HTML本地链接没有新增断链；第三方包README与Aseprite生成模板的11处既有资源引用原位保留、单独列账。Editor仍停在原营地场景并保留dirty状态，临时夹具已退出AssetDatabase。正式机器回执见[整理核验](evidence/folder-organization-20261005.json)；本次目录／文档变更不伪造编译、Play、构建、前台性能或联机通过。

阶段结束可用空间：C盘约10.80GiB、D盘约26.82GiB。
