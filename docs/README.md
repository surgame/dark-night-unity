# Dark Nights 文档索引

本页负责导航。当前源码身份、完成情况和待验项统一见[DEVELOPMENT](DEVELOPMENT.md)，开放／暂停玩法见[地面基础玩法](GROUND_GAMEPLAY_BASELINE.md)。实施记录的日期、协议、Player和通过数只代表其原输入。

## 按任务阅读

| 任务 | 入口 |
| --- | --- |
| 开始日常开发 | [快速上手](QUICK_START.md)、[执行状态](DEVELOPMENT.md) |
| 确认阶段目标、待决问题与并行模块 | [矿洞远征核心闭环目标](EXPEDITION_CORE_LOOP_GOALS.md)、[长期模块计划](ROADMAP.md)、[阶段台账](DEVELOPMENT.md#stage-cl) |
| 准备MVP发布并核对一次基础远征体验 | [MVP版发布计划](MVP_RELEASE_PLAN.md)，含暂选补给电脑、正式航行至结算的QA和待讨论地下路线；不代表已构建／发布或CL阶段结束 |
| 审阅固定船舱、交货出售及星图导航方案 | [飞船远征与星图导航策划草案](SHIP_EXPEDITION_DESIGN.md)，含全员抵达时间、动态距离、危险mock和后续规则；已激活普通交货首片，实现及验证边界见执行台账 |
| 开展飞船远征与星图开发 | [开发执行方案](SHIP_EXPEDITION_IMPLEMENTATION.md)，含现有源码复用、YYGC状态／命令／事务、联机／存档、切片依赖及授权后的验收门槛 |
| 修改状态、对象装配或程序集 | [技术架构](ARCHITECTURE.md) |
| 评审地图工作台整体重做 | [地图工作台模块重构](ROADMAP.md#cl-map-workbench-redesign)、[本轮状态](DEVELOPMENT.md#cl-map-workbench-redesign-status)；四区模块界面已按2026-10-28确认方案进入工程，操作见EDITOR_WORKBENCH；编译与待验状态分列于台账 |
| 地图生成与基本／高级工作台重构 | [F01～21、影响矩阵及GF-01～05方案](ROADMAP.md#cl-map-generation-flow-refactor)、[实际状态与无船衔接](DEVELOPMENT.md#stage-cl)；2026-10-28用户已确认实施，原型或编译不替代完整验收 |
| 修改权限、同步、Ready或恢复 | [联机合同](MULTIPLAYER.md)、[存档合同](SAVE_FORMAT.md) |
| 修改地图、碰撞、矿层或采集 | [地图与地形](TERRAIN_GENERATION.md)、[工具能力](TOOL_DEFINITION_HARVESTING.md) |
| 配置固定／随机／种子池并保存地图 | [统一种子策略](TERRAIN_GENERATION.md#统一种子策略与预制地图输出)、[生成与历史身份边界](TERRAIN_GENERATION.md#制作配置生成与历史身份边界)、[工作台操作](EDITOR_WORKBENCH.md#cl01-tuner-uitk-design)；旧H5底图输入已退场，游戏继续加载预制结果 |
| 试玩武器、道具与场景玩点候选 | [独立H5内容实验室](../experiments/expedition-playground/README.md)，含48入口、新增六类局部洞穴／三件装备／组合试采、工程素材实景、人工评审与导出；不是Unity正式导入 |
| 制作天空、植被与真实地表起伏 | [现行制作合同](TERRAIN_GENERATION.md#surface-environment-authoring)、[Tuner操作与保存域](EDITOR_WORKBENCH.md#surface-environment-workbench)、[切片方案](ROADMAP.md#cl-surface-environment-plan)、[本片台账](DEVELOPMENT.md#cl-surface-environment)；同图衔接地形、环境、装饰与预制地图加载 |
| 审阅CL01／04近壁盲盒矿团与侧巢 | [近壁三候选](TERRAIN_GENERATION.md#cl01-near-wall-mining)、[薄壳动态出没](TERRAIN_GENERATION.md#cl01-nest-breach)、[多方向与矿巢共生](TERRAIN_GENERATION.md#cl01-nest-orientations)、[六情境离线样板](../experiments/nest-orientations-v1/README.md) |
| 制作矿巢地图及进入新局 | [地图设计工作台](EDITOR_WORKBENCH.md#cl01-tuner-uitk-design)与[规则制作流程](EDITOR_WORKBENCH.md#cl01-vein-tuner)、[正式单图首片](TERRAIN_GENERATION.md#cl01-runtime-joint-map)，来源、候选、冻结与新局装配分离；实际GUI／运行验收及完整R3待办见台账 |
| 制作塌方设计点及查看接入边界 | [Tuner字段与操作](EDITOR_WORKBENCH.md#cl02-map-design-point)、[开发交接与当前合同](TERRAIN_GENERATION.md#cl02-map-integration-handoff)、[独立场景操作](EDITOR_WORKBENCH.md#cl02-collapse-lab) |
| 修改照明、挂载或受光材质 | [照明合同](LIGHTING.md) |
| 修改F1面板及房主调试操作 | [Debug Hub规范](RUNTIME_DEBUG_HUB_SPEC.md) |
| 操作编辑器、场景、预览或诊断工具 | [编辑工作台](EDITOR_WORKBENCH.md) |
| 查正式地图与场景装配、配置生效来源及工作台缺口 | [现行业务模块与接入边界](EDITOR_WORKBENCH.md#workbench-integration-audit)、[实施范围](ROADMAP.md#workbench-integration-candidates)；编译与实际待验见DEVELOPMENT |
| 测试、构建、内存保护及薄worktree验收 | [Unity CLI流程](UNITY_CLI_WORKFLOW.md) |

## 长期合同

| 文档 | 唯一职责 |
| --- | --- |
| [地面基础玩法](GROUND_GAMEPLAY_BASELINE.md) | 当前开放／暂停范围与恢复条件 |
| [技术架构](ARCHITECTURE.md) | 状态所有者、程序集、装配、事务和生命周期 |
| [联机设计](MULTIPLAYER.md) | 可信连接、权限、请求、同步、Ready与epoch |
| [存档格式](SAVE_FORMAT.md) | 文件字段、关系约束、冻结捕获及原子恢复 |
| [地图与地形](TERRAIN_GENERATION.md) | 两层权威地图、生成、碰撞、装卸和采集 |
| [工具Definition与采集能力](TOOL_DEFINITION_HARVESTING.md) | 工具配置、目标匹配、装备身份与动作事务 |
| [照明](LIGHTING.md) | 光效复用、后端、材质、挂载和本地资源生命周期 |
| [手电返光优化后续目标](LIGHTING_PRUNING_FOLLOWUP.md) | 剪枝及复用、GPU／至少四人性能、候选预算与性价比评估；用户暂缓，已列入CL阶段待验 |
| [Debug Hub规范](RUNTIME_DEBUG_HUB_SPEC.md) | 面板注册、界面、输入和调试操作合同 |
| [依赖说明](DEPENDENCIES.md) | 现行依赖来源、锁定配置和准备入口 |
| [YYGC改动账本](YYGC_CHANGES.md) | 具体授权、逐文件差异、补丁来源及验证边界 |

## 操作与目录

[快速上手](QUICK_START.md)面向开发者；[Player指南](PLAYER_GUIDE.md)面向当前试玩操作；[编辑工作台](EDITOR_WORKBENCH.md)集中场景目录、编辑、Play预览与只读诊断；[Unity CLI](UNITY_CLI_WORKFLOW.md)描述执行通道；[工作区说明](WORKSPACE.md)管理目录和产物归属。

## 历史与证据

- [归档索引](archive/README.md)提供历史摘要、实施记录、失败、未完成验收和用户周报；其中待验项是否适用于当前范围以执行状态为准。
- [截至2026-10-25的执行流水](archive/DEVELOPMENT_LOG_20261026.md)承接原DEVELOPMENT中的逐批过程；当前状态页只保留模块结论、未解决失败及下一步。[2026-10-26整理回执](archive/WORKSPACE_TIDY_20261026.md)记录临时验证资产的迁移与恢复入口。
- [证据目录](evidence/)保留近期批次机器摘要，具体来源从执行状态及对应归档进入。[历史证据](archive/evidence/)继续保存原始身份。
- [LAN Sample](samples/LAN_SAMPLE.md)是独立模板，正式游戏不反向引用其代码、注册或资产。
- [第三方声明](third-party/)持续保留许可和来源。

## 维护方式

根README介绍项目和入口，本页只维护导航，DEVELOPMENT只维护当前状态。ROADMAP长期维护模块、依赖与后续候选，阶段目标维护目标、决策、问题和临时约束；两者不重复实现进度。长期合同描述现行不变量，操作手册描述现行操作；历史阶段进度、源码SHA、测试计数和旧Player记录进入归档。新增小切片先更新所属合同和状态，不按每次任务增加顶层文档。阶段结束须由用户明确确认，再解除该阶段约束并承接遗留项。移动记录前先承接有效规则与待验项，并修正引用；历史证据不重生成。
