# Debug Hub 与手电筒分支集成

2026-10-09，按用户要求将 `ref-20261008-runtime-debug-hub-uitk` 合入 `ft-20261008-flashlight-lighting`。手电筒原未提交工作先从受保护 stash 完整恢复，并保存为 `123f5b8`；调试候选与首次导入元数据保存为 `d2c7895`。合并保留两个历史父链，不改本地 main，不推送。

## 合并合同

当前游戏协议 **30**／存档 **v22**／AMP1 schema **2**。22 个普通操作的整数编号保持手电筒候选原值，包括 `SetHeroLight`；开发操作仍固定为1000–1004。手电独立装备身份、开关、照明方向、GPU 光场、原生资产及地形加载修复保留；Debug Hub 的物体网格、四槽装备区、独立喷气背包及房主事务保留。

6 个冲突文件集中处理：启动装配同时保留 `HeroLightPresentation`、`DebugObjectHub` 和 `QuickTestHub`；协议使用新身份30；枚举合并普通手电操作与独立开发编号；开发状态和文档索引保留两批历史记录；接入规范采用已实现的网格版本，原需求稿仍可从手电检查点恢复。调试库存短事务不修改独立手电状态，手电定义仍作为只读定义显示，没有新增其调试发放入口。

恢复已安装的 TextMesh Pro 必需资源及其原 GUID。试运行时的临时 worktree UPM 地址已撤除，manifest／lock 恢复 `file:../../.deps/YYGC-grid-business`。没有修改 Unity 版本、后端、渲染管线或包版本。

## 依赖落点与资源

现有获批 Debugging 改动通过原准备脚本接入 Local `.deps/YYGC-grid-business`。框架基线仍为 `fee18645c997ed7529c4592917de6c412033c84e`，既有网格补丁完整保留；Debugging 补丁只补入9个 Unity 自动生成的 `.meta`，没有新增框架源码行为。新补丁 SHA-256为 `64490b4dabd9249aba2483709df9aafad93d96ee3e39f978cc7ba9b24b0d8a4b`，输入文件和原因见 [YYGC账本](YYGC_CHANGES.md)。15个游戏资源元数据与首次导入原件逐字节一致；18个框架源码／元数据与候选一致（只规范化文本行尾作比较）。

用户 `D:/Developer/YYGC` 主工作区既有 `Runtime/Debugging/RuntimeDebugHub.cs` 修改保留；不切分支、不重置、不覆盖。本次依赖接入授权来自用户对现成 worktree 成果的合并请求，范围保持原 Debugging 候选及生成元数据。

## 本次验证

- Editor 7/7、Development 5/5、Release 5/5，共17个程序集静态编译，0错误。既有警告分别43／31／31，未宣称无警告。
- 架构772个手写文件、16项守卫自测、0命中；两个UXML／USS源码合同及26个C#文件长度检查通过。
- 普通命令编号、开发编号、存档版本、游戏元数据、18个依赖文件及无残留冲突标记核对通过。
- 编译复用 Local 的冻结只读 Unity 引用与生成器，未启动 Editor、运行游戏测试或构建 Player。`compile_static.py` 增加显式旧框架引用根参数，支持响应文件仍记录试运行 worktree 地址的情况；编译源码实际读取 Local 已合并依赖。

此前调试分支已在协议29下完成Unity导入、主菜单和物体页开启／截图；随后独立监控在提交余量3.776 GiB时自动停止Play。该画面属于合并前候选，不能代替当前协议30的玩法、联机或画面验收。本次合并版Play、Mono、IL2CPP及联机均未运行。

机器记录位于 `artifacts/debug-hub-merge-20261009/`，包括三种静态编译输入／结果、`architecture.json` 和 `integration-checks.json`。原试运行状态和截图继续保留在 `artifacts/debug-hub-manual-20261009/`。

## 工作树保全

移除前将原正式编译证据保全到 `artifacts/debug-hub-original-20261008/`，原锁定框架副本保全到该目录的 `framework/YYGC-grid-business/`，并修复其 Git 工作树登记。既有待清理静态产物统一归档到 `artifacts/待清理/20261009-debug-hub-worktree/`，写入原／目标绝对路径、体积和处理条件；归档不计释放空间，不永久删除这些产物。

应用附加入口因 worktree 归属开发对话而拒绝跨对话附加，因此使用 Git 在确认候选为当前分支祖先、源工作树干净、无活动进程和外部链接后移除源码工作树，再以 `git branch -d` 删除已合并分支。最终路径、体积及执行结果以任务目录中的 `cleanup-receipt.json` 为准；原保护stash暂留作恢复来源。

执行完成：原游戏源码worktree及其Git登记已移除，旧分支已用 `git branch -d` 删除；`d2c7895` 与 `123f5b8` 均为合并提交祖先。正式证据229,237,815字节、原依赖副本46,724,689字节持续保留；待清理旧产物686,396,233字节及长路径失败复制46,715,469字节集中保全，无待清理产物永久删除。Windows长路径同时用于移动和核验；394个普通路径预览漏统计的冻结引用另按原内容寻址SHA-256核对。
