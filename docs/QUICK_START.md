# 开发快速上手

本页是已有工程的最短开发路线。Game为唯一Unity宿主；当前开放范围、版本、源码及待验先读[项目入口](../README.md)、[文档索引](README.md)和[执行状态](DEVELOPMENT.md)。正式从Bootstrap进入星球地面，试玩操作见[Player指南](PLAYER_GUIDE.md)。

## 准备已有工程

1. 在仓库根核对Git分支和未提交修改，保留用户／其他会话编辑；检查C／D磁盘余量和[工作区归属](WORKSPACE.md)。
2. 对照manifest、packages-lock和[依赖说明](DEPENDENCIES.md)。缺少当前锁定输入时依次运行 `pwsh -NoProfile -File tools/grid-business/prepare-dependency.ps1` 与 `pwsh -NoProfile -File tools/prepare-fishnet.ps1`；未知改动不重置，不改用户框架工作区。
3. 用锁定6000.4.9f1打开Game，复用Local已有Library和单一Editor，Linear及现有管线保持。不另复制／链接缓存或并行导入。
4. 等本批必要导入／脚本编译就绪。正式Prefab、场景、Definition、注册及Addressables已存在，普通导入／构建不执行Initialize Environment、Install Initial或一次性校准覆盖作者资产。
5. 获准且通过内存／磁盘门控后，短时从 `Game/Assets/Scenes/Bootstrap.unity` 进入本机Host，按当前切片检查就绪、地面主角和实际操作；不是每次文档／静态修改都启动Play。

当前照明后端首次导入、Shader和画面仍待验，开项目或静态编译成功不等于完成这些检查。测试及构建的有限操作入口见[CLI流程](UNITY_CLI_WORKFLOW.md)。

## 从当前路径理解实现

路径相对 `Game/Assets/DarkNights/Scripts`；旧Godot基线只用于冻结规则／作者来源。

| 顺序 | 入口 | 重点 |
| --- | --- | --- |
| 1 | Entry/GameSessionStartupModule | Bootstrap准备、会话装配及资源所有权 |
| 2 | Runtime/Session/SessionAuthority | 可信连接、策略／租约、请求及去重 |
| 3 | Runtime/Objects/ObjectSession | 组合YYGC能力与对象索引，不另持有运行世界 |
| 4 | Runtime/Objects/HeroInventoryBehaviour及ShipTradeService | 真实持有、购买、支付及装备短事务 |
| 5 | Runtime/Objects/Hero*能力及地形／矿层入口 | 主角输入、动作、权威地图修改与货袋 |
| 6 | Runtime/Objects/ObjectSnapshotMapper及ObjectWorldRestore | 冻结捕获、候选准备和原子恢复 |
| 7 | View／Entry的展示和照明接线 | 只读副本、视图／镜头、后端及生命周期 |

跟一次“E打开商店→购买矿镐”：本地交互形成明确请求，经既有YYGC链进入SessionAuthority，服务端验证权限、位置、价格及库存，在同一事务支付／装配；客户端从回执与冻结投影更新界面。采集再沿真实装备能力及目标地图事务处理。旧住宅建造、训练和自动矿工不作为当前入门操作。

## 按任务找到职责

| 任务 | 目录／合同 |
| --- | --- |
| 对象状态、装配或程序集 | Runtime/Objects、Framework及[架构](ARCHITECTURE.md) |
| 权限、输入、同步、Ready、重连 | Runtime/Session、Network；View/GameInputActions及Entry接线；[联机](MULTIPLAYER.md) |
| 工具参数／目标匹配 | Res/Objects所属Definition；[工具能力](TOOL_DEFINITION_HARVESTING.md) |
| 地图生成、坡形、矿层、装卸 | Runtime地形适配及纯Core算法；[地图合同](TERRAIN_GENERATION.md) |
| 照明／材质／挂点 | Res/Shared/Lighting及对象Prefab、View、Entry；[照明](LIGHTING.md) |
| UI、场景、草稿与作者资源 | Res所属目录及原生工具；[工作台](EDITOR_WORKBENCH.md) |
| 保存格式和关系 | Core/Save、Runtime/Save及Objects捕获／恢复；[文件合同](SAVE_FORMAT.md) |
| 框架通用缺口 | 先只读调查并列具体文件／原因／落点／验证，按AGENTS取得范围同意 |

Core仅纯计算和冻结合同，业务不迁回旧Core运行世界。C#9、.NET Standard 2.1、中文summary及手写单文件300行上限见[AGENTS](../AGENTS.md)。

## 首批检查与交付

按影响安排规则／架构、Editor、场景／Prefab、独立Host＋Client及真实文件恢复；暂停业务的旧矩阵不自动加入。需要新Player时默认Mono，每配置只构建一次并复用完整产物。源码／依赖／配置不同则旧Player通过数不能替代；IL2CPP、前台性能和双机器按各自条件。

只提交本次授权源码／配置／文档及meta，不提交缓存、用户存档、密钥或本机设置，不推送远端。产物归属及保全见WORKSPACE。日常操作通过CLI串行，不因超时重发已经消费的请求；文档修改只报告文档核验。
