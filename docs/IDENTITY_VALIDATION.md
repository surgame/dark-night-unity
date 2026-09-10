# YYGC 定义身份隔离验证

2026-09-10。本分支保存受控测试夹具，不代表 Dark Nights 的玩法或旧业务存档已经完成迁移。

配套框架为同级 `definition-id` checkout 的 `codex/definition-identity`，实现提交 `bc7f710`，版本 `0.3.0-preview.1`。复跑方法在框架 `Tools~/DefinitionIdentity/README.md`；结果和哈希在 `Documentation~/Evidence/definition-identity-2026-09-10`。两个分支仅本地提交，未推送。

该测试宿主当前依赖采用本机覆盖：YYGC 指向实现 checkout，六个已锁定 Git 依赖使用相同版本 `.deps/upm` 缓存，manifest 增加 YYGC testables。缓存和这些临时绝对路径不作为发布依赖提交。使用此分支前按框架验证指南在隔离宿主配置正确的实现依赖；不要把旧主 checkout 当作已包含预览 API。

`Assets/IdCompatibilityFixtures` 与 `Assets/Plugins/IdentityCompatibility` 是旧包冻结的资产/二进制调用方；不可重新生成以覆盖失败。`Assets/IdentityPlayerProbe` 是可区分 V1/V2 配置的测试输入，Build 只修改这个测试目录。`Assets/IdentityValidation` 和 Editor 验证入口的权威源码副本在配套框架 Tools~ 中。

已通过：56 个独立规则、79 个身份专项 EditMode、正式 Mono Player、本地 Key/GUID/UGUI、V1/V2 独立进程、晚加入/重连、混连拒绝及新旧 V1 双向互通。全量当前 99/103，4 个失败均在原版复现；原版对照 18/24。详细错误及旧网络宿主的显式初始化适配记录在框架实施文档。

原始日志、构建、冻结 DLL 与备份保留在本机 `artifacts`，不提交大型构建和依赖缓存。IL2CPP、本机外干净依赖恢复及真实旧业务存档仍未验证。
