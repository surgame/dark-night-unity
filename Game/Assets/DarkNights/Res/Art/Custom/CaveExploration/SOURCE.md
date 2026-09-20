# 洞穴像素素材来源

2026-09-20，通过 imagegen-codex-provider 调用当前 provider。gpt-image-2.5 首次返回 model_not_found；用户确认支持后重试成功（27.8 秒），最终采用 rock-source-gpt-image-2.5.png。此前备用 gpt-image-1.5 输出保留为独立研究源，不参与当前派生。两次来源均未请求透明背景，不将暂时的路由失败写成模型不支持。

必要性：低像素轮廓、掩码与坡形直接制作；天然岩层研究图经对照优于第一版程序纹理，因此采用生图作为岩层源，不把生成网格当作成品图集。

提示词：Flat orthographic pixel art texture study of natural dark blue grey fractured shale, filling the square. Large irregular geological rock masses with warm brown chipped facets, crisp deliberate pixel clusters, quiet near-black cores, sparse fine cracks. Never brick rows or round cobblestones. Limited dark palette, no blur, no objects, no text, no gradients, no transparency. Material source, not a game scene or tileset grid.

派生：tools/cave-art/build_cave_art.py 将岩层源收敛到 256×256、28 色，修复周期边缘，再按精确 NW/NE/SW/SE 掩码制作 32×32 DualGrid 图集（8 材料×16 掩码×4 变体）。Point、无压缩、无 mipmap。

cave-art-manifest.json 记录来源和输出 SHA-256；重建需显式 --update-generated 且旧输出哈希完全匹配，拒绝覆盖人工修改。普通 Unity 导入与构建不会运行脚本。

渲染：世界坐标岩层提供连续纹理，DualGrid 图集在块内部提供有限变体；实际坡面轮廓由格 Flags 和共用形状公式裁切。源图不定义碰撞。
