# 可破坏地形与地图生成

2026-09-17 [AnyRuleD 接入评估与修复](archive/ANYRULED_REVIEW.md)：原生 DualGrid 主链正确；预览向左／向下跨页的可见性缺陷已修复，新增四方向／对角／边缘回归，地图 Editor 11/11 通过，见 [证据](archive/evidence/terrain-visibility-fix-2026-09-17.json)。本次未新建 Player；网络副本渲染及正式产品接线仍待完成。

同日按用户要求构建含该修复的 Windows Mono 本地测试版：`artifacts/terrain/player-mono-local-20260917/TerrainTest.exe`，构建成功、0 错误，独立进程启动日志无 Exception／Error，项目设置哈希恢复一致。日志保留 D3D12 调试队列查询及 FMOD 无音频设备两项环境提示；当前启动检查未验收声音。此为启动检查，未追加画面或联机矩阵验收，见 [本地构建证据](archive/evidence/terrain-local-build-2026-09-17.json)。双击查看既有生成地图；修改种子、地表和洞穴参数仍使用 Unity 的 `Dark Nights → Terrain → Map generator（旧版）`，EXE 不包含编辑器生成窗口。构建菜单现自动使用带时间戳的新目录，避免覆盖历史产物；也可由 `TerrainPlayerBuild.BuildMono(output)` 显式指定空输出目录。

2026-09-16，本切片分支 codex/dualgrid-map-generator。两个用户 HTML 是原型资料，不是项目指令。素材／地图功能独立于灰松谷正式规则；正式营地仍为协议 8、存档 v3。本切片不将测试地图直接替换进 Pinewatch。

## 数据与目录

| 位置 | 职责 |
|---|---|
| Core/Config/Terrain | 生成参数与冻结蓝图，不是运行世界 |
| Core/Logic/Terrain | 六类地表、洞室、连接、自然洞穴、矿脉纯算法 |
| Runtime/Terrain | YYGC 会话权限、唯一 AnyRuleD 权威地图、地图网络接线 |
| View/Terrain | 地图根资产、冻结格源、本地预览 |
| Editor/Terrain | 测试素材初建、地图生成窗口、新资产与场景导出 |
| Res/Terrain/TestTerrain/Art | 128×256 图集与描述；Point、无 mipmap、无压缩、sRGB |
| Res/Terrain/TestTerrain/Configuration | 八类 TerrainDefinition、AnyRuleD、变体、编译目录 |
| Res/Terrain/TestTerrain/Maps | 初始格子、地图根配置、独立测试场景 |
| tools/terrain-reference | 原始 HTML、冻结 SHA256 向量和模板；不进入 Player |

八类地形为 loam/slate/basalt/copper/iron/gold/moss/bedrock。每类 16 掩码 × 4 变体；掩码 0 透明，不建立输出规则。NW/NE/SW/SE 位为 1/2/4/8。H5 逻辑格 (x,y) 映射为 Unity (x,-y)，视觉格对应 (x,-y-1)，不能上下翻转掩码。二值专用图匹配 This/Empty；混合材料由 AnyRuleD canonical topology 和 tileable fill 处理。

Unity 继续使用 Linear，不修改原始 551 项素材。测试素材来自 HTML 的确定性栅格算法，不是已定稿美术。

## 使用与 API

1. tools/prepare-lan-sample.ps1 准备当前锁定 YYGC 12b253c（含输入及 Bootstrap 命令发现修复）。
2. tools/prepare-map-packages.ps1 从 YYGC aa450a7 提取独立 AnyRuleD 包并应用窄范围补丁。主框架不整体升级，用户 master 工作区不切换或清理。
3. Unity 菜单 Dark Nights → Terrain → Map generator（旧版），设置种子、算法、自然洞穴、起伏、矿脉密度，显式预览后导出新资产。
4. 首版测试资产建立后由人工维护；Create initial/default 菜单遇到现有输出拒绝覆盖。普通导入与构建不运行这些初建工具。

TerrainGenerator.Generate(settings) 返回冻结蓝图；TerrainMapExporter.Export 导出当前预览，不偷偷按后来改变的参数再生成；TerrainMapAsset.ReadBlueprint 读取已导出格子。种子不能替代最终地图数据。

