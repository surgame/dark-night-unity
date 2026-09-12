# Aseprite 像素角色与换装试验

2026-09-12。已完成原生分层文件、空闲／行走／跳跃、部件切换与 H5 技术验证。**美术仍待用户评审**，不作为正式 Dark Nights 素材，也不计入 M3/M5 验收。

这次使用 Aseprite Lua 按整数像素绘制轮廓与色块，再由 Aseprite 导出；不是 Blender 渲染，也不是宣称已完成的专业手绘成品。它用于判断直接控制 2D 轮廓是否比此前的 3D 试验更适合参考风格。

## 打开成果

- [原生源文件](model/worker.aseprite)：64×64，13 个图层，24 个时间轴帧，3 个 Tag。使用本机 Aseprite 打开。
- [H5 预览](preview/index.html)：动作播放／暂停／逐帧、慢放、整数倍放大、背景、像素网格、脚底原点、即时换装和键盘小场景。
- [换装行走 GIF](preview/outfits-walk.gif)、[逐帧总览](preview/contact-sheet.png)，以及单独的 [空闲](preview/idle.gif)、[行走](preview/walk.gif)、[跳跃](preview/jump.gif)。
- [原生 PNG 图集](preview/sheet.png)、[Aseprite 帧数据](preview/sheet.json)、[部件与组合清单](preview/parts.json)。

H5 的角色图集和各图层已内嵌，没有第三方库、字体服务或远程请求。技术验收使用仅绑定 127.0.0.1、仅提供 preview 目录的本地服务器；内置浏览器阻止 file:// 导航，离线直接打开未完成浏览器实测。

在本试验目录用 Python 启动预览服务：

~~~powershell
python -m http.server 8768 --bind 127.0.0.1 --directory preview
~~~

然后访问 [本地预览](http://127.0.0.1:8768/)。点击小场景后 A/D 或左右方向键移动，空格起跳，也可使用屏幕按钮。点击场景外释放键盘。向左使用镜像，没有声称已画出独立左向帧。

## 可替换部件

| 部位 | 选项 | Aseprite 图层 |
| --- | --- | --- |
| 头部 | 红色鸭舌帽、黄色安全帽、头发／不戴帽 | Cap、Hard hat、Hair |
| 上衣 | 蓝色工装、橙色工作背心 | Torso、Work vest |
| 背包 | 绿色背包、帆布包、不背包 | Backpack、Canvas bag、两层都隐藏 |

共 3×2×3＝18 种组合；所有选项覆盖同一套 24 帧。默认启用 Cap、Torso、Backpack。H5 切换部件时保留当前动作、帧和播放状态，按原生图层顺序合成；不是在网页上简单换色。

在 Aseprite 编辑时，同一部位只显示一个备选图层；无背包时两个包层都隐藏。保留图层用户数据，例如 headwear=hard_hat、outfit=work_vest、pack=canvas_bag。它们是导出器识别部件的依据。角色根原点为 Slice feet_origin 的 (32,58)，不逐帧裁切或居中。

## 动作与复用边界

| 动作 | 帧数 | 原速时长 | 行为 |
| --- | ---: | ---: | --- |
| idle | 6 | 880 ms | 呼吸、眨眼，循环 |
| walk | 8 | 800 ms | 前后腿交换、摆臂，循环 |
| jump | 10 | 930 ms | 预备、起跳、腾空、落地，场景中单次播放 |

H5 遵循每帧的原生时长，不按固定 FPS 替换。跳跃高度已经画入帧内，场景没有再叠加一份垂直位移。移动速度为 30 个逻辑像素／秒；尚未宣称完整消除了脚步滑动。

帽子、背包和躯干在生成脚本里共用绘制函数，按帧做整数平移；肢体姿势单独定义。因而无需重新画出 18 套完整角色。但源文件的 Cel 当前是独立可编辑副本：修改某一帧的帽子，不会自动更新其他帧。实测 Aseprite 的 linked cels 同时联动像素和位置，直接链接不同高度的帧会破坏呼吸／跳跃位移，本次未这样处理。

这证明了部件与组合的复用，不代表任意服装都能自动适配任意动作。长衣摆、头发摆动、前后遮挡变化、武器、转身及独立左右向仍需要逐帧设计或修正。当前样稿的造型、轮廓节奏、动作力度和参考一致性仍需美术评审。

## 编辑后重新导出

环境：Aseprite 1.3.18.5-x64（本机已有 Steam 安装）、PowerShell 7、Python 3.13、Pillow 12.0.0。没有安装新的 Aseprite，也不需要 Aseprite MCP。

Pillow 版本锁定在 requirements.txt；新环境可使用 python -m pip install -r requirements.txt 安装。

默认程序位置：D:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe。

先在 Aseprite 保存源文件，在本目录执行：

~~~powershell
.\run.ps1
.\run.ps1 -AsepritePath '你的 Aseprite.exe 路径' -PythonPath '你的 python.exe 路径'
~~~

结果进入新的 exports/时间戳目录。普通导出读取维护中的 .aseprite，不运行角色生成器；前后核对源文件 SHA-256。导出目录必须为空，保护已有输出。

仅在需要从配方创建另一个独立初稿时，使用新模型路径：

~~~powershell
.\run.ps1 -Initialize -Model .\local\new-model\worker.aseprite
~~~

初始化目标的父目录必须为空。scripts/character.lua、actions.lua、palette.lua 是初稿配方；修改配方不会自动覆盖已经编辑的源文件。导出及 H5 可替换部件的合同限定为本试验的三动作、13 个普通绘画图层、纯不透明色板和上述部件 ID，不是通用 Aseprite 运行时。

## 实际验证

- [像素与部件检查](preview/verification.json)：24 个独立 PNG 与原生图集完全一致；固定 64×64、硬透明、无裁切、Tag 不重叠、动作可见变化、空闲／行走脚底固定。默认角色用 22 色，所有备选部件共 23 色，均在 24 色设计色板内。
- 18 种组合 × 24 帧＝432 帧，与 Aseprite 原生合成逐像素一致；所有组合不触边。原生对照保存在 preview/native-combinations/，分层图集保存在 preview/layers/。
- [原生文件检查](preview/document-verification.json)：真实 Aseprite 打开、在隔离副本中改像素及切换安全帽、保存、关闭、重开，修改与部件数据保留；源文件哈希未变。
- [浏览器检查](preview/browser-verification.json)：18 种换装保留暂停帧、逐帧、原点／网格、放大、单次跳跃、0.5× 慢放、A/D 移动、空格起跳、落地恢复与焦点释放。原生像素比较与浏览器交互检查分别记录，不把浏览器 DOM 状态检查写成逐像素截图比较。
- 初轮检查拦截了 Aseprite 新增帧自动扩展既有 Tag 导致的区间重叠，以及 CLI filename-format 干扰逐帧文件名的问题；修正后生成当前源文件并完整导出通过。

中间稿与详细日志保留在仓库忽略的 artifacts/aseprite-pixel-crew/。没有导入 Unity，没有改变 Game／原始素材／玩法规则，没有运行 Unity 构建。
