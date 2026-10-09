# 工具Definition、装备与采集能力

本页维护静态工具能力、装备身份、动作与权威采集的关系。前景／矿层状态见[地形合同](TERRAIN_GENERATION.md)，文件字段见[存档合同](SAVE_FORMAT.md)，操作和当前验收分别见Player指南及执行状态。旧独立矿床实例与暂停自动矿工不是正式运行模板。

## 人工配置

从 `Dark Nights → 工作台 → 对象与装备` 打开原生YYGC Definition Workshop，也可Project双击定义。游戏侧采集辅助区只校验装配／白名单／匹配，不提供第二套配置编辑或保存。

| 配置 | 用途与边界 |
| --- | --- |
| 正式矿镐 `Res/Objects/ShipTrade/item-pickaxe.asset` | BehaviourTypes装配MiningToolBehaviour，SharedConfigs保存MiningToolConfig |
| Targets | Foreground=1、MineralDeposit=2，当前矿镐为3；支持前景及露出的矿层目标，不表示允许隔墙采矿 |
| AllMaterials／Materials | 全支持或按稳定材料Key白名单；关闭且列表为空表示不支持任何材料 |
| Deposits | 可选矿床Definition引用白名单，空列表表示所有定义；由初始静态元数据匹配，不按实例ID声明能力 |
| Level／RequiredMiningLevel | 工具等级与目标静态需求匹配；能力通过仍须距离、遮挡、版本和容量验证 |
| Damage／Reach／HandHeight／Seconds／ImpactFraction | 当前作者值10／64／36／0.48／0.6；吸附与挥砍共用Reach，64逻辑像素为4玩法格 |
| 矿床Definition | 保留材料、等级、耐久、产量及来源；不拥有当前最终矿格状态，AllowPickaxeHarvest旧开关已退出 |

例如仅采铁矿层：启用MineralDeposit，关闭AllMaterials，Materials填iron，需要限制特定来源时选择Deposits定义。还需采岩壁则启用Foreground并列出相应Key。前景采用AnyRuleD材料身份，矿层来自其冻结资源／材料规则。

退出Play再编辑，校验／保存归Workshop，新会话读取冻结规则；工具配置不编译地形或创建地图目录。只读匹配预览复用MiningToolRules，但不能替代真实输入及服务端采集。原距离调整及工具批次来源见[统一距离](archive/PICKAXE_UNIFIED_REACH.md)、[工具机器记录](archive/evidence/tool-definition-harvesting-20261002.json)。

## 状态与装配合同

工具沿YYGC RequireConfig／Inject、生成注册、Definition、ObjectInstance和主视图装配。库存槽唯一权威值为canonical Definition GUID；Runtime冻结目录解析定义，Core／存档／线缆只保存引擎无关GUID字符串。视觉枚举由目录派生，相同外观可以有不同定义身份。

ObjectSessionResources启动冻结装备目录并预加载；角色能力缓存只持有当前工具引用，不复制背包状态。换装、角色退池、加载及会话退休释放原装配。工具没有新增独立耐久，动作计时、选择版本、货袋及装备可变状态仍归ActorState。

新增定义由正常Unity导入meta及现有Definition身份入口登记，已有GUID、Prefab和素材保留。炸弹沿既有item.bomb身份与原数值，不新增商品或价格。作者定义能力与商品规则分开，不因网格收录而开放购买或生成。

## 目标、动作与事务

选取和服务端执行共享MiningToolRules.BlockReason，比较目标类型、实际材料、等级及可选来源定义；服务端从真实装备槽解析能力，不接受客户端自报伤害／工具权限。

每轮冻结工具身份、选择版本、目标及瞄准角。落镐时重新验证目标和地图代次／内容版本；失效不改打后一层，下一轮才重新选择。换装、取消、死亡、登船、权限撤销、输入超时及世界退休撤销未命中动作。

前景耐久和矿层耐久／储量均由所属原生地图权威事务修改，不创建逐格或逐矿床对象。奖励与角色货袋同一短事务结算，最后一份竞争和重发输入不重复发收益。前景遮挡、未知格及距离规则见[采集事务](TERRAIN_GENERATION.md#采集事务)。

自动矿工伤害标定的历史适配和独立工作台默认工具来源继续保留，暂停自动派工不因工具配置编辑而恢复。手持枪弹／炸药／投射物沿原会话能力，不把动画或碰撞回调当伤害结算。

## 装备与照明关系

正式四格库存包含手枪、矿镐、炸弹及手电等实际持有定义；数量、重复、容量、选择和移除由同一库存事务验证。喷气背包为独立能力，不占四格，装备／燃料仍归角色状态。

新角色仅创建时配发一件手电，占一格；接管或恢复不重新配发已移除手电。快速测试先给矿镐再配手电，保留矿镐槽位。照明引用必须指向实际库存道具，移除清引用／开关并释放实例。切矿镐／枪照明继续，F走可信控制入口；选择手电时获取其他手持工具可切到新工具，照明保持。

手电商品键为空，配发或现有房主调试添加；不新增帽子、电池、掉落或价格。光效只消费冻结持有、开关与方向，详细复用和本地后端见[照明合同](LIGHTING.md)。

## 验证边界

当前源码／Player验收只在[执行状态](DEVELOPMENT.md)登记。2026-10-02工具批次62个不同Editor及指定Mono正常网络27项、弱网重连失败、Core两项旧断言和架构既有问题保留在原机器记录与Git基线，不据此签署当前四人、第二工具跨进程或照明恢复通过。

当前需按影响覆盖真实不同能力Definition装配、同视觉不同身份、持有关系、切换／移除取消、最后一份竞争、去重、保存与新epoch恢复；正式手感及跨进程不以只读匹配窗口替代。
