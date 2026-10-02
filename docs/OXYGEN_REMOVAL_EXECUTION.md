# 氧气业务移除：源码候选与待验证清单

2026-10-03后续：用户恢复验证并授权转用Local后，已完成后台验证：Editor119/121、原生资源12/12、正常Mono双进程130/130；弱网失败，整批未通过。原两项旧路线用例已按用户要求移除：角色矿房交互、矿工采集交货尚待实际业务设计；本次剩余远征回归6/6，历史失败报告保持原样。结果见[验证记录](OXYGEN_REMOVAL_VALIDATION.md)，具体原因见[失败分析](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md)。

源码实施基于已提交基线baedc158b3d1b5616cf6118765a3c776b416f64d，在独立薄worktree分支ref-20261003-remove-oxygen完成。验证时复用Local现有Unity缓存；前一任务工作已完整备份并暂存，没有复制或覆盖其未提交修改。源码阶段原本暂不验证的状态已被上述后续结果替代。

游戏协议 **23**／存档 **v16**／AMP1 schema **2**。YYGC 源码、版本及补丁均未修改；[AGENTS](../AGENTS.md) 中“修改 YYGC 必须事先取得具体范围的用户同意”继续有效。

Local已在用户授权后转为本氧气候选，未合并、推送或代改YYGC。旧任务HEAD、保护分支、stash与26项文件备份均保留，详见验证记录。未来与main集成仍应审查共同文档、存档指纹和协议修改，不直接覆盖其他任务成果。

## 已实施范围与定位

| 职责 | 修改结果 | 主要文件 |
| --- | --- | --- |
| 远征规则 | 删除耗氧、补氧、缺氧扣血、缺氧死亡登船／丢货；删除初始化和复活填氧 | ExpeditionOperations、ObjectCampCommands |
| 设备与矿工 | 删除氧气站及额外站部署、OxygenAt、RequestRelay、低氧返程、强制氧气返船与氧气撤收优先级 | ExpeditionDevices |
| 配置 | 删除 buildings.oxygen、OxygenSeconds、OxygenRadius；通用 RelayRange 改为 PowerLinkRange，仍为360 | balance.json、ExpeditionDefinition、GameCatalogJson、SaveContentFingerprint |
| 权威与数据合同 | 删除 Oxygen 字段及捕获、恢复、网络投影、JSON和校验，不留下停用字段 | ActorState、ExpeditionActorData、ExpeditionMapping、ExpeditionActorWire、ExpeditionSaveJson、ExpeditionValidator |
| 命令及表现 | 删除 relay 的许可、分派、HUD命令、两套 Prefab按钮及氧气显示；船HUD16项、远征面板11项 | SessionOperations、SessionExpeditionControl、ExpeditionHud、ExpeditionPanel、ShipSceneAssetSetup、ShipHud.prefab、Expedition.prefab |
| 正式资源 | 移除 DefinitionDatabase 与 Addressables 的氧气注册；资产原样移出Assets，保留GUID、人工内容和源图 | [资源退役清单](archive/retired-assets/README.md) |
| 制作及回归入口 | 首版安装器不再创建氧气站／中继；诊断不再读取氧气；相关构造调用调整 | tools/expedition/ExpeditionInstall.cs、ShipDiagnostic、PlanetFlowRegression |
| 原死亡回归 | 缺氧死亡用例改为通过现有通用伤害／生命周期触发战斗死亡，保留结算失败、复活、写盘和移动断言；已更新并执行通过 | ExpeditionRecoveryTests、JourneyWalkwayTests |
| 兼容界限 | 协议22客户端及v15存档不兼容；不迁移、不删除用户旧档。依赖锁中游戏版本同步，框架提交及补丁哈希不变 | SessionAuthority、SessionSnapshot、ObjectWorldSaveJson、tools/grid-business/dependency.lock.json |

所有业务继续由原 YYGC Behaviour／State 拥有，未新增规则总开关、空实现、插件调度或另一套状态系统。以后重新加入氧气应依据新玩法定义自己的状态与规则边界，再接入现有权威事务、投影和存档；本次提交与退役资产可追溯旧实现，但不建议整段直接还原。

