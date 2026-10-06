# 可破坏地形与地图

现行正式星球地图从 `WorldSession` 的航程／地图配置生成，预览与正式远征复用同一完整生成入口。主菜单玩法从 Bootstrap 装配；工作台的拆填只修改预览，不能作为正式挥镐与奖励事务验收。

## 当前职责

| 层 | 状态与用途 |
| --- | --- |
| 前景 | 会话托管 ARDMap，格业务状态由 GridBusinessStateStore 拥有；权威移动查询真实坡形与格碰撞 |
| 矿层 | 独立地图保存矿种、耐久与储量；矿床身份只作初始静态元数据，不创建正式矿床实体 |
| 静态背景 | 从冻结初始参考生成，局部换区不重建或随前景破坏消失 |
| 表现 | 客户端只读两层局部副本，AnyRuleD 按可见页求解与绘制，Unknown 不当作空格 |

前景与矿层客户端按可信角色或船体位置订阅附近区块；房主常驻有界权威数据。普通采集修改一层地图及角色状态，安装后再通知。拆墙不消耗后面的矿物；采空矿格明确保存，恢复不能从初始元数据补回。

## 配置与入口

- [场景索引](SCENES.md)：RandomCave、ReferenceChamber、正式远征和专用探针。
- [编辑工作台](EDITOR_WORKBENCH.md)、[运行时预览](RUNTIME_TERRAIN_TUNER.md)：地图、岩壁、背景与航程草稿。
- [生成 Modifier 过程与合同](archive/TERRAIN_GENERATION_MODIFIER_PIPELINE.md)、[统一生成记录](archive/CAVE_GENERATION_ALIGNMENT.md)：完整生成入口及配置来源。
- [工具与采集能力](TOOL_DEFINITION_HARVESTING.md)：工具自身的材料／等级／范围配置。
- [原生矿层实现](MINERAL_MAP_MIGRATION_IMPLEMENTATION.md)：当前网络、Ready、保存与验收边界。

原地图初次接入、旧逐床对象方案、独立探针数字及早期性能策略保存在[地形历史摘要](archive/TERRAIN_HISTORY.md)。当前完整验证状态见[开发执行计划](DEVELOPMENT.md)，不沿用旧地图 Player 的计数。
