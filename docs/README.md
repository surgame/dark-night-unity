# Dark Nights 文档索引

本页负责导航。当前源码身份、完成情况和待验项统一见[DEVELOPMENT](DEVELOPMENT.md)，开放／暂停玩法见[地面基础玩法](GROUND_GAMEPLAY_BASELINE.md)。实施记录的日期、协议、Player和通过数只代表其原输入。

## 按任务阅读

| 任务 | 入口 |
| --- | --- |
| 开始日常开发 | [快速上手](QUICK_START.md)、[执行状态](DEVELOPMENT.md) |
| 修改状态、对象装配或程序集 | [技术架构](ARCHITECTURE.md) |
| 修改权限、同步、Ready或恢复 | [联机合同](MULTIPLAYER.md)、[存档合同](SAVE_FORMAT.md) |
| 修改地图、碰撞、矿层或采集 | [地图与地形](TERRAIN_GENERATION.md)、[工具能力](TOOL_DEFINITION_HARVESTING.md) |
| 修改照明、挂载或受光材质 | [照明合同](LIGHTING.md) |
| 修改F1面板及房主调试操作 | [Debug Hub规范](RUNTIME_DEBUG_HUB_SPEC.md) |
| 操作编辑器、场景、预览或诊断工具 | [编辑工作台](EDITOR_WORKBENCH.md) |
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
| [Debug Hub规范](RUNTIME_DEBUG_HUB_SPEC.md) | 面板注册、界面、输入和调试操作合同 |
| [依赖说明](DEPENDENCIES.md) | 现行依赖来源、锁定配置和准备入口 |
| [YYGC改动账本](YYGC_CHANGES.md) | 具体授权、逐文件差异、补丁来源及验证边界 |

## 操作与目录

[快速上手](QUICK_START.md)面向开发者；[Player指南](PLAYER_GUIDE.md)面向当前试玩操作；[编辑工作台](EDITOR_WORKBENCH.md)集中场景目录、编辑、Play预览与只读诊断；[Unity CLI](UNITY_CLI_WORKFLOW.md)描述执行通道；[工作区说明](WORKSPACE.md)管理目录和产物归属。

## 历史与证据

- [归档索引](archive/README.md)提供历史摘要、实施记录、失败、未完成验收和用户周报；其中待验项是否适用于当前范围以执行状态为准。
- [证据目录](evidence/)保留近期批次机器摘要，具体来源从执行状态及对应归档进入。[历史证据](archive/evidence/)继续保存原始身份。
- [LAN Sample](samples/LAN_SAMPLE.md)是独立模板，正式游戏不反向引用其代码、注册或资产。
- [第三方声明](third-party/)持续保留许可和来源。

## 维护方式

根README介绍项目和入口，本页只维护导航，DEVELOPMENT只维护当前状态。长期合同描述现行不变量，操作手册描述现行操作；阶段进度、源码SHA、测试计数和旧Player记录进入归档。新增小切片先更新所属合同和状态，不按每次任务增加顶层文档。移动记录前先承接有效规则与待验项，并修正引用；历史证据不重生成。
