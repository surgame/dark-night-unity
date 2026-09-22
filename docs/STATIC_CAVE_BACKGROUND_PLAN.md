# Dark Nights：静态背景岩层与中层柔边改造执行方案

**基线：`surgame/dark-night-unity` · `main@8dbee1d05f684afb0390a2cd878a0b56395a1068`**  
**H5 目标：v16.1 中层柔边／冻结轮廓版**  
核对日期：2026-09-22。提交时间：2026-09-21 08:54 UTC；提交说明：`fix: 主角模式隐藏选择框`。本文的“main 已有”均指这个提交，不代表之后的 main。[S01]

> **实施结论：在现有 CaveVisualSource / CavePixelRock 表现链中增量接入静态三层背景，不替换 AnyRuleD、YYGC 权威格子、FishNet 或采矿系统。先做可切换的背景风格，再补齐初始轮廓的保存／同步。**

本次交付完成的是 H5 改进、源码核对和实施设计；**没有修改仓库，没有执行 Unity 编译、Play、Player、存档升级或联机验收**。文中的新增类、字段、版本和验收项均标为拟实施；仓库历史测试结果不算本次验证。

## 01｜当前可用交付与本次边界

| 交付 | 状态与使用方式 |
|---|---|
| `dark_nights_material_pipeline_explainer_v16_1_soft_middle.html` | 已实现、已在 Chromium 运行测试；单文件离线打开。 |
| 中层柔边 | 默认 2 个 H5 源像素；0–4 px 可调。0px 与同源 v16 中层逐字节一致。 |
| 软硬对照 | 下拉框“原 v16 硬边”“左硬边 / 右柔边”；两者用同一冻结源，不重新随机。 |
| 冻结源快照 | 已在 H5 补齐；地图快照 v3 保存初始最终轮廓，普通参数预设不含手工地形。 |
| main 改造 | 本文为执行方案；保持旧模式可回退。 |

### 为什么不是高斯模糊

v16 的中层已经有像素纹理，生硬主要来自**中层边界从透明直接跳到约 95% 不透明**。本次不改变岩面形状、配色或 RGB 纹理，只降低边界内侧窄带的不透明度，让下方深层／底墙透出来。[H01]

```text
原中层 Mask + RGB
   ↓
计算中层自己的内部距离（不是前景距离）
   ↓
只衰减边界内侧 Alpha
   ↓
保持内部岩面、浅层、深层、前景、矿不变
```

定义：`d` 为岩石像素到透明像素的曼哈顿距离，第一圈 `d=1`；`w` 为柔边参数。

```text
w = 0：Aout = Araw
w > 0：Aout = round(Araw × smoothstep(0, w + 1, d))
RGBout = RGBraw
```

默认 `w=2` 时，第一圈保留约 25.9% 原 Alpha，第二圈约 74.1%，第三圈起完整保留。**不在原 Mask 外生成像素、不整体模糊、不改变碰撞**。这是窄带的像素透明过渡，不是把滤镜改为 Bilinear。Canvas 内部预乘／反预乘带来的个别边缘 RGB 量化误差，在本次测试中最大为 2/255；这不意味着做了 RGB 模糊。[H01][H02]

### 本次同时修正的冻结与回读问题

v16 在同一运行中通过缓存做到“拆墙不变背景”，但原构建函数仍读取当前前景；其世界快照也没有保存初始轮廓。**缓存失效、改配色后重建，或破坏后保存再打开，可能把已破坏的地图当成新的生成依据**。此外，预设加载仅识别 `v15-` 前缀，v16 自己的背景参数可能回退默认值。[H01]

v16.1 将这两件事明确化：新世界第一次冻结 Modifier 后的 Alpha；拆造、柔边、配色变化都不替换它；世界快照保存该源。只有“以当前轮廓重新烘焙”或重置世界才换源。旧 v16 快照缺失原始数据时，导入会提示以保存时地图建立新源，**不宣称能恢复丢失的初始背景**。[H01]

## 02｜与 main 的实质差异

**main 已经有背景墙和内嵌矿，不是空白起点。** 当前交付文档描述的正式切片是远征营地，协议 12／存档 v8；同仓库旧洞穴工作台文档中的协议 11／v7 是历史记录，不能用来覆盖当前基线。[S02][S03]

