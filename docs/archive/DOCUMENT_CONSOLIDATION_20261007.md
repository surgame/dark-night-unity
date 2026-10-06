# 2026-10-07 文档精简回执

按用户要求，排除进行中对话的资料，再次整合旧归档文档并移除不再需要的实现证据。本次仅修改仓库Markdown、两份周报的导航链接及选定docs/archive/evidence文件；没有运行Unity、构建Player或新增游戏通过数。

## 结果

- 51篇旧文档合并：49篇归入4份主题摘要，2篇氧气评估／执行范围归入原验证记录。根README、现行／归档索引、工作区说明、AGENTS历史链接和相关工具说明同步更新。
- docs中的Markdown由114篇减至68篇；已提交文档及证据文件由364个减至265个，工作树文件体积净减少约3.05MiB。Git历史保留原文，不计作Git对象存储释放。
- 移除53个旧证据文件：包括重复测试摘要、早期Player文件清单和已退役批次截图／日志。保留181个历史证据及全部6个当前证据，字节和SHA-256保持。
- 保留操作说明、美术编辑／重建来源、冻结规则及RNG夹具、第三方声明、框架授权与锁定来源、未完成验收、失败待办和用户周报／可视化成果。
- 同步纠正技术架构中仍写作协议8和泛化YYGC修改授权的旧表述，分别对齐协议25与2026-10-03具体范围授权约束；快速上手改以当前main为入口。实现、依赖和规则未改变。

## 合并映射

下列旧文件原位于docs/archive；原文按Git基线恢复，不再常驻工作树。

### [MIGRATION_HISTORY](MIGRATION_HISTORY.md)

- `ASSESSMENT_STATUS.md`、`FRAMEWORK_REVIEW.md`、`MIGRATION_PLAN.md`、`CORE_MIGRATION.md`
- `C_REFACTOR_PLAN.md`、`C_REFACTOR_IMPLEMENTATION.md`、`OBJECT_DEFINITION_AUDIT.md`、`FORMAL_NETWORK.md`
- `NETWORK_RECOVERY.md`、`SESSION_AUTHORITY.md`、`SESSION_PROJECTION.md`、`YYGC_UNIFIED_REFACTOR_PLAN.md`
- `YYGC_UNIFIED_IMPLEMENTATION.md`、`YYGC_UNIFIED_STATE_MAP.md`、`YYGC_UNIFIED_PERFORMANCE.md`

### [PRESENTATION_HISTORY](PRESENTATION_HISTORY.md)

- `M5_EXECUTION.md`、`MONO_ACCEPTANCE.md`、`M5_WORLD_PRESENTATION.md`、`M5_UI_CALIBRATION.md`
- `M5_RESULT_UI.md`、`NATIVE_ART.md`、`NATIVE_OBJECT_VIEWS.md`、`NATIVE_EFFECTS.md`
- `NATIVE_UI.md`、`LOCAL_COMMAND_RINGS.md`、`HERO_INPUT_EXECUTION.md`

### [TERRAIN_HISTORY](TERRAIN_HISTORY.md)

- `RANDOM_PINEWATCH.md`、`MAP_PLAN_EXECUTION.md`、`TERRAIN_DEBUG_BOOTSTRAP.md`、`CAVE_EXPLORATION_ART.md`
- `CAVE_WORKSHOP.md`、`EXPEDITION_CAMP_EXECUTION.md`、`EXPEDITION_CAMP_DELIVERY.md`、`STATIC_CAVE_BACKGROUND_PLAN.md`
- `TERRAIN_MODIFIERS_PROGRESS.md`、`EDITOR_WORKBENCH_ASSESSMENT.md`、`QUICK_TEST_SCENES_ASSESSMENT.md`、`EMBEDDED_MINERAL_LAYER_PLAN.md`
- `EMBEDDED_MINERAL_LAYER_REVIEW.md`、`EMBEDDED_MINERAL_LAYER_IMPLEMENTATION.md`、`MINERAL_MAP_MIGRATION_PLAN.md`

### [PROJECT_HISTORY](PROJECT_HISTORY.md)

- `PROJECT_CHANGELOG_20261005.md`、`DEVELOPMENT_HISTORY_20261005.md`、`ARCHITECTURE_HISTORY_20261005.md`、`MULTIPLAYER_HISTORY_20261005.md`
- `SAVE_FORMAT_HISTORY_20261005.md`、`SCENE_ORGANIZATION_HISTORY_20261005.md`、`TERRAIN_GENERATION_HISTORY_20261005.md`、`EDITOR_WORKBENCH_HISTORY_20261005.md`

### [OXYGEN_REMOVAL_VALIDATION](OXYGEN_REMOVAL_VALIDATION.md)

- `OXYGEN_REMOVAL_ASSESSMENT.md`、`OXYGEN_REMOVAL_EXECUTION.md`

## 旧证据移除范围

移除仅限53个已提交的旧证据，不处理artifacts原始输出。包含C重构、旧正式网络／会话接线、旧主角平台和Player清单、M5重复摘要及清单、旧手采地图／Debug批次、早期原生效果、旧保存和U3～U6重复过程；旧terrain-modifiers-2026-09-22批次11个文件一并移除。

逐文件删除路径可从本次Git提交的 `--diff-filter=D --name-only` 核对；本机执行计划与哈希在 `artifacts/doc-consolidation-20261007/plan.json`。删除前已逐文件核对基线哈希、绝对路径及链接目标。保留文件继续按用途引用，历史机器记录中的原输出路径不改写。

## 对话与产物保护

已核对本项目近期对话的状态与结束记录。Bootstrap启动耗时分析仍在补测，其 `artifacts/bootstrap-startup-analysis-20261007/` 全部保留；新进行中的飞船固定停泊评估所用飞船、航程、交易专题及现行设计文档继续保留。主角修复、工作台近期证据和最终弱网矩阵资料继续保留。完成的地形尖角评估已结束，原归档产物也未处理。

所有Game源码、作者资源、配置、依赖补丁、冻结夹具、Library、Player、原始artifacts、待清理目录、.codex及其他工作树均未进入本次移除范围。当前共享文档只更新必要索引和过期表述；没有改写其他对话的分析或验收结果。

## Git恢复

整理前基线：`75f51a099bae601378aa2c742425b85dd5f51d2b`。例如读取原文：

```powershell
git show 75f51a099bae601378aa2c742425b85dd5f51d2b:docs/archive/YYGC_UNIFIED_IMPLEMENTATION.md
```

需要恢复某个旧文件时，使用该基线与原路径在独立目录提取；不恢复整个仓库或覆盖当前改动。Git保留原文和移除证据，因此本次工作树精简不等于Git历史对象回收。

## 核验

最终核验：89份第一方Markdown及保留HTML中的575个本地链接／锚点通过；187个保留证据的SHA-256一致；删除路径与51篇文档／53份证据的执行清单一致，范围外源码、资源和依赖无改动。3个原位忽略的历史日志继续保留，没有纳入删除或体积收益。

详细结果在 `artifacts/doc-consolidation-20261007/verification.json`。仅文档检查，不套用历史Unity或Player通过数。统计体积按文件字节计算，Git历史与文件系统块分配另计。
