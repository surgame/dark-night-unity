# Debug Hub 物体面板改造

> 本页保留原批次日期、源码／Player身份、协议和实际验证结果。2026-10-09整合后，现行职责由[合同／操作入口](../RUNTIME_DEBUG_HUB_SPEC.md)承接；未完成项查[当前执行状态](../DEVELOPMENT.md)。归档不核销待验或借用旧通过数。

2026-10-09，分支 `fix-20261009-debug-hub-icons`，从干净的手电／DebugHub集成分支继续。协议31／存档v23／AMP1 schema2保持；本批只改游戏侧目录、UI及图标引用，YYGC源码、补丁锁、UPM版本和正式玩法范围保持。

## 最终界面与筛选

- 图标格48×48、图标32×32、间距6。少量物体使用自动换行的按钮，退出按行虚拟化的ListView；只有一个按钮获得选中类。整个页由Hub滚动，网格不再另开滚动条。
- 格内没有名称或状态两行文字。名称、Key和短状态在tooltip与下方详情展示；名称／Key搜索忽略大小写并去掉首尾空白，选中物体被筛出时清空目标并关闭添加／移除。
- 根据EquipmentItemConfig、ActorRuleConfig、BuildingRuleConfig、WorksiteRuleConfig、MineralDepositRuleConfig收录物体；无业务配置的箭矢／手持投射物显式登记。当前30项。`effect.command`是旧指令圈表现，和UI、音频、会话、连接、漂浮文字、独立商店／出售服务一起排除。
- 暂停玩法的真实物体保留查看。添加／移除仍只开放既有装备和手持投射物能力，权限、Ready、epoch、库存版本、请求去重及回执继续走原入口。
- 四个持有装备槽、独立喷气背包及最近操作保留。游戏侧HubTheme在Hub专属Panel挂载并随面板最终释放，覆盖内部滚动条、输入框、下拉、Toggle、Foldout及hover／focus／disabled；切页和关闭不会丢失主题。

## 图标来源

30份Definition资产仅改Icon字段，名称、Key、GUID、配置和Prefab引用没有变化。29份复用既有图标／静态帧；弓手用身体帧，搬运机器人与无人机使用自身帧，炸弹使用实际BombIcon，避免误用Prefab中的借位图。

喷气背包通过指定 `imagegen-codex-provider` 的原生适配器流程、当前provider `1qq`（`https://sub.1qq.xyz/v1`）及 `gpt-image-2.5` 生成。本机为ChatGPT认证，原适配器只检查auth.json而失败；本任务包装器在内存读取同一provider已有的bearer配置，传给原适配器，未修改技能或认证文件、未输出或保存密钥。

AI源图保留在 `experiments/imagegen-debug-hub-20261009/jetpack-source.png`；来源哈希、实际尺寸、模型与请求见同目录source.json。它是可编辑高分辨率源图，不作为原生像素成品直接导入。派生步骤在 `tools/debug-hub-icons/prepare-jetpack.py`：阈值透明边缘、裁取物体、最近邻缩至28px范围、24色调色板、居中32×32。最终 `Res/UI/DebugHub/Icons/Jetpack.png` 在原生及8倍预览和真实界面检查，Unity采用Point、无mipmap、无压缩。

清单在 `tools/debug-hub-icons/icon-sources.json`；Editor接线脚本assign-icons.cs先完整验证引用再保存，新增meta由Local Editor生成。运行面板只读Definition.Icon，不加载所有Prefab，也不按名称找运行组件。

## 本批验证

证据：`docs/evidence/debug-hub-icon-panel-20261009.json`，完整回执及截图位于 `artifacts/debug-hub-icons-20261009/`。

- 真实Editor编译完成、无错误；实际Console错误0，UITK导入／样式警告0。保留原有LoggerSettings缺失提醒。
- Development和Release分别静态编译5程序集、0错误。已有GameCore弃用警告29条及Entry TerrainFieldControls.GetInstanceID弃用警告1条，没有将它们写成0警告；没有构建对应Player。
- ArchitectureGuard：779个手写文件／16自测／0命中；git diff检查通过。
- 正式Bootstrap Host：19项面板探针通过，覆盖真实目录、全部30图标、单一滚动、名称／Key搜索、空结果、隐藏目标、单格高亮和正确点击目标。探针使用真实UITK回调，未注入权威状态。
- 720／480／320逻辑宽度分别12／8／5列；固定48px格、横向不溢出、单选及6px拖柄四项约束全部满足。UI Toolkit像素对齐会产生不足1逻辑像素的舍入。
- 通过Toggle启用开发权限、添加矿镐、从槽位移除，收到原网络链的成功回执并核对冻结库存。飞船页切换和关闭重开后主题、选择、30图标正常。
- 实际画面含默认、窄窗口及滚到底部的装备区；完整面板截图仅临时使用700逻辑高度，默认600高度仍由Hub滚动。Foldout与Toggle选中样式的区分及喷气区文字间距已定向导入核对。这不代表独立Player、四人／弱网矩阵、长期性能或双机器验收。没有生成IL2CPP。

独立监控覆盖Play批次，按8GiB私有／6GiB可用RAM／4GiB提交余量停止线检查；1427次采样均未越界，Editor私有最高4.780GiB、可用RAM最低11.211GiB、提交余量最低5.665GiB。任务结束停Play，保留Bootstrap场景与现有Library。分批数值及最终短时样式探针见机器证据。

## 产物保全

本次静态编译引用副本／DLL、过期编译批次和Assets内临时截图按原相对结构归档到 `artifacts/待清理/20261009-debug-hub-icons/`，共298,315,728字节／284.5MiB，清单列出原绝对路径、目标、体积及后续条件。归档后D盘可用约112.4GiB。正式报告、截图、源图与派生资源保留。归档释放空间0，没有永久删除；共享Library、工具缓存及现有Player保留。