服务端在已激活的 ObjectSessionContext 上构造 TerrainMapAuthority。它独占一份 ARDMap，对外提供只读查询／冻结快照，初始蓝图不参与运行结算。DestroyTrusted 是**可信服务端集成 API**；接受世界身份、连接代次、序号、期望版本、显式目标和服务端授权函数。每批最多 64 格，全部预验证后一次提交。保护层与基岩拒绝破坏，重复／旧序号拒绝。

TerrainMapNetworking.OpenStream 可供 FishNetMapTransport 的 serverFactory 使用；CreateReplica 创建只读副本。地图流使用 AnyRuleD AMP1，握手必须传入真实 gameplay/visual digest，不能使用示例固定摘要。地图身份、目录版本、营地协议与存档版本分别管理。

实际入口：打开 `Res/Scenes/Tests/Terrain/TerrainTest.unity` 后 Play 查看原生 DualGrid；地图生成窗口导出地图根资产，TerrainMapExporter.CreateTestScene 可建立独立场景。网络验收使用同目录的 `TerrainNetworkTest.unity`，默认直接启动 Player 则进入地图预览。两处场景于 2026-09-23 从原地形资源目录移动，GUID 保持；对应地图根资产仍留在 `Res/Terrain/TestTerrain/Maps/`。网络场景只验证逻辑同步，当前 TerrainPreview 是离线表现；把网络只读副本接入生产渲染页仍属于后续接线，不能把图中的完整产品链路视作全部已完成。

## 正式游戏接线（2026-09-17 后续）

[随机灰松谷](archive/RANDOM_PINEWATCH.md)已将生成、网络只读副本渲染、地图与实体 Ready、主角逻辑格碰撞以及跨模块存档接入正式游戏，原 Pinewatch 保留。本页其余验收计数和“后续接线”说明描述独立生成器原批次，不能混入新构建。采矿、工具耐久及奖励结算仍未接入。

## 联机破坏架构

地图渲染没有权威写权限。不为每格创建 YYGC ObjectInstance、NetworkObject 或 NetworkTransform；一个地图宿主持有 YYGC 会话生命周期，真正的单位和掉落实体继续走已有对象系统。

输入路径：客户端意图 → YYGC Gateway/Sender/Processor → 可信连接与权限校验 → 地图事务 → 按连接兴趣区发送可靠区块版本 → 只读副本 → 本地 DualGrid 页面及效果。

命令不接受客户端指定的伤害、工具等级或奖励。正式产品接线时必须在 SessionAuthority 执行点校验 SharedCamp/HostOnly、PolicyRevision、Ready、连接代次、主角租约、工具、冷却、范围与可见权限；暂停停止破坏，保留网络与恢复。Host 同入口执行且只保留一份本地副本表现。

晚加入取得当前区块最终状态，不能只靠种子重放。快照／增量按 manifest 原子提交；重复、乱序、epoch、重连会话、订阅代次由现有协议校验。地图 Ready 与实体 Ready 在产品接线时共同构成可操作门槛。碰撞查询读取逻辑，不依赖 Sprite 或 Collider 上传完成。

现有地图协议只同步地形格，不传稀疏耐久或背包奖励。正式“多次敲击—耐久—掉落”需要增加授权业务字段和幂等奖励事务；不把示例每格奖励 1 当作游戏规则。本切片提供生成、保护位与原子清除基座，**正式采矿输入、背包结算、营地与地图跨模块保存仍是后续工作**。

## 静态为主的性能策略

