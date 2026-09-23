# 天然洞穴与像素地形实验

后续实现已推进到[洞穴地图工作台](CAVE_WORKSHOP.md)：新美术、坡形碰撞和行走／破坏测试已可用。以下为早期原型与合并的历史记录，当前验证结果以新文档和证据为准。

2026-09-20，分支 `codex/cave-exploration-art`。从 `codex/map-plan-execution` 的 `e60e1fb` 创建，合入主分支 `6b7b74c`。保留两个父分支、原 Pinewatch、RandomPinewatch 和随机地图 Debug Bootstrap。

2026-09-20 后续评估：用户实测后再次确认参考图目标，并指定生图使用 `imagegen-codex-provider`，同时要求先评估低像素素材是否适合生图。该规范已写入 [AGENTS.md](../../AGENTS.md#prefab美术与内容)，不再等待 provider 路径选择。当前差异、素材方式和分阶段验收见[洞穴视觉与空间目标](CAVE_EXPLORATION_TARGETS.md)；本次只完成评估与文档，没有新增美术、斜面或运行验收。

## 本次范围与资料使用

用户要求先同步主分支装备，再分阶段尝试新地图，第一阶段重点是像素 DualGrid / RuleTile 美术与基础洞室。输入 `D:/Downloads/dark_nights_final_core_loop_map_design_v1.md` 的 04、05 章为地图目标，06、07、08、11、15、16 章提供相关约束。该文件里的实施建议和完整游戏循环是设计资料，不自动成为本轮任务指令。交易星球、飞船结算、成长、怪潮、完整矿物碎片链不在本轮范围。

## 合并的行为合同

- 四槽为手枪、矿镐、炸药、喷气背包，复用主分支的 YYGC 输入、投射物池、瞄准与表现。
- 地图分支的软岩、矿床、最终格子恢复和有限炸药数量继续保留。矿镐沿权威瞄准射线寻找首个岩格或矿床；普通岩拒绝手挖。
- 炸药成功抛出才扣数量；取消蓄力或池满不扣。服务端投射物在真实落点引爆后才提交地图变更，仍使用原十三格范围、过滤基岩及保护格。格子命令不能借背包槽直接爆破。
- 两个父分支的协议号都为 10，但字段不一致；合并后使用协议 **11**、存档 **v7**，存入独立 v7 目录。不迁移或删除旧存档。
- 新字段使支持上限的原始投影超过旧解压预算。解压预算为 768 KiB，传输封包仍限制 512 KiB；压缩后仍超限则拒绝，不截断实体。

## 洞室技术原型

独立 `cave-exploration` profile，经 `TerrainGenerator` 返回冻结蓝图，使用现有 AnyRuleD 页面显示。原两个 profile 的路径与冻结向量保持。

生成顺序：连续起伏地表及 24 列着陆台 → 10–13 个不规则洞室 → 最短连通树及三个局部回环 → 标记 Open / LooseFill / ThinRock / DeepRock → 挖出弯折连接 → 覆盖部分连接 → 保留洞室核心和不可破坏世界边界。

`TerrainBlueprint.Passages` 保留隐藏图的两端洞室、掩埋类型、折点、半径与覆盖长度。该数据只属于生成候选；不会作为客户端权限、另一个运行地形状态或自动导航路径。实验没有切换正式开局，不把这些静态元数据写成已接通的正式保存格式。

Unity 菜单：`Dark Nights/Debug/打开天然洞穴实验`。场景于 2026-09-23 保留 GUID 移至 `Game/Assets/DarkNights/Res/Scenes/Workbenches/Terrain/CaveExploration.unity`。沿用 WASD 穿墙观察、F 回入口、R 换种子、滚轮缩放、F1 面板；房间按钮按实际数量生成。这是离线观察器，不是正式主角的可玩性验收。

## 美术制作规格（待执行）

参考图用于岩壁风格与空间轮廓，不照搬 UI、人物、飞船或具体构图。目标是低饱和蓝灰／煤黑岩体，暖棕碎岩边缘，有限琥珀高光；岩块用像素团块和断裂层理组织，避免整面噪点。洞腔背景和前景承重岩壁保持层次。

- 原始艺术源与派生图集放 `Res/Art/Custom/CaveExploration`；不覆盖 Original 素材或旧地形图集。
- 基础材质：普通页岩、松散土砾、深色硬岩／基岩。先生成材质源，再按明确的边界合同制作 tile；不把生图模型输出的任意格子直接当成可用图集。
- 每个基础 tile 32×32 像素，Point、无 mipmap、无有损压缩、sRGB 输入到 Linear。游戏逻辑格仍为 16 玩法像素，图集分辨率与玩法尺寸分开。
- DualGrid 16 个四角掩码、每种至少四个纹理变体；角位顺序服从 AnyRuleD 的 NW / NE / SW / SE。透明边界、共边采样和材质交汇必须检查。
- **斜面是独立形状类型**：左右 45° 地面坡、左右 1:2 缓坡的上下半块、对应顶面斜坡、坡顶／坡脚及墙面过渡。材质与形状分开，不能只给方格圆角或缩小阶梯。
- 斜面必须有对应占据区域／碰撞高度定义，后续穿过网络、保存、局部刷新后仍保持。当前原型尚未实现这些独立形状，不能称为斜坡功能完成。
- 验收图应含洞口、非矩形洞室、长斜坡、缓坡、顶面坡、断面、材料交界和松散填充；先看近景拼接，再看整张洞室构成。

上次原型制作时未提供内置 image_gen，且当时尚未收到配置 API 路径选择，因此未调用 API、未生成新像素图。后续用户已明确指定 `imagegen-codex-provider`；未来素材批次先评估必要性，再按该路径调用。旧测试贴图仍不构成美术通过证据。

### 评估决定使用生图时的可选材质源提示词

> Use case: stylized-concept. Asset type: source textures for a pixel-art side-view underground exploration tileset. Reference image: user-provided Dark Nights cave cross-section, style only. Create three clearly separated large material studies: dark blue-grey layered shale with restrained warm ochre chips, loose warm earth and broken rubble, near-black dense bedrock. Crisp deliberate pixel clusters, limited palette, readable fractured rock masses, subtle mineral specks, no blur, no antialiasing, no gradients. Surfaces should support irregular cave walls and long diagonal slopes. Flat orthographic material samples, no perspective. No characters, UI, text, ship, furniture, water, or complete game scene. Consistent texture scale and understated ambient lighting. These are source textures to be cut into exact DualGrid masks and explicit slope pieces later, not a finished tileset.

## 验证与后续边界

本批证据见 [cave-exploration-2026-09-20.json](evidence/cave-exploration-2026-09-20.json)。Core 1048/1048；旧 Terrain 24 冻结向量／100 矿床种子与新洞室 100 种子通过；ArchitectureGuard 389 文件／12 自测／0 错误。Editor 全批 205 项首次 203 通过、2 个正式 Play 失败；定位为合并生成了重复 Item4 输入，修复后两项正式 Play 2/2 通过，按影响合并为 **205 个不同用例通过**，不是重新完整跑了一遍。

独立洞穴场景完成生成、保存、重开和实际 Play。种子 `CAVE-EXPLORATION-01` 为 10 洞室／12 通路，页面 273，全景证据使用旧测试图集，不能计为美术验收。正式 Mono 仅构建一次，产物 `artifacts/cave-exploration/player-mono/DarkNights.exe`；独立启动 6/6、正常网络 20/20；同一产物在 200 ms RTT + 5% loss + 25 ms jitter 下 20/20（实际丢弃 693、乱序 8435 包）。启动首轮的旧定义数量断言在新增弹道后失效，修正为 30 后复验通过。

合并还修复了完整投影原始尺寸 543082 字节超出旧解压预算的问题，压缩结果 7931 字节；新增压缩后超限拒绝检查。Prefab 重开时自动补全手持子对象的 SpriteRenderer 引用，保留全部 GUID。

未完成的第一阶段内容：新美术源及图集、独立斜面块和匹配碰撞、对应近景拼接验证。洞室技术原型的隐藏图连通不代表真实角色／喷气能力可往返，也不保证每个掩埋点都没有绕行路线。后续需对最终栅格做通行验证、掩埋区域开挖和保护约束，再考虑接入正式开局。

## 本批清理

已将 Unity 捕获用的临时 Assets/Temp 图像移出并删除临时资产。工具 bin/obj 与 `artifacts/cave-stage` 共 76069757 字节的后续清理被自动审批拒绝，未重试、未改工具绕过；路径和体积见本批证据。现有 Editor Library／依赖缓存继续复用，Mono、报告、截图和测试恢复存档按后续验收需求保留。