| 项目 | main 已核对实现 | H5 v16.1 / 目标变化 | 实施判断 |
|---|---|---|---|
| 地形权威 | `TerrainMapAuthority`、YYGC 会话拥有活动格子；`TerrainReplicaSource` 只读复制已提交副本。 | H5 自己编辑本地数组。 | 保留 main；不移植 H5 的地图所有权。 |
| 地图入口 | `ExpeditionTerrainGenerator` 基于洞穴生成器，处理泊位保护区、首矿房通路、坡形和矿床。 | H5 是 64×40 逻辑采样、少量固定洞室。 | 不用 H5 地图替换正式关卡生成器。 |
| 前景轮廓 | AnyRules 页面渲染；Shader 按材料和 Flags 裁切真实坡形。 | 8×8 Dual Grid + 像素距离位移 Modifier。 | 本轮不改正式坡形和碰撞；Modifier 只可作为额外视觉研究。 |
| 岩面材质 | `cave-rock.png` 的世界坐标纹理 + 图集变体；Shader 多方向探测形成明暗。 | CPU Voronoi／tone／距离场烘焙。 | 两条前景风格不同，不能把 main 写成“缺少程序材质”；此次先保留 main 前景。 |
| 底墙 | `_Background` 分支已有暗后壁与地表远景，并承载矿色。 | 简单常驻底墙；三层装饰在上。 | 沿用 main 后壁载体，增加可选岩层输入；不要引入另一道入口硬边。 |
| 三层背景岩层 | 未见初始轮廓派生的浅／中／深三层缓存或配置字段。 | 沿初始轮廓生成，含内部部分；拆造后冻结。 | 主要新增项。 |
| 中层柔边 | 现有后壁没有这张中层 Alpha，因而无对应独立柔边参数。 | 中层自己的边缘距离 → Alpha 衰减。 | 新增样式参数与烘焙步骤；不是修改全局透明设置。 |
| 矿 | `SetMinerals` → `_OreMap`，背景晶簇 + 前景透岩矿光；剩余量来自正式矿床。 | H5 `oreCells` 独立编辑与采集。 | 复用正式矿床与 `_OreMap`；不要新增并行矿格权威。 |
| 光照 | `CaveLightField` 生成暖／冷光场，Shader 采样；没有变化时不持续重建。 | H5 Canvas 整图逐像素演示灯光。 | 保留 main 光照链；不移植 CPU 逐像素场景灯光。 |
| 动态刷新 | `TerrainPreview` 合并变化 Chunk 并局部卸载／加载；`CaveVisualSource.Flush()` 仍整张上传三张小数据纹理。 | 前景局部更新；背景保持原对象和像素。 | 将背景失效与地图／矿／光照失效彻底分离。 |
| 原始参考 | `SessionTerrain.Activate()` 后丢弃 initial；Capture 保存当前最终格子。 | v16.1 新增独立、只读的初始轮廓源。 | 正式接入的关键数据缺口，必须保存并送达晚加入者。 |
| 预设／存档 | `TerrainSaveJson` 固定 9 个地图字段，严格数量校验；现有正式存档并不是 H5 格式。 | H5 预设／快照／PNG ZIP。 | 需要显式导入与正式格式演进；不能直接塞字段或读取 ZIP 当玩家存档。 |

证据分布：权威／副本 [S04][S05][S08]；表现／Shader／光照 [S06][S07][S13]；生成／坐标 [S09][S10]；保存／联机 [S08][S11][S12]。

### 保留的工程前提

保留 Unity 6000.4.9f1、项目要求的 Linear 色彩空间，以及 YYGC 锁定 `12b253c6bdd262feb860ab905b9e56e940ec9c40` 的可重现依赖链；联网继续走现有 FishNet／AnyRules 适配。不要为了背景效果更新整个框架，也不要恢复旧的并行世界状态模型。[S02][S03][S14]

## 03｜目标结构：三类数据，不混用

```text
A. 当前玩法状态                         B. 初始视觉参考（不可变）
YYGC TerrainMapAuthority               初始材料/占用 + 坡形 + 样式身份
        ↓                                        ↓
只读网络副本                            固定种子派生背景三层
        ↓                                        ↓
前景页面 / 遮挡 / 当前灯光               可重建的页面纹理缓存
        └──────────────┬─────────────────────────┘
                       ↓
C. 渲染合成：原底墙 → 深 → 中(柔边) → 浅 → 正式矿 → 前景
```

B 不是第二张可编辑地形，也不参与移动、碰撞、爆破权限。C 是纯表现缓存；丢弃后能用 B 重建。矿仍由正式对象数据决定，不能被背景 PNG 中的像素代替。

