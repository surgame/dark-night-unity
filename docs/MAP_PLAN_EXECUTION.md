# 地图方案执行与分阶段验收

更新日期：2026-09-19。执行分支：`codex/map-plan-execution`。本批执行依据：`D:/Downloads/Dark Nights 地图改造修复执行文档 v1.1.md`；原方案来源：`D:/Downloads/地图方案.html`。

本文件把 HTML 作为本次开发的功能方案与验收清单；仓库 `AGENTS.md`、已有架构文档和锁定依赖仍是工程约束。没有把方案中的示例路径、阶段描述或历史计数当成已完成实现。

## M0：依赖与边界冻结

状态：**代码、隔离依赖准备、远端基线模拟、主 Editor 导入与注册复核通过；真正新机器的公网恢复仍待执行。**

- `tools/prepare-lan-sample.ps1` 已实际执行，输出 YYGC `12b253c6bdd262feb860ab905b9e56e940ec9c40`。
- 地图包准备脚本应用了隔离 AnyRules 宿主补丁，442 项源文件校验通过；Tag 3 仍为既有 `TerrainEditCommand`，YYGC 用户仓库未修改。
- `prepare-lan-sample.ps1` 不再要求远端解析不可达的 `12b253c`：新环境从可达基线 `0c7cec0` 克隆，再应用 `NetworkCommandInterfaceGenerator.patch` 与锁定的 LAN 补丁；空 checkout 的本地克隆模拟已通过，结果等价于 `12b253c`。当前环境对 GitHub 的 `ls-remote` 未在限时内返回，因此实际公网可达性仍待新机器确认。
- 协议不新增地形消息类型：破坏仍使用稳定 Tag 3 的 `TerrainEditCommand`；矿床属于 YYGC 对象／业务投影。
- Unity `6000.4.9f1` 主工程已重开并完成刷新，矿床 Definition／Prefab／Archetype／Addressable 注册正常；钻机 Definition、Prefab、数据库和 Addressable 条目已删除。本批没有修改用户 YYGC master。

## M1：随机地图生成

状态：**Core 回归通过。**

已实现：

- 保留 8 房间、7 通道和原始入口／Boss／Secret 拓扑；新增房间特征、软岩和矿脉蓝图元数据。
- Gameplay profile 使用簇状散落矿石、房间矿脉和软岩带；Reference profile 保留旧生成器向量，冻结旧地图回归不被新分布覆盖。
- 生成结果包含稳定矿脉身份、房间归属、稀有度、容量和软岩位置；旧的 `TerrainBlueprint` 构造入口仍只作为兼容调用转发到新字段。

本阶段证据：

- `ArchitectureGuard`：372 个 C# 文件、12 项自测、0 错误。
- `TerrainRegression`：24 个 H5 生成向量通过；Gameplay `scattered=24`、`deposits=11`、`softRock=352`。
- `CoreRegression`：1048 checks、0 failures，新增同 seed 确定性、8 房间／矿脉标记和工具／爆破保护策略断言。
- CoreBuild：0 warning、0 error。

## M2：破坏策略、权威路由与幂等

状态：**主 Unity、真实双进程与实际 UDP 弱网通过。**

已实现：

- 手持工具可采集散落铜／铁／金和软岩，但不能采集普通岩、基岩或保护格；爆破使用有限半径目标集合。
- 客户端地形请求的玩法字段只有 `RequestId/U/V`；服务端先验证可信连接身份，再查 `RequestId` 缓存，未命中才检查 Ready、epoch、权限／主角租约、选中工具、冷却、距离、目标和剩余爆破次数；提交始终读取服务端 `TerrainMapAuthority.CommitId`。
- `connection generation + RequestId` 结果窗口保存首次结果；重复请求返回首次结果，不重复改格、不重复扣爆破次数或发放资源。Host 复用同一处理入口；炸药次数已移入 ActorState，并随投影／存档恢复。
- 软岩允许在最终空格上保留地质标记；挖空、保存、重启、加载后仍为空格且保留标记。
- 权威提交后通过 `TerrainAuthority` 的副本提交路径推进 `AMP1`；客户端只读副本不参与结算。

完整 Editor 196/196 已覆盖破坏策略、v6 恢复、随机矿床对象和图形局部更新。Mono 启动 6/6 通过；同一 Mono 的正常网络与 `200 ms RTT + 5% loss + 25 ms jitter` 各 18/18。弱网实际丢弃 585 包、发生 7,190 次延后重排，覆盖 Client 手挖、保护格／基岩拒绝、重复 RequestId 同一回执且只扣一次炸药，以及 Host／Client／LateJoin／Reconnect 最终地图一致。

## M3：矿室矿床与手动采集

状态：**业务状态、投影、恢复映射、Unity 资源注册及真实客户端验收通过；钻机整链已排除，只保留玩家手动采集。**

已实现：

- `MineralDepositBehaviour`／`MineralDepositState` 只拥有矿床房间、稀有度、容量、剩余量和 Available／Depleted 阶段；矿床不作为每格 NetworkObject。
- 矿床候选采用空格、支撑、入口避让、不重叠和最小间距约束；100 个 seed 回归通过。玩家必须近距离使用手持工具，服务端从对应矿床剩余量扣减并发放单次资源。
- 对象快照、展示副本、JSON 保存和关系校验只保留上述矿床字段。`DrillCharges`、`DeployMineralDrill`、钻机 Prefab／Definition、输出缓冲、钻进和自动结算均已删除。
- 正式矿床 Prefab、Definition、Archetype、Addressable 条目和能力绑定已在主 Editor 载入；`StateDataRegistry` 包含 `MineralDepositState`。

