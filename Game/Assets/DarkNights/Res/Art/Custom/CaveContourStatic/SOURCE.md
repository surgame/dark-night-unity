# H5 v16.1 静态轮廓背景源

输入：`D:/Downloads/方案/dark_nights_material_pipeline_explainer_v16_1_soft_middle.html`。
原件冻结于 `tools/contour-reference/source-v16.1.html`；SHA-256 与掩码字节哈希见同目录 `source.json`。

本批不需要生图。目标为精确二值轮廓、变体共边和中层内侧 Alpha，选择直接提取 H5 的 8×8 原生掩码，保留 4×16 库；没有缩放大图或像素滤镜。导出图集 128×32、透明背景、Point、无 mipmap、无压缩、数据纹理非 sRGB。

`tools/cave-art/import-contour-source.py <H5路径>` 在空目标生成图集、源清单和 `BackgroundMaskAtlas.cs`；拒绝覆盖已有 PNG。生成 C# 是掩码只读镜像，禁止手改。运行算法使用该镜像，不依赖 PNG 的 CPU 可读性。

`tools/contour-reference/export-golden.cjs` 用 Chromium 导出原生 504×312 金样：冻结源、三层硬边和中层 0／2／4px。`tools/TerrainRegression/BackgroundRegression.cs` 对照这些金样。源占用不是正式玩家存档；正式参考保存材料／坡形与 SHA-256，渲染在同一逻辑坐标中重建。

背景颜色沿 H5 默认 warm_rock／coherence=1 的 `materialPalette()[4] = (92,70,50)`，没有光照或矿图烘焙。生成三层 RGBA 保留 straight alpha，运行 Texture2D 按 sRGB RGB 解码到项目 Linear。旧对照场景的前景 AI 岩层来源仍见 `CaveExploration/SOURCE.md`；新 StrataCave 不使用该源。新前景按同一 H5 的 Voronoi 分面、暖岩调色板、距离明暗直接生成原生 8px／格 RGBA，岩块当前 4px。独立外轮廓为 HybridB／OUTLINE-0921／3px／20px／2px。所有算法金样、重建入口见 `tools/contour-reference/README.md`，没有新的生图或缩小像素滤镜。
