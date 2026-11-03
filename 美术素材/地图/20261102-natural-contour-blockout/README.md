# 纯色天然轮廓研究 · 体量未采纳

2026-11-02，本聊天／Local，CL-01／08。用户要求移除长硬斜面、重复方格及纹理图案。三份Custom SVG保留贝塞尔曲线，designs.json记录初始控制点和调色；无生图或外来纹理。原生Mesh从同一曲线确定性细分，误差目标0.0018 Unity单位，地形只使用颜色。

9.6单位宽的侵蚀拱厅、岩肩抱厅、偏心深厅已输出到`Game/Assets/DarkNights/Res/Scenes/Workbenches/Terrain/EntranceWhiteboxAssets/NaturalContours/`。用户认为体量仍过大，不可作为已采纳模块。首轮预览还漏继承原主角父级缩放4，错误预览和回执原位保留，不代表真实角色尺度；角色仅在预览中显示，不包含在地形Prefab。

`make-natural-contours.py`负责首批来源，`create-natural-contours.csx`通过现有单一Editor写新增资源并拍摄，使用独立PreviewScene及相机culling mask。用户原Play持续运行，原活动Scene与已加载数量保持，无Game脚本或依赖改动。

当前用户要求先定模块标准。建议草案1U=3.2×3.2单位，允许2×1、2×2等整数占格，M01先考虑2×1，尚未批准为正式合同。7.2单位紧凑几何与纠正角色比例的重拍只有脚本准备，未执行。实际状态归DEVELOPMENT，证据归`artifacts/natural-contour-blockout-20261102/`。

本目录仅为视觉候选，无WhiteboxEntry、业务状态或碰撞后端，未将曲线冒充现有12坡形，未执行人物运行、游戏测试、Player或运行性能验收。