### 本轮明确不改

不改房间连通、氧气、设备、经济、怪潮、背包、采矿收益，不新增自由拆硬岩或自由填墙玩法；H5 的“拆墙／填墙”是验证工具。正式爆破与采矿原路由保留。相同世界的前景、坡形、矿床数据回归必须不受新增背景影响。[S02][S03]

### 视觉范围不要悄悄扩大

v16 的三层岩层主要在**原始洞穴的空气区域**生成，底墙则覆盖整个地下，包括被前景挡住的位置。拆开原先实心岩体后，可能主要露出简单底墙；这不等于背景缓存丢失。若以后要求“原本实心区域后面也密布完整三层”，需单独改变背景生成规则并重新签署效果，不属于本次柔边补丁。[H01]

## 04｜冻结参考：新世界、拆造、晚加入、恢复

### 4.1 何时捕获

在 `ExpeditionTerrainGenerator.Generate()` 完成**泊位平整、必经通路和最终坡形重建**之后，从返回的 `PlayableTerrain` 形成不可变参考。不能取基础 `CaveExplorationGenerator` 的中间结果，否则背景会与正式出生区、通路不一致。[S10]

建议正式源保存**逻辑格材料／占用与形状码**，而不是整世界高分辨率 RGBA。320×192、每格 1 字节材料 + 1 字节形状的原始体量为 122,880 字节（120 KiB），可以 RLE/压缩；这是格式预算计算，不是当前网络包尺寸或实测压缩率。

### 4.2 拟新增合同

下列字段与类名是**建议设计，main 尚未实现**：

| 字段 | 语义 |
|---|---|
| `BackgroundBakeDescriptor.Version` | 背景参考合同版本；与正式存档外层版本区分。 |
| `WorldId` | 持久世界身份。 |
| `ReferenceMaterials / ReferenceShapes` | 初次完成生成的不可变参考，不随地图编辑变化。 |
| `ReferenceHash` | 规范字节序下的 SHA-256；校验包含尺寸、坐标和形状版本。 |
| `LayoutSeed / GeneratorVersion` | 背景排列种子与算法版本；不用载入顺序消耗 PRNG。 |
| `StyleId / StyleContentHash` | 配色、图集、Mask 变体、柔边及纹理采样约定。 |
| `Bounds / RasterPixelsPerCell` | 地图范围与背景烘焙采样密度，明确 Y 轴方向及像素中心。 |
| `Policy = InitialBakeImmutable` | 初始生成后冻结；运行中不会自动重采样前景。 |

**World Epoch 只用于网络／异步任务的陈旧结果隔离，不要混入随机种子。** 同一持久世界重连或恢复会话时，不能因会话 epoch 改变而换一套背景。

### 4.3 四种生命周期

| 事件 | 正确行为 |
|---|---|
| 新世界完成生成 | 捕获一次最终初始参考，记录描述符，后台产生视觉缓存。 |
| 拆墙／爆破／填墙 | 只更新现有前景副本、页面、遮挡和光场。背景源与三层字节不变。 |
| 矿量减少 | 更新正式矿床显示及必要矿光；不重烘焙三层岩层。 |
| 晚加入／断线重连／存档恢复 | 收到同一背景描述符及参考源；当前格子仍是最新破坏状态；两者不能相互替代。 |

`SessionTerrainNetwork` 当前发放的是地图 Epoch/Seed 与当前 AMP1 副本，且 Ready 已包含 `DataReady && PresentationReady`。新增背景参考应进入现有会话基线就绪流程：收到并校验描述符及必需参考数据、完成当前可见背景后，才算对应表现就绪。迟到任务必须核对 WorldId/Epoch，不能贴到下一颗星球上。[S12]

**不能仅凭 Seed 保证恢复。** 生成算法或样式升级、人工修改、恢复的当前格子都可能改变依据。保存初始参考 + 明确版本是本方案默认选择；如采用“只保存种子”，必须同时保存不可变生成参数、兼容算法版本并证明旧版本仍可执行，成本不一定更低。

### 4.4 正式存档与传输约束

`TerrainSaveJson.Read()` 当前要求 9 个字段，并恢复当前材料、保护、软岩、坡形、房间、矿床；`SessionTerrain.Capture()` 没有原始轮廓字段。故正式接入不能把 H5 的 `backgroundSource` 直接附加进去。[S08][S11]