## 保留行为及可见影响

- 仓储、炮塔、灯相对飞船的部署坐标继续为-50、+30、+110；功耗仍为1、4、1，供电预算仍为12。
- 氧气站删除后释放原3点功耗，船员舱额外站也不再消耗3点。已供电设备每台贡献0.15的风险倍率仍保留，少一／两站会相应减少0.15／0.30；实际部署期间的贡献随供电阶段变化。
- 船员舱继续提供矿工；货舱、机器人舱、侦察机、搬运、供电、满包卸货、撤收、登船和正常／紧急起飞继续使用原规则。矿工无有效矿床时的卸货路径也保留。
- 敌人伤害及“全部所属玩家死亡→Settle→ResetDock／船内恢复”仍是独立业务，所以以后受战斗伤害仍可能回船。移除氧气不能等同于删除所有失败返航。
- 风险达到阈值生成敌人的规则仍保留，旧默认阈值90未改。验证“长时间不因氧气回船”时应隔离敌人影响，另行验证战斗死亡。
- 已发现的独立问题：结算后ExpeditionPhase=4但Journey仍为Landed，再次出发入口可能无法重新推进。此问题在移除前已存在，本切片未修；不能宣称再次远征已通过。
- 原始氧气图像及历史生成脚本继续作为资料保留，不注册为氧气设备。退役Definition不再被Unity导入，避免不存在的RuleKey造成作者配置错误。

## 原实施阶段验收计划（现执行状态以验证记录为准）

下表保留原实施阶段的范围与预期，不代表各项全部通过；已执行、失败及尚未执行项见本页顶部验证记录。验证使用Local单一Unity通道，薄worktree没有生成第二套Unity缓存。

| 项目 | 预期及边界 |
| --- | --- |
| 锁定依赖恢复、生成和Editor编译 | 复用现有锁定YYGC；ActorState复制／MemoryPack／绑定按正常入口重建，Core、Runtime、View、Entry、Editor与Tests全部编译，无旧Oxygen引用 |
| Definition／Addressables／Prefab导入 | 氧气无运行注册和悬空GUID；两套HUD分别16／11命令，绑定等长；实际保存重开后无Missing引用、按钮空位及布局异常；人工资源其余GUID不变 |
| 新开局与长时间步行 | 正常到达、着陆、下船；隔离敌人后离船超过原120秒及原缺氧死亡时长，无持续耗氧、无氧气扣血或自动返船；多人分别观察 |
| 三种设备与舱段 | 部署及撤收位置保留；机器人／货舱／船员舱可执行；船员舱只生成矿工；供电预算、范围和风险变化符合剩余设备数 |
| 矿工／搬运／侦察机 | 不因原25氧气阈值返船；采集、满包／无矿卸货、寻路、仓储供电、搬运优先顺序与召回正常 |
| 独立失败与返航 | 战斗死亡仍经通用伤害／生命周期；全员死亡结算、货物损失、租约更新与复活移动；正常／紧急返航和保存异常回滚，更新后的Recovery与Walkway用例 |
| 存档v16 | 真正写盘、退出重启、地面／空中／结算恢复，冻结副本与原子失败；v15拒绝读取且原文件保留，恢复无氧气字段 |
| 协议23与多人 | Mono一次构建复用；独立Host＋Client、晚加入、重连、暂停、权限与epoch、正常及弱网投影；旧协议22拒绝混连；AMP1地图合同不变 |
| 再次远征 | 先复现并单独处理上述phase4／Landed入口问题，再验证完整第二轮；不把已知问题写成通过 |
| 人工画面与其他平台 | 氧气文案／操作消失，其余HUD可用；真实前台画面、性能与双机器仍待验。IL2CPP必须另行取得用户明确同意，Mono结果不能代替 |

静态审阅只核对源码调用、版本引用、JSON文本、Prefab删除子树与剩余绑定、资源移出后的引用及Git差异。它不代替上述编译、Unity资源和真实运行验证。
