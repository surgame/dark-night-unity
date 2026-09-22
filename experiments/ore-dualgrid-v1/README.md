# 按格矿层美术 v1

2026-09-22；分支 `ft-20260922-embedded-ore-art`。按用户最新要求，矿呈连续格块，使用 AnyRuleD 四角 DualGrid 自动连接。**不是分叉矿脉**。

## 本批范围与制作选择

- 铜、铁、金、银、钻石五种矿化岩面。格内是有岩石基质的矿物纹理；相邻同种矿合成面状矿层。
- 目标为 **8×8 原生像素／格**，匹配当前 StrataCave 密度。每种矿 `16 masks × 4 variants`，单图集 **128×32 RGBA**。每个 32×32 区块包含按行排列的 0–15 连接形状，四区块横排是四个变体。
- 这是低像素共边资产，选择确定性原生像素绘制。形状、纹理和透明度全部直接在 8×8 绘制；没有使用大图缩小、像素滤镜或模型生成网格。本次方向修正后没有再调用生图 API。
- `body` 为矿化岩面，`alteration` 为稀疏低 Alpha 外缘，`merged` 为 Linear source-over 后的便捷 RGBA。没有每格独立描边；内部连接边不衰减，只有矿层外轮廓过渡。
- 铜为褐铜与少量绿锈，铁为暗石墨灰，金为赭金，银为冷灰银，钻石为灰蓝晶面；不自发光。当前是首轮候选，尚未获得最终美术认可。

## 文件

- `atlases/{copper,iron,gold,silver,diamond}-{body,alteration,merged}.png`：15 张原生图集。
- `anyruled-contract.json`：75 条非空规则、300 个非空变体的命名及 Unity 左下原点切片 Rect；另有 20 个透明 mask 0 槽位，无需规则输出。
- `manifest.json`：PNG SHA-256、布局、固定洞室放置、检查结果。
- `preview/five-ore-dualgrid.png`：五矿、三种深度色样、16 形状一览。
- `preview/cave-grid-ores.png`：504×312 原生三层洞室样板；`cave-grid-ores-2x.png` 为最近邻放大。
- `ore-dualgrid-v1-png.zip`：图集、规则映射、说明、五矿预览。
- `browser-verification.json`：可绘制样板的离线浏览器检查；`provenance.json`：来源与被撤下版本的位置。

## AnyRuleD 对接合同

已对照锁定包 `.deps/AnyRules-locked-aa450a7/AnyRuleD~/Packages/com.tsgame.anyrules` 的 `RulePrimitives.cs`、拓扑角序，以及项目的 `CaveTerrainAssets.cs`。角序为 **NW／NE／SW／SE = 1／2／4／8**。这里的顺序是逻辑角序，导出 PNG 的 Y 向下，Unity Rect 的 Y 向上，JSON 已换算。

对应目标矿种的角使用 `This`，其他已知角使用 `NotThis`；mask 0 无输出。只启用 Identity，不旋转整张图集。`ExactCell`，`CanonicalMaterialEdgesV1`；mask 15 标为 TileableFill。四个变体按空间哈希选择，边界 RGBA 锁定一致。mask 6／9 的对角矿格不在中心硬连。

不同矿种不自动融合成同一片。当前按单矿通道验证；**多矿相邻的覆盖优先级、空隙或混合边界尚未接入引擎验收**。未来导入应由 Unity Editor 批量生成 `.meta`／资源 GUID，Sprite Multiple、PPU 8、Point、FullRect、无 mipmap、无有损压缩、sRGB RGB＋straight alpha。没有手写 Unity GUID 或覆盖既有地图规则。

`anyruled-contract.json` 是美术接入映射，**不是已经创建的 Unity AnyRuleD ScriptableObject**。此轮没有修改 Runtime、权威矿床、采矿、存档、协议或 YYGC 框架；不把浏览器自动连接计作 Unity 运行通过。

## 三层嵌入

固定样板按 底墙 → 深层 → 深层矿 → 中层 → 中层矿 → 浅层 → 浅层矿 → 前景 合成，矿透明度乘所在岩层遮罩，较近岩片和前景会遮住矿层。

浅／中／深的 Linear RGB 倍率为 0.72／0.48／0.28，属于离线美术试验参数，不是实际游戏光照。五矿色样与可绘制样板用同一块原版完成岩墙比较明度，不对绘图区域施加层间遮挡；下方完整洞室样板才演示三层遮挡。当前只检查暖棕 v16.1，不能保证任意墙色都无需调整。

## 验证与重建

- 15 张图集的所有兼容水平／垂直边、全部四变体组合，**30,720 对 RGBA 完全一致**。
- mask 0 透明；mask 6／9 中央断开；各图集的原生尺寸固定。
- 浏览器 **8/8**：五矿切换、三深度、按格增加、自动连接画面、擦除恢复、格线、窄屏、脚本错误。
- 重复生成的 PNG 哈希检查见 `provenance.json`。未运行 Unity 导入、Prefab 保存重开、Play、Player 构建或性能验收。

```powershell
node experiments/ore-dualgrid-v1/export-reference.cjs
python experiments/ore-dualgrid-v1/build_tiles.py
python experiments/ore-dualgrid-v1/build_preview.py <预览片段绝对路径>
```

依赖 Pillow、NumPy；参考导出使用 Playwright／Chrome，可用 `DN_PLAYWRIGHT` 指定模块位置。图集已有清单时先校验旧哈希，检测人工修改即停止，不覆盖人工资产。背景只读提取自仓库 `tools/contour-reference/source-v16.1.html`，参考哈希记在 `reference.json`；沿用原三层，没有采用已被否决的单一噪声深浅场。

## 撤下内容与保留

较早的分叉矿脉源图、PNG 和预览按本轮新指示退出交付，保留在 `artifacts/ore-art-rejected-seams-20260922`，不提交、不导入，不删除唯一 AI 源图。源图曾通过 `1qq`（`https://sub.1qq.xyz/v1`）的 `gpt-image-2.5` 生成；它不是当前图集输入。

当前只提交按格矿层成果，不合并 main、不推送。所有现用图集、源脚本、预览与证据保留；没有新增 Unity 缓存、构建或依赖。体积与剩余空间记录在 `provenance.json`。