建议沿既有原子存档边界增加版本化的背景参考子合同。若仍以上述 main 为基础、且工程中尚无其他版本占用，可评估**存档 v9／协议 13**作为下一版候选；这只是建议编号，执行时必须重查，不代表已升级。仅本地风格试验可以先不改协议；正式晚加入与持久化交付不能跳过这项。

恢复候选必须先完整校验再替换活动世界；损坏长度、越界形状、未知样式、Hash 不匹配不得半成功。旧档没有初始参考时，不可声称还原原貌：提供一次明确迁移并保存新参考，或保留旧表现路径。不要悄悄按已挖地形产生一份“初始背景”。

传输采用可靠、有大小上限的分块基线数据或复用已有内容分发机制；**不新增每 Tile RPC，不逐帧同步纹理，不把背景像素注册成对象**。收到源之后在本地确定性烘焙即可；客户端间实际像素一致性由测试确认。

## 05｜表现接入：保留现有 Shader 路线

### 5.1 配置与入口

`CaveTerrainStyle` 当前只有 `Shader` 和 `Rock`。建议增设可选 `BackgroundStyle` 资源，包含模式 `Legacy / ContourStatic`、模板引用、配色、采样密度、种子偏移、柔边宽度和算法版本。默认 Legacy，先复制测试风格资产，保留旧资源 GUID 和字节。[S06][S15]

`TerrainPreview.ShowBlueprint / ShowReplica` 在创建 `CaveVisualSource` 时传入只读背景描述符。异步后台仅算数据；Texture/Mesh/Material 的创建与释放仍在 Unity 允许的主线程路径，绑定同一个取消令牌与世界身份。[S04][S06]

### 5.2 合成位置

优先在现有 `CavePixelRock.shader` 的 `_Background` 分支采样新层，复用已有后壁 Quad，不先增加三张全屏透明几何。以下为拟实现逻辑：

```text
color = 原底墙 / 远景
color = lerp(color, deep.rgb, deep.a)
color = lerp(color, middle.rgb, middle.a)   // middle.a 已窄带柔化
color = lerp(color, near.rgb, near.a)
color = 原有内嵌矿表现(color, _OreMap)
color = 一次性应用项目当前照明
return opaque background
```

现有前景 `clip(solid(p)-.5)`、坡形公式和透岩矿光保留。新增岩层只是后壁的表现，不能把背景 Alpha 接成碰撞，不能让矿因柔边／关闭岩层而从玩法中消失，也不能把同一光照重复烘焙进 PNG 后再乘一次。[S07]

若最终打包为每页一张合成后壁纹理，**合成时不包含会被采掉的矿和动态灯光**。调试版保留分层开关，发布版可缓存无矿／无光的三层合成以降低采样和内存；选型以实际性能和美术对照为准。

### 5.3 像素与坐标不能直接照抄

main 的 `PlayableTerrain.CellPixels=16`；Cave Shader 又在局部坐标中按 `floor(p*32)` 采样，两者是不同概念。H5 的 `TILE=8` 与 504×312 整图也不等于正式 320×192 逻辑地图。**不要为了导入 H5 改动全局 PPU、角色尺寸、碰撞比例或正式地图边界。**[S06][S07][S09]

建立一处 `TerrainVisualCoordinates`（拟新增）转换：源数组 row 向下；AnyRules 格 V 向上/负行；Shader 使用 `floor(float2(p.x,-p.y)+.5)` 定位格中心。验证至少包含四角、负 V、Chunk 接缝、45° 与 1:2 坡。不要在不同代码里分别塞 `+1`、`-0.5`、`/16`。

柔边保存两项：**烘焙像素密度和柔边像素数**，或明确换算后的格单位宽度。H5 的 2px/8px = 0.25 格；换成 16 或 32 采样／格时，保持格比例分别为 4 或 8 样本。如果美术要的是屏幕上仅两像素的细边，则是另一项标定，不能把这些数值混称“2px”。第一轮 Unity 以同屏放大对照选定采样密度，记录进 Style 后不再漂移。

### 5.4 颜色、Alpha 和数据纹理

项目已明确 Linear；H5 Canvas 预览不能作为切回 Gamma 的理由。[S03] 正式导入时区分：

