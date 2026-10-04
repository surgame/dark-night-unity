# Dark Nights 文档结构

2026-10-04 [Cave Wall Tuner 星球工作台](TUNER_JOURNEY_WORKBENCH.md)：四个折叠组共享地图与航程草稿，保留原生样式 Inspector；入口步道启停、显式保存、旧草稿转移及航程示意见本页。影响相关 Editor 用例合并45/45、作者资产1111项保持、真实 GUI 已检查；仅本地 Editor 候选，完整重启／Dark 主题／Player 等边界见记录。

2026-10-04 [斜坡跳跃连续运动](SLOPE_JUMP_FLOW.md)：`fix-20261004-slope-jump-flow` 的代码提交 `a711897` 已快进合入本地 `main`，本轮未推送；连续斜向运动与接坡、点跳输入保留、默认固定的策略开关和喷气衔接。协议23／AMP1 schema2不变，规则摘要v5／存档v17；首轮Editor66/66，最终跨缺口修正的Editor补测待执行，完整验证边界与看效果入口见该页。

2026-10-03 [工作台原生聚合](EDITOR_WORKBENCH.md)：四类任务按需停靠 Workshop、Viewer、航程、地形和岩壁编辑器，场景只保留快捷操作。获批工坊导航修正通过，Editor16/16、作者资产436项保持；操作、证据及依赖账本见说明。

2026-10-03 [氧气移除验证记录](OXYGEN_REMOVAL_VALIDATION.md)：原Editor119/121、资源12/12、正常Mono双进程130/130；两项未实现玩法的旧路线用例已移除，剩余远征回归6/6。纯航程1903/1903、Core1046/1048；[弱网及旧路线历史诊断](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md)保留，弱网修复延后，整批未通过。

2026-10-03 [氧气业务移除及合并记录](OXYGEN_REMOVAL_EXECUTION.md)：分支已合入本地main，协议 **23**／存档 **v16**，薄worktree实施、Local串行后台验证；YYGC未修改，实施范围与原验收计划见该页；此前[评估](OXYGEN_REMOVAL_ASSESSMENT.md)保留评估时点。

2026-10-03 [天空与矿洞分层衔接](CAVE_ENTRANCE_ART_LAYERS.md)：复用已有三层，退出深度透明／底色／亮度过渡；原三层 RGBA 对照与 Editor 36/36、Mono、正式 Play 已验。新图草稿未采用，验证边界见本批记录。

2026-10-03 [矿镐统一触及范围](PICKAXE_UNIFIED_REACH.md)：矿镐自身配置同步控制吸附与挥砍，当前64逻辑像素／4格；YYGC合同、验证及配置入口见本页。

2026-10-02 [快速测试与场景整理](QUICK_TEST_SCENES.md)：主菜单专属 Hub 入口、已着陆矿镐预设及完整场景导航；Editor 规则 21/21、实际主菜单 Play 1/1、快捷键／GUI 点击 1/1、跨 Editor 重启记忆通过。Player、多人快速局未验。

2026-10-02 [Definition 与 Editor 复评及最终 Review](DEFINITION_EDITOR_REVIEW.md)：Editor 首批 6 项修正已完成，8 组装备／远征能力化缺口与后续执行顺序逐项列明。专项 11/11，生命周期与资产保持 52/52。

2026-10-02 [统一编辑工作台](EDITOR_WORKBENCH.md)：日常配置、预览和正式场景聚合到 `Dark Nights / 工作台`；入口分类、作者来源和保存流程见本页说明。

2026-10-02 当前[工具 Definition 与采集能力](TOOL_DEFINITION_HARVESTING.md)：工具自身配置能力与目标匹配，矿床配置材料与要求，原生 Definition 编辑入口不编译地形。协议 **22**／存档 **v15**／AMP1 schema **2**；先前矿床开关候选已被替代，当前入口见操作指南。

2026-10-01 [地表天空与环境衔接候选](SURFACE_ENVIRONMENT.md)：分支 `ft-20261001-surface-environment`；方案 B 的范围、配置、简单验证及人工清单。自然地表生成重做尚未实施。

2026-10-01 最新矿镐源码候选为 **3 格触及、直线最近表面、持续挥镐与落镐伤害**，见 [DEVELOPMENT.md](DEVELOPMENT.md) 和 [PLAYER_GUIDE.md](PLAYER_GUIDE.md)。游戏协议 **20**／AMP1 schema **2**／存档 **v14**；本批按用户要求未测试、编译、Play 或构建。此前空占用者 GUID 的 28/28 属于此前协议 19 的专项证据，不代表本批通过。

2026-09-30 当前网格业务化源码候选记录于 [DEVELOPMENT.md](DEVELOPMENT.md)、[ARCHITECTURE.md](ARCHITECTURE.md)、[PLAYER_GUIDE.md](PLAYER_GUIDE.md) 和 [YYGC_CHANGES.md](YYGC_CHANGES.md)。协议 **19**／AMP1 schema **2**／存档 **v14**，本批验证 **NOT_RUN**。下方早期切片的协议与计数不代表本候选。

本目录保留当前合同、开发入口与进行中切片。已完成批次的记录见[历史文档索引](archive/README.md)，对应机器证据在 [`archive/evidence/`](archive/evidence/)；历史结论只适用于各自记录的构建与日期。

## 合同与架构（长期有效）

