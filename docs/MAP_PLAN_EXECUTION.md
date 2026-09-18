# 地图方案执行与分阶段验收

更新日期：2026-09-19。执行分支：`codex/map-plan-execution`。方案来源：`D:/Downloads/地图方案.html`。

本文件把 HTML 作为本次开发的功能方案与验收清单；仓库 `AGENTS.md`、已有架构文档和锁定依赖仍是工程约束。没有把方案中的示例路径、阶段描述或历史计数当成已完成实现。

## M0：依赖与边界冻结

状态：**代码与静态准备通过；Unity 资源导入后的注册复核待执行。**

- `tools/prepare-lan-sample.ps1` 已实际执行，输出 YYGC `12b253c6bdd262feb860ab905b9e56e940ec9c40`。
- AnyRuleD、FishNet 4.7.2 和 Tag 3 的已有锁定路径未切换；YYGC 用户仓库未修改。
- 准备脚本增加了本机框架不可用时的远端可重现回退参数，但本机依赖已存在，因此本次未进行网络克隆。
- 协议不新增地形消息类型：破坏仍使用稳定 Tag 3 的 `TerrainEditCommand`；矿脉、钻机和掉落属于 YYGC 对象／业务投影。

## M1：随机地图生成

状态：**Core 回归通过。**

已实现：

- 保留 8 房间、7 通道和原始入口／Boss／Secret 拓扑；新增房间特征、软岩和矿脉蓝图元数据。
- Gameplay profile 使用簇状散落矿石、房间矿脉和软岩带；Reference profile 保留旧生成器向量，冻结旧地图回归不被新分布覆盖。
- 生成结果包含稳定矿脉身份、房间归属、稀有度、容量和软岩位置；旧的 `TerrainBlueprint` 构造入口仍只作为兼容调用转发到新字段。

本阶段证据：

- `ArchitectureGuard`：373 个 C# 文件、12 项自测、0 错误。
- `TerrainRegression`：24 个 H5 生成向量通过；Gameplay `scattered=24`、`deposits=11`、`softRock=352`。
- `CoreRegression`：1048 checks、0 failures，新增同 seed 确定性、8 房间／矿脉标记和工具／爆破保护策略断言。
- CoreBuild：0 warning、0 error。

## M2：破坏策略、权威路由与幂等

状态：**源码编译和 Core／Runtime 回归通过；真实 Unity EditMode、双进程联机和弱网验收待执行。**

已实现：

- 手持工具只破坏软岩中心格；爆破使用有限半径目标集合；空格、保护格、基岩和普通不可破坏材料被策略拒绝。
- 客户端输入进入 Reliable ServerOnly `TerrainEditCommand`；服务端检查 Ready、epoch、权限／主角租约、选中工具、冷却、距离、目标和剩余爆破次数，再提交 `TerrainMapAuthority`。
- `connection generation + RequestId` 结果窗口保存首次结果；重复请求返回首次结果，不重复改格、不重复扣爆破次数或发放资源。Host 复用同一处理入口。
- 权威提交后通过 `TerrainAuthority` 的副本提交路径推进 `AMP1`；客户端只读副本不参与结算。

本阶段还没有把“代码编译通过”扩展成真实网络结论。下列项目必须在 Unity 正常刷新并能启动正式场景后验收：伪造工具／范围／目标、乱序弱重试、Host 重入、Host＋Client＋LateJoin 最终地图 SHA、200 ms RTT／5% loss／25 ms jitter。

## M3：矿脉对象与钻机

状态：**业务代码、状态、投影、恢复映射和 Editor 安装菜单已编译；正式 Prefab／Definition／StateData 生成尚未执行。**

已实现：

- `MineralDepositBehaviour`／`MineralDepositState` 拥有矿脉剩余量、阶段、钻机身份和钻进进度；矿脉不作为地形格上的 NetworkObject。
- 手动提取、钻机开始／停止／Tick、枯竭状态、铁／金资源映射和 ObjectSession 经济写入已接入同一权威事务。
- 对象快照、展示副本、JSON 保存和关系校验已增加矿脉字段；钻进进度复用现有 Worksite `Progress` 字段，避免新增并行网络协议。
- `Dark Nights/Content/Install Mineral Deposit Object` 已提供批量安装入口，会从现有 Stone Prefab 创建矿脉 Prefab、Definition、Addressable 条目和能力绑定。

仍需在 Unity 导入完成后按顺序执行 YYGC StateData 发现／生成，再执行上述 Content 菜单。不得手改 `Scripts/Generated`；生成后需检查 `MineralDepositState` 的注册 Tag、Prefab 绑定、Address／Label 和重开持久性。

## M4：地图表现与局部刷新

状态：**代码路径编译通过；真实 Play 画面和精确脏页验收待执行。**

- `TerrainReplicaSource` 只读读取 `MapChunkData`，副本提交变化通知 `TerrainPreview`。
- 预览只重载当前摄像机可见区域，并在连续提交期间合并刷新请求；没有新增每格 GameObject／NetworkObject，也没有把表现副本变成权威状态。
- `MineralDepositPresentationBehaviour` 只读取 WorksiteView，按稀有度切换变体，枯竭时隐藏表现。

当前实现是“可见区域局部重载”，不是已经证明的逐脏页精确重建。M4 通过条件仍包括：正式地图运行画面、主角权威碰撞、破坏后可见区域与碰撞同步，以及提交期间连续刷新不丢最后一次状态。

## M5：保存、加载与恢复

状态：**序列化代码与静态回归通过；真实进程重启、重连和加载后 Play 验收待执行。**

- Object world save 版本从 4 提升到 5；地形保存最终材料、保护格、软岩、房间元数据、矿脉蓝图及其身份／容量。
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
| 矿脉／钻机对象注册与 Prefab | 源码通过，资产未安装 | Unity 刷新、StateData 生成、Content 菜单 |
| 表现局部刷新与碰撞 | 源码通过 | 正式 Play 视觉和交互证据 |
| v5 最终地形／对象恢复 | 代码路径通过 | 真实写盘、重启、重连、epoch |
| 弱网、LateJoin、最终地图 SHA | 未执行 | 独立进程与网络条件 |
| 前台性能、IL2CPP、双机器 | 未执行 | 按仓库约束需另行授权／条件 |

## 下一次 Unity 验收顺序

主编辑器目前未对新增脚本自动刷新，因此本次没有伪造 Unity EditMode／Play 通过记录。编辑器完成刷新或重开后，按以下顺序一次性执行：

1. 等待 `DarkNights.Core/Runtime/View/Entry/Editor/Tests` 域重载，确认 Console 无新增编译错误。
2. 执行 YYGC 的 StateData Discover／Update 和 Generate 入口，确认 `MineralDepositState` 进入生成注册；禁止手改生成文件。
3. 执行 `Dark Nights/Content/Install Mineral Deposit Object`，关闭并重开项目，检查 Definition、Prefab、绑定和 Addressable 条目。
4. 先跑 EditMode 的 Terrain／Save／Object 测试，再跑 M2 的 Host＋Client＋LateJoin、重连和幂等场景。
5. 生成一次 Mono Player，复用同一产物完成地图画面、碰撞、保存恢复和弱网检查；不生成 IL2CPP，除非另获确认。

本分支未提交或推送远端；Unity 临时导入工程仅用于生成新增脚本 `.meta`，已移出工作区，不作为正式资源证据。
