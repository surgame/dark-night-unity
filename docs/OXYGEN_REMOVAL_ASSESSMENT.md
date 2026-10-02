# Dark Nights 氧气业务移除评估

2026-10-03。用户要求当前真正移除氧气及关联业务，后续根据实际玩法重新接入；本轮要求再次评估，并将修改 YYGC 必须事先取得明确同意写入 AGENTS。约束已更新，本报告仅评估，未删除生产业务、修改资源、升级协议或运行验证。

结论：可作为独立功能切片完整移除，影响面中等、边界可追踪。氧气在现有游戏代码中执行，暂无必须修改 YYGC 的具体缺口；按游戏侧移除方案推进，不预留氧气开关、空实现或占位字段。当前读取到游戏协议22／存档v15，最终实施时以当时版本为基准升级。

## 移除范围

| 范围 | 必须退出的内容 | 主要落点 |
|---|---|---|
| 生存结算 | 耗氧、船内／坡道／设备补氧、缺氧扣HP、缺氧死亡标记登船和货物损失 | [远征运行][OPS] 142–154 |
| 初始化与恢复 | 出生、着陆、矿工生成、返航复活填氧以及氧气恢复映射 | [OPS]、ObjectCampCommands、[DEV]、ExpeditionMapping |
| 矿工任务 | Oxygen<25造成返程及强制选择飞船；删除两处条件 | [DEV] 138、144；保留满包、撤收和无矿可采时的返程 |
| 设备业务 | 氧气站、第二氧气站、OxygenAt、RequestRelay、氧气功耗和氧气后收优先级 | [DEV] 36–68、87、107 |
| 命令与UI | relay请求白名单、可信命令分派、操作按钮、氧气数值和中继文案 | SessionOperations、SessionExpeditionControl、ExpeditionOperations、ExpeditionHud、ExpeditionPanel、[UI] |
| 作者规则 | buildings.oxygen、OxygenSeconds、OxygenRadius及构造参数、JSON解析和指纹项 | [BAL]、ExpeditionDefinition、GameCatalogJson、SaveContentFingerprint |
| 状态合同 | ActorState.Oxygen、ExpeditionActorData.Oxygen、对应网络和JSON字段、捕获恢复及校验 | [STATE]、ExpeditionActorData、ExpeditionActorWire、ExpeditionSaveJson、ExpeditionMapping、ExpeditionValidator |
| 正式注册 | expedition.oxygen Definition、Prefab的正式加载和Addressables条目 | [DATABASE]、[ADDRESS]、[OXYGENDEF]；核对GUID引用后退出正式内容 |
| 工具与验证 | 现行制作入口不再重新创建氧气站；删除氧气专属用例和断言 | ShipSceneAssetSetup、现行安装工具、ExpeditionRecoveryTests、JourneyWalkwayTests及其他构造调用 |

游戏侧生成状态或序列化代码若受手写合同变化影响，按现有生成入口重建；不手改生成输出，不因此修改 YYGC 生成器。原始美术、历史制作脚本及冻结验收证据可继续作为历史资料，明确退出日常制作和运行链，不因业务移除自动删除用户成果或旧存档。

## 自动回船的准确边界

当前有两条氧气关联链，均属于删除范围：

1. 玩家或矿工缺氧扣血，归零后标记已登船；全员死亡再进入 Settle，恢复生命并 ResetDock／Cabin.Place，直接回船。
2. 矿工尚未死亡，氧气低于25即寻路回船。

删除氧气计算及低氧条件后，这两条链不能再由氧气触发。全员被敌人打死后的结算回船、手动紧急撤离、飞船载员随动、坡道进出、正常任务交货是独立规则，不能因为函数共用而一并误删。本轮记录这些独立规则仍在；若后续要求所有死亡均不自动回船，应另行明确死亡后状态及重新开始流程。

当前 Settle 将远征阶段设4但保留 Journey.Landed 的状态断路也仍存在；移除氧气会减少一个触发原因，不会修复战斗死亡或其他结算后的断路。后续可独立修正，不把氧气退出验收冒称为整个死亡／航程流程完成。

## 共享依赖与副作用

