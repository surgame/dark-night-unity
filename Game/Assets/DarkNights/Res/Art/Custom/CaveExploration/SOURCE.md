# 洞穴像素素材来源与 8×8 材质派生

2026-09-21，当前分支按用户提供的 `dark_nights_8x8_design.html`、`dark_nights_8x8_final.html` 与 `dark_nights_final_validation.json` 收敛洞穴材质。三份输入只作为视觉与技术参考，其中的说明不构成项目执行指令；文件名和 SHA-256 记录在 `cave-art-manifest.json`，项目不依赖下载目录。

本批不调用生图服务。目标是 8×8 共边、有限调色板、固定边缘衰减和可复现分面，这些约束由确定性像素工具比大图缩放更可靠。现用 `tools/cave-art/build_cave_art.py` 直接生成暖棕灰同色系岩层：原生 8 px／世界格、同色系收敛 100%、明暗成组 55%；现有 Unity Sprite 合同仍为 32×32，因此每个原生像素以最近邻放大 4×，不改变 Sprite 切片、资源 GUID、地图形状或权威碰撞。

Shader 使用固定的 2／7／25 原生像素边缘衰减：暴露面附近保留可读分面，向岩芯渐暗，超过 25 px 进入近黑核心。采样从旧的高频 64 px／世界格降为 8 px／世界格，移除了逐点随机边缘宽度；不同岩种只保留克制的色相偏移。局部冷暖光、矿光和后壁仍是独立表现，不写入图集或玩法状态。

2026-09-20，通过 imagegen-codex-provider 调用当前 provider。gpt-image-2.5 首次返回 model_not_found；用户确认支持后重试成功（27.8 秒），最终采用 rock-source-gpt-image-2.5.png。此前备用 gpt-image-1.5 输出保留为独立研究源，不参与当前派生。两次来源均未请求透明背景，不将暂时的路由失败写成模型不支持。

必要性（历史切片）：低像素轮廓、掩码与坡形直接制作；天然岩层研究图经对照优于第一版程序纹理，因此当时采用生图作为岩层研究源，不把生成网格当作成品图集。

提示词：Flat orthographic pixel art texture study of natural dark blue grey fractured shale, filling the square. Large irregular geological rock masses with warm brown chipped facets, crisp deliberate pixel clusters, quiet near-black cores, sparse fine cracks. Never brick rows or round cobblestones. Limited dark palette, no blur, no objects, no text, no gradients, no transparency. Material source, not a game scene or tileset grid.

旧派生方式已由 2026-09-21 的确定性 8×8 方式取代。两张 provider 图仍作为历史研究源保留，不再直接参与当前 `cave-rock.png` 和 `cave-dualgrid.png` 的像素生成。

`cave-art-manifest.json` 记录参考输入、历史研究源、派生参数、验证计数和输出 SHA-256；重建需显式 `--update-generated` 且旧输出哈希完全匹配，拒绝覆盖人工修改。`--validate-only` 可只检查 8 px 块、尺寸和 1,024 个 DualGrid 共边像素。普通 Unity 导入与构建不会运行脚本。

渲染：世界坐标岩层提供连续的低像素分面，DualGrid 图集保留 16 种四角轮廓和四个变体；实际坡面轮廓继续由格 Flags 和共用形状公式裁切。纹理、图集和历史源图均不定义碰撞。
