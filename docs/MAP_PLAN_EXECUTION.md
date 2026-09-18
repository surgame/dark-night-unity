# 地图方案执行与分阶段验收

更新日期：2026-09-19。执行分支：`codex/map-plan-execution`。本批执行依据：`D:/Downloads/Dark Nights 地图改造修复执行文档 v1.1.md`；原方案来源：`D:/Downloads/地图方案.html`。

本文件把 HTML 作为本次开发的功能方案与验收清单；仓库 `AGENTS.md`、已有架构文档和锁定依赖仍是工程约束。没有把方案中的示例路径、阶段描述或历史计数当成已完成实现。

## M0：依赖与边界冻结

状态：**代码、隔离依赖准备、远端基线复现和 Unity 资源安装通过；主 Editor 的导入／注册复核待执行。**

- `tools/prepare-lan-sample.ps1` 已实际执行，输出 YYGC `12b253c6bdd262feb860ab905b9e56e940ec9c40`。
- 地图包准备脚本应用了隔离 AnyRules 宿主补丁，442 项源文件校验通过；Tag 3 仍为既有 `TerrainEditCommand`，YYGC 用户仓库未修改。
- `prepare-lan-sample.ps1` 不再要求远端解析不可达的 `12b253c`：新环境从可达基线 `0c7cec0` 克隆，再应用 `NetworkCommandInterfaceGenerator.patch` 与锁定的 LAN 补丁；空 checkout 的本地克隆模拟已通过，结果等价于 `12b253c`。当前环境对 GitHub 的 `ls-remote` 未在限时内返回，因此实际公网可达性仍待新机器确认。
- 协议不新增地形消息类型：破坏仍使用稳定 Tag 3 的 `TerrainEditCommand`；矿脉、钻机和掉落属于 YYGC 对象／业务投影。
- Unity `6000.4.9f1` 隔离工程已实际安装矿床和钻机各 1 个 Definition／Prefab，补齐 Archetype、数据库和 Default Local Group Addressable 条目；记录见 [`artifacts/map-fix-editor-install2.log`](../artifacts/map-fix-editor-install2.log)。主 Editor 尚未对本批源码完成刷新，因此不把资源安装日志扩展成主工程运行时通过。

## M1：随机地图生成

状态：**Core 回归通过。**

已实现：

- 保留 8 房间、7 通道和原始入口／Boss／Secret 拓扑；新增房间特征、软岩和矿脉蓝图元数据。
- Gameplay profile 使用簇状散落矿石、房间矿脉和软岩带；Reference profile 保留旧生成器向量，冻结旧地图回归不被新分布覆盖。
- 生成结果包含稳定矿脉身份、房间归属、稀有度、容量和软岩位置；旧的 `TerrainBlueprint` 构造入口仍只作为兼容调用转发到新字段。

本阶段证据：

- `ArchitectureGuard`：375 个 C# 文件、12 项自测、0 错误。
- `TerrainRegression`：24 个 H5 生成向量通过；Gameplay `scattered=24`、`deposits=11`、`softRock=352`。
- `CoreRegression`：1048 checks、0 failures，新增同 seed 确定性、8 房间／矿脉标记和工具／爆破保护策略断言。
- CoreBuild：0 warning、0 error。

## M2：破坏策略、权威路由与幂等

状态：**源码编译和 Core／Runtime 回归通过；真实 Unity EditMode、双进程联机和弱网验收待执行。**

已实现：

- 手持工具可采集散落铜／铁／金和软岩，但不能采集普通岩、基岩或保护格；爆破使用有限半径目标集合。
- 客户端地形请求的玩法字段只有 `RequestId/U/V`；服务端先验证可信连接身份，再查 `RequestId` 缓存，未命中才检查 Ready、epoch、权限／主角租约、选中工具、冷却、距离、目标和剩余爆破次数；提交始终读取服务端 `TerrainMapAuthority.CommitId`。
- `connection generation + RequestId` 结果窗口保存首次结果；重复请求返回首次结果，不重复改格、不重复扣爆破次数或发放资源。Host 复用同一处理入口；炸药次数已移入 ActorState，并随投影／存档恢复。
- 软岩允许在最终空格上保留地质标记；挖空、保存、重启、加载后仍为空格且保留标记。
- 权威提交后通过 `TerrainAuthority` 的副本提交路径推进 `AMP1`；客户端只读副本不参与结算。

本阶段还没有把“代码编译通过”扩展成真实网络结论。下列项目必须在 Unity 正常刷新并能启动正式场景后验收：伪造工具／范围／目标、乱序弱重试、Host 重入、Host＋Client＋LateJoin 最终地图 SHA、200 ms RTT／5% loss／25 ms jitter。

## M3：矿脉对象与钻机

状态：**业务代码、状态、投影、恢复映射和 Unity 资源安装已完成；主 Editor 的 StateData／Definition 加载及运行时对象复核待执行。**

已实现：

