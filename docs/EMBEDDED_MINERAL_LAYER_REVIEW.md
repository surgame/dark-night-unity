# 背景墙内嵌矿方案深度审查

日期：2026-10-04。审查对象：[执行方案](EMBEDDED_MINERAL_LAYER_PLAN.md)及其所引用的 `9df79dc` 游戏源码与当前锁定依赖。审查由本聊天的代理分轮完成，包括架构边界、实际规则探针、调用链与失败路径复核；没有调用另一模型或外部审查服务，不能把本记录称为独立第三方签署。

结论：一个独立矿层可以复用现有 AnyRuleD，无须为“不同矿不连接且不制作专属过渡图”修改框架。实际候选规则已编译和求解通过；真实素材的裁剪边缘仍是首个待验门槛。此前“无需修改”的回答应限定为需求和规则路线成立，不能理解为旧图集可原样导入或正式画面已通过。

## 范围及严重度

P1 为会造成错误状态、丢失收益、无法启动、跨矿覆盖或错误交付声明的问题；P2 为需要明确合同以避免后续返工或扩大范围的问题。表中的“已写入方案”表示设计补上了处理方式，不表示运行代码已修复；“待验证”必须通过对应阶段的实际证据关闭。

| 发现 | 来源和风险 | 处置及状态 |
| --- | --- | --- |
| P1 旧 ExactCell 合同不能直接导入 | RuleCompiler.ValidateCoverage 不接受 NotThis；同一四角组合只有一个 ExactCell 所有者；旧 JSON 是艺术映射，不是已编译规则 | 用 ore／TerrainSurface 重建规则，不手改框架。反例和候选实际编译通过，设计已关闭 |
| P1 NotThis 不含 Empty | RuleCompiler.Normalize 只对 AnyKnown／Empty 加 0；将其理解为“所有非本矿”会丢外缘 | This／AnyKnown 配合 popcount 优先级，覆盖 1,296 组合及 3,355 次矿选择；设计已关闭 |
| P1 特殊 surface 通道丢混矿形状 | MultiTerrainSolver 只在 materialCount==1 时采用专用 surface；混矿转为 Fill | 使用独立 ore 通道，配置透明 TileableFill；实际求解反例通过，设计已关闭 |
| P1 透明 Fill 没有真实像素证据 | 求解器先要求 Fill，探针资源只有身份；用 mask15 作为实心 Fill 会填掉素材凹边 | P0 导入真实透明 Fill 并验 Alpha／底墙透出；待验证 |
| P1 原图外扩柔边可能被裁掉 | TerrainSurface 被原生材料 Geometry 裁剪，不能等同于浏览器全矩形叠图 | P0 对所有形状及混矿角做真实渲染；必要时在新资源目录调整内侧凹边，不承诺原图 RGBA 保持；待验证 |
| P1 原背景排序放不下矿层 | GroundBatchKey 使用 SortingOrder+part.Kind，当前 −100 至 −97 相邻；只加一个数仍可能穿点缀 | 给矿层预留完整 0–3 区间，统一检查天空／后壁／点缀／前景；已写入方案，待 P0／P3 验证 |
| P1 现用岩壁材质不显示矿图 | CaveStrata 读取 RockSurface，不读取矿页 MainTex；直接复用会画错或不可见 | 原生 Sprite 兼容透明材质，优先验证现有 CaveBackgroundLayer；已写入方案，待验证 |
| P1 多业务 Definition 重复 RuleKey | ObjectSessionResources.Find 使用 SingleOrDefault，保存定义表按 RuleKey 建字典；五份 mineral-deposit 会冲突 | 首批共用现有业务 Definition，以实例矿种区分；AnyRuleD 的不同 TerrainDefinition 不冒充业务定义；设计已关闭 |
| P1 多格与单点状态双写 | 旧 Remaining／Durability／Stage 与新 Cells 同时写，会出现剩余量不守恒或错误阶段 | 每格状态唯一，汇总只读；删除旧单点采集入口，所有设备调用迁移；已写入方案，待 P2 验证 |
| P1 StateData 不会自动扩展会话投影 | 当前 SyncMode.Session 通过 WorldSessionBehaviour 传可靠完整载荷，WorksiteWire 只有单点矿字段 | 专用矿 DTO 同步贯穿投影／Replica／保存，退出旧混入工位分支；已写入方案，待 P4 验证 |
| P1 池状态和数组浅复制 | CaptureState、事务草稿、MemoryPack 与 Wire.Freeze 都可能保留同一数组；异步页面会读到后续修改或归池数据 | 校验生成 CopyFrom／Equals／Reset，各层深冻结，故障和退池测试；已写入方案，待 P2／P4 验证 |
| P1 目标版本不足或过严 | 现有 ContentVersion 是前景格版本，不能识别同一空格内矿内容；用整体 revision 又会拒绝合法合作命中 | 分开前景和矿格内容版本，耐久变化不改内容版本，耗尽／替换才推进；已写入方案，待 P2 验证 |
| P1 Ready 仍只检查旧地形 view.Ready | RandomLevelEntry 最后直接写 PresentationReady；新增异步矿层若未合取，会允许看不到矿时输入 | 合取地形／背景／矿层实绘基线，特别检查全空矿层和迟到资源；已写入方案，待 P3／P4 验证 |
| P1 恢复校验必须早于对象切换 | ObjectSessionPersistence 先准备地图，ObjectWorldRestore 提交对象后再替换地图；新增跨矿重叠检查不能放在后半段 | 准备阶段完成全部矿与地图关系校验，替换引用索引具有回滚；不先切世界再判断；已写入方案，待故障注入 |
| P1 并发最后一次产出与满袋 | 矿格、货袋和风险由多个 State 拥有；索引缓存 HP 或先发奖再提交会重复领矿 | 复用短对象事务，最终容量预检，服务端顺序结算及每镐门闩；已写入方案，待 P2／P5 验证 |
| P2 五种图集不等于五种资源 | MineralDepositRuleConfig、ExpeditionCargo 和货袋／设备／结算仅支持铁／金 | 正式先铁金，五矿仅验规则／美术。其他资源需要单独完整业务范围；设计已关闭 |
| P2 生成时点和第二次随机生成 | 原矿床允许掩埋；Modifier／泊位筛选后再改 footprint 才有最终合法性，客户端重生成会漂移 | 在最终地形后生成冻结矿格，独立种子，单一生成入口及指纹，容量守恒；已写入方案，待 P1 验证 |
| P2 自动矿工与爆破扩大范围 | 旧矿工走矿床中心已有失败，前景爆破也不是矿对象伤害；多格不自动补齐导航 | 更新必要调用并维持明确拒绝；不宣称全路线自动采矿，不新增爆破矿奖励；设计已关闭 |
| P2 上限和复杂度 | 实体上限 256、完整投影 512KiB，逐格对象和无限矿格会超限；透明 Fill 也可能占原生批次 | 有限对象、64 格／床及 4,096 格／世界候选预算，真实最大载荷与页／批次数检查；待 P4／P5 验证 |
| P1 旧弱网失败不能算新矿通过 | 当前地图／Ready 弱网仍失败，正常 Mono 和纯规则通过不能替代弱网 | 单一新产物完整检查；阻断时保留失败并报告整批未通过，不自动修框架；验收合同已补齐 |

