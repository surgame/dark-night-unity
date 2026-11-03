# MapModule游戏内调试台交互预演

2026-11-03按用户要求演示游戏内F1地图调试台，沿已选“画面优先”方向。两种布局为“随玩侧栏”和“集中面板”，均可收起F1后观察同一地形；F1打开阻断本预演的角色输入，不自动暂停。正式接入方案见[MapModule工作流](../../docs/MAP_MODULE_WORKFLOW.md#游戏内f1调试入口)。

## 交互与边界

- 观察：模块边界、碰撞占位、连接接口、生态开关；点选地形编号并记录类型。
- 问题点：最近4条记录带样例、示例版本和位置，支持摘要预览；跨样例仍保留原身份。
- 联机：显示计划中的真实回执字段，数据均为待接入；不生成Ping、Ready或假网络成功。
- 试跑：选择六组入口样例和房主／客户端身份预演；开始后锁定样例与身份，结束后解锁。房主可暂停与同版重开，重开显式确认并保留记录；客户端控件禁用。
- 收起F1后可按A／D或按钮移动位置，空格／按钮播放650ms跳跃示意；生态只在明确操作后播放5秒，支持手动停止和减少动态设置。

所有操作仅改变此界面预演，不连接Unity／FishNet、不产生真实Bake、不持久修改作者文件或玩家存档。跳跃为位置表现示意，沿草案Floor插值的移动不等于正式碰撞。正式UI复用唯一F1 Hub与Interaction Session，正式运行复用原主角、FishNet、YYGC Stateful和SessionTerrain。

## 来源与重建

原始地形只读复用`experiments/cave-entry-variants-v1/variants.cjs`，原主角232×251 PNG只读嵌入，画面中可见高度保持0.48讨论单位。采用矢量白模以审阅调试界面，无纹理，不是生产碰撞资源或美术。

源片段为`console.fragment.html`，产品主题／布局为`console.css`，本地行为为`console.js`。两种布局共享场景和功能；不覆盖上一轮`mapmodule-workbench-ui.html`或`mapmodule-lab-ui.html`。

```powershell
node experiments/mapmodule-debug-console-v1/build.cjs 'C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55' artifacts/mapmodule-debug-console-20261103
```

`review.cjs`仅检查本预演的布局、控件、版本锁定、权限显示、记录上限、状态恢复和有限动画，不执行Unity或游戏测试。证据与输入身份归`artifacts/mapmodule-debug-console-20261103/`，失败回执亦保留。

旧工作台实现、设计与保存边界见[保全索引](../../docs/archive/MAP_WORKBENCH_REFERENCE_20261103.md)；本轮快照保留74份相关源码／meta／UXML／USS，原位输入与旧原型保持。
