# 太空到星球纯 Core 回归

独立 .NET 8 控制台引用 `CoreBuild`，并复用 `CoreRegression` 的实际 JSON 规则读取器。生产 Core 按 C# 9 / .NET Standard 2.1 构建，不启动 Unity、不修改玩家存档或冻结夹具。

在仓库根目录执行：

```powershell
dotnet run --project tools/PlanetFlowRegression/PlanetFlowRegression.csproj --configuration Release -- artifacts/space-planet-flow/core-planet-flow.json
```

报告输出路径可替换；存在失败时返回非零退出码。`--snapshot <报告路径>` 仅执行完整冻结快照场景；`--diagnose <种子>` 输出默认泊位的房间支撑与字符地图，供失败定位。

覆盖星球配置边界、非法值、取消、不可变副本；默认 100 个种子与其他五种泊位各 12 个种子；开放天空、全宽平台、矿床筛选、背景参考、船壳和起落架几何；航程阶段、目录、身份、驾驶关系与完整 `SnapshotValidator` 交叉校验。

`TerrainWalkProbe` 以 10×22 权威单位占地、2 单位步幅和最大 2.05 单位高差采样最终坡形，检查无需跳跃的连续地面路径。它是独立纯几何探针，不运行 `TerrainHeroMotion`，不等同于实际主角通行、联网或视觉验收。

2026-09-26 修正生成器误将房间中心竖井底部作为洞室支撑点，以及随机实际种子／太空载体地图的完整快照交叉校验后，`core-planet-flow-r3.json` 为 **1903/1903**；同批 `CoreRegression` 为 **1048/1048**。初始失败报告保留于同一证据目录；后续代码变化须重新记录对应结果，不能沿用本次数值。
