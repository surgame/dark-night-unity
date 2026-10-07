# Runtime Debug Hub 重构实现记录

2026-10-08，隔离分支 `ref-20261008-runtime-debug-hub-uitk`，基线 `dfc523b018d182d7f2e4f51409a705af0cf68126`。本批按用户要求完成开发与静态编译，没有 Unity 导入、Play、测试执行或 Player 构建，尚未集成 Local/main。

## 最终界面与行为

已记录最新需求：物体页上方为 **grid 网格**，下方保持原预览的四格持有装备、独立喷气背包和最近操作。没有“道具发放”名称或产品简介。

网格从 ObjectDefinitionDatabase 读取全部有效定义及现有图标；支持名称／Key 搜索、主角目标选择与定义选择。ListView 按86单位行高虚拟化，行内用 Flex 排列，按宽度调整1–6列。窗口内容可滚动，下方装备区保持可访问。没有 CSS Grid、gap、阴影或 UXML 行内 style。

添加／移除需要房主打开上帝模式并得到服务端确认。库存操作复用商店的初始化短事务，免支付；普通商店的距离、价格和扣款保持原入口。手枪、矿镐、炸弹进入原四槽，喷气背包独立持有。炸弹批量1–100、库存最多1000，普通装备一次一个，库存满或重复装备被拒绝。炸弹没有新增购买商品或售价。

手持子弹复用原 ProjectileBehaviour 生成与权威投射物数组，移除明确选中的 ViewId。其他定义仍可查看，并显示只读／暂停。NPC、物流、刷怪、营地修改等暂停玩法没有恢复，唯一飞船和当前主角受到保护。

装备移除取消当前矿镐动作／炸弹蓄力，撤除缓存工具，清炸弹数量或喷气燃料，并增加原库存与选择版本。所有 UI 展示读取冻结副本；当前开发者授权是 Host 会话能力的只读查询，不是客户端勾选框授予权限。

## 通用 Hub 接入

原 IMGUI Hub、快速测试页和飞船页转换为保留式 UITK。新的契约使用稳定描述、面板工厂和 IDisposable 注册句柄；支持懒创建、显式激活取消、停用和最终释放。重复ID拒绝，旧句柄不能注销新注册。

F1、窗口导航、拖动、主题和 Interaction Session 由 Hub 管理。隐藏页停止150ms调度与回执订阅，输入租约在关闭／异常／销毁时释放；F10继续使用 Smart Console。面板创建失败保留关闭与导航能力。SubsystemRegistration 清理注册及旧宿主，真实 Domain Reload 行为待验。

Resources只用于开发工具的自带模板，未登记为Addressable；视图绑定复用 UIPanel 和 DIContainer.Root。PanelSettings克隆现有有效主题，中文沿用既有船内UI的TextCore字体方式。反馈最多256字符，最近操作最多4条，请求超时12秒只提示、不自动重发。异步完成校验页面代次、连接代次和epoch。

新增模块的接入方式及生命周期见[规范](RUNTIME_DEBUG_HUB_SPEC.md)。本次没有另建 DI、对象状态、日志控制台或网络通道。

## 命令、协议和依赖

开发操作保留编号1000–1004，复用 SessionClient、SessionAuthority 的可信连接、Ready、策略版本、epoch、序号去重和原事务。SharedCamp不能升级为开发权限；非开发构建参数入口拒绝全部调试操作。授权不写玩法存档，换epoch失效。

候选协议 **29**／存档 **v21**／AMP1 schema **2**。另一对话手电筒候选已使用协议28，本候选单独占用29；当前候选不含其未提交的存档v22变更。

框架基线及UPM版本／路径不变。既有 `tools/grid-business/yygc.patch` 保留，再应用 `tools/debug-hub/yygc-debug-hub.patch`，SHA-256为 `081456b98c116b352addd1d852414c59acf3d26aa9928a212936af6eb599b67a`。原准备入口已接入两阶段锁验证与幂等应用，未知差异拒绝覆盖。

