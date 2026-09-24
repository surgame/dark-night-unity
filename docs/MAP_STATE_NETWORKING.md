# AnyRuleD 地图联网重构

本轮以 `ft-20260922-terrain-modifiers` 的 `e204c1d` 为游戏基线，在 `ref-20260924-map-state-networking` 实施。YYGC 从 `4939af2` 建立同名隔离工作树；用户维护的 `D:/Developer/YYGC` 主检出未切换。完整目标、阶段进度和未验收项记录于 `D:/Downloads/AnyRuleD_MapSync/PROGRESS.md`。

## 归属

| 层 | 职责 |
|---|---|
| `com.tsgame.anyrules` | 地图权威内核、变化集、离线编辑与表现基础；没有 FishNet/YYGC 依赖 |
| `com.tsgame.anyrules.networking` | AMP1 V1 协议、每连接发布、只读副本、诊断数据合同；没有 FishNet/YYGC 依赖 |
| `com.tsgame.anyrules.networking.fishnet` | FishNet Reliable 广播、认证连接生命周期、队列泵送；不结算业务编辑 |
| `com.tsgame.anyrules.yygc` | YYGC 地形命令、对象和业务适配 |
| Dark Nights | 工具、距离、权限、奖励、地图生成与背景、存档、场景表现接线 |

`AuthorityRevision`、每连接 `StreamCommit` 和 `ChunkRevision` 是不同编号。AMP1 V1 帧结构不变；新 Delta 仅含最终变化格，Snapshot 仍含完整区块。权限版本变化先检查撤权。客户端在事务所有区块齐备后原子安装，并将变化范围发给表现层。

## 当前接入

游戏通过 `TerrainMapAuthority : IGridChangeSource` 向流发送器提供提交变化集；`SessionTerrainNetwork` 以区块摘要维护联测指纹；`RandomLevelEntry` 在副本 `Applied` 时通知 `TerrainPreview` 刷新相应范围。地图网络包从完整 YYGC 提交锁定提取到忽略的 `.deps` 目录，使用 `tools/prepare-map-packages.ps1` 重建并逐文件校验。旧补丁式入口改名为 `prepare-map-packages-legacy.ps1`，供历史切片复现。

## 验证与限制

- 新协议的 .NET 测试已通过 130/130，含不同权限投影对比；协议 runner 写出本轮 TRX/JSON。无 YYGC 独立 FishNet 样板已在 Unity 6000.4.9f1 编译为 Mono，Host 0/1/2/4/8/16 与 Dedicated 1/4 客户端通过；TypicalWeak、Severe、Blackout 四客户端通过，真实 UDP 分别丢弃 25、63、28 包。完整样板矩阵基于先前 `b0e2387` 产物；新桥接代码的 Unity 编译和游戏进程检查单列。
- 游戏的 Unity 6000.4.9f1 Editor 最终锁 `e07e9a9` 编译通过，调试器菜单可开窗；上一锁 Terrain 定向 41/41、全量 Editor 255/259，旧包路径断言已修正并定向 2/2，仍有两个旧场景对象数断言及既有按钮主题失败。`b239df6` 锁的 Mono 正常与真实 UDP 弱网三进程各 28/28，v10 地图编辑正常与弱网各 15/15；最终锁的新 Mono 地图循环 17/17，包括真实 Host 与客户端诊断桥的授权投影 SHA 对比和错误令牌拒绝。每个构建的身份见[本轮证据](evidence/map-state-networking-2026-09-24.json)。
- 旧 Player 初次混连在 YYGC 类型表握手阶段拒绝；原因是 `TerrainEditCommand` 搬移程序集后类型身份哈希变化。对唯一迁移命令显式登记旧线缆程序集名，保留其余握手校验。修正后最终锁 Host＋基线客户端、基线 Host＋最终锁客户端各通过真实独立进程地图循环 15/15，含晚加入、重连和写盘恢复。
- 正式运行不注册调试操作端点。Editor/Development 会话仅以显式端口和令牌开放回环只读桥，调试编辑仍须经过游戏业务命令授权。不同权限连接目前由 .NET 投影测试验证，尚无异权限双 Player 实测；调试窗口故障档的按钮操作未人工验收，故障脚本本身已有真实 UDP 三档证据。
- 用户明确选择本轮不构建或验证 IL2CPP，状态为 NOT_RUN。前台性能与双机器验收也未完成。

本轮依赖文件、提交、验证结果及回退步骤在[YYGC 改动账本](YYGC_CHANGES.md)中继续记录。任何包路径或锁 SHA 变化后，先重建 `.deps`，再进行 Unity 编译与 Player 验收。
