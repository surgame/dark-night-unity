# 简洁Q版主角左右向设计

2026-11-04用户否定v1，指出造型不够简单利落、不够Q，并明确横版游戏只需要左右向。本片重新探索，原稿保留来源。

## 制作规格

横版1536×1024设计图，浅色不透明背景。只展示严格正侧面的左右站姿与右向侧面T-Pose。保留浅蓝头罩和小脸身份，采用约1.6头身、巨大头部、极小身体、短小四肢、平涂与少量阴影。取消纹理、分节关节线、护腕、厚靴等碎细节。设计板仅作造型与拆件参考，实际透明附件、关节补绘和Spine绑定另行制作。

## 完整生成提示

```text
Use case: stylized-concept.
Create a genuinely cute, extremely simple and clean 2D SIDE-SCROLLING game hero design sheet. This is a full redesign of the existing pale cyan hooded character, based on reference 1 (original head) and reference 2 (original tiny body). Preserve the memorable pale cyan oversized hood and warm little face, but improve appeal through silhouette and proportions.
The user rejected the previous textured, segmented, booted humanoid design. Make the design dramatically simpler and much more chibi. Approximately 1.6 heads tall: enormous softly squarish round hood/head, a tiny bean-shaped body, tiny soft mitten hands, very short legs with little rounded feet integrated into the suit. The head must dominate. Very small face opening near the front edge of the hood in side profile, one small dark oval eye visible, a tiny friendly mouth. The face is looking directly horizontally. Round buoyant shapes with clear negative space between feet. The body and legs should remain visibly present, small and cute.
Visual style: crisp confident smooth charcoal contours, uniform line weight, flat pale cyan fill, warm cream face, at most one slightly darker cyan shadow shape per major form. A restrained 3-color character palette plus outlines. Clean digital cartoon shapes. Absolutely no painterly texture, grain, gradients, glossy lighting, 3D rendering, armor, boots, cuffs, belts, pouches, fabric folds, ribbed neck ring, antennae, joint division lines, technical skeleton overlay or decorative markings. The neck can be one tiny simple dark curved join. Hands and feet are the same cyan suit material.
Composition: wide landscape 1536x1024 design sheet, plain warm off-white background. THREE generously separated full character drawings only, each matching the identical character and proportions. Left: pure SIDE PROFILE standing facing right. Middle: the same pure SIDE PROFILE standing facing left, an exact mirrored design. Right: the same character in a pure SIDE PROFILE T-POSE facing right, with both straight arms horizontal at shoulder height, the near arm reaching right and far arm reaching left, little hands, feet apart. Show a small but usable upper and lower arm length for future Spine cutout animation, while keeping the design tiny-bodied. No front view, no back view, no 3/4 view, no overhead angle. Only one eye visible in each view, face opening at the leading edge of the head, hood back mass behind. Keep the T-pose head and body strictly profile. Every body part fully inside the image.
The first two standing silhouettes are the visual focus. No expressions panel, no exploded parts, no additional props or characters, no title or labels, no scenery, no ground shadow. Finished polished character concept with exceptional simplicity, charm and immediately readable small-scale silhouette.
```

## 侧身修正提示

第二次调用以上一张生成图为唯一编辑输入，完整提示如下。

```text
Refine the latest three-character cyan chibi sheet. Keep its simple cyan hood, cream face, black oval eye, extremely minimal cute design, huge head, and clean thick dark outline. Keep the same three figures facing RIGHT, LEFT, RIGHT. Main correction: the bodies must be genuine SIDE PROFILE bodies matching the heads, with narrow sideways torso, little forward-projecting toes oriented in the facing direction and near limbs overlapping far limbs. The standing bodies currently look front-on with feet pointing outwards: correct this. Give the very short legs a little extra length so the thighs and calves can bend for a 2D Spine running animation. Tiny integrated feet, no separate boots, tiny rounded hands, no fingers. For each standing pose the arms hang naturally at the side, near hand near the hip, far arm only subtly visible behind torso. Keep identical exact mirrored character design for the two standing drawings. On the right, a SIDE VIEW rigging T-POSE with head AND torso facing right, near arm extended straight forward to the right, far arm extended straight backwards to the left to reveal attachments. Side-profile pelvis and legs with both toes aimed right. Keep overall approximately 1.6 to 1.7 heads tall, extremely cute and very small body, arms usable for cutout bending. Flat color fills with just ONE flat darker cyan shadow area, no gradients, NO grain or painterly texture, no interior joint lines, no clothing details. Warm off-white background, three widely separated fully visible figures in one horizontal row, generous empty margins, no words, no front or back views, no perspective or 3D rendering. This revision should stay exceptionally simple and appealing, like a carefully designed 2D cartoon sprite model sheet.
```

## 产出状态

[当前候选](hero-q-side-design-v2.png)为1536×1024 RGB不透明原生输出。左右正侧面站姿及右向侧面展开姿完整可见，采用大头小身、简化色块和轮廓。第一张身体偏正面的[中间稿](hero-q-side-draft.png)保留，当前候选已修正躯干和脚部方向。两张图的原输出路径、尺寸和SHA-256见[来源记录](provenance.json)。

静态观察仍有轻微平滑色彩变化。远侧腿在站姿中被近侧腿遮挡，后续需绘制隐藏肢体和完整关节端部；图上展开姿仅供拆件参考。当前待用户视觉评审，实际Spine附件、绑定和动作尚待制作。
