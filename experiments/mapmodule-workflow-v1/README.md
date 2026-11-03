# MapModule地图工坊与地图试验场UI设计

本片回应2026-11-03用户要求：MapModule工程命名、较中式的中文显示、全新制作流程、FishNet／YYGC Stateful及R3／UniTask复用、执行方案和两套可交互UI设计。

具体工作流合同方案归`docs/MAP_MODULE_WORKFLOW.md`，MM-00～05长期切片归ROADMAP，实际产出与已验／未验归DEVELOPMENT。本目录仅维护交互界面的可重建来源，不作为Game运行代码或原生UITK资源。

## 两套界面

- 地图工坊：独立EditorWindow方向，模块／拼装／试跑任务、单片／片段／整图观察，提供“画布优先”“样例优先”两种布局。可切换六组样例、错配后段、查看冲突，保存作者源、检查及演示烘焙分别改变本地状态。
- 地图试验场：原生运行UITK方向，提供“画面优先”“联机排查”两种布局。可演示本机房主／客户端选择、运行时锁定样例、房主暂停／重开、客户端控制禁用、退出与本地观察叠层。

所有操作只改变界面原型内的状态；没有连接Unity、FishNet或业务世界，没有保存Scene／Prefab，没有真实Bake或联机Ready。准备及网络指标保持待回执，不填写虚构Ping或成功结果。运行／地址属于临时演示，不随widgetState恢复。

中文显示采用“地图工坊”“地图试验场”，工程前缀MapModule。整体视觉沿现有原生工具与橄榄色游戏UI方向，支持浅色／深色和320～1024宽度；窄屏折叠目录／属性，原生Unity目标仍为桌面。

## 来源与尺寸

`workbench.fragment.html`和`lab.fragment.html`为可编辑片段，`product.css`定义产品自己的主题／尺寸，`workbench.js`和`lab.js`仅处理本地交互。`scene.js`绘制界面中的地图草图，不执行12坡形适配、碰撞或WFC。

地形数据只读复用`experiments/cave-entry-variants-v1/variants.cjs`六组作者草案；渲染范围为两片12.8×3.2讨论单位，主角只读嵌入原生232×251的`Res/Objects/Hero/AnimationFrames/idle/idle-000.png`，可见高度保持0.48单位。工坊中两个角色为比例参考，试验场只显示一名参考主角，不冒充已连接玩家。所有形状、端口检查和叠层均为方案／UI演示，不当作正式资源共边或原生形状验收。

轮廓白模无纹理，局部模块可重用的来源仍是前一草案；这次选择代码矢量是为UI布局和交互讨论，不制作最终美术。原始角色与旧原型字节保留。

## 构建与核对

仓库根目录执行：

```powershell
node experiments/mapmodule-workflow-v1/build.cjs 'C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55' artifacts/mapmodule-workflow-20261103
```

构建前校验嵌入脚本语法，嵌入主角与有限作者数据，输出两份小于1MB的独立片段；已存在目标先保留来源副本。前版入口设计、尺寸图及所有旧页面路径不写入。

`review.cjs`仅核对UI布局、主角图像比例、状态／主操作、界面权限、版本失效、轮播与恢复，不执行游戏。结果保留于`artifacts/mapmodule-workflow-20261103/`。首次语法错误、早期预览、SVG尺寸及关闭details的核对修正原位保留。

本轮未修改Game、YYGC、依赖或原生资源，未运行Unity编译、测试、Play、Player或网络会话。后续原生实现与联机验证按MM切片进入Local单通道，不能用本图的操作通过数替代。
