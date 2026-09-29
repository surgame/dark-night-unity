# 地形生成阶段 Modifier

2026-09-30，分支 `ft-20260930-terrain-modifier-pipeline`。首版 `c4d9f3e` 按要求只导入、不测试；用户随后授权验收。本轮纯 Core **1903/1903**、Editor 定向 **181/181**，Mono 构建成功。用户要求先收尾后停止扩展验收，未启动 Player 或联机矩阵；剩余边界见文末。

## 产生截图中斜向阻断的链路

`TerrainGenerator.GenerateCave` 产出天然洞穴后，`PlanetTerrainGenerator` 清理天空、铺设保护泊位，再执行下洞步道。旧 `CarveWalkway` 逐列清出头部空间，同时把脚下两格写成材料 `2`。斜线跨过已有空腔时，就可能形成截图中的石带。此前删除的是天然洞穴里的塌方 `Cover` 与竖井 `Shelves` 回填；它们和泊位后的步道属于不同生成阶段。

## 当前结构

权威地形阶段固定为 `AfterCave → AfterSky → AfterDock → BeforeGeometry`。每阶段按 `WorldSession.asset → ExpeditionFlowConfig.Modifiers` 中的原始顺序执行适用步骤；完成后统一生成坡形、筛选矿床并捕获背景。`ITerrainGenerationModifierConfig` 是可序列化的作者接口，使用 Unity `[SerializeReference]` 保存具体类型；Editor 用 YYGC Objects 同样的 Odin `PropertyTree` 绘制类型选择与字段。作者对象只在主线程读取，进入后台前由 `FreezeModifiers()` 转为不引用 Unity 的 `ITerrainGenerationModifier`。业务实现可声明阶段并在该阶段访问 `TerrainGenerationContext` 的权威材料、保护位和软岩。

原步道已迁为 `EntranceWalkwayModifierConfig → EntranceWalkwayModifier`，默认启用，位于 `AfterDock`。可调净空 3–7 格、最多 1–256 个候选支撑点；比较修改前后的空腔分量，若步道分隔原有空腔，则在保留步道支撑、保护格和基岩的前提下开凿局部绕行孔，最多开凿 256 个岩格、处理 16 次分量分裂，再重新检查。仍不安全则尝试下一候选，候选耗尽时明确失败。关闭此项可直接观察天然洞穴与泊位的结果，但不保证存在从泊位步行进入洞穴的路。旧资源缺少新列表时仍视为默认步道，首次 Apply 会把显式步骤写入正式配置。显式空列表代表没有附加权威步骤。

现有 `ICaveMaskModifier` / `CaveModifierAsset` 仍处理岩壁外观和点缀；其输入是表现遮罩，不能改权威格子。它与权威生成步骤属于不同类型的阶段，不再把所有 modifier 限制为点缀层。新增需要改权威通行的业务步骤实现 Core 接口及 Runtime 作者配置；只改外观的步骤继续使用现有遮罩链。

## 编辑入口与身份

- `Dark Nights / Terrain / Cave Wall Tuner`：在正式星球与预览种子下展开权威步骤列表，选类型、启停和调参；重建后显示各步骤的阶段、变更格数和包围范围。保存正式生成配置才写 `WorldSession.asset`，取消恢复草稿。临时拆填依旧只作用于当前预览。
- `Dark Nights / 配置 / 星球与航程`：同一列表与星球目录共用 Apply／Cancel，蓝图缩略图使用冻结步骤。运行时工作台只读取正式配置，不维护第二份接口列表。
- 草稿深拷贝逐项调用具体配置的 `Copy()`，避免依赖 `JsonUtility` 对嵌套接口引用的复制行为。内容身份和冲突检查显式包含类型、顺序、启停及参数；修正后的生成器版本为 4，步道身份为 `entrance-walkway-v2`。现有地图与存档不自动重生，需生成新地图观察变化。

## 本轮已验证与修复

首轮纯 Core 为 1584/1613，29 个生成组合因步道分割空腔且没有安全替代路线而失败。新增有界绕行修复后，通过 1903/1903；更新几何探针为当前主角 16×44 权威占地后同样通过 1903/1903。另修复首版错误的矿床过滤：矿床允许锚定在地板上方空格，不能用“材料非空”作为保留条件。

Unity 6000.4.9f1 首轮测试编译发现新增测试的命名空间冲突及 Odin 测试程序集缺少引用，修正后定向批次 **181/181，失败 0，跳过 0**：

| 覆盖 | 结果与实际边界 |
| --- | --- |
| 101 个固定种子 | `STRATA-0922` 加 `OPEN-CAVE-0..99`，不依赖随机重试；修改前每个空腔不被拆分，全部房间中心连到入口空腔 |
| 原 100 种子失败复现 | `HundredFinalMapsKeepEveryRoomInTheSameOpenCavity` 本次实际运行并通过，移除旧 Explicit 隔离标记 |
| 阶段与开关 | 屏障次序、同阶段列表顺序、标记差异诊断、冻结后作者修改隔离、关闭步道与空流水线结果一致 |
| 接口配置 | 真实 ObjectDefinition 资产保存、卸载、重导入保留具体类型与参数；Odin 属性路径解析、Unity managedReference 类型设置、Undo/Redo、草稿复制、取消与冲突检查 |
| 共用入口 | Tuner、ReferenceChamber、RandomCave 最终格子一致；实际航程与 Tuner 在步道开启／关闭时的材料、坡形和背景一致 |
| 真实角色 | `FLOW-PURE-12` 默认泊位与高泊位，从选择、着陆、下船、下洞到回船，走可信 Runtime 输入，核对当前速度和碰撞占地 |
| 航程回归 | 权限、对抗输入、准备／预览、着陆、当前格式保存恢复与内容指纹等同进程 Editor 回归通过 |
| 渲染与构建 | 实际 TerrainPreview 离屏渲染并保留截图；Mono development Player 构建成功、退出码 0，未启动 Player |

完整 [Editor XML](evidence/terrain-modifier-20260930-editor.xml)、[Core 报告](evidence/terrain-modifier-20260930-core.json)、[本轮摘要](evidence/terrain-modifier-20260930.json)。截图为 `artifacts/terrain-modifier-tests/tuner-formal-map.png`，Player 为 `artifacts/terrain-modifier-tests/player-mono/DarkNights.exe`。初次失败、修复后报告及构建日志均保留在该任务目录，未改写旧证据。

## 收尾时剩余未验证项

1. 同一新 Mono 的实际启动、Host＋Client 地图／背景／指纹一致性、晚加入、重连和实际写盘后进程重启恢复；正常网络及弱网均未执行。
2. Odin 原生类型选择弹窗的鼠标操作、面板完整重开／Editor 重启后的人工体验；属性树、序列化、Undo 和草稿逻辑已测，不能等同于原生 UI 操作验收。
3. 用户原截图位置的真实角色人工通行、多个种子的全部洞室往返与美术观感；101 种子空格连通不是整条角色可达路线，真实往返仅覆盖上述一个种子的两种泊位。
4. 净空／候选数量的极端组合、额外业务 Modifier 组合，以及绕行算法的大样本耗时和前台性能；当前默认配置的算法样本通过不替代这些门槛。
5. 生成版本 3 或更早内容指纹的历史存档行为。当前格式新建世界保存恢复已测，不宣称旧指纹兼容。
6. IL2CPP 与双机器未执行；后续 IL2CPP 仍需单独明确授权。