- [ARCHITECTURE.md](ARCHITECTURE.md) — 当前技术架构合同
- [MULTIPLAYER.md](MULTIPLAYER.md) — 联机协议与同步设计（矿镐网格协议 18，Local 显示已验证、联机待验证；旧切片按日期保留）
- [SAVE_FORMAT.md](SAVE_FORMAT.md) — 世界存档格式合同（氧气移除候选v16；下文含历史版本）
- [YYGC_CHANGES.md](YYGC_CHANGES.md) — YYGC 修改授权与改动账本（持续维护）

## 开发入口

- [EMBEDDED_MINERAL_LAYER_PLAN.md](EMBEDDED_MINERAL_LAYER_PLAN.md) — 独立 AnyRuleD 矿层、YYGC 多格矿床、采集同步与恢复的执行方案；2026-10-04 纯规则探针通过，真实素材及玩法未实施；[深度审查](EMBEDDED_MINERAL_LAYER_REVIEW.md)
- [BUSINESS_RULE_AUDIT.md](BUSINESS_RULE_AUDIT.md) — 当前业务规则、隐藏数值、模式生效范围和组件职责问题；2026-09-30 仅审查，未整改或运行验证
- [DEVELOPMENT.md](DEVELOPMENT.md) — 开发执行入口与当前状态
- [QUICK_START.md](QUICK_START.md) — 人工开发快速上手
- [PLAYER_GUIDE.md](PLAYER_GUIDE.md) — 操作说明与本机验收入口
- [DEPENDENCIES.md](DEPENDENCIES.md) — YYGC／AnyRules 依赖准备与锁定
- [SCENES.md](SCENES.md) — Unity 场景目录、入口及工作台／测试用途

## 使用说明

- [MINING_GRID_EXECUTION.md](MINING_GRID_EXECUTION.md) — 白色矿镐网格、远征显示修复、Local 14/14 及尚未执行的采矿／联机清单

- [TERRAIN_GENERATION_MODIFIER_PIPELINE.md](TERRAIN_GENERATION_MODIFIER_PIPELINE.md) — 生成阶段接口 Modifier、入口步道开关、Odin 配置入口与未验收边界
- [CAVE_GENERATION_ALIGNMENT.md](CAVE_GENERATION_ALIGNMENT.md) — 洞穴唯一完整生成入口、旧通路阻断退出、Tuner 原功能保留及人工测试清单

- [RUNTIME_DEBUG_HUB.md](RUNTIME_DEBUG_HUB.md) — F1 调试入口、远征按钮迁移及驾驶台 E 交互
- [SESSION_INPUT_FEEDBACK.md](SESSION_INPUT_FEEDBACK.md) — Smart Console 消息归集、滚轮退出装备选择及返航恢复修复
- [RUNTIME_TERRAIN_TUNER.md](RUNTIME_TERRAIN_TUNER.md) — 新版工作台运行时左栏、分 Tab 调参、拆填与 UI 缩放
- [LAN_SAMPLE.md](LAN_SAMPLE.md) — 独立 LAN 合作联机模板
- [SCENES.md](SCENES.md) — 新版 ReferenceChamber／RandomCave、Cave Wall Tuner 与正式远征入口
- [TERRAIN_GENERATION.md](TERRAIN_GENERATION.md) — 可破坏地形与地图生成器

## 近期切片与候选（2026-09-26）

- [主角比例与移动调整](HERO_MOVEMENT_SCALE.md) — 2026-09-29：3 格高、7/11 格每秒、镜头及工作台同步；v13，按用户要求未测试/构建

- [飞船交易、冲刺与背包改版验收](SHIP_TRADE_EQUIPMENT_ACCEPTANCE.md) / [设计](SHIP_TRADE_EQUIPMENT_DESIGN.md) — 2026-09-29 源码候选：船内 E 出售／购物、Shift、空装备开局、四格与独立喷气能量格；测试结果以验收页为准
- [太空到星球流程](SPACE_TO_PLANET_IMPLEMENTATION.md) / [验收清单](SPACE_TO_PLANET_ACCEPTANCE.md) / [设计](SPACE_TO_PLANET_FLOW_DESIGN.md) — 主分支 `31aaecf` 基线；YYGC 航程、UI Toolkit 星球表格、会话内切图和驾驶降落；Local 编译、Mono r2、纯回归及实际 Play 主流程已执行；多人／弱网等结果持续记入清单
- [主角控制链路前后对照](HERO_INPUT_ARCHITECTURE.html) — 输入采样、命令、服务端授权、ObjectsV2 模拟与镜头表现的交互式职责图；重构后的人工验证待完成
- [输入平台实施前后对照](INPUT_PLATFORM_COMPARISON.html) — 可切换输入流、通用改键和驾驶键扩展的交互式对照；本轮代码已实现，按用户要求未运行 Unity 回归
- [MAP_STATE_NETWORKING.md](MAP_STATE_NETWORKING.md) — AnyRuleD 地图联网重构与本轮验证边界

## 其他

- [项目周报与进度分析 · 2026.09.21–09.27](reports/weekly-2026-09-27.html) — 对照远征营地开发方案的离线交互周报，附 09.28 最新变更、构建证据、差距与下周建议
- `evidence/` — 近期批次的机器证据；历史证据从[归档索引](archive/README.md)进入
- `third-party/` — 第三方许可与声明