## 实际规则探针结果

探针只读包含 41 个 AnyRules Core／Compiler 源文件，没有编译整个 Unity 工程。编译前后哈希一致。最终源码使用五矿、每矿 15 个形状、每形状四个变体、透明 Fill 身份和 ore／TerrainSurface 输出。

实际 RuleCompiler 编译成功且无诊断；实际 MultiTerrainSolver 对 1,296 种四角组合得到 Empty 或 Ready，3,355 次非空矿种选择正确；RuleCatalogCodec 编码恢复后各组合输出一致。四种 Unknown 输入都 Pending。旧 ExactCell／NotThis 被拒，NotThis＋空格不匹配、特殊 surface 的混矿回退及缺失 Fill 均按源码合同复现。

本轮没有执行 Unity 导入、真实 Sprite 资源读取、GPU 或 CPU 栅格化、页刷新、Definition 装配、采矿、事务故障、序列化业务帧或局域网。Part 的矿种归属正确只证明求解器输出，不证明矿素材的半透明边缘看起来分离，也不证明 4,096 格预算符合实际内存或带宽目标。

正式机器证据：[embedded-mineral-layer-plan-20261004.json](evidence/embedded-mineral-layer-plan-20261004.json)。可重建源：[OreRuleReviewProbe](../tools/OreRuleReviewProbe/README.md)。完整原始日志在 Local `artifacts/embedded-ore-plan-20261004/`；首轮单变体输入和最终四变体结果分开保留。

## 文档及实现审查门槛

实施前复核当前协议／保存常量、内容锁、工作区及作者资产，不能照抄本计划预估版本。P0 通过前不批量导入五矿或承诺不改外缘；P1–P4 改动准备齐后统一生成和编译。新增手写类型中文 XML summary、C# 9、职责及文件行数按 AGENTS.md 执行。

本次计划文字的门槛已补齐；运行风险仍按表中待验证项保留。后续代码完成后须审查最终差异、绑定、输入字段、投影／保存完整性、池生命周期和反例，再运行对应矩阵。没有独立第三方审查意见或未经授权的框架变更。
