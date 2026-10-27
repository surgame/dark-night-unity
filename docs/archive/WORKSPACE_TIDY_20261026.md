# 文档与临时产物整理回执 · 2026-10-26

用户要求整理文档、artifacts和项目中看似无用的目录，按最小保留原则处理。本轮由本聊天执行，只维护当前仓库；既有功能修改、作者草稿及原始证据作为输入保护。永久删除仍受AGENTS具体清单授权约束，本批只作可恢复保全，同盘移动释放空间为0。

## 已处理

| 范围 | 处理结果 |
| --- | --- |
| 当前执行状态 | DEVELOPMENT由297,565字节收敛为22,143字节整理稿；保留CL模块、协议35／v27、当前主图、最近验证边界、未解决失败和下一步。另一任务后续新增的开工记录原文保留，最终文件含该增量及本轮收尾 |
| 历史流水 | 原状态页全部逐批记录进入[单份归档](DEVELOPMENT_LOG_20261026.md)，只调整相对链接及历史说明，旧成功／失败不改为当前结果；原页逐字节前像另留本机任务目录 |
| Unity临时验证资产 | 10个`__Tuner...`目录及各自顶层meta移出Assets，747文件／20,113,141字节；内部meta、GUID及失败夹具保持 |
| 隔离静态编译 | 四个工作台／配方任务下258份DLL／PDB，76,682,988字节；原位置保留日志、rsp、result、源码前像及最终身份 |
| 合计 | 278项／1,005文件／96,796,129字节，约92.31 MiB；全部逐文件SHA-256与搬迁前一致 |
| 导航与目录说明 | 文档索引、归档索引、工作区说明和本机artifacts索引更新；未移动正式／失败报告或修改其内容 |

保全根为`artifacts/待清理/20261026-文档与临时产物整理/`，下方保持原仓库相对结构。完整原／目标绝对路径、体积、任务、原因及后续条件见其中`清单.md`。机器计划与逐文件指纹、搬迁回执及最终核验位于`artifacts/project-tidy-20261026/`。

十组目录为：

- `__TunerCandidateValidation20261022`、`__TunerJourneyValidation20261004`、`__TunerSaveValidation20261006`
- `__TunerRecipeBasicValidation20261025`、`__TunerRecipeSaveValidation20261025`、`__TunerRecipeValidation20261025`
- `__TunerWorkbenchConfigFinal20261025`、`__TunerWorkbenchConfigValidation20261025`、`__TunerWorkbenchConfigVisible20261025`、`__TunerWorkbenchValidation20261025`

这些输出合计407个GUID，排除meta自身声明后，正式Assets、Packages、ProjectSettings、工具／实验中没有外部GUID引用；文本路径出现于创建测试及历史记录。没有已加载临时对象、选中对象或未保存工作台窗口，目录只含asset／json／meta，不含脚本、程序集、场景或Prefab。原测试创建逻辑保持，未来执行可能重建目录；整理不等于删除用例或失败。

## 保留依据

`artifacts`开始约5.17 GiB。约2.32 GiB的`lan-20261023-input`、约1.08 GiB的`local-menu-multiplayer-20261023`含隔离源码／依赖、独立缓存、Player与正常／弱网／退出失败证据；约916 MiB的`worktree-launcher`还含宿主、任务、会话与来源。这些不具备整目录退役依据，原位保留。旧的最小清理授权只覆盖2026-10-19当时具体清单，不外推到本批新Player／独立缓存。

正式Game的全部作者地图及非默认JointMaps、MapDocuments、主菜单Artwork、作者Prefab／Definition、来源素材与许可、当前Library／Temp／Logs、IDE配置、依赖源码、旧存档和Git均保留。Addressables、Settings、Resources、Plugins、Samples等有现行用途，不按目录名判废。上一轮Git bundle及恢复清单继续保护。

## 核验与恢复

本批开始有一个Local Editor 6000.4.9f1／PID34392。首次搬迁保护发现后来进入Play，拒绝操作并保留回执；本任务没有发出退出／暂停Play命令。实际资产搬迁开始及结束均为非Play、无编译／导入；自动刷新仅在搬迁期间禁用并在finally释放，批量结束只主动同步一次资源列表，最终Assets无`__Tuner`目录。现有Bootstrap无脏标记，未触发游戏脚本编译、测试或Player构建。

恢复某组夹具时，在Local单一通道确认无占用，将该组目录与相邻顶层meta按清单恢复到原绝对路径，保持GUID，再同步资源列表；不能覆盖后来测试新建的同名目录。编译载荷可按清单恢复，或从保留的源码身份／rsp重建；DLL指纹不作为重新编译通过的证据。

逐文件搬迁核验、非本任务输入保护、文档链接／锚点与差异核对结果详见本机`artifacts/project-tidy-20261026/verification.json`和`整理回执.md`。正式／失败证据不重生成，游戏实现／运行验收未新增，CL阶段未结束。本批无Git提交、分支切换或推送。

2026-10-26续接收尾：上轮最终核验发现六处并发变化后停止写入并保留失败回执，用户随后告知已恢复空闲。只读核对确认与DEVELOPMENT新增的山林纵向视差任务一致，涉及`SurfaceBackdropLayer.cs`、`SurfaceEnvironmentContentSetup.cs`、共享及正式私有Style、`EDITOR_WORKBENCH.md`和`TERRAIN_GENERATION.md`；这些成果及其开工记录不回退、不纳入本任务修改。最终核验分别报告7,652份初始输入字节保持、六份并发文件相对续接快照保持和四份本任务文档更新，不将全部原输入笼统写成未变。初始未通过记录另保存在`verification-initial-concurrent-change.json`，不覆盖或改写成通过。


## 同日补充授权后的实际删除

用户在上轮具体清单和回执后再次要求“移除所有无用内容，遵循最小保留原则”，本轮据此对确认闲置的载荷执行永久删除：650项／18,797文件／2,274,809,187字节（2.119 GiB）。具体路径／原因、逐文件指纹、删除回执与保留核验位于`artifacts/cleanup-minimal-20261026/`，不是将前轮归档自行解释为删除许可。

删除闲置`lan-20261023-input/Game/Library`、历史启动器宿主派生Library、四份旧菜单Player、隔离静态DLL/PDB、快照生成IDE索引及两组Python缓存；原报告、日志、rsp、源码前像／依赖快照和Git恢复材料继续保留。仍保留最终`build-5-connection-generation/player`及`map-state/player-mono-20261022-184459-e9d1189f`两种必要复验包，不能将前四个菜单包的旧路径再写成可运行。

前轮保全清单内的258份编译载荷已删除，原搬迁哈希／成功回执只证明当时状态，当前不可从该保全路径恢复这些DLL/PDB。747份Tuner冻结／失败夹具及其meta／GUID仍保留；原文“本批没有永久删除”描述第一轮保全，本节承接后续实际删除。独立缓存退役不涉及现用Game/Library，也未修改YYGC或锁定依赖源码。随后移除空编译目录的命令被自动审批以blocked by policy拒绝，空目录留原位，未换方式绕过。