| 资源 | 用途与处理 |
|---|---|
| 背景彩色层 PNG | RGB 作为作者颜色；Alpha 是正常透明度。按项目现有 Linear 工作流正确解码，避免重复转线性。 |
| `middle_soft_opacity_r8.png` | byte/255 为中层不透明度，**不是距离**；非颜色数据。 |
| `distance_field_r8.png` | H5 前景内部距离数据；0=空气、radius+1封顶，曼哈顿度量。 |
| `terrain_signed_distance_r8.png` | 正负距离编码见包内清单，不能按普通灰度色显示后反算。 |
| 背景规则 Alpha 图集 | 4×16 掩码；采样为 Point；不替代冻结源或整图布局。 |

算法输出使用 straight-alpha 约定；单独透明绘制时使用匹配的 `SrcAlpha / OneMinusSrcAlpha`，若改成预乘，则 RGB 与 Blend 必须同时改。保留 Point 与无 mipmap 的像素风基线；柔和来自源 Alpha，而非整体模糊。颜色编码和 Alpha 混合需在 Unity 实际截图中确认，不能用浏览器字节相等代替跨引擎观感验收。[U02]

## 06｜明确到文件的执行清单

所有“新增”路径均为**拟议命名**；先检查同职责类是否已经被并行分支加入，再决定落点。现存路径是本次实际读取的 main 文件。

| 落点（相对 `Game/Assets/DarkNights/Scripts/`） | 类型 | 本轮职责 |
|---|---|---|
| `Core/Config/Terrain/BackgroundBakeDescriptor.cs` | 拟新增 | 不可变数据合同，尺寸／形状／Hash／版本校验；没有运行写权限。 |
| `Core/Logic/Terrain/BackgroundContourBaker.cs` | 拟新增 | 纯数据生成背景 Mask、色阶和中层柔边；无 Unity 场景对象、无独立世界管理器。 |
| `Core/Config/Terrain/PlayableTerrain.cs` | 修改 | 带入背景参考/描述符或其只读持有者；保留当前格数据语义。 |
| `Core/Logic/Terrain/ExpeditionTerrainGenerator.cs` | 修改 | 在正式地图最终形状定稿处捕获初始视觉参考。 |
| `Runtime/Terrain/SessionTerrain.cs` | 修改 | 生命周期内保留不可变参考；Capture/Replace 与活动地形原子一致。 |
| `Runtime/Terrain/SessionTerrainNetwork.cs` | 修改 | 现有基线同步和 Ready 接入；核对样式内容身份、陈旧 epoch；不改变逐格权威。 |
| `Runtime/Save/TerrainSaveJson.cs` | 修改 | 显式格式演进、长度／Hash 校验、旧档策略；不得直接破坏 9 字段旧读法。 |
| `View/Terrain/CaveTerrainStyle.cs` | 修改 | 引用可选背景风格；默认保持 Legacy。 |
| `View/Terrain/CaveBackgroundStyle.cs` | 拟新增 | 美术可编辑参数及固定 Mask 资源，不放玩法状态。 |
| `View/Terrain/CaveBackgroundCache.cs` | 拟新增 | 背景页缓存、纹理主线程提交、释放及背景构建计数。 |
| `View/Terrain/CaveVisualSource.cs` | 修改 | 绑定新背景页/描述符；拆分 dirty 类别；保留 `_OreMap`、当前光场与坐标矩阵。 |
| `View/Terrain/TerrainPreview.cs` | 修改 | 接线描述符和生命周期；维持旧页面刷新、Ready、取消与 Dispose。 |
| `View/Terrain/TerrainReplicaSource.cs` | 原则上不改 | 继续只读读取最新副本，禁止从这里隐式回填“初始背景源”。 |

另修改/新增：

- `Res/Art/Custom/CaveExploration/CavePixelRock.shader`：可选三层背景采样及一次照明；前景分支行为保持。
- `Res/Terrain/CaveExploration/Style/`：新增候选 Style，不覆盖已有 `CaveStyle.asset`。
- `Res/Art/Custom/CaveContourStatic/`（拟新增）：规则图集、预设、源说明、SHA 清单与 H5 金样。保留现有 `CaveExploration/SOURCE.md` 素材来源，不改写其历史来源。[S16]
- 存档外层版本、会话内容指纹和网络消息注册：在现有 Runtime/Save 与 Runtime/Network 边界内核对后改。**不要伪造本次未逐项核对的常量名或新增框架接口**。

## 07｜分阶段实施与停点

### P0：建立可回退候选

