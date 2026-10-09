# 可复用照明组件与手电道具

> 本页保留原批次日期、源码／Player身份、协议和实际验证结果。2026-10-09整合后，现行职责由[合同／操作入口](../LIGHTING.md)承接；未完成项查[当前执行状态](../DEVELOPMENT.md)。归档不核销待验或借用旧通过数。

2026-10-09，分支 `ft-20261008-flashlight-lighting`。本批将光效拆成共用Prefab，手电本体进入四格库存；协议 **31**／存档 **v23**／AMP1 schema **2**。Unity6000.4.9f1、Linear和锁定YYGC保持，没有修改框架、依赖版本或原生素材字节。

## 资源与复用

- 共用效果：`Game/Assets/DarkNights/Res/Shared/Lighting/LightEffect.prefab`。
- 头部挂载变体：同目录 `HeadMountedLightEffect.prefab`，继承共用效果，仅覆盖灯口位置。
- 手电道具：`Game/Assets/DarkNights/Res/Objects/Flashlight/Flashlight.asset`／`Flashlight.prefab`，保留原GUID、外观和物理灯口，内嵌共用光效。

`LightEffect`组合`LightEnvironmentEmitter`与`LocalLightFill`。环境分支读取真实灯口、方向与地形遮挡；补光分支只作用于明确绑定的SpriteRenderer，不写环境光场或作者颜色。两个分支分别可禁用，总开关关闭两者。

制作矿工帽时，在帽子挂点下嵌入头部变体，将Environment Origin放在灯口，将Local Fill Origin放在补光位置，并在LocalLightFill的Targets绑定帽子、头部或身体精灵。空Targets不产生局部补光，固定灯具可只启用环境分支。运行时挂载调用`LightEffect.Bind(context, occlusionAnchor, fillAnchor, receivers)`；静态场景光源可保留场景引用，自动登记至同场景的正式相机照明入口。持有、权限或供电由业务对象判断，再传入`SetOn`。本批提供头部光效变体，不新增帽子产品。

光源方向支持移动、旋转及左右镜像。Worker、Spearman、Archer均显式绑定灯口、补光锚点和受光精灵，保留已有作者引用。每台游戏相机仍共享一份384×216光场、地形缓存与最多16个环境光源；效果实例不单独创建全屏缓存。每个精灵最多合成4个局部补光贡献，保留其他材质属性，关闭、移除或退休时清除贡献。MaterialPropertyBlock在首次主线程合成时创建，避免MonoBehaviour构造阶段调用原生API。

环境距离14格、锥角90度、强度1.35及暖色保留。原近身补光2.2格／0.55转入指定对象补光，环境近身散光默认0，避免同一补光重复加入光场。半径按当前地形XY比例换算，支持非等比变换。原灯口贴墙限制、未知区遮挡、柔影与墙内补光上限继续用于环境照明；局部补光是目标限定的艺术表现，不承担权威判断。

视觉参数唯一保存在共用效果Prefab，Runtime配置仅声明照明道具能力与新角色配发标记。安装入口为`Dark Nights → Tools → 安装可复用照明与手电道具`，通过Editor API批量保存；普通启动或构建不重建作者资产。复用原6×3像素素材，无需生图。

## 库存、状态与兼容

手电继续使用YYGC Definition、ObjectInstance、Behaviour与主视图；效果是普通View子组件，无第二份权威状态。正式新角色只在创建时通过库存事务配发一件，占一格；重新接管或恢复已移除手电的角色不会再配发。快速测试预置先给矿镐再配手电，保留矿镐原槽位。

照明引用必须指向实际持有的道具。添加、重复、容量、移除和调试权限沿用既有事务；移除清引用／开关并释放实例。切换矿镐／手枪时手电继续工作，F仍走可信控制入口。选中手电时获得其他手持工具，会切到新工具，照明保持。HUD、商店持有区和调试装备区均识别手电。

库存能力与商品规则分开：手电商品键为空，通过配发或现有房主调试添加；没有新增商品、价格、电池或掉落拾取。协议31新增槽位视觉类型4及库存持有校验，旧普通操作编号和Debug1000–1004保持。v23沿用JSON形状，强化库存／照明关系，保存身份与开关，恢复方向并重建缓存。正式目录自动使用`Saves/v23`；v22及更旧文件保留、不迁移、不覆盖。

## 本批验证

真实Editor最终编译0错误／0警告。手电专项10/10通过，正式Bootstrap短时探针1/1通过：占格、真实装配、可信开关、目标补光清理、添加／切换矿镐后继续照明、移除释放和退出恢复。128×64实际Sprite Shader探针验证绑定精灵变亮、邻近未绑定精灵不受补光、关灯恢复；头部挂点移动／旋转／镜像及格尺寸换算通过。真实640×360游戏相机开／关图已检查，仅代表本机短时场景。

首轮扩展35项为30通过／5失败；修复新增登记及请求形状后按影响复验。最终合并33个不同用例通过，3个旧用例失败留账：`HeroRecoveryTests.DeathRemovesPossessionWithoutAnOrphanedPlayerIndex`依赖暂停的StartNight刷怪，`ShipTradeTests.ShopAndSaleUseLeasePositionAndAtomicState`依赖暂停的出售，`ShipTradeTests.ShopReservesFirstPickaxeBudget`依赖现有未实现预算规则。对应业务入口本批未改，不能宣称全部回归通过。

静态Editor／Development／Release编译与架构摘要见`docs/evidence/reusable-light-20261009.json`。首次装配监控在提交余量3.229GiB时触发4GiB停止线；装配已完成但当时未提交测试。余量恢复后在新监控下运行有限回归。保留全部失败、编译修正和Play修正记录。无Player构建、独立进程联机、IL2CPP、双机器或长期性能验收。

原始结果在`artifacts/reusable-light-20261009/`；Runner原始XML与摘要按批次身份保留在`artifacts/terrain-final-20260926/`，任务目录另存副本。过期可重建编译副本集中保留至`artifacts/待清理/20261009-reusable-light/`并写清单，不删除且不计释放空间。
