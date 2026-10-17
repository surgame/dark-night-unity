# R2 富矿伴巢离线样板

本目录承接已保留的[R1采矿探索](../mining-exploration-r1/README.md)，实现独立、可关闭的开发端巢位步骤。原R1源码、快照与演示保持原字节；新结果另行导出，不修改正式Game、作者图或CL-04原生素材。

## 输入与输出

`RichVeinNestPass.Apply(MiningInput, NestPlacementSettings)`只读消费同一R1矿脉／洞室用途结果、真实地形和保护格；自身使用由种子及矿脉Id派生的独立确定性抽样，不消费矿脉随机流。输出冻结的矿脉—SiteId关系、锚点、巢腔／封岩／支撑格、出虫额度提案、抽样及剔除原因，以及局部改形后的真实Core坡形。

- 关闭：巢位和挖空补丁均为空，坡形逐字节沿用R1；矿种、储量、矿格不变。
- 开启：仅在矿脉中段侧旁的非保护围岩内挖出平行巢腔；矿格、原天然空腔不被改写。巢腔不直接贴矿，保留侧向可挖封岩；空间不足则拒绝，不退回末梢、不移动原矿脉或改变主路。
- 当前试参：壁内矿脉储量至少18、壁内格至少6；近／中／远抽样概率60%／75%／90%，整图最多8巢、锚点间距至少12格，深处优先占用上限。按矿脉主轴使用5×3或3×5格巢腔、封岩1～3格、保留底部支撑；每巢出虫额度2仅为有限遭遇提案。
- `VeinSideProfile`提取露头到深处的主轴，中段侧向接触点取R1冻结序的25%～60%，同时约束几何进深；揭露示意后至少保留四成矿格。冻结Contact／RevealCount／后续格数与储量，浏览器不再在显巢时直接展示整条矿脉。
- 分别记录`placed`、`roll-empty`、`budget`、`space-or-spacing`和`disabled`；设定概率不等于最终挂巢率。侧旁版普通布局实际近1/2、中2/5、远5/6，共8巢。当前金矿示例在9/21格处侧开巢腔，后续12格、24/49储量保留；这是静态剖面序，不是实际挖掘进度或收益结算。

上述参数用于看分布效果，尚未成为获批平衡值。当前空间检查采用格级模板；不证明角色／生物身体可通行、反应窗口或挖掘节奏合格。

## 复现

需要本机.NET 8兼容SDK、原R1快照，以及与该快照地形身份一致的本地已编译`DarkNights.Core.dll`。仅引用现有程序集，不启动Unity，不重新导出或覆盖R1。

```powershell
dotnet build experiments/rich-vein-nests-r2/RichVeinNests.csproj --nologo -v minimal
dotnet experiments/rich-vein-nests-r2/bin/Debug/net8.0/RichVeinNests.dll '<项目绝对路径>' '<保留的R1快照绝对路径>' '<独立R2输出绝对路径>'
```

输出`rich-vein-nests-snapshot.json`和`rich-vein-nests-generation.json`，侧旁版schema=2、version=`rich-nest-side-v2`。请使用独立新输出目录，保留首版R2结果；旧版源码checkpoint、演示和数据身份见侧旁回执。快照包含普通布局与42个既有洞室用途变体上的巢位结果，共43个方案；它们属于同一地图，不是43张地图。正式Core地形与R1摘要不符时拒绝导出。编译的bin／obj留原位供复现，不提交或复制Library。

交互演示可对比开关、整图／局部及露头到巢腔的逐步揭露，使用CL-04已有9张PNG，按16像素／格及原生锚点最近邻绘制。前兆、出虫和清理仅切换冻结姿势；不推进AI、伤害、采集结算、玩家视野、噪声、联机或恢复。揭露时的挖矿及封岩清除只属于静态剖面示意。

正式生成入口／审核导出／加载、所属Behaviour／State和运行遭遇仍待后续实现。合同见[巢穴步骤](../../docs/TERRAIN_GENERATION.md#cl01-rich-vein-nests-r2)，编译与演示核对见[首版回执](../../docs/evidence/cl01-rich-vein-nests-r2.json)及[侧旁版回执](../../docs/evidence/cl01-rich-vein-nests-r2-lateral.json)，状态见[执行台账](../../docs/DEVELOPMENT.md#stage-cl)。