读取本提交 README、AGENTS、最新远征交付文档；执行前记录本地 HEAD、未提交改动、包锁文件和正在运行的 Editor。保留 v15/v16；建议独立候选分支。不要在未确认的共享工作区清理、覆盖或重导入整个项目。[S01][S03]

产物：基线清单、候选风格目录、H5 原始／柔边金样。若 main 已前移，重新审阅与上述文件有关的差异，不能继续把此文 SHA 当“当前”。

### P1：纯算法与离线导入

实现冻结参考合同、坐标适配、背景生成和柔边；读取 H5 冻结源 + 参数作为小尺寸对照，确认共享 Mask 边界和0px复现。再用正式含坡的 320×192 初始源测试，不将 H5 8px 逻辑当成正式碰撞格式。

停点：同输入确定性；0px 与硬边一致；柔边只改中层内带；不引入额外入口形状。达成前不进入正式网络/存档修改。

### P2：Unity 可选表现接入

先接独立洞穴工作台/候选 Style，增加 Legacy/ContourStatic 切换。复用 CaveVisualSource 后壁、矿和光照输入；主材质和坡形保持。记录主角尺度、同相机位置的无光与有光截图。

停点：前景与矿的纯数据 Hash 保持；开关背景不影响碰撞；关闭三层后回到已有底墙；填墙覆盖、拆墙显露而不换背景图案。**这一步只能宣称单机表现验证。**

### P3：正式世界参考的保存、基线同步

捕获最终初始源并贯穿 SessionTerrain Capture/Prepare/Replace、当前原子存档和会话基线。样式资源/算法版本进入视觉内容身份；客户端晚加入不从当前破坏地图自建参考。[S08][S11][S12]

停点：Host 先破坏→Client 晚加入→断线重连→退出进程→读档，背景参考 Hash 相同、三层一致、当前前景破坏和矿剩余正确。缺参考/错误版本不能静默当成有效世界。

### P4：缓存与失效隔离

首版可完整烘焙再分页，但进入地图时不能卡在主线程全量 Texture 操作。进一步拆为纯数据后台任务 + 主线程每帧提交队列；页面缓存按原参考和风格身份寻址。取消后结果不落地，退出释放全部纹理/材质/任务引用。

停点：静止状态背景 BuildCount 不增长；多次爆破/采矿不增加背景 BuildCount；可见区移动只加载已有或缺失页，页面接缝一致。内存与上传需真机 Profiler 数据支撑，不能引用 H5 耗时替代。

### P5：合并候选前验收

完成下节矩阵、实际 Player 与前台性能采样，提交本批证据和失败项。未完成正式保存／晚加入前，不把 ContourStatic 切为正式默认；不删除旧 Style 和旧 H5。是否合入/发布另外决定，本次文档不构成仓库写入授权。

## 08｜性能评估：避免把“静态”误当成“没有成本”

main 现有 `CaveVisualSource` 的地图／矿／光场都是 320×192 小数据图，统一 dirty 时 `SetPixels32 + Apply`；这不等于把 8px/16px/32px 的整世界美术纹理上传一次也很便宜。[S06]

| 计算情形 | 一张 RGBA32 | 三层 RGBA32 |
|---|---:|---:|
| 320×192 数据图 | 0.234 MiB | 0.703 MiB |
| 320×192 格 ×16 像素／格 | 60 MiB | 180 MiB |
| 320×192 格 ×32 像素／格 | 240 MiB | 720 MiB |

上表仅按 `宽×高×4字节` 计算，不含 CPU 副本、中间 Mask、光场、GPU 对齐和 mip。它说明应该优先考虑**按可见页缓存、共享颜色表／Mask、发布时预合成无矿背景**，不是建议固定采用全图大纹理。

`Texture2D.Apply` 可能将整张纹理像素复制到 GPU，即便只改少量像素；因此不能把一个小矩形的 CPU 重算直接等价为小矩形 GPU 上传。将更新限制在小页面，并批量提交；是否释放 CPU 可读副本取决于是否需要重建/恢复，须明确所有权。[U01]

建议分离的失效信号：

```text
TerrainChanged → 前景页面 + 当前地图数据 + 必要光场
OreChanged     → 矿数据 + 必要矿光
DeviceChanged  → 灯/设备光场
CameraMoved    → 可见页调度
BackgroundIdentityChanged / StyleChanged → 静态背景烘焙
```

前三类事件**不能**设 BackgroundDirty。常规爆破不重做背景并不代表“没有画面代价”：遮挡、光照和绘制仍会发生。

