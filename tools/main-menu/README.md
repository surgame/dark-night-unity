# 主菜单背景重建

- 用途：原生UITK全屏菜单的静态插画背景，不是可拼接tile、地图几何或碰撞来源。
- 原生尺寸：768×480、RGBA PNG；不要求共边，不使用透明裁切，不承诺低分辨率地形tile的形状合同。左侧暗部及上下暗角已在源图绘制，菜单文字不是图片的一部分。
- 来源：2026-10-22本会话已确认的交互设计，确定性Canvas绘制，种子71893。保留树林、停泊飞船和暖光的方向；没有导入外部素材或使用AI生图。
- 方法理由：延用已确认原型的确定性绘制，避免重新生成插画导致构图变化；没有修改既有作者图、名称或LOGO。
- 单一源：同目录`landscape-source.html`。`export-landscape.cjs`直接导出原生canvas，无截图缩放、滤镜或模型网格。`Game/Assets/DarkNights/Res/UI/MainMenu/Artwork/MenuLandscape.png`为派生资源，首次安装设置Point、无mipmap、无有损压缩；Linear工程设置不变。
- 重建：准备Node.js及Playwright，设置`NODE_PATH`指向其安装目录；执行`node tools/main-menu/export-landscape.cjs <新的输出PNG绝对路径>`。默认使用已安装Chrome，或通过`CHROME_PATH`指定。导出拒绝覆盖已有文件，替换正式派生资源前须审阅差异。
- 原生尺寸检查：已查看768×480输出，左侧文字安全区、右侧飞船暖窗及森林层次可辨。实际UITK缩放、不同宽高比裁切及游戏内文字对比度仍需运行验收；本记录不代表Play或前台视觉通过。

锁图标为本轮独立16×16 SVG几何形状，透明背景，复用行容器中的固定图标列。未将“有密码”文字塞进房间名，未修改原有图标资源。
