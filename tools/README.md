# 开发工具索引

从仓库根目录运行工具；依赖与参数以各工具说明、脚本和[依赖合同](../docs/DEPENDENCIES.md)为准。当前状态见[开发执行计划](../docs/DEVELOPMENT.md)。同一 Unity Editor 的写入、测试与构建串行执行。

| 分类 | 目录 | 用途 |
| --- | --- | --- |
| 纯规则与架构 | `ArchitectureGuard`、`CoreBuild`、`CoreRegression`、`LanSampleRules` | 程序集边界、源码规范和纯业务回归 |
| 地形与航程回归 | `TerrainRegression`、`TerrainRepairTests`、`PlanetFlowRegression`、`slope-jump-flow` | 地形、背景、Modifier、恢复与运动检查 |
| 当前联机矩阵 | `embedded-minerals`、`space-planet-flow`、`tool-definition-harvesting` | 同一 Player 的矿层、航程和装备两人／四人、正常／弱网矩阵 |
| 专项联机复现 | `expedition`、`walkable-ship`、`ship-trade`、`runtime-debug-hub`、`oxygen-removal`、`bootstrap-startup` | 旧切片和仍需追溯的脚本；结果按构建身份区分 |
| 只读审查／探针 | `CaveTunerProbe`、`MineralMapProbe`、`NetworkReviewProbe`、`OreRuleReviewProbe`、`grid-business` | 核对锁定依赖、规则与状态边界 |
| 地形与美术源 | `terrain-reference`、`contour-reference`、`cave-art`、`cave-entrance-art`、`terrain-modifiers` | 原生图集／参考重建和专项配置工具，保留唯一源码 |
| 历史框架补丁与验证 | `fishnet-patch`、`lan-framework-patch`、`map-framework-patch`、`workbench-navigation` | 锁定依赖的可重现准备输入与已获批专项验证；不因整理改写或执行框架补丁 |

根目录的 `prepare-*.ps1`、`test-*.ps1`、构建、采集与审核脚本继续保留原路径，避免破坏已有文档和自动化入口。不能仅按创建日期判定脚本弃用。

新增验证日志与最终报告放对应 `artifacts/` 任务目录。验证结束后仅将确认可重建、无人使用的中间输出归档到 `artifacts/待清理/YYYYMMDD-任务名/`，保留源码、报告与当前 Player；不直接删除。
