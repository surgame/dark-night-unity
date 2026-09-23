# YYGC 统一对象状态与恢复覆盖

2026-09-13，状态迁移、正式入口与旧模型退出已在 U3–U5 完成；134 项 Editor／Play 回归通过。U6 最终同一 Mono 产物的 347 项自动检查已通过，性能和临时目录清理仍有未完成项；本表记录状态所有权，交付边界见[实施记录](YYGC_UNIFIED_IMPLEMENTATION.md)。

| 所有者 | 权威状态 | 保存／恢复 | 主要验证落点 |
|---|---|---|---|
| ActorBehaviour / ActorState | 身份、名字、阵营、位置／HP、订单／目标、移动／回防、朝向、行动／攻击／前摇／AI 时钟、待命中／强制攻击 | ActorSnapshot；身份另含 DefinitionGuid 和 PlacementKey；转职保留 EntityId 与家族顺序 | UnifiedGameplayTests 的前摇／2×、训练替换和活跃恢复；UnifiedCampaignTests |
| BuildingBehaviour / BuildingState | HP、施工、工人、农田工位、攻击冷却、已付款训练队列 | BuildingSnapshot + TrainingSnapshot；FarmSiteId 由经过验证的反向 FarmId 重建 | 农田完成、顺序训练、销毁退款和施工恢复 |
| WorksiteBehaviour / WorksiteState | 位置、工人、存量、生产余量、变体、所属农田 | WorksiteSnapshot；双向占用与所属农田先校验 | 初始 ID、工位互斥、耗尽、农田销毁释放和完整三夜 |
| CampSimulationBehaviour / CampSimulationState | 模式、暂停／倍速、总时间、下一实体 ID、RNG seed／state、击杀／损失 | SessionSnapshot 与 StatisticsSnapshot；RNG 位状态完整恢复 | 冻结一倍速三夜／无人照料、2×单步、失败整步回滚 |
| EconomyBehaviour / EconomyState | 五种库存、五种采集统计、食物消耗／饥饿计时、招募冷却 | EconomySnapshot 与 StatisticsSnapshot | 跨对象支付、顺序部分成功、退款、招募和冻结三夜资源 |
| WaveBehaviour / WaveState | 当前夜次、昼夜、白天余量、生成余量／游标 | WaveSnapshot | 三夜 34 个敌人、时间／奖励、暂停及原 RNG 顺序 |
| ProjectileBehaviour / ProjectileState | 起终点、目标 ID、伤害、年龄、时长、当前 epoch 的表现序号 | ProjectileSnapshot；新 epoch 重新分配表现 ViewId，不改变伤害和时序 | 发射不即时伤害、一次命中、活跃箭矢恢复、失效目标安全消耗 |

ActorState 的 Walking、ActorState／BuildingState 的 HitFlash 是可重算的短时表现字段，沿用原恢复语义，在首次模拟步重新推进；它们不影响伤害、支付或生产。网络投影包含当前展示值，但不包含 RNG。

SessionEntityIndex 只保存 YYGC 能力引用及各家族插入顺序。ObjectSession、工作／营地命令与生命周期协调器不另存实体或玩法状态。状态草稿只存在于一个同步事务；没有 await、跨线程 SessionScope 或客户端模拟。

训练队列与飞行记录用值结构保存，State 的 CopyFrom 显式克隆数组。ObjectSnapshotMapper 创建脱离对象池的 DTO；ObjectWorldRestore 先准备候选对象，提交失败保留旧状态。场景放置键仅在原 Definition 匹配时复用原实例；转职使用新职业 Prefab，恢复原职业时可重新接管原放置实例。

PolicyRevision、房间控制模式、连接代次、恢复凭据与 Ready 归 Runtime/Session／Network，不写入存档。成功切世界增加 epoch；失败不改变现世界、暂停或倍速。

新增状态注册由 YYGC 的 StateData 注册生成流程生成；能力注入由既有 YYGC 源生成器处理。重新导入对应手写声明即可生成，不能修改生成源码来替代声明。
