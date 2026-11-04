# Q版主角与Spine制作设计候选

2026-11-04用户要求参考现有主角，探索更好看、更Q、带T-Pose且适合Spine制作动画的设计图。本候选归CL-08美术探索，保留浅蓝圆头罩、暖色小脸、点状眼睛及V形嘴的识别特征。设计方向采用约2.5头身、圆润轮廓及清楚的四肢分段，具体造型待用户评审。

## 制作规格

- 用途为角色造型与Spine拆件参考，拟制作横版1536×1024设计板，实际原生尺寸随生成结果记录。
- 设计板使用浅色不透明底，轮廓完整、视图分开。正式拆件需另行制作透明PNG，并补齐关节遮挡部分。
- T-Pose双臂水平伸展，展示肩、肘、腕和髋、膝、踝的拆分位置。增加侧向游戏视角及背面参考，保持同一角色的比例、颜色与服装结构。
- 本片为较大尺寸角色风格探索，采用内置imagegen。设计板中的关节和拆分示意仅供人工制作参考，实际蒙皮、绘制顺序与动作效果须后续在Spine中验证。
- 参考输入为原作者`../../images/head.png`、`../../images/body.png`、旧设计`../../../20261030-cave-explorer-concept/painted-spine-study-02.png`及地图`../../../../地图/20261101-hero-terrain-palette-studies/01-terracotta-strata.png`。均只读使用。

## 完整生成提示

```text
Use case: stylized-concept.
Asset type: polished 2D game protagonist character design sheet for Dark Nights, with practical Spine cutout animation reference.
Input images: reference 1 is the original cyan hood/head and establishes identity; reference 2 is the existing cave scene showing the original hero's complete proportions, face and visual world; reference 3 is an older painted explorer study used only to understand assembly, with its heavy costume simplified; reference 4 is the original simple cyan body.
Primary request: redesign the existing hero to be more appealing, cute and chibi while remaining immediately recognizable. Create one consistent design, approximately 2.5 heads tall including the hood. Large softly rounded pale cyan hood, small warm cream face opening, expressive simple black oval eyes, friendly tiny V-shaped smile derived from the original, soft eyebrows, tiny rounded body, short but articulatable arms and legs, rounded mitten hands and rounded short boots. Keep the cyan outfit and restrained charcoal outlines, a simple dark grey ribbed collar, very restrained muted cream cuff and sole accents. Use attractive controlled shapes and a hand-painted 2D finish with broad clean color areas, only subtle texture. Limbs must be long enough to bend visibly. Avoid bulky armor and excessive costume details.
Composition: landscape professional character model sheet on a clean warm off-white background, generous spacing, no decorative border. Upper two thirds show three coherent complete views at the same scale and baseline: large FRONT T-POSE, large GAME SIDE 3/4 T-POSE facing screen right with the near arm stretched horizontally forward and the far arm extending horizontally behind, and a smaller BACK T-POSE. Both arms straight at shoulder height in each T-pose, hands readable and separate from body, legs straight with a small gap between feet. The game side view is nearly side-on with just enough face visible for the original character expression, with NO overhead view. Character proportions, clothing seams and colors match across views. No cropped hands or feet, no tools covering the limbs.
Lower strip: a small pure SIDE standing view for side-scrolling gameplay, three small face-expression studies (neutral friendly, pleased, surprised) using the same face opening, and one modest arm construction diagram showing upper arm, forearm and mitten separated with complete rounded hidden overlap ends and small pivot dots. Label only with short clean English captions: FRONT T-POSE, GAME VIEW, BACK T-POSE, SIDE, EXPRESSIONS, ARM OVERLAP. Minimal typography, images dominate.
Spine-oriented design constraints: shoulders and hips have clear attachment areas, elbows and knees have simple consistent fabric segments, overlapping joint ends are concealed naturally under cuffs and rounded sleeves, no unbroken stripe or large emblem crossing joints, no dangling scarf or long straps, no baked directional cast shadows crossing separate body parts. Face features can be isolated as eye, brow and mouth attachments. Clean readable silhouette even at small game size. Natural appealing chibi anatomy with usable rigging proportions. This is a concept design sheet, not a pretend transparent packed atlas.
Lighting: soft even illustrative lighting, very subtle local volume, no dramatic rim light. No photorealism, no glossy 3D vinyl rendering, no dense grain, no pseudo-pixel-art filter, no watermark, no unrelated characters, no environment scenery.
```

## 产出状态

已生成[主角设计图](hero-q-spine-design-v1.png)，原生1536×1024、RGB不透明设计板，采用内置image_gen，原输出按字节复制入库。完整提示见上节，输入与输出尺寸、SHA-256及原生成路径见[来源记录](provenance.json)。

静态检查可见正面T-Pose、斜侧向T-Pose、背面T-Pose、纯侧面站姿、三种表情和手臂关节重叠示意，手脚完整。实际头身比接近2头身，比提示中的约2.5头身更短；背面展示尺度略小，各视图的透视和比例仍需制作时统一。图中手臂分段为拆件示意，需另行绘制完整遮挡部分、透明附件及实际骨架。

当前等待用户视觉评审。实际Spine拆件／蒙皮／动作、Unity接入及运行验证均待后续任务。