性能门槛建议：60FPS 目标时总帧预算 16.7ms；为背景主线程提交先设可配置的约 1ms 调度预算，再按目标机器实测调整。这是预算起点而非达标声明。验收记录静止、移动、爆破和晚加入的 p50/p95/p99 主线程时长、GC、GPU时长、上传字节和峰值内存，并与 Legacy 同场景比较。

## 09｜验收矩阵与确定性细节

| 测试 | 通过条件 |
|---|---|
| 初始布局固定 | 同参考、种子、样式和算法版本，重复烘焙 Mask/图集变体/像素输出一致。 |
| 柔边0／2／4px | 0复现硬边；2/4只降低中层内侧 Alpha；浅/深/前景/矿均不变。 |
| 碰撞与坡形 | 12种现有形状与移动/爆破回归不变，背景永不参与阻挡。 |
| 初始实体背后 | 底墙在原实体后预存；大面积挖空不漏黑图；没有凭空重长装饰。 |
| 当前前景编辑 | 背景参考 Hash、三层对象与像素不变；前景局部/全量对照一致。 |
| 矿事件 | 数量/遮挡/透岩矿光正确；不改变背景参考和前景SDF。 |
| 页边／负坐标 | 同源分块与整图一致；4方向页边、坡形、柔边内带无裂缝或灰边。 |
| 存档/晚加入 | 先挖后加入和重启恢复的背景与早入客户端一致；当前地形和矿仍反映最新状态。 |
| 样式身份错误 | 拒绝或进入明确迁移/同步流程，不静默各自随机。 |
| 反复换图/退出 | 老任务不能污染新World；纹理/材质/缓冲/订阅不持续累积。 |
| 视觉回归 | 相同镜头比较 Legacy、硬边、柔边；Linear、像素尺度、矿可读性共同签署。 |

算法移植时必须处理 JS/C# 的差异：`Math.floor` 的负值、32位溢出/无符号右移、哈希整数宽度、四角顺序、字节序、`Math.round` 与 .NET 默认舍入方式。不能改用 `string.GetHashCode()` 或 Unity 全局 Random 代替现有固定哈希。样式序列化需规范字段顺序/数值编码；任务并行顺序不得决定变体。

背景冻结源来自带坡原始格子。按页生成距离或地形邻域时读取 halo，写入裁回页内部。柔边 halo 至少覆盖 `ceil(width)+1` 样本；轮廓生成/模板/形态滤波若有更大依赖，应取整条流水线依赖范围，不仅取柔边半径。

## 10｜本次 H5 已测结果及未测部分

| 项目 | 本次结果 |
|---|---|
| 浏览器加载 | Chromium 实际运行，无 pageerror。 |
| 功能巡检 | 15个步骤、7个步骤内开关、7种显示模式跑通；中键平移不编辑地形。 |
| 与 v16 对照 | 柔边0的中层与原 v16 默认同源输出，不同字节数 0。 |
| 2px柔边 | 默认地图 6,264 个中层像素 Alpha 改变；原范围外新增像素0；未受柔边影响的像素 RGB 不同数0。 |
| 地图独立性 | 前景、浅层、深层的校验值在只调柔边时不变。 |
| 动态对照 | 3组批量拆/造，前景局部/整图一致；三层对象及字节不变；底墙/矿不变。 |
| 快照/预设 | 非默认中层参数和柔边回读；破坏后快照保存/实际文件输入恢复；冻结源与结果一致。 |
| 调色后冻结 | 破坏后切换配色再切回，原冻结源和背景像素恢复一致。 |
| ZIP | 实际按钮下载，29文件，CRC检查通过，含硬边中层/柔边不透明度/冻结源。 |
| 未测 | Unity编译、Shader实际画面、正式网络/存档格式、IL2CPP、双机器与目标硬件前台性能。 |

证据：`v16_1_test_report.json`。上述是小尺寸 H5 的功能与一致性结果，不应写进 Unity 已达标栏；没有将本批3组测试与历史仓库计数相加。

## 11｜素材包与落地注意事项

**可直接作为美术对照/算法金样：** 中层硬边PNG、柔边PNG、Alpha图、前景/底墙/矿/三层独立PNG、固定规则Alpha图集、参数JSON、冻结源JSON、世界快照。

**不能直接充当 main 的资源合同：** H5 逻辑地图、H5 矿格、Canvas 合成灯光、PNG可视化距离、H5 CRC32、本地 `world_snapshot` 格式。正式资源应有自己的 ScriptableObject/导入器、源说明与 SHA-256 清单。[S03][S16]

