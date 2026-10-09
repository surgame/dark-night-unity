# 2026-10-09 协作约定整理回执

用户确认100项审阅清单后，将AGENTS.md整理为13组长期约束。阶段日志、旧版本和重复操作细则退出日常指令正文，现行状态仍由DEVELOPMENT及专题承接；授权、内存、资产、权威状态、验证和提交边界继续有效。

## 范围与恢复

- 整理前Git基线：`a00508bb92e3defeacd91385b4fe3f3dd5a42268`。原AGENTS.md为158行、39,448字节；SHA-256为`09eb97a0b0894f905c261b34b2be586093d454ef70f43a1fe0464fbd30b1c2e5`。
- 原100个审阅项覆盖20段阶段记录、83条原规则、阅读入口及提交规范背景，共105个原文块；本回执的原行号和编号均指该基线，不是精简后的行号。
- 只更新本仓库Markdown：AGENTS、Unity CLI流程、工作区说明、项目历史摘要和两个文档索引，并增加本回执。没有修改游戏源码、作者资源、协议／存档／依赖配置、YYGC、Player、冻结证据或历史失败结果；未运行Unity编译／测试／构建。
- 会话另附的9条`.codex`清理规则不在原磁盘AGENTS.md中，未并入游戏正文；本次没有执行`.codex`清理、永久删除或定时任务。

原文保留在Git，读取命令如下；恢复需要的段落时提取到独立路径，不覆盖当前约定：

```powershell
git show a00508bb92e3defeacd91385b4fe3f3dd5a42268:AGENTS.md
```

## 20段阶段记录的承接

保留原批次日期、输入和验收边界；不汇总旧测试数字成当前候选结论，也不删除所引用的证据、资源或产物。