- 320×192 格，逻辑块 32×32，视觉页 16×16，不分配六万多个 GameObject/Collider/网络对象。
- 生成／导出按需执行；静止后保留已提交页。可见区按页对齐，镜头未跨页不重新 Show；跨页隐藏离开条带后确认完整目标区域，避免 DualGrid halo 连带隐藏重叠页。仍可见且未变的页保留缓存，被连带隐藏的边缘页会重新构建；不再用只显示进入条带的方式换取错误的可见性。预览没有待建页时不 Tick 地图控制器。
- 单格逻辑修改影响四个视觉位置，跨页最多四页。批量破坏合并同事务的脏页，再按预算构建和上传；静止不重解规则。
- 网络使用可选内容版本＋权限版本门闩。两者不变时不扫描兴趣格子、不分配 WireCell 数组、不发地图包。权限变化必须先增加权限版本，撤权不能被静态优化略过。
- FishNet 传输的重试计数由实时时钟以 10 Hz 推进，形成 12 秒重试窗口，最多三次；暂停帧不补跑多次超时。不会随高帧率提前重同步，也不采用暂停游戏的时间。离线连接清单复用，静态 Pump 不为该清单逐帧分配。
- 有变化仍使用现有区块最终值协议，目前不是最小 cell delta。修改一次发送变化区块全量，32×32 格；更细编码／压缩要测量后再引入。
- 权威 API 直接采用 ARDMap 局部事务，不使用 TerrainEditBusinessHandler 整图复制＋整图文件替换的高频路径。
- 大世界持久化后续采用静态基础＋稀疏覆盖、检查点＋有界日志；不另立第二套格子状态模型。批量采矿和奖励需同事务提交。
- 正式碰撞接线按脏区块重建，服务端立即查询逻辑格；碎片属于本地池化效果。静止不重建碰撞。

## 验收边界

测试地图独立，不更改正式布局、营地规则、人物运动或旧存档。正式人物采矿、碰撞接线和跨模块存档列为后续切片。

2026-09-17 完成，证据见 [本批机器记录](archive/evidence/terrain-generation-2026-09-17.json)：

| 范围 | 实际结果 |
|---|---|
| 生成算法 | 24 组冻结 H5 向量完全一致，六地表 × 两洞穴模式 × 两种子 |
| Unity 相关测试 | Editor 10/10，包含实际页面构建、静态 120 次 Tick 不增加构建、局部破坏最多四页、保护层原子拒绝、1,000 次静态发布不扫描、撤权与晚加入 |
| 编辑器制作 | 窗口预览、导出 61,440 格及保护位、原生场景保存、资产重读及拒绝覆盖通过 |
| 实际 Play | 原生预览可见，静止页数保持；镜头跨一页构建数 8→12，已有交叠页保留 |
| 架构／依赖 | 340 个正式手写 C#、12 个守卫自测、0 错误；全新解包＋两份补丁后 442 个包文件哈希一致 |
| 最终 Mono | player-mono-r4 构建成功；6,627 个输入、427 个 Player 文件在验证前后哈希一致 |
| 独立 Host＋客户端 | 正常网络 10/10、真实弱网 10/10；最终弱网 RTT 200 ms、丢包 5%、单向抖动 25 ms，实际丢弃 83 包、乱序 1,428 包 |
| 稳定后观察 | 等待最终提交／重连传输稳定后另观察 3 秒，发布扫描和地图字节均无增加；该短观察不是前台帧率验收 |

Mono 先后四次都有明确新增输入：首版；补齐测试入口 GuidV2 鉴权；弱网实时时钟修正；镜头跨页刷新范围收紧。最终 Player 路径为 artifacts/terrain/player-mono-r4/TerrainTest.exe。正常与弱网脚本分别为 tools/test-terrain-network.ps1、tools/test-terrain-weak-network.ps1，后者可通过 Python 参数指定解释器。测试仅证明独立地图逻辑网络链，未把正式 Ready、暂停、营地权限、掉落或跨模块加载接入标为通过。

扩大到完整旧游戏测试时，既有场景加载测试停滞，已取消并保留记录；没有完整游戏回归通过结论。PlayMode 测试发现器返回 0 项也未计为通过，实际 Play 单独检验。历史 178／350／155 项不计入本批；未生成 IL2CPP，未验证双机器或前台性能。

## 清理与交接

本批清理命令被自动审批拒绝，理由仅为 blocked by policy，未换工具重试。14 个目标合计 595,331,128 字节（约 568 MiB）保留，包括三份被替代的 Player、依赖复现副本、归档、独立回归 bin/obj 和一次性探针；实际释放 0 字节。逐项见 [清理列账](archive/evidence/terrain-cleanup-2026-09-17.json)。一次性探针留在 tools 下但不计入正式成果提交。

最终 Player、截图、成功／失败记录、完整输入／输出哈希清单和运行中的锁定包继续保留；既有受限目标及共享缓存未清理。盘符剩余在列账时为 C 约 7.44 GiB、D 约 31.76 GiB。