- `MineralDepositBehaviour`／`MineralDepositState` 拥有矿脉剩余量、阶段、钻机身份和钻进进度；矿脉不作为地形格上的 NetworkObject。
- 矿床候选采用空格、支撑、入口避让、不重叠和最小间距约束；100 个 seed 回归通过。钻机从主角背包部署，创建真实 `WorksiteBehaviour`，服务端附着矿床并按 Tick 将产出写入钻机对象缓冲，不直接调用全局 Economy。
- 对象快照、展示副本、JSON 保存和关系校验已增加矿脉字段；钻进进度复用现有 Worksite `Progress` 字段，避免新增并行网络协议。
- `Dark Nights/Content/Install Mineral Deposit Object` 与 `Dark Nights/Content/Install Mineral Drill Object` 已在隔离 Unity 中实际执行，从现有 Stone Prefab 创建正式 Prefab、Definition、Archetype、Addressable 条目和能力绑定；`StateDataRegistry` 已包含 `MineralDepositState`。

仍需在主 Unity Editor 导入完成后复核 `MineralDepositState` 的注册 Tag、11 个蓝图到 11 个运行时矿床对象的映射、Prefab 绑定、Address／Label 和重开持久性；不得把隔离工程的安装日志写成正式 Play 通过。

## M4：地图表现与局部刷新

状态：**变更 Chunk 合并刷新代码路径和源码编译通过；真实 Play 画面和页面计数验收待执行。**

- `TerrainReplicaSource` 为副本 Chunk 建立指纹，只把发生变化的 Chunk 交给 `TerrainPreview`；不新增每格 GameObject／NetworkObject，也不把表现副本变成权威状态。
- `TerrainPreview` 将连续提交的相邻 Chunk 合并为有限重载区域，卸载／加载只覆盖变更 Chunk；随后只重新展示已存在的可见页，让 ARDMap 的 `PageTargets`／脏页管线处理受影响 DualGrid Page，不再卸载整块可见区域。
- `MineralDepositPresentationBehaviour` 只读取 WorksiteView，按稀有度切换变体，枯竭时隐藏表现。

M4 的代码路径已切换为“变更 Chunk → 合并区域 → ARDMap 脏页”；尚未把它扩展成正式 Play 的逐页计数证据。通过条件仍包括：正式地图运行画面、主角权威碰撞、破坏后可见区域与碰撞同步，以及提交期间连续刷新不丢最后一次状态。

## M5：保存、加载与恢复

状态：**序列化代码与静态回归通过；真实进程重启、重连和加载后 Play 验收待执行。**

- Object world save 版本从 5 提升到 6；地形保存最终材料、保护格、软岩、房间元数据、矿脉蓝图及其身份／容量。
- 动态矿脉快照保存剩余量、阶段、钻机身份和进度；读取后由 ObjectSession 恢复，不能仅按初始随机种子重建最终状态。
- 旧 v4 入口按当前无旧档适配约定拒绝；新格式保留严格校验和原子写入路径。

代码层面的旧版本拒绝与恢复映射已由 CoreRegression／Unity 编译覆盖；真实文件写入、崩溃中断、重启、重连、epoch 拒绝旧命令必须在正式 Player／Editor 场景中补验。

## M6：平衡与发布验收

状态：**未签署完成。**

当前生成器已经有散落矿石、房间矿脉和隐藏／事件房间的结构数据，但尚未用正式运行时价值预算证明方案要求的 20–30%／60–70%／约 10% 资源贡献比例，也未完成最终 Play 体验校准。因此不把 `scattered=24`、`deposits=11` 或容量计数写成平衡达标。

## 统一验收闸门

| 闸门 | 当前结果 | 还缺什么 |
|---|---|---|
| 分支与依赖可重现 | 通过 | 无 |
| 生成确定性、8 房间／7 通道、受保护房间 | Core 通过 | Unity 生成资产与场景重开 |
| 破坏策略、保护格、幂等 | 静态／Core 通过 | Unity EditMode 与真实 Host＋Client |
| 矿脉／钻机对象注册与 Prefab | 隔离 Unity 安装通过 | 主 Editor 刷新、StateData／11 个运行时对象重开检查 |
| 表现局部刷新与碰撞 | 源码通过 | 正式 Play 视觉和交互证据 |
| v6 最终地形／对象恢复 | 代码路径通过 | 真实写盘、重启、重连、epoch |
| 弱网、LateJoin、最终地图 SHA | 未执行 | 独立进程与网络条件 |
| 前台性能、IL2CPP、双机器 | 未执行 | 按仓库约束需另行授权／条件 |

## 下一次 Unity 验收顺序

主编辑器目前未对新增脚本自动刷新，因此本次没有伪造 Unity EditMode／Play 通过记录。编辑器完成刷新或重开后，按以下顺序一次性执行：

1. 关闭并重开或主动刷新主 Editor，等待 `DarkNights.Core/Runtime/View/Entry/Editor/Tests` 域重载，确认 Console 无新增编译错误。
2. 复核既有 StateData 生成输出中的 `MineralDepositState` Tag 8，并检查两份 Definition、两个 Prefab、Archetype、数据库和 Addressable 条目；不得手改生成文件。
3. 以随机模板检查 11 个蓝图是否创建 11 个运行时 `MineralDepositBehaviour`，再保存／重开验证最终剩余量和钻机身份。
4. 先跑 EditMode 的 Terrain／Save／Object 测试，再跑 M2 的 Host＋Client＋LateJoin、重连和幂等场景。
5. 生成一次 Mono Player，复用同一产物完成地图画面、碰撞、保存恢复和弱网检查；不生成 IL2CPP，除非另获确认。

本分支未提交或推送远端；隔离 Unity 工程只用于编译／安装内容，已移出工作区；正式资产、脚本和证据已回填仓库，主 Editor／Play 仍待执行。
