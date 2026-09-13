# YYGC 统一对象重构实施记录

本记录接续 [分阶段计划](YYGC_UNIFIED_REFACTOR_PLAN.md)，只记录实际实施和取得的证据。游戏分支为 `codex/yygc-unified-object-migration`；不推送远端。2026-09-13 开始执行完整开发，U2–U6 尚未实施，正式玩法入口仍使用旧模型。

## U0：功能基线与输入归档

游戏输入提交为 `5c83614`（规划提交，游戏实现与 `7072b26` 相同）。框架输入为 `ccd61e01f15332b1197cfa5ee72af8777c4a0b49`，沿用并逐字节核验仓库中的四份补丁及 `SampleAssemblyAccess.cs`／`.meta`；`tools/prepare-lan-sample.ps1` 成功。用户框架工作区检查时干净，未对其执行重置或清理。

| 验证 | 实际结果 | 本机证据 |
|---|---|---|
| CoreRegression | 1361 项，0 失败 | `artifacts/yygc-unified/u0/core-regression.json` |
| ArchitectureGuard | 225 文件，10 自检，0 错误 | `artifacts/yygc-unified/u0/architecture.json` |
| Unity EditMode `DarkNights.Tests` | 95/95，通过 | `artifacts/yygc-unified/u0/editor-tests.xml` |
| 源码／资产／配置及冻结证据归档 | 1997 个 SHA-256 | `artifacts/yygc-unified/u0/input-hashes.json` |
| 未保存场景保护 | 另存副本与磁盘原场景哈希相同；随后安全保存原场景 | `artifacts/yygc-unified/u0/Pinewatch-unsaved.unity` |

Editor 测试任务 `9e27cb92cd774896bf0d50205d4e3944` 的自动化回传在测试完成后未结束。实际 NUnit XML 记录开始于 `2026-09-12T23:32:19Z`，结束于 `23:32:24Z`，95 项全部通过。核实编辑器没有模态框后，仅停止并重启 Pipeline 服务，恢复命令连接；没有重复提交测试或重启 Unity。后续测试使用 Pipeline 的异步测试入口，并读取其状态文件。

现有 Player 属于历史输入，不能作为此次重构的同输入性能基线。U0 尚无可靠的新性能对比采样；后续必须分别报告有效测量与这个缺失项，不用历史隐藏窗口或全零工作集报告声称性能改善。

## U1：框架前置能力（完成）

隔离框架 worktree 为 `.deps/YYGC-unified`，分支为 `codex/dark-nights-unified-objects`，起点同为 `ccd61e0`。已有 Sample 补丁按原内容应用；它们仍属于游戏依赖准备配置。

已编译并接受分批回归的内容：

- `ObjectSessionContext`：显式会话容器、动态权限检查、准备／激活／退休及加载取消。容器由调用方持有；退休撤权并停止已登记对象。
- `StatefulBehaviour`：状态写权限与网络角色分开；`SyncMode.Session` 不加入 `StateSynchronizer` 的网络状态索引。增加副本应用和脱离池的捕获入口，拒绝过期生命周期提交和订阅重入。
- 对象装配：刷新会话注入，严格检查能力／配置／绑定／工厂；失败清理容器，未激活对象也可显式释放。Loader 接管同一个场景 ObjectInstance。
- 工厂预加载：经 `FastInstantiator` 取得明确的 Addressables 租约，`PreparedObjectDefinition.Create` 同步准备未激活对象，取消／失败释放当前请求持有的引用。
- 真实 Unity 测试：独立状态、客户端误写、深复制、异常回滚、旧作用域提交、换会话依赖、缺配置／能力／绑定，以及真实 Worker Prefab 的预加载与取消。

框架已提交 `ddce2ffdf422c8c9cb8e872fb5f20053cdedcda6`。用户仓库在重新检查干净且 HEAD 未变后快进到该提交；游戏 manifest／lock 指向 `.deps/YYGC-unified`，准备脚本已锁定完整提交并精确校验通过。逐文件修改、原因和落点见 [YYGC 账本](YYGC_CHANGES.md)。

已取得的 U1 证据：

- 第一批 104/104 Editor 用例通过，见 `artifacts/yygc-unified/u1/editor-104-passed.xml`。
- 新增退休与真实 Play 用例后的整批结果为 109 项中的 108 项通过，唯一失败为开启域重载的测试协程超时，见 `editor-109-before-play-fix.json`。原因是 Unity 恢复协程执行位置但不保存局部循环计数，导致测试重复进入 Play；没有提高超时或跳过断言。
- 将两次 Play 写成独立进入点后，开启／关闭域重载两项均通过，见 `play-2-passed.json`。检查真实生成调度器、未激活不更新、Start 一次、退休停止、同定义重入和退出清空状态；该修正只影响测试驱动。
- 退休路径现在幂等；清理失败的 Behaviour 不能通过同定义快速重入继续使用，也不返回池。Start 中发生退休会拒绝后续更新注册。

之后补齐 Loader 原 Prefab 实例接管和跨定义能力替换，14 项非 Play 装配用例通过。整批测试创建的临时空场景被标脏时，Play 驱动误调用 SaveOpenScenes，弹出保存对话框；已改为只保存有资产路径的制作场景。两种域重载最终复测 2/2 通过，见 `play-final.json`。

Mono 正式 Player 和 Sample 均已实际构建成功。正式 Player 显式装配探针 14/14，包含已确认仍 Pending 的冷加载取消、缺配置／重复配置／缺能力／缺绑定拒绝、生成注入、延迟激活和退出撤权；报告为 `artifacts/yygc-unified/u1/player-assembly.json`。Sample 复用同一构建运行 Host＋3 客户端，30/30 通过，报告为 `artifacts/lan-sample/run-20260913-091454/result.json`。该旧报告工具含硬编码旧 frameworkCommit，已明确排除该字段，另存实际二进制 SHA-256；工具后续改为直接记录 Mono DLL 哈希。

构建期间一次 Addressables 写入 `ScriptableBuildPipeline.json` 因 Win32 1224 失败；随后调用被未保存场景提示阻塞，Pipeline 的 5 秒主线程请求超时。保存保护过的场景后让同一构建完成，没有重复提交活动构建。Sample 采用一次 delayCall 加完成记录，避免同步接口等待影响结果。构建临时设置已恢复。

汇总为分批覆盖 111 个不同 Editor／Play 用例，零未解决失败；不是一次 111 项测试运行。ArchitectureGuard 为 236 文件、10 自检、0 错误；Mono 装配 14/14、四进程 Sample 30/30。机器可读证据为 [U1 报告](evidence/yygc-unified-u1.json)。没有据此宣称完整玩法迁移、性能改善、IL2CPP 或双机器 LAN 通过。

## 空间管理

每阶段开始和构建前检查 C／D 盘；不复制整个 Unity Library。阶段收尾保留后续复用的 Player、人工资源、保护副本及报告，清理可重建中间产物。记录落在 `artifacts/yygc-unified/<stage>/cleanup.json`。

U0 已识别闲置 `Game/Library/Bee/artifacts/WinPlayerBuildProgram`，约 3.39 GiB。确认没有 Bee 构建进程、目标及子项没有重解析链接后，递归清理仍两次被自动审批以 `blocked by policy` 拒绝，未执行删除。该限制单独记录；继续复用当前缓存，不再建立整套验证副本，也不把该空间记为已释放。
