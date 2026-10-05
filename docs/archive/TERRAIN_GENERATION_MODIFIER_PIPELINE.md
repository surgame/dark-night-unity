# 地形生成阶段 Modifier

2026-09-30，分支 `ft-20260930-terrain-modifier-pipeline`。首版 `c4d9f3e` 按要求只导入、不测试；用户随后授权验收。本轮纯 Core **1903/1903**、Editor 定向 **181/181**，Mono 构建成功。初次收尾按用户要求停止扩展；本次再次授权后复用同一 Mono，完成六组独立进程正常／弱网矩阵，累计 **945/945** 项断言。协议 **17**／存档 **v13**，剩余人工、性能和后端边界见文末。

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
| 渲染与构建 | 实际 TerrainPreview 离屏渲染并保留截图；Mono development Player 构建成功、退出码 0，后续独立进程补验复用此产物 |

完整 [Editor XML](evidence/terrain-modifier-20260930-editor.xml)、[Core 报告](evidence/terrain-modifier-20260930-core.json)、[本轮摘要](evidence/terrain-modifier-20260930.json)。截图为 `artifacts/terrain-modifier-tests/tuner-formal-map.png`，Player 为 `artifacts/terrain-modifier-tests/player-mono/DarkNights.exe`。初次失败、修复后报告及构建日志均保留在该任务目录，未改写旧证据。

## 独立进程联机补验

用户再次授权完成联机测试并关机。复用 `artifacts/terrain-modifier-tests/player-mono/DarkNights.exe`，未启动 Unity、未重新构建、未改生产代码或 YYGC。六组串行运行，使用同一 Player 与 Core／Runtime／Entry／View 程序集哈希；弱网通过真实 UDP relay 注入 **200 ms RTT + 5% loss + 25 ms jitter**，核验实际丢包与乱序。

| 网络 | 人数 | 驾驶者 | 断言 | ArrivalSync 暂停阶段 |
| --- | ---: | --- | ---: | --- |
| 正常 | 2 | 来宾 client1 | 130/130 | 钩子未捕获，明确不计覆盖 |
| 正常 | 4 | 房主 | 160/160 | 钩子未捕获，明确不计覆盖 |
| 正常 | 4 | 来宾 client1 | 168/168 | 实际捕获并通过 |
| 弱网 | 2 | 来宾 client1 | 141/141 | 实际捕获并通过 |
| 弱网 | 4 | 房主 | 172/172 | 实际捕获并通过 |
| 弱网 | 4 | 来宾 client1 | 174/174 | 实际捕获并通过 |

累计 945 项是六组断言之和，不是去重用例数。覆盖地图／背景／航程内容指纹一致性、唯一主角、驾驶竞争与权限、Preparing／Transit／ArrivalSync 的已捕获暂停和重连、去重／旧序号、加载 epoch、悬停与松手缓降、自动着陆下船及回船。每组实际写盘并停止所有 Player，再依次启动新进程恢复 Orbit／Descent／Landed；共 **18 份 v13 存档**重新核验 SHA-256，恢复检查包含原人物、库存、船姿态、最终格子／背景及生成指纹。退出后全部日志未发现运行时异常，测试与 Editor 进程均已退出。诊断截图不计人工视觉或前台性能通过。

首两次定位超时、四人来宾弱网首次 Ready 后缺少主角投影、随后旧驱动忽略新 epoch 的 `EpochChanged` 回执共四份失败报告均保留。只修测试驱动：显式存档版本、自适应提前制动、受控主角启动屏障以及按请求序号匹配跨 epoch 的拒绝回执；不接受跨 epoch 的 `Applied`。回执匹配定向断言另为 6/6。后两项增强只重跑失败配置；前五组已通过的实际断言保留原报告，摘要分别记录驱动哈希，不冒充同一脚本版本。

完整 [联机摘要](evidence/terrain-modifier-network-20260930.json) 保存六组身份、计数、原始报告路径、relay 统计、未覆盖项及失败轮次。原始证据目录为 `artifacts/terrain-modifier-network-20260930/`，共约 **42.09 MiB**；日志、隔离存档、截图和失败轮次均为唯一证据，保留同一 Mono 供后续验收。本批没有新增 Unity／生成器缓存或可移出的重建中间产物，未删除或移动文件，实际释放空间为 0。

## 当前剩余未验证项

1. 正式目录只有一颗启用星球，不能覆盖不同目的地竞争；同目的地竞态已测。正常双人与四人房主组的 ArrivalSync 暂停未捕获，其他四组已覆盖；逐阶段全新进程晚加入仍未全覆盖，本轮短暂阶段主要为同身份重连，稳定阶段覆盖重启后的进程晚加入。
2. Odin 原生类型选择弹窗的鼠标操作、面板完整重开／Editor 重启后的人工体验；属性树、序列化、Undo 和草稿逻辑已测，不能等同于原生 UI 操作验收。
3. 用户原截图位置的真实角色人工通行、多个种子的全部洞室往返与美术观感；101 种子空格连通不是整条角色可达路线，真实往返仅覆盖上述一个种子的两种泊位。
4. 净空／候选数量的极端组合、额外业务 Modifier 组合，以及绕行算法的大样本耗时和前台性能；当前默认配置的算法样本通过不替代这些门槛。
5. 生成版本 3 或更早内容指纹的历史存档行为。当前格式新建世界保存恢复已测，不宣称旧指纹兼容。
6. IL2CPP 与双机器未执行；后续 IL2CPP 仍需单独明确授权。