本版仍保留 4×16 背景 Mask 图集；v15 的8种剥落模板没有参与 v16 生成，v16.1 已从默认导出清单中移除，不再将其误列为本版必需素材。原始 v15/v16 HTML 均保留。[H01]

落地先使用经批准的颜色/纹理；不得未经选择用 H5 的程序纹理覆盖 main 现有图像派生岩层源。前景风格统一可作为下一轮独立切片：对比世界纹理 vs Voronoi，确定后再迁移，不能夹在本次“中层边缘柔一点”的补丁里。[S16]

## 12｜可追溯来源

以下仓库链接全部锁到本文基线提交；函数名用于定位，不使用会随main移动的行号。网页文档用于解释 Unity API 行为，不作为仓库已经实现某功能的证据。

[S01] [main 基线提交](https://github.com/surgame/dark-night-unity/commit/8dbee1d05f684afb0390a2cd878a0b56395a1068)：分支头、提交时间及说明。  
[S02] [EXPEDITION_CAMP_DELIVERY.md](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/docs/EXPEDITION_CAMP_DELIVERY.md)：当前正式切片、协议12/存档v8、背景矿床与验收边界。  
[S03] [AGENTS.md](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/AGENTS.md)：权限/工作区/Linear/YYGC约定；首段是当前切片，后文含历史。  
[S04] [TerrainPreview.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/View/Terrain/TerrainPreview.cs)：ShowBlueprint/ShowReplica、Ready、OnReplicaChanged、StartReplicaRefresh、OnDisable。  
[S05] [TerrainReplicaSource.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/View/Terrain/TerrainReplicaSource.cs)：只读副本/Chunk指纹。  
[S06] [CaveVisualSource.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/View/Terrain/CaveVisualSource.cs)：320×192数据图、Quad、SetMinerals、Flush、SetDevices。  
[S07] [CavePixelRock.shader](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Res/Art/Custom/CaveExploration/CavePixelRock.shader)：solid/rock、_Background、矿和光照、32倍采样。  
[S08] [SessionTerrain.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/Runtime/Terrain/SessionTerrain.cs)：Activate、Capture、Prepare、Replace。  
[S09] [PlayableTerrain.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/Core/Config/Terrain/PlayableTerrain.cs)：16px格、CampRow40、shape范围与不可变合同。  
[S10] [ExpeditionTerrainGenerator.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/Core/Logic/Terrain/ExpeditionTerrainGenerator.cs)：泊位、首矿房通路、最终坡形。  
[S11] [TerrainSaveJson.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/Runtime/Save/TerrainSaveJson.cs)：Write/Read与严格9字段。  
[S12] [SessionTerrainNetwork.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/Runtime/Terrain/SessionTerrainNetwork.cs)：AMP1/FishNet、Epoch/Seed、Ready及VisualCatalog指纹。  
[S13] [CaveLightField.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/View/Terrain/CaveLightField.cs)：Build/Spread、灯与矿光。  
[S14] [prepare-lan-sample.ps1](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/tools/prepare-lan-sample.ps1)：YYGC完整锁定SHA和隔离依赖策略。  
[S15] [CaveTerrainStyle.cs](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Scripts/View/Terrain/CaveTerrainStyle.cs)：当前样式仅Shader/Rock。  
[S16] [CaveExploration/SOURCE.md](https://github.com/surgame/dark-night-unity/blob/8dbee1d05f684afb0390a2cd878a0b56395a1068/Game/Assets/DarkNights/Res/Art/Custom/CaveExploration/SOURCE.md)：已有岩层源、派生脚本、哈希保护、资源归属。  
[U01] [Unity 6000.4 Texture2D.Apply](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Texture2D.Apply.html)：上传和CPU可读副本行为。  
[U02] [Unity 6000.4 ShaderLab Blend](https://docs.unity3d.com/6000.4/Documentation/Manual/SL-Blend.html)：透明混合约定。  
[H01] 本地已读取源码：`dark_nights_material_pipeline_explainer_v16_contour_compare.html`；本次派生：`dark_nights_material_pipeline_explainer_v16_1_soft_middle.html`。原版SHA-256与新文件SHA见 `baseline_manifest.json`。  
[H02] 本次浏览器验证：`v16_1_test_report.json`；截图：`v16_1_middle_edge_comparison.png`。不含Unity运行结果。
