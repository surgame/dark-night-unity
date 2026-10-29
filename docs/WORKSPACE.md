# 工作区目录与产物归属

本页维护目录归属、来源保护和阶段产物保全。年代较早、未导入或退出产品入口不等于可丢弃；历史整理回执及恢复见[归档索引](archive/README.md)，当前任务状态见[DEVELOPMENT](DEVELOPMENT.md)。

## 仓库根目录

| 目录 | 用途与保留依据 |
| --- | --- |
| Game/ | 唯一日常Unity宿主，导入／构建独立于外部基线和参考项目 |
| 美术素材/ | 未定稿、待评审的美术作者源与候选，位于Game之外，不自动导入Unity；当前主角素材见[素材索引](../美术素材/README.md) |
| tools/ | 准备、规则检查、联机驱动、美术重建；[工具索引](../tools/README.md) |
| experiments/ | 实验唯一编辑源、图集重建关系和用户成果；[实验索引](../experiments/README.md) |
| docs/ | 现行合同与操作；README只导航，DEVELOPMENT只状态 |
| docs/archive/ | 历史设计、过程、审查、失败、未完成验收和周报，原输入身份保持 |
| docs/evidence/、docs/archive/evidence/ | 批次机器摘要、截图／日志／原报告，按引用与来源保留字节 |
| docs/samples/ | 独立样板文档，不作为正式工程依赖／玩法合同 |
| docs/third-party/ | 许可及来源声明 |
| artifacts/ | 本机生成输出；现存最小保留及10月28日退役边界见[下节](#artifacts-minimum)，不作日常源码／文档仓库 |
| artifacts/待清理/ | 确认不用的中间产物集中保全，归档不释放空间或授权永久删除 |
| artifacts/待移除/20261028-最小保留/ | 本轮按用户指定集中待手动删除的产物；清单与630项原相对结构一并保留 |
| .deps/ | 现行及历史锁定依赖，未知漂移／用户改动保留；未批准退役不移源码／补丁 |
| .github/、.git/ | 自动化配置及Git索引／对象，不作为普通产物整理 |

外部 `../projects` 为已提交游戏基线，`../reference projects` 为研究／素材来源；不用于日常导入／构建。用户YYGC／AnyRule工作区和其他会话checkout不随游戏整理切换、覆盖或合并。

## Unity宿主

| 位置 | 边界 |
| --- | --- |
| Assets/DarkNights/Scripts | Core／Runtime／View／Entry／Editor／Tests，不为目录整理改职责或生成绑定 |
| Assets/DarkNights/Res | 作者Definition、Prefab、材质、动画、场景和唯一素材源，保留meta／GUID |
| Assets/Scenes、Samples | Bootstrap、默认模板及隔离LAN／InputActions样例，按实际引用保留 |
| Assets/AddressableAssetsData、Addressables | 已有加载／构建配置，目录名不等于资源身份 |
| Assets/Editor、Network、Scripts、Resources、Settings | 宿主支持及特殊加载／主题／管线配置，不当普通缓存 |
| Assets/Packages、Plugins | 第三方payload、许可和依赖，不随意搬走 |
| Assets/__Tuner* | 隔离验证临时输出，不是正式作者目录；2026-10-26移出的10组旧输出及meta已于10月28日集中至“待移除”，后续按测试源码显式重建 |
| Packages、ProjectSettings | manifest／lock、Editor／平台／场景设置，继续提交 |
| Library、Temp、Logs、obj | Local仍用缓存，单一写入通道，不与其他checkout共享／联接 |
| UserSettings、IDE配置 | 本机用户状态，不提交，也不据此判断可丢弃 |
| Build、生成项目／解决方案 | 核对来源与后续用途，未退役前原位保留 |

退役地形场景仍供定位／来源，两份营地场景仍供专项回归与显式构建；原位保留，路径见[场景目录](EDITOR_WORKBENCH.md#场景目录)。原生矿床Prefab／Definition仍供静态配置／作者来源，不能因运行矿床对象退出就搬走。冻结规则、失败、作者资源、未保存草稿、唯一备份、正式证据及后续Player继续保护。

2026-10-26移出Assets的旧冻结地形及临时Tuner输出已集中到本轮“待移除”目录。当前历史测试直接读取的20261022-171627-699/layout.json及配套存档／配置仍留原路径、字节不变；正式MapDocuments、GreypineForest当前引用、作者源及ShutdownBackup留在Game，不随artifacts整理。

<a id="artifacts-minimum"></a>

## artifacts最小保留

2026-10-28用户要求artifacts近乎清空、必要文档简化并回主文档，随后指定“移入待移除，我手动删除即可”。630项／150,145文件／3,381,456,009字节（3.149GiB）已按原相对结构集中至`artifacts/待移除/20261028-最小保留/`；用户确认无需恢复后可手动删除该整个目录。其内`清单.md`保留原／目标绝对路径、体积、来源、原因及恢复条件。旧Player、独立验证工程／依赖镜像、日志截图及清理前像均在其中，不能再按原路径直接运行；未来验证须对齐源码／配置／锁定依赖重新构建，失败及待验仍由DEVELOPMENT承接。两份本轮新增的完整过程文档已撤去，必要结论直接维护在主文档。

“待移除”目录之外只留下以下17个文件，合计约39.87MiB，原路径不变：

| 保留内容 | 必要原因／边界 |
| --- | --- |
| project-tidy-20261019/branches-before.bundle，约39MiB | 含7个当前引用未覆盖的旧本地／远端分支头，保住唯一恢复来源；可用git bundle list-heads查看，恢复为新分支，不覆盖main |
| 历史地图、v27存档及配套flow／map JSON，约0.84MiB | 现有MapContract测试直接读取，保持原路径及字节，避免清理破坏测试；不是普通玩家存档 |
| local-menu-multiplayer-20261023/delivery中的三份candidate源码及product-fixes.patch | 尚未移交的房间指纹、密码waiting页及连接世代修正；不得据此直接覆盖当前Game |
| shutdown-20261028-070009的map-draft-before.json／workbench-before.json | 作者草稿及工作台状态备份，正式ShutdownBackup资源另在Game保留 |
| terrain-final-20260926/editor-status.json | 活动Local Editor持续更新，清理不干预该进程 |

保留文件及SHA-256、移动统计及核验只留一份[精简回执](evidence/artifact-retirement-20261028.json)，完整路径清单随待移除内容放置。630项源位置已撤出、目标均存在，文件数／体积吻合，15份冻结保留内容哈希保持；本批永久删除0，同盘移动释放0，等待用户手动删除。artifacts/README只导航到本节；本次范围不扩大到未来新增产物、Game／Library、正式.deps、外部工作区或个人.codex。

## 阶段产物盘点与保全

本节承接[AGENTS](../AGENTS.md#磁盘与产物保全)，不把成果变成可删除缓存。

1. 每阶段开始及编译／构建前检查相关磁盘，结束盘点编译副本、依赖解包、生成器bin／obj、原生临时副本、隔离存档、日志／截图和报告中间文件。父子目录不重复计体积，空间不足暂停下一高空间步骤，不直接删除。
2. 逐项确认任务、可重建性、后续用途、引用及活动进程。共享缓存、脏依赖、唯一源、人工资源、未保存场景／草稿备份、冻结夹具、最终Player、正式／失败证据、用户成果、待验产物及归属不明对象原位保留。复用现有Library及同产物，不复制完整缓存。
3. 移动前解析原／目标最终绝对路径及链接目标，确认在批准范围内，核对占用、共享引用和归属；无法确认不移动。.codex遵循个人专门规则，外部文件不因工程规则获得授权。
4. 仅将确认可移出的对象保全到 `artifacts/待清理/YYYYMMDD-任务名/`，保留原相对结构。目标冲突选择新任务目录，不覆盖旧内容，不换输出根；待清理产物不删除、清空、裁剪或永久删除。
5. 清单.md记录每项原／目标绝对路径、体积、产生任务、原因和后续条件，汇总保全体积及实际磁盘余量，归档释放为0。占用／权限／策略阻碍留原位记录，不换工具删除、删父目录或绕过保护。

执行后核对清单、原／新位置及保留对象可用性。只有用户另行明确具体删除清单后才处理永久删除；归档、创建规则或移除源码工作树不等于删除产物许可。

## 文档与证据保全

DEVELOPMENT只保留当前模块结论、输入与待验；逐批开工、命令、提交、详细计时／测试计数进入有日期的执行流水归档，保留来源链接和未解决失败。2026-10-26的承接入口为[执行流水](archive/DEVELOPMENT_LOG_20261026.md)。顶层合同、计划和操作文档各有现行职责，不按文件年代或大小合并删除。


旧过程文档移archive前，现行合同、作者操作及待验项必须有承接入口；链接／锚点和工具／样板README同步更新。历史日期、协议、构建、失败及通过数保持原语义，JSON／XML／截图／冻结资产不改写。只做文档整理不重跑游戏测试或构建，也不把旧成功计入当前候选。

历史Player／报告按各次实际退役状态处理，不保证原路径可用；旧矩阵不自动变为当前验收。当前状态、未解决失败、必要输入及重建约束保留在现行文档；本轮只整理artifacts，不移动Game作者资源或玩家真实存档。
