# 运行中地形网格检查

> 本页保留原批次日期、源码／Player身份、协议和实际验证结果。2026-10-09整合后，现行职责由[合同／操作入口](../EDITOR_WORKBENCH.md#运行地图与网格检查)承接；未完成项查[当前执行状态](../DEVELOPMENT.md)。归档不核销待验或借用旧通过数。

2026-10-06。AnyRuleD 编辑器入口统一在 `YY → AnyRuleD`。原 `Tools → AnyRules → Grid debugger` 退出，不保留重复菜单。

| 入口 | 用途 |
| --- | --- |
| 地形工作台 | 编辑态制作素材／规则、独立试画与预览；Play 时选择运行地图并打开网格调试器 |
| 网格调试器 | 只读检查运行地图的逻辑格、DualGrid 渲染格四角来源、求解配方和页面事件 |
| 联网 → 同步调试器 | 查看前景网络副本、连接／数据流与权威对比；矿层尚未注册此联网诊断入口 |

## 使用

1. 从 Bootstrap 进入游戏并装载星球地图，也可使用现有“已着陆 · 矿镐”快速测试。
2. 打开 `YY → AnyRuleD → 网格调试器`，在“检查地图”选择“前景地形”或“矿层地形”。地形工作台在 Play 中提供相同的地图列表和“打开网格调试器”按钮，两条路径复用同一窗口。
3. 输入地图 U／V，或在 Scene 视图按住 Shift＋左键选中逻辑格。游戏地图坐标 `(x,y)` 对应 AnyRuleD `(U,V)=(x,-y)`；宿主位置和缩放由绑定 Transform 转换。
4. 查看逻辑格状态、TileId／Height／Flags、材料 Key／GUID；渲染格显示 NW／NE／SW／SE 四角、缺失角 mask、求解配方和资源版本。
5. Scene 叠图可开关逻辑采样、Chunk／Page 边界与所选 Page 的 halo。采样范围有界，不扫描整张地图。

逻辑格中心位于整数坐标；渲染格由四个邻接逻辑格决定，中心错开半格。窗口中的同一组 U／V 分别用于逻辑查询和渲染格查询，四角来源以窗口列出的坐标为准。

## 数据与生命周期

Cave Wall Tuner 与运行工作台的笔刷叠图也支持两种网格自由切换。选择拆／填时默认逻辑网格；Editor Tuner 的选格与悬停以整数逻辑格中心为准，修正了原向下取整造成的半格偏移。渲染网格切换仅影响显示，拆填始终修改逻辑格。当前界面和验证见[工作台说明](../EDITOR_WORKBENCH.md)。

两层都绑定当前表现宿主已有的 `ARDMapController.Debugger`。正式 Host 和客户端检查的是本端冻结输入构成的局部表现地图；未订阅区域显示 Unknown。窗口不创建第二张权威地图，不推进采集、耐久、资源或地图提交。

“求解配方”表示从当前逻辑输入求得的结果；页面状态与事件用于分析呈现进度，不能把它直接当作 GPU 已绘制内容证明。网络权威对比继续使用同步调试器。

选择页面或其子对象可定位所属地图。切换地图、关闭窗口不会释放地图；地图退休及退出 Play 沿用宿主生命周期，退休地图退出可选列表，退出 Play 撤销借用绑定。独立 Player 没有此 Editor 窗口，也未因此开放新的联网诊断接口。

运行地图另存画布仍为显式操作，要求作者目录身份匹配、小地图不超过4096格且没有 Unknown。正式局部星球地图不满足完整画布条件时会拒绝导出。编辑画布及源资产保存语义保持原样。

## 实现与验证

框架修改限于 `com.tsgame.anyrules/Editor/Debug/GridDebugWindow.cs` 与 `Editor/Workbench/AnyRuleDWorkbench.cs`，由 `tools/grid-business/yygc.patch` 和 SHA-256 锁重建。游戏侧 `TerrainPreview` 与 `MineralLayerView` 仅在 Editor 绑定选择桥；Player 不加入桥组件。

验证结果及失败记录保存于 `artifacts/terrain-debug-integration-20261006/`，[机器摘要](../evidence/terrain-grid-debug-integration-20261006.json)记录源码与截图身份。本批不构建 Player，也不新增联机、手感或前台性能验收结论；游戏协议25／存档v19／AMP1 schema2和YYGC基线提交不变。

最新编译完成且无错误；真实两层宿主输入变化、四角来源、窗口切换／关闭及退休释放检查1/1通过；Bootstrap正式Host两层就绪、运行工作台绑定和退出Play检查通过。架构732文件／16自测／0命中，补丁干净基线重建20/20。首次后台Scene未绘制的尝试保留；受控聚焦后的真实Scene Shift事件在缩放宿主中正确选中(88,-71)，600×800[窗口截图记录](../evidence/terrain-grid-debug-integration-20261006.json)已核对格属性、四角及配方布局。

原截图路径 `artifacts/terrain-debug-integration-20261006/grid-window.png` 在本次文档检查时已不在原位置；上方入口改指保留的机器记录，不重生成截图或改写原画面验收。
