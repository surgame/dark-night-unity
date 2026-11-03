# Blender 洞穴风格小样

这是独立的局部视觉研究，不是正式地图、可玩场景或完整随机地图。使用 Blender 4.4.3，正交相机和 Cycles，输出 1440×810 图像；游戏视角与斜角结构图分别为 `cave-game-view.png` 和 `cave-depth-inspection.png`。

`cave-style-study.blend` 已打包三个图像，可在 Blender 中编辑、切换视角；模型按地形、细节、背景、人物和灯光相机分集合。地面顶面、前沿和顶棚均为真实三维网格，远景是单张不透明绘图平面；右侧矿石和石块为简单三维体，人物是平面尺度参照。材质不是最终可拼接资源，实际洞口与岩体轮廓仍有较大美术差距。

## 输入

复用“设计中间版地图风格”聊天的素材，仅用于本次独立研究；不覆盖源文件。路径根为 `C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a18683-ec6b-7c81-82f7-5707635d072c/`：

| 输入 | 原生尺寸 | SHA-256 |
| --- | --- | --- |
| `painted-map-workbench-v5/opaque-rock-source.png` | 1254×1254 | `89da9361f6ccf5f5a29a2a54cd4e683c23358da6c8c305f4e8bcd03b64b9bbc9` |
| `painted-cave-depth-v4/far-source.png` | 1942×809 | `32fea4bcf1e359b6f84cc5587a3353a6b2c7a644a2bb1c8ab541edec8b4b0fa` |
| `painted-cave-rebuild-v2/hero-embedded.webp` | 282×309 | `8e115d98a9c9859106277b2ec27380cb809ac3051b3a076d45023bd2f47c2290` |

前两张为关联聊天生成的画面素材，人物来源按关联聊天的素材记录追溯。打开现成 `.blend` 无需外部贴图；重新运行 `build_scene.py` 则需要上述原路径，脚本会重建并覆盖本目录的 `.blend` 和两张渲染图。请先保全手工修改。

```powershell
& 'D:\Program Files\Blender\blender.exe' --background --factory-startup --threads 6 --python 'D:\Developer\MiniGames\Dark Nights\unity-projects\experiments\blender-cave-style-v1\build_scene.py'
```

## 后续边界

- 完整随机地图可考虑继续由现有地图生成器产出权威二维格、坡形和矿层，再按区块派生地形网格、边缘、装饰与可见顶面；Blender 用于制作材质、模型和验证，不负责运行时权威状态。
- 采挖需要以真实格及坡形重建受影响区块的表面、侧壁、碰撞和矿层显露；需处理区块共边、贴图密度、采挖帧耗和联机一致性，本样板没有实现或测量。
- 当前平面远景不代表三维视差已实现；矿壁低多边形、路面纹理、连续岩面重复和轮廓生硬均未达到参考图。也未验证导出 Unity、运行性能、碰撞、角色动画或多屏拼接。
