# R1 采矿探索离线样板

用户在确认R0本地地图基线后要求继续采矿探索样板，改善均匀小团块分布，并保留洞室／连接和可指定的特殊洞室。本目录为开发端C#候选算法；它读取本地已编译正式Core生成地形，输出已有`TerrainDepositBlueprint`占格与静态演示元数据，不创建第二份运行世界。

## 当前范围

- 使用原`rift-expanded.minemap`和已保存WorldSession星球参数，保留最终前景、坡形、原作者资产和正式配置。
- 普通分布包含较多路边小矿，以及露头相连的壁内矿脉；围岩厚度、支撑附近的露头、非重叠间距、弯曲生长和区域预算共同约束落位。
- 近／中／远按入口沿真实空腔的距离分档，首版阈值取最大距离的30%／65%；不是按Y或Floor分档。算法不模拟身体、重力、跳跃及梯具，不能据此签署角色通行。
- 普通样板储量试参为90／180／360，矿种暂用现有铁／金；这些是采矿布局参数，不是售价或获批经济平衡。
- 以稳定RoomId为作者选择，提供普通、富矿、少矿三种用途。原洞室几何与路径不变；富矿增加当地壁内矿体，少矿移除该归属区的矿脉。用途可调整当地预算，仍保持近段＜中段＜远段总储量，最终继续整图审核。
- 输入要求不满足时拒绝导出；富矿用途缺少可用围岩时显示未满足，不为此打穿另一通道或改变作者洞形。
- 揭露顺序在C#生成时冻结，浏览器只显示逐步剖面，不模拟采集支付、耐久、掉落、视野或角色运动。

## 复现

在仓库根目录执行；需要已存在且身份可核对的`Game/Library/ScriptAssemblies/DarkNights.Core.dll`及本机.NET 8兼容SDK。

```powershell
dotnet build experiments/mining-exploration-r1/MiningExploration.csproj --nologo -v minimal
dotnet experiments/mining-exploration-r1/bin/Debug/net8.0/MiningExploration.dll '<项目绝对路径>' '<本次演示输出绝对路径>'
```

导出`mining-exploration-snapshot.json`与`mining-exploration-generation.json`；前者包含同一地形、坡形、空腔距离、原始矿床、矿脉及按洞室用途预生成的差分，后者记录输入身份和静态不变量。编译输出留在忽略的bin／obj，不提交或复制整套Library。

UI通过当前会话的visualize审阅，来源与核对见[回执](../../docs/evidence/cl01-mining-exploration-r1.json)，计划及状态见[演示阶段](../../docs/ROADMAP.md#cl01-demo-milestones)与[执行台账](../../docs/DEVELOPMENT.md#stage-cl)。当前仅离线样板已产出；正式Game生成／采掘接线、完整路线合同、巢穴、梯具、Unity运行及联机／存档均未由本目录实现或验收。