主 Unity 对象测试已验证 11 个蓝图创建 11 个运行时矿床，随存档恢复最终剩余量；真实客户端首轮暴露其动态 `terrain.deposit.*` 键未被副本接受，现已改为只对 `mineral-deposit` 开放的唯一动态身份并由正常／弱网 Player 复验。验收 seed 的矿室容量为 600，沿途散矿为 28 格，满足本轮“矿室明显更值得寻找”的结构性门槛；M6 的最终贡献比例仍未签署。

## M4：地图表现与局部刷新

状态：**变更 Chunk 合并刷新、真实双端最终地图与权威碰撞同步通过。**

- `TerrainReplicaSource` 为副本 Chunk 建立指纹，只把发生变化的 Chunk 交给 `TerrainPreview`；不新增每格 GameObject／NetworkObject，也不把表现副本变成权威状态。
- `TerrainPreview` 将连续提交的相邻 Chunk 合并为有限重载区域，卸载／加载只覆盖变更 Chunk；随后只重新展示已存在的可见页，让 ARDMap 的 `PageTargets`／脏页管线处理受影响 DualGrid Page，不再卸载整块可见区域。
- `MineralDepositPresentationBehaviour` 只读取 WorksiteView，按稀有度切换变体，枯竭时隐藏表现。

M4 的“变更 Chunk → 合并区域 → ARDMap 脏页”路径已由实际页面构建测试确认静态地图不重建、单次破坏只提交 1–4 页。正式 Player 正常／弱网均确认 Host 与 Client 最终地图 SHA 一致；附加碰撞用例确认角色站在两格支撑边缘时，爆破清除支撑后的同一权威物理 Tick 立即进入下落。

## M5：保存、加载与恢复

状态：**主 Unity 序列化／恢复、真实进程写盘重启、后加入、重连及弱网加载通过。**

- Object world save 版本从 5 提升到 6；地形保存最终材料、保护格、软岩、房间元数据、矿脉蓝图及其身份／容量。
- 动态矿床快照保存剩余量与 Available／Depleted 阶段；读取后由 ObjectSession 恢复，不能仅按初始随机种子重建最终状态。
- 旧 v4 入口按当前无旧档适配约定拒绝；新格式保留严格校验和原子写入路径。

旧版本拒绝、恢复映射和原子应用已由 CoreRegression／完整 Editor 覆盖；正式 Player 实际挖墙、爆破和采矿后写入 v6 文件，退出进程、重新启动并加载，最终地图 SHA 和矿床剩余量完全一致。LateJoin、断线重连和加载后的新 epoch 也已在正常／弱网双进程通过。突然断电／进程崩溃中断写入没有在本批模拟，不与正常退出重启混写。

## M6：平衡与发布验收

状态：**未签署完成。**

当前生成器已经有散落矿石、房间矿脉和隐藏／事件房间的结构数据，但尚未用正式运行时价值预算证明方案要求的 20–30%／60–70%／约 10% 资源贡献比例，也未完成最终 Play 体验校准。因此不把 `scattered=24`、`deposits=11` 或容量计数写成平衡达标。

## 统一验收闸门

| 闸门 | 当前结果 | 还缺什么 |
|---|---|---|
| 分支与锁定依赖 | 本机及空 checkout 模拟通过 | 真正新机器的公网准备仍待执行 |
| 生成确定性、8 房间／7 通道、受保护房间 | Core、主 Editor 通过 | 无 |
| 有意义入口、破坏策略、保护格、幂等 | Editor 与真实 Host＋Client 通过 | 无 |
| 矿床对象注册、Prefab 与手采 | 主 Editor、11 个运行时对象、独立 Client 通过 | M6 最终比例另行调优 |
| 表现局部刷新与碰撞 | 1–4 页局部刷新、双端 SHA、权威碰撞通过 | 前台画面体验不在本批签署 |
| v6 最终地形／对象恢复 | 真实写盘、退出、重启、LateJoin、Reconnect 通过 | 突然断电中断写入未模拟 |
| 弱网、LateJoin、最终地图 SHA | `200 ms RTT + 5% loss + 25 ms jitter` 18/18 | 无 |
| 前台性能、IL2CPP、双机器 | 未执行 | 按仓库约束需另行授权／条件 |

## 本次验收结果

1. 主 Editor 完整批次 196/196；碰撞补充用例 1/1；Core 1048/1048、Terrain 24 向量／100 seed、ArchitectureGuard 372 文件／12 自测／0 错误。
2. 正式 Mono `artifacts/map-fix/player-mono-r3/DarkNights.exe` 启动 6/6；正常网络 18/18、实际 UDP 弱网 18/18。
3. 验收 seed 的入口位于 `(100, 90)`，设计通路 `x=94` 从地表到入口前没有实心格；普通墙手挖同时被真实客户端拒绝，因此不能以任意向下挖替代找入口。
4. Host、Client、LateJoin 和 Reconnect 最终地图 SHA 一致；重复 RequestId 不增加 CommitId，只消耗一次炸药。
5. 实际 Player 写入 v6 存档、退出并重新启动后，最终地形和矿床剩余量均精确恢复。
6. 完整证据索引见 `docs/evidence/map-fix-2026-09-19.json`。本分支未推送远端；没有生成 IL2CPP，也没有改动用户 YYGC master。
7. 两份过期 Mono、失败验收副本和工具 bin/obj 共 482,873,498 字节的有界清理被文件系统策略拒绝；按约定未重试或改删父目录，收尾时磁盘仍有 34,684,579,840 字节可用。
