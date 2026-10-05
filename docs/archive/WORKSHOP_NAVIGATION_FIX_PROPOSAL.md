# Workshop 导航修正：提案与获批执行

2026-10-03；用户已在本聊天明确批准下述范围，修正已在隔离 checkout 实施并通过 Local Editor 验证。前半部分保留审批时的缺口与方案。

## 已核对缺口

当前锁定 `.deps/YYGC-grid-business` 中，`ObjectDefinitionWorkshopWindow.OnFocus` 延迟调用 `AutoSyncAndCleanConfigs(..., forceRefresh: true)`；`WorkshopConfigService` 在 `isModified || forceRefresh` 时 SetDirty。因此已完整装配的 Definition 只切焦点也会标记未保存。`LoadTarget` 的自动同步还可能补齐缺失配置，切目标并非完全只读。

游戏侧原生窗口探针已经确认焦点会标记未保存，但当前有效临时 Definition 的配置内容不变；详见 `artifacts/workbench-aggregation-20261003/native-focus.json`。原生窗口继续使用原保存与 Undo 入口；项目没有屏蔽焦点、反射移动控件或偷偷清除用户的未保存状态。

## 拟申请的具体范围

| YYGC 文件 | 改动与原因 |
| --- | --- |
| `Editor/Objects/Definition/ObjectDefinitionWorkshopWindow.cs` | 打开、切目标、获得焦点及 Undo 后只刷新编辑树，不自动补配置；能力编辑、显式“同步配置”、显式保存继续同步。保留原窗口、公开入口及保存流程。 |
| `Editor/Objects/Definition/Workshop/WorkshopConfigService.cs` | 仅在真实配置变化时 SetDirty；forceRefresh 只刷新视图回调。避免任何调用者把刷新等同资产变化。 |
| `Tests/IdRegistry/ObjectDefinitionWorkshopNavigationTests.cs`（新增，沿用现有 Editor 测试程序集） | 检查正常／缺失配置的打开、选择、聚焦不改变配置／dirty，显式同步及保存仍能补配置，Undo／Redo 和目标切换释放正常。 |

落点：新建隔离 YYGC checkout；不操作用户 `D:/Developer/YYGC` 主工作区、不升级 Unity 或其他依赖。验证后更新游戏的可重建依赖补丁／锁记录与 `docs/YYGC_CHANGES.md`。这部分接入同样属于需获批范围，不以只改 `.deps` 规避授权。

影响：原先靠“打开或聚焦”补配置的资产需要用户显式同步或保存；正确装配资产的编辑／保存功能保持。无运行协议、游戏数值、Definition 身份、存档或资源 GUID 变化。

验证计划：先隔离框架回归，再经 Local 单一 Unity 通道检查真实原生工坊、资产内容和 dirty 保持、缺失配置的显式修复、保存／Undo／Redo，以及游戏原生聚合与航程草稿。只运行 Editor 验证，不构建 Player／IL2CPP。通过后逐项登记框架文件、原因、落点和结果。

## 实际落点与验证

两个工坊文件及新增导航测试／Unity生成meta已完成；隔离检查点0d461a29294a3932479f12db8800976b3261914a。框架导航3/3、原生接入2/2；游戏本批按不同用例合并16/16。依赖完整补丁在干净基线索引重建18/18，与当前依赖逐文件一致；准备入口幂等检查通过。锁定补丁SHA-256为7b16e941cd7cc119e22b531678584103c697c6b5456b637658cdd0915a039811。

当前UPM包默认不导入测试程序集；新增框架测试保持在依赖补丁唯一源中，通过 tools/workbench-navigation/prepare-probe.ps1 临时镜像到已有DarkNights.Tests程序集进行真实Editor验证，没有改Packages/manifest或另建Unity缓存。镜像和独立持久夹具验完后按工程规则归档，正式源码及报告保留。原生编辑／保存和实际原始配置归属没有接管。

用户维护的D:/Developer/YYGC master及已有RuntimeDebugHub.cs修改保持；没有推送。未构建Player／IL2CPP。完整证据见[evidence/workbench-aggregation-20261003.json](evidence/workbench-aggregation-20261003.json)。