| 依赖 | 已核实事实 | 移除时的边界 |
|---|---|---|
| 供电范围 | RelayRange既用于氧气中继放置，也用于所有非船设备的父节点供电判断 | 供电距离及Powered／ParentId保留；可改为明确的供电命名，不能整项删除 |
| 供电父连接 | RequestRelay只搬氧气站；其父可为船或另一氧气站。现行部署仓储／炮塔／灯直接设ParentId=Ship.Id | 新开局删除氧气中继后不需替代中继网络；不承诺旧档中父关系兼容 |
| 部署位置 | 当前数组 oxygen/storage/turret/lamp 对应Ship.X−130/−50/+30/+110；额外氧气在+190 | 明确保留仓储−50、炮塔+30、灯+110，避免删除数组项导致它们各左移80 |
| 电力预算 | 总电力12；每氧气站占3，按ID分配 | 删除氧气站自然释放3或6功率，其他设备预算不暗中改小 |
| 风险增长 | 每个非船有电设备每模拟秒贡献0.15风险 | 氧气站退出自然减少0.15或0.30；时间风险及采矿风险作为独立规则保留，不补偿隐藏增长 |
| 船员舱 | 同时提供矿工及额外氧气站 | 仅去掉氧气收益，矿工及其非氧气任务保留；描述同步 |
| 机器人 | 搬运多类设备、仓储转运、撤收共用一个入口 | 删除氧气选择/排序分支；保留其他设备搬运及货运 |
| 结算 | 损失和补充费按实际对象／设备计算 | 不再产生氧气站损失／补充费用，其他结算保持明确的原合同 |
| 交互 | AtShip同时用于卸货和board合法性 | 只删除补氧调用，不删共享AtShip；正常登船与卸货继续存在 |

以上是移除导致的明确行为变化，不是本轮已修改结果。供电、警戒、模块经济的其他数值无须为了氧气退出整体重新设计。

## 存档 联机和框架

- 氧气字段从State、DTO和序列化中删除，网络线格式与存档合同一起变化；升级游戏协议、存档版本和规则／内容指纹，重新生成所需代码及锁定元数据。现有协议22／v15客户端和档案不冒称兼容。
- 本项目无需建立旧数据适配体系。旧存档文件保留，新版本采用新开局及新格式保存恢复；不自动迁移、覆盖或清空旧档目录。
- 删除Definition注册会改变内容身份摘要。检查现有定义/Prefab/Addressables引用，其他资产维持GUID和手工内容；不能通过重建所有定义绕过引用检查。
- 不涉及地图材料、地图耐久或AMP1格数据合同；没有因氧气移除必须升级地图schema的依据。
- 对象仍由原YYGC生命周期与事务管理，不增加替代对象系统、网络层或通用规则插件框架。
- 根据当前代码，这些改动均可在游戏仓库完成。若实际实施发现框架缺口，遵守 [AGENTS](../AGENTS.md) 的新约束：完成只读定位和具体方案，取得用户对框架修改范围的明确同意后才写YYGC，包括隔离checkout和`.deps`内源码。

## 最小验证范围

1. 统一编译和生成引用检查；正式运行链不再包含Oxygen字段、氧气设备定义、relay命令及氧气更新调用。历史资料和冻结证据的同名内容单独列出，不能用全仓库关键词零命中代替运行检查。
2. 正式航程和已着陆快速测试进入地面，持续超过原120秒氧气＋10秒满血耗尽窗口，确认无氧气引发的HP、货物、Boarded、位置和阶段变化；隔离敌人伤害，以免把战斗结算误判成缺氧残留。
3. 启用矿工／机器人模块，验证非氧气任务、既定设备位置、部署撤收、供电、船员舱描述与正常货物转移。
4. 复用受影响配置的一次Mono构建，独立Host＋Client验证投影、晚加入和重连；新旧客户端混连拒绝。IL2CPP仍按项目约束另需用户明确确认。
5. 新格式保存、真实重启恢复和存储失败行为；用有效的战斗死亡夹具继续覆盖既有死亡／结算路径，不删除这些测试来制造通过率。

本轮未执行以上验证。未来重新加入氧气时，按新玩法定义角色生存能力、供氧设备和明确死亡后果，复用现有状态与伤害入口；当前不为未来保留失效业务和预建抽象。

[OPS]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionOperations.cs:128>
[DEV]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/DarkNights/Scripts/Runtime/Objects/ExpeditionDevices.cs:16>
[BAL]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/DarkNights/Res/Config/balance.json:38>
[STATE]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/DarkNights/Scripts/Runtime/Objects/ActorState.cs:14>
[UI]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/DarkNights/Res/UI/Expedition/Expedition.prefab:544>
[DATABASE]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset:47>
[ADDRESS]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset:263>
[OXYGENDEF]: <D:/Developer/MiniGames/Dark Nights/unity-projects/Game/Assets/DarkNights/Res/Objects/Expedition/oxygen/oxygen.asset:17>
