# 地形 Modifier 与可插拔点缀层进度

更新时间：2026-09-22。分支：`ft-20260922-terrain-modifiers`，基于 `aaf92a1`（可步入远征飞船分支）。本页是本次工作的独立交接入口。

**当前状态：两种下缘规则及可插拔点缀生成器已接入主工程，资产已保存，相关 Editor 检查和实际 Play 检查通过。用户要求尽快收尾，本轮停止扩展验证；尚未构建本批 Mono Player、运行本批联机矩阵或完成前台性能验收。** 不把历史 Player／联机结果计入本批。

## 已实现

### 两种规则均为可选 modifier

- `ICaveMaskModifier` 为纯 C# 扩展合同；`CaveModifierAsset` 提供 Unity 资产入口。新增规则实现这两个入口即可，不需要在页面缓存中增加模式分支。
- `CaveModifierStack` 保存有序、冻结的配置，最多八项。空列表关闭，列表顺序决定组合顺序；丢失引用在后台任务开始前报错。
- **下坠岩齿**保留 H5 v3 算法和用户参数 `7 / 100 / 5 / 0 / 89`（长度／密度／宽度／尖锐度／变化）。密度 100% 解除固定随机禁生区，仍受支撑、间距及净空约束。
- **花菜圆簇**保留宽肩、多瓣椭圆和距离场融合，默认 `8 / 20 / 3 / 90 / 65`（厚度／宽度／小瓣／密度／变化）。局部岩粒融合可单独关闭；点缀层使用自身色阶，不套用前景岩粒。
- 每次从原始基础轮廓重新计算，不在上次修饰结果上累积。两种实现仅增补表现下缘；当前格子、真实坡形碰撞、采矿及网络权威状态不变。冰层资产仅提供尖长形状，不包含冰材质或冰地图。

前景生成顺序：当前只读格形 → 原外轮廓 → modifier 栈 → 新距离场／岩块明暗 → 原材质 → 动态光。

### 点缀层独立可替换

- `ICaveBackgroundGenerator / ICaveBackgroundLayout` 定义背景算法和只读三槽布局；Unity 入口为 `CaveBackgroundGeneratorAsset`。当前轮廓跟随算法封装为 `ContourBackgroundGenerator`。
- `Background.asset` 独立引用生成器，并分别提供近／中／深层的 modifier 列表、显示开关和中层柔边。前景与三层背景互不强制跟随同一种规则。
- 当前生成器开放独立种子、三个层各自的密度／范围／贴边参数。后续可替换算法，复用现有着色、分页和缓存。当前渲染合同仍是近、中、深三个槽位；增加任意数量的渲染层不在本批实现范围。
- 背景仍只读保存并同步的**初始参考**；挖掘不重生成背景。修改配置后通过 R／重新进入场景创建新预览，活跃预览不追随资产修改而混用新旧页面。
- 所有 modifier 参数、顺序、生成器版本及配置进入 `VisualIdentity`，沿已有地图视觉身份校验链使用。协议仍为 **14**，存档仍为 **v10**；没有修改参考封套、YYGC 或依赖锁。

### 分页与缓存

H5 锚点采用全局优先级选取，不能在每页独立求解。开启前景 modifier 时，后台先生成同一份完整轮廓，再供各页着色。编辑后重新求解完整轮廓，并比较新旧掩码及岩粒字段；差异扩展 25px 材质距离场范围，只有受影响页面重烘焙。连续输入合并到最新版本，旧结果不发布。空栈继续原有局部分页路径。

这里仍有明确成本：每次有效前景编辑会重新计算完整 modifier 轮廓，圆簇还需要完整距离场和岩粒字段。当前不是局部增量锚点算法；CPU 峰值、内存峰值、连续采矿负载和前台帧时仍待测量。

## 策划／美术入口