| 审阅编号／原行 | 原记录日期与内容 | 承接入口 |
| --- | --- | --- |
| 001／L3 | 10-09 可复用照明、四格库存和本批验证 | [照明记录](../REUSABLE_LIGHTING_20261009.md)、[执行状态](../DEVELOPMENT.md) |
| 002／L5 | 10-09 Debug Hub与手电合并 | [集成记录](../RUNTIME_DEBUG_HUB_MERGE_20261009.md) |
| 003／L7 | 10-07 地面范围、装备恢复及内存保护 | [当前范围](../GROUND_GAMEPLAY_BASELINE.md)、[内存事故](../UNITY_MEMORY_INCIDENT_20261007.md) |
| 004／L9 | 10-07 地形调试集成及已有Modifier配置 | [执行状态](../DEVELOPMENT.md)、[项目摘要](PROJECT_HISTORY.md) |
| 005／L11 | 10-05 矿层集成与主角跳跃／Host展示修复 | [执行状态](../DEVELOPMENT.md)、[原证据](../evidence/hero-input-presentation-20261005.json) |
| 006／L13 | 10-05 原生矿层迁移、两层局部流与初轮计数 | [矿层实现](../MINERAL_MAP_MIGRATION_IMPLEMENTATION.md) |
| 007／L15 | 10-04 内嵌矿检查点及初轮失败 | [地形历史](TERRAIN_HISTORY.md) |
| 008／L17 | 10-03 氧气移除与失败边界 | [验证记录](OXYGEN_REMOVAL_VALIDATION.md)、[失败分析](OXYGEN_REMOVAL_FAILURE_ANALYSIS.md) |
| 009／L19 | 09-28 原地安全着陆候选 | [航程验收](SPACE_TO_PLANET_ACCEPTANCE.md) |
| 010／L21 | 09-27 太空到星球流程与原Player结论 | [航程实现](SPACE_TO_PLANET_IMPLEMENTATION.md)、[验收](SPACE_TO_PLANET_ACCEPTANCE.md) |
| 011／L23 | 09-22 可步入飞船及原生素材 | [飞船记录](WALKABLE_EXPEDITION_SHIP.md) |
| 012／L25 | 09-22 岩层／三层背景，09-23场景移动补注 | [独立背景](STATIC_CAVE_BACKGROUND_EXECUTION.md)、[场景索引](../SCENES.md) |
| 013／L27 | 09-21 远征营地Demo | [地形历史](TERRAIN_HISTORY.md) |
| 014／L29 | 09-20 洞穴工作台、坡形与隐藏连通边界 | [地形历史](TERRAIN_HISTORY.md)、[视觉目标](CAVE_EXPLORATION_TARGETS.md) |
| 015／L31 | 09-20 天然洞穴原型及当时未完成事项 | [地形历史](TERRAIN_HISTORY.md) |
| 016／L33 | 09-19 手采地图收口及旧自动玩法退出 | [地形历史](TERRAIN_HISTORY.md) |
| 017／L35 | 09-17 独立随机地图Debug Bootstrap | [地形历史](TERRAIN_HISTORY.md)、[场景索引](../SCENES.md) |
| 018／L37 | 09-17 正式随机灰松谷及旧依赖补丁 | [地形历史](TERRAIN_HISTORY.md)、[YYGC账本](../YYGC_CHANGES.md) |
| 019／L39 | 09-17 Bootstrap注册修复及旧YYGC锁定 | [YYGC账本](../YYGC_CHANGES.md)、[原证据](evidence/bootstrap-registry-2026-09-17.json) |
| 020／L43 | 09-16 主角输入、Ready专属人物与恢复 | [表现历史](PRESENTATION_HISTORY.md)、[现行架构](../ARCHITECTURE.md#主角与输入)、[联机合同](../MULTIPLAYER.md) |

## 长期约束与细则映射

| 原审阅编号／行号 | 保留或迁移方式 | 现行入口 |
| --- | --- | --- |
| 021／L41 | 阅读入口改为基础文档必读、架构／联机按改动触发；移出旧15类完成声明，不取消历史验收边界 | AGENTS「现行范围」；[DEVELOPMENT](../DEVELOPMENT.md) |
| 022–030／L47–55 | 保留产品目标、外部目录保护、具体YYGC授权、锁定依赖、旧数据边界、行为／素材保护和Linear；移出旧迁移阶段及备份例子 | AGENTS「范围／工作区／YYGC／联机／美术」；[架构](../ARCHITECTURE.md)、[账本](../YYGC_CHANGES.md) |
| 031／L59–62 | 四条合并；保留分类、创建本地日期、重名处理、长期分支／旧命名例外 | AGENTS「Git分支与提交」 |
| 032–034／L68 | 拆清内存阈值、构建额外余量和日志／失回执保护，保留8／6／4GiB数值与独立监控 | AGENTS「内存与故障门控」；[事故与保护](../UNITY_MEMORY_INCIDENT_20261007.md) |
| 035–040／L72–77 | 保留薄树、缓存禁令、checkpoint、移交不授权集成、缓存变化选择；将操作步骤迁入手册，澄清移除工作树不授权删除产物 | AGENTS「用户要求worktree时」；[Local流程](../UNITY_CLI_WORKFLOW.md#薄-worktree-与-local-验证) |
| 041–048、053／L79–86、91 | 合并批次准备、读取复用、导入／编译屏障、一次触发、串行、结果格式和按影响复测 | AGENTS「批量操作／验证」；[有限批次](../UNITY_CLI_WORKFLOW.md#有限批次与依赖屏障) |
| 049–051／L87–89 | 保留磁盘门控、禁止删除、唯一待清理路径、清单、失败留原位与保护例外；盘点字段和步骤由现有手册承接 | AGENTS「磁盘与产物保全」；[保全流程](../WORKSPACE.md#阶段产物盘点与保全) |
| 052／L90 | 原样保留Mono默认及主动生成／覆盖／验证IL2CPP须事先明确确认；获批后一次构建复用、后端分别记录 | AGENTS「验证与交付」 |
| 054–066／L95–107 | 保留C#9／框架、程序集／目录、300行硬上限、中文summary、partial和生成物边界；旧Core退出改成当前不变量，Sample／R3／VitalRouter仍保例外 | AGENTS「代码／联机」；[架构](../ARCHITECTURE.md)、[LAN Sample](../LAN_SAMPLE.md) |
| 067–076／L111–120 | 保留唯一权威、冻结副本、可信连接／Host同链、控制策略、60Hz／暂停、身份／epoch和异步快照边界及不默认扩架构 | AGENTS「权威状态与联机」；[MULTIPLAYER](../MULTIPLAYER.md) |
| 077–091／L124–138 | 保留指定生图provider、低像素制作／真实坡形、原生Prefab／人工来源保护、绑定禁兜底、规则唯一源、预览隔离和meta／提交边界；移出固定素材数量与旧场景入口语境 | AGENTS「Prefab、美术与内容」；[架构](../ARCHITECTURE.md)、[场景](../SCENES.md)、[依赖](../DEPENDENCIES.md)、[视觉目标](CAVE_EXPLORATION_TARGETS.md) |
| 092–097／L142–147 | 保留按影响验证、独立进程、冻结夹具、真实编辑／Player验收、待验区分和不推送；完整矩阵按当前范围触发 | AGENTS「验证／Git」；[联机验收](../MULTIPLAYER.md#联机验收重点)、[执行状态](../DEVELOPMENT.md) |
| 098–100／L151–158 | 合并中文提交type／scope、正文／真实换行、验证与版本影响、UTF8消息文件、暂存范围；移出GameVS参照路径与示例SHA | AGENTS「Git分支与提交」 |

旧L77与L85的轮询规则合并为：首次有界等待，通常20–30秒检查；未变化退避，稳定长任务最多每60秒一次小状态检查，单次阻塞不超过60秒。旧U6阶段未交付句迁出，历史结果仍归原产物；既不借用历史通过，也不把旧阶段警告当作当前失败。

## 维护与核验

新增维护规则要求一类约束一条、授权／禁止摘要留AGENTS、状态进DEVELOPMENT、细节进现行专题，不再逐批追加日志。PROJECT_HISTORY同时将旧太空默认入口和“当前协议25”改标历史，指向当前地面范围；没有恢复暂停玩法或核销未完成验收。

整理后AGENTS为111行、18,945字节（约18.5KiB），较原工作树39,448字节减少52.0%。13组结构、100项映射、20段历史承接和7份文档的212个本地路径／锚点检查通过，Git差异范围及空白检查通过。

本次仅文档核验，不新增Unity或Player通过数。检查摘要和本轮中间文件保全记录在 `artifacts/agent-guidelines-20261009/` 及对应 `artifacts/待清理/20261009-agent-guidelines/清单.md`；Git原文恢复和中间文件归档不计作磁盘释放。
