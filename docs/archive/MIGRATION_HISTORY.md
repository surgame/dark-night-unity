# 移植与统一对象历史摘要

2026-10-07 整合早期评估、Core 迁移、C 方案、会话接线和 YYGC U0–U6 过程。这里解释现行架构的来源；当前合同以[技术架构](../ARCHITECTURE.md)、[联机设计](../MULTIPLAYER.md)、[存档格式](../SAVE_FORMAT.md)为准，当前验收以[执行状态](../DEVELOPMENT.md)为准。

## 已收敛的架构决定

| 历史阶段 | 最终保留的决定 |
| --- | --- |
| Godot → Unity 规则迁移 | 不变更原数值、布局、RNG 调用顺序或攻击时机；引擎、网络、存储放 Runtime，Core 保留纯计算和冻结合同 |
| C 方案职责拆分 | 模拟、可信会话入口、投影、客户端输入和外观分开；显式请求参数替代全局选择状态 |
| 正式网络与恢复 | 服务端唯一写入；Host 走同一授权入口；策略版本、去重、Ready、epoch、连接代次在执行点验证；加载失败保留旧世界 |
| U1–U4 YYGC 接入 | Behaviour／State 拥有业务状态；配置和绑定通过真实对象装配校验；工厂、资源租约及短事务组成创建／转职／恢复流程 |
| U5 旧模型退出 | 删除旧 Entity、WorldState、GameSession 和过渡运行入口；ObjectSession 只组合能力和索引，不存第二份经济／实体状态 |
| U6 功能与性能修正 | 冻结完整投影、严格有界封套和真实多进程回归；功能结果、后台容量观察与前台性能分别记录 |

状态、恢复及回归入口的详细映射继续保留在[测试覆盖](YYGC_UNIFIED_TEST_COVERAGE.md)。框架缺口的历史复评继续保留在[YYGC 能力复评](YYGC_REASSESSMENT.md)，具体修改与锁定来源见[YYGC 账本](../YYGC_CHANGES.md)。早期“必要时修改框架”的措辞不再构成授权；2026-10-03 起必须先取得具体范围的同意。

早期恢复弱网发现FishNet断线后仍持有未收齐的SplitReader，旧连接残片可混入新连接。固定4.7.2基线的非Started客户端状态分支通过 `ResettableObjectCaches<SplitReader>.StoreAndDefault` 释放残片，补丁为 `tools/fishnet-patch/ResetClientSplitOnDisconnect.patch`，由 `tools/prepare-fishnet.ps1` 校验。随后YYGC关闭Domain Reload的重入单例登记修正独立记录在账本；两者不混算为同一框架改动，当前准备输入以[依赖说明](../DEPENDENCIES.md)为准。

## 冻结规则与来源

规则输入来自旧游戏提交 `91cb09ff0894f26134f07fd544f1273d9fe7ffaa`；布局、内容、三夜／无人照料结果、旧档和随机向量保存在 `tools/CoreRegression/Fixtures`。这些文件仍是只读期望，不用当前实现重新生成。来源提交与哈希继续保留在[Core 迁移来源记录](evidence/core-migration-2026-09-11.json)。

随机算法标识为 `godot-pcg32-clz-f32-v1`，仅移植实际使用的 PCG32、整数闭区间和 float 采样。早期 Unity Mono 的浮点中间精度差异通过各步骤固定 binary32 舍入修正；整数、RNG 状态和调用顺序保持。向量与探针分别位于 `Fixtures` 和 `tools/CoreRegression/GodotProbe`。

旧 Godot／Unity v1 成功读取在统一对象阶段退出产品要求。旧格式夹具保留用于拒绝测试及研究；新格式仍要求冻结、严格字段和关系校验、原子写入及失败回滚，不能用删除旧兼容测试代替新恢复验收。

## 历史验证边界

早期 Core、C 方案和正式网络各有独立源码及 Player 身份。它们证明迁移过程中的问题曾被发现和处理，不证明当前协议25／v19通过。

统一对象协议7候选 `4e3798f` 当时记录 Editor／Play 144项、Mono完整功能矩阵350项及240秒摘要容量检查21项通过。容量使用256实体／1024箭矢的合成投影，Host仍只有17个真实权威对象，不能解释为256个AI的模拟性能。前台观察由用户暂缓，IL2CPP与双机器也未随该批签署。

性能调查区分了装配反射、序列化、对象应用与自动化JSON报告开销。YYGC最终只缓存不可变装配声明，仍逐次检查实例配置和绑定；额外类型解析缓存因收益不足撤回。投影积压的修正保持原MemoryPack完整帧，在外层使用有界原始／GZip封套，解封后继续校验身份与规则。封套预算以现行实现为准。

历史失败、被取消的批次和不足的性能窗口没有改写为通过。普通前台、最终手感、IL2CPP、双机器和真正新机器依赖恢复仍按当前执行计划核销。历史清理拒绝与待处理产物见[阶段清单](STAGE_CLEANUP_INVENTORY.md)，本次文档整理不重试这些操作。

## 查阅与复跑

从[工具索引](../../tools/README.md)进入当前验证工具；Core只做纯计算，真实状态、装配、资源和生命周期在Unity中验证，联机使用独立进程。不同构建的计数不能相加形成当前通过数。

本次移除重复的实施过程和部分旧机器摘要。完整原文可从Git整理前提交恢复，映射见[整理回执](DOCUMENT_CONSOLIDATION_20261007.md)。冻结夹具、作者来源、框架授权与仍有用途的报告保持。