编辑态调参窗口：Unity 菜单 **Dark Nights / Terrain / 岩壁实时预览（编辑态）**。默认读取固定样板 `ReferenceChamber.asset` 和现有 `Style.asset`；窗口内可打开样式、背景、点缀生成器及当前各层 Modifier 的原资产 Inspector。参数停止变化约 0.4 秒后后台重新烘焙 504×312 原生像素局部画面，也可调整取景原点、放大倍率或手动重烘焙。连续变化取消旧结果，不运行 Play、不保存场景、不改地图格子、网络或存档。**这是无动态灯光的材质预览，底墙为简化颜色；角色、光照及最终合成仍须在 Play 核对。** 窗口编辑的是共享资产，保存后会影响引用相同样式的固定样板、随机样板和远征；如需独立实验先复制资产并重新指定引用。窗口不覆盖旧的场景静态预览图，也不自动导出 PNG。

2026-09-23 窗口布局改为左侧可滚动资产 Inspector、右侧裁剪画布；在右侧中键拖拽即时平移，松开后按新取景位置重新烘焙，滚轮以指针为中心缩放（0.5–8 倍）。画布左上方显示编辑器画布刷新 FPS 和最近一次烘焙耗时；该 FPS **不是**游戏 Player／前台性能指标。原 `Map generator` 菜单和窗口标题标记为“旧版”，其生成／导出行为不变。

本次改窗后的 Unity Editor 脚本编译无错误，定向 `TerrainStylePreviewTests` 1/1 通过；中键、滚轮、Inspector 连续调整的人工画面操作尚待窗口中实际确认，不宣称 Player 性能或视觉验收通过。

2026-09-23 编辑态窗口新增后，Unity 脚本编译、Core 构建及 ArchitectureGuard 通过；定向 `TerrainStylePreviewTests` 已提交，但共享 Editor 正处于 Play／测试场景恢复冲突，Test Runner 未得到可用结果，已请求取消本次测试任务。此项和窗口实际拖动观察仍待退出 Play 后验收；不把编译通过写成视觉或 Editor 测试通过。

2026-09-23 后续复验：退出 Play 后，`TerrainStylePreviewTests` 定向 Editor **1/1 通过**，确认固定地图与当前资产只读烘焙、背景开关改变画面且源格子字节及视觉身份不变。窗口实际拖动、不同缩放与最终相机光照对照仍未作为人工视觉验收签署。

资源目录：`Game/Assets/DarkNights/Res/Terrain/StrataCave/`。

| 资源 | 用途 |
| --- | --- |
| `Style.asset → Modifiers` | 前景有序规则列表；当前默认 `RoundedRock` |
| `Background.asset → Generator` | 当前引用 `ContourDecor`；可换成后续生成器资产 |
| `Background.asset → Near/Middle/Deep Modifiers` | 三个层分别选择规则；当前均引用 `RoundedDecor` |
| `Modifiers/DownwardRock.asset` | 用户确认的下坠岩壁参数 |
| `Modifiers/DownwardIce.asset` | 长 15px、尖锐度 90% 的形状预设 |
| `Modifiers/RoundedRock.asset` | 花菜圆簇及局部岩粒融合 |
| `Modifiers/RoundedDecor.asset` | 相同圆簇轮廓，关闭前景岩粒融合 |
| `Modifiers/ContourDecor.asset` | 原 v16.1 点缀生成器及三个层的生成参数 |

要切换下坠模式，把目标列表中的圆簇资产替换为 `DownwardRock`；要关闭则清空列表。独立样板 `ReferenceChamber.unity`、`RandomCave.unity` 已于 2026-09-23 保留 GUID 移至 `Res/Scenes/Workbenches/Terrain/`，与当前默认远征仍引用相同 StrataCave 样式；见[场景索引](SCENES.md)。首版安装菜单拒绝覆盖已有 modifier 资产，后续直接在 Inspector 编辑和另存资产。

## 本批真实验证

