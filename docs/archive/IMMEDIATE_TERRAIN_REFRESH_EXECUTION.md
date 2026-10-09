# 局部地形即时刷新执行记录

**2026-09-26 状态更正：** 下文保留 09-25 首轮候选时点。最新代码 `CaveTerrainStyle.ImmediateForeground = true`，`CaptureModifiers()` 将活动圆簇捕获为 LocalV2；正式 `Expedition` 和固定／随机工作台已共用该 StrataCave 样式。因此“LegacyV1 仍为默认”“Unity 未导入”不再是当前结论。Local Unity 的[原始结果](evidence/terrain-local-validation-20260926.xml)为 32 通过／2 失败，失败为只读 chunk 编辑和镜头换向缺页；视觉门槛、完整发布栅栏、前台即时性与新 Player 联机仍未验收。当前打开路径和整理后的 `(old)` 场景见[场景索引](SCENES.md)。

日期：2026-09-25
状态：**候选实现；未完成 Unity 导入、运行时、画面与性能验收。不得标记“填拆延迟已解决”。**

## 工作分支与依赖

- 游戏候选：`ft-20260925-immediate-terrain-refresh`，基于 `68a4552d26fc423557460a06bd7b24fcbbf240e0`。
- AnyRuleD 候选：`ft-20260925-immediate-terrain-refresh`，基于 `e07e9a9e3e36cdbae1e0d39ec07aea555e95fbad`；提交 `6b85403630c07d886811e5f18494c15c056eb2ef` 已推送到同名远端分支。
- 按用户要求，YYGC 主检出 `D:/Developer/YYGC` 已从 `master` 切换到 `ft-20260925-immediate-terrain-refresh`；提交推送且工作树确认干净后，隔离工作树 `D:/Developer/YYGC-worktrees/immediate-terrain-refresh` 已通过 Git 安全移除。没有覆盖未提交的 YYGC 改动。
- 游戏 `Packages/manifest.json` 与 `tools/map-framework-patch/source-lock-map-state.json` 仍指向 `e07e9a9`。它们尚未包含本候选新增的 AnyRuleD 输入安装 API；正式编译前需更新锁定包并重建 `.deps`。

当前 Unity Editor 仍在原 `unity-projects/Game` 检出中运行，本候选未启动第二个 Editor，也未导入工作树。切换到候选包会触发 UPM 重新导入并影响 Local 的 `Library` 缓存；完成前需按项目规定检查活动 Editor 与改动状态，并通过单一 Local 验收通道导入。Unity 也需负责为新增脚本生成 `.meta`，本分支没有手工分配 GUID。

## 已实现的候选代码

1. **同一输入合同。** Editor 蓝图和联网副本都发布冻结的批次；批次携带 world、输入代次、网络会话、流代次及源提交游标。离线笔刷只提交最终稀疏格，Undo、Redo、Cancel 继续作用于草稿，不逐格重载地图。
2. **只读地图原位安装。** AnyRuleD 新增源驱动地图入口，先验证整批坐标、瓦片、快照和只读权限，再一次安装快照与增量；失败不写入部分结果。普通规则编辑 API 仍拒绝修改该表现副本，常规 Delta 不改变区块代次。
3. **有界的输入排队。** 连续同源 Delta 按最终格值合并，快照边界保持顺序，过期提交丢弃；队列超限撤销旧画面资格并要求完整基线。Reset、撤权、断开时隐藏地图页和背景页，旧代次工作不再获得当前可见资格。
4. **岩壁局部候选。** 新增世界坐标 LocalV2 岩簇、依赖 Halo、岩壁页烘焙和本地 staging 纹理上传；脏页按受影响范围重算，页面结果按版本整体核验后提交。局部柔光只重算受影响标记及邻近页。
5. **美术默认保护。** 原 LegacyV1 实现及现有样式仍是默认；新增菜单仅能创建独立 LocalV2 对照样式，尚未执行创建，也未通过 V1/V2 实际画面门槛。当前正式绑定走旧全图岩壁路径，因此完整的即时前景刷新**尚未交付**。

## 本轮验证

- AnyRuleD Core 独立 .NET 用例：**138/138 通过**。覆盖冻结批次、重置后基线、只读权限、非法批次原子拒绝，以及同一源提交中“完整区块快照 + 另一已装载区块增量”的一次安装。
- 独立纯 C# 核验：**10 组全图/分页对照通过**。包含不规则顶壁、页边拆填、默认及边界参数、关闭岩粒和零强度；比较最终遮罩、岩粒字段及 RGBA 岩壁页，并检查输入队列的过期提交过滤和快照后的增量合并。该核验调用候选源文件，不替代 Unity 测试。
- `Game/Assets/DarkNights/Scripts/Tests/TerrainModifierTests.cs` 已加入 LocalV2 页烘焙与全图结果对照、零强度边界回归；尚未由 Unity Test Runner 执行。

以下状态仍是 **NOT_RUN**：Unity 6000.4.9f1 导入和编译、Editor/PlayMode、V1/V2 实际画面对照、真实相机绘制栅栏、碰撞同步、Mono Player、Host/Client 正常及弱网、单格/64 格性能、IL2CPP、双机器。尚无前台即时性或整项完成声明。

证据摘要：[terrain-refresh-2026-09-25.json](evidence/terrain-refresh-2026-09-25.json)。

## 阶段判定

| 阶段 | 候选状态 | 退出条件 |
|---|---|---|
| P2 基础 Dual Grid 局部更新 | 源接线与原位安装代码已完成；运行时计数和场景结果未验 | Editor/运行时填拆与快照、旧代次、未加载边界等用例通过；实际装卸为 0，并补齐计数证据 |
| P3 当前岩壁风格局部刷新 | LocalV2 算法和纯算法分页一致性通过；LocalV2 尚未导入或成为候选样式 | Unity 内 V1/V2 画面对照通过；活动 Modifier 全量前景 Bake/Diff 为 0；运行时最终岩壁页与全量结果一致 |
| P4 完成判定与发布 | 版本门禁、批量页提交、局部柔光和绘制游标代码已实现；Unity 未验 | 连续填拆、跨页、撤权、碰撞和相机实际绘制测试通过；旧任务无回闪，FinalPresented 仅在整批完成后更新 |

## 下一步依赖

在继续 Unity 验收前，需要把游戏 `.deps` 的 AnyRuleD 来源锁定到该隔离候选，并更新对应来源 SHA 清单。该操作会改变 `Packages/manifest.json`／`packages-lock.json`，需要复用 Local Unity Editor 并重导入缓存。项目规则要求在这类包变更前报告缓存失效风险；当前未修改锁文件，也未触碰运行中的原检出。完成依赖选择后，先让 Unity 批量生成 `.meta`、编译并跑上述 EditMode 用例，再按计划进行 Editor Play、V1/V2 截图、联机和性能验收。