逐项框架文件、原因及验证见 [YYGC账本](YYGC_CHANGES.md)。框架源码改动只在本候选 `.deps/YYGC-grid-business`；用户YYGC主仓库、Local依赖和正在运行的Unity未被写入／切换。

## 静态结果与边界

| 编译条件 | 范围 | 结果 |
| --- | --- | --- |
| Editor | GameCore.Runtime + Core、Runtime、View、Entry、Editor、Tests | 7/7，0错误 |
| Development | GameCore.Runtime + 四个游戏运行程序集 | 5/5，0错误 |
| Release | 同上，去掉Editor/Development调试入口 | 5/5，0错误 |

使用C#9、Local既有Unity编译参数、冻结只读引用及生成器，全部输出在候选 artifacts。Editor有42条、Development/Release各30条既有弃用／未用字段警告，本次改动文件无新增编译警告。最终源码哈希与17份编译输入一致。

源码核验覆盖两个UXML的XML语法、控件契约、相邻USS和禁用CSS属性；26个改动C#文件均不超过300行。最终Debugging补丁从干净锁定基线重建9/9文件一致，依赖准备入口幂等通过。这些检查**不代表Unity的UXML／USS导入或实际渲染通过**。

新增脚本、资源及目录的 `.meta` 等待首次正常Unity导入生成，不人工分配GUID；已有资产及元数据未移动或重建。接入时需将生成的meta加入候选提交。

最终日志为 `artifacts/debug-hub-20261008/final-editor/`、`final-development/`、`final-release/`；源码和重建摘要位于同任务目录。正式机器记录见[证据](evidence/runtime-debug-hub-20261008.json)。早期编译和被替代的产物移入 `artifacts/待清理/20261008-debug-hub-static/`，未永久删除，归档不计释放空间。

## 并行开发与后续集成

薄工作树只含源码、最小锁定依赖及静态产物，没有Library／Temp／Logs，也未启动第二个Editor。Local继续停留在 `ft-20261008-flashlight-lighting`，不自动合并或切分支。本批未运行会修改场景、资产、玩家存档或测试夹具的检查。

已识别将来的交叉文件：GameSessionStartupModule、ObjectSessionCommands、SessionAuthority、SessionOperation、SessionOperations，以及DEVELOPMENT和文档索引。集成时需保留手电正常操作编号、控制及存档字段，同时合入独立的Debug编号与装配；最终统一协议／存档身份并重编译。现在没有覆盖这些Local文件。

## 关键人工测试项

基础开关、颜色和按钮检查由用户自行判断，重点验收以下边界：

1. **网格复用身份**：先滚动，再搜索／改列数／换主角；操作必须指向当前绑定GUID、角色和实例，不能误用旧下标；多实例不能静默删第一项。
2. **动作中移除**：挥镐待命中、炸弹蓄力、喷气进行时移除对应装备，确认不补发攻击／爆炸、工具引用撤除、燃料及数量清零；满槽、重复装备和炸弹上限不破坏库存。
3. **权限与时效**：普通Client、SharedCamp、旧epoch、旧库存版本、伪造数量和重复序号；只有可信房主有效请求执行一次，信用余额不变，正式购买仍扣款。
4. **页面生命周期**：注销活动页、重复ID、同类型不同ID、旧句柄、关闭Domain Reload反复进入；切页、F10抢占、异常、断线和卸载后没有重复订阅或残留输入租约。
5. **联机及恢复**：Host操作后Client、晚加入、重连看到一致装备／投射物；调试产生的合法库存能保存恢复，上帝授权在新epoch失效。这里只列项，本批未运行。
6. **资源接入**：一次正常导入生成meta，查看UXML／USS Console、中文及原生下拉／滚动控件；新工具页不能使旧飞船操作或快速测试入口失效。