| 检查 | 结果与边界 |
| --- | --- |
| Core 构建 | C# 9／.NET Standard 2.1，0 警告、0 错误 |
| H5 算法对照 | 原 JS 独立生成的 12 组轮廓哈希逐像素一致；覆盖两种规则、不同强度与宽图。源 JS 原样冻结在 `tools/terrain-modifiers/reference` |
| 页面与编辑 | 36 组完整 RGBA 切片与整图一致；验证输入不变、原固体保留、连续附着、深度上限、栈顺序、确定性及编辑失效覆盖 |
| 原有地形回归 | v16.1 六轮廓／岩壁／背景金样仍通过；24 生成向量、100 个资源分布种子、100 个洞穴隐藏图检查通过。没有重跑 Core 1048 全集 |
| 架构守卫 | 483 个手写文件，12 个守卫自测，0 错误 |
| Unity Editor | 48/48 相关用例通过，包含新增 modifier 的 9 项；资产保存、场景保存重开、独立背景实现替换及配置冻结通过。不是完整 Editor 全集 |
| 固定样板实际 Play | 无 modifier、下坠、仅前景、仅背景、圆簇五种配置正常；实际行走 0.9375 格；连续三次破坏，背景提交 6→6，前景 6→8 |
| 随机样板实际 Play | 下坠与圆簇正常；实际行走 0.9375 格；连续三次破坏，背景提交 6→6，前景 6→7 |

Play 图像来自 Unity 实际相机，Linear，原生 8px／格。运行时对比配置均为临时克隆，未回写资产。两种模式在固定样板的生成就绪约 1.27s／1.66s，这只是本次 Editor 观察值，包含调度，不代表目标设备性能门槛。

固定场景另存了 `ReferenceChamberModifiersPreview.png` 作为新的 504×312 原生 Editor 预览，已通过 Unity 导入并保存重开 Sprite 引用；旧 `ReferenceChamberPreview.png` 字节保留。

随机地图首个验证请求在进入 Play 的脚本域重载中被清除，查询返回 `Job Not Found`；确认请求已不存在后重新提交，最终检查通过。没有把被清除的请求算作通过或重复启动活跃任务。

冻结证据位于 [本批证据目录](evidence/terrain-modifiers-2026-09-22/results.json)，完整工作报告和截图保留在 `artifacts/terrain-modifiers/`。

![Unity 固定样板：花菜圆簇](evidence/terrain-modifiers-2026-09-22/fixed-rounded.png)

![Unity 固定样板：下坠岩壁](evidence/terrain-modifiers-2026-09-22/fixed-downward.png)

## 待验证与接续步骤

1. 本批 **Mono Player 尚未构建**，正式开局、独立 Host／Client、晚加入、重连和保存恢复须复用同一个新构建验证；旧飞船 Player 不含本批实现。
2. 模式／参数不同的两个客户端，需实际验证视觉身份不一致时拒绝就绪；当前只完成配置身份和现有校验接线检查。
3. 测量大地图连续编辑、页面切换、取消和换世界的 CPU／内存峰值，以及前台 p50／p95／p99 与 GPU 上传。当前截图、后台吞吐和 Editor 就绪时间不代表前台性能通过。
4. 尚未进行 IL2CPP、双机器、全洞穴角色路线和最终美术验收。需要 IL2CPP 时仍按项目约定先取得明确授权。

接续工具已保存：`ModifierEditor.Build` 是尚未执行的单次 Mono 构建入口；`tools/walkable-ship/test_network.py` 新增可选 `--player / --output-root`，可复用现有正常／弱网检查而不覆盖旧证据。`ModifierPlay.Run` 和 `ModifierEditorBatch.Run` 可按改动影响重跑，已通过项目无需无理由重复。

## 产物与清理

- 主工程源码、新 modifier 资产和本批证据纳入 Git；独立 H5 预览仍保留在 `D:/Downloads/方案/`，未覆盖用户调参。
- 本批 .NET 可重建输出直接集中在 `D:/Downloads/方案/临时待删除/20260922-terrain-modifiers/dotnet`，不散落源码目录。此前本会话同类清理被自动审批以 `blocked by policy` 拒绝，本轮未重试删除；清单、体积和后续条件见本批 `cleanup.json`。名称不表示自动删除授权，实际释放空间为 0。
- 复用 Local 唯一 Unity Editor 及现有 Library，没有另建缓存、Player 或修改包依赖。检查结束退出 Play 并恢复原固定样板场景。
- 中文提交并推送本功能分支；本轮没有合并 `main`，没有创建 PR。
