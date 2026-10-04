# 内嵌矿独立进程检查

复用现有局域网测试工具及真实客户端输入，不注入权威矿格状态。`landed-embedded-minerals` 只选择同一正式最终地图中有支撑、净空和矿格射线的初始出生点。需要开发版 Mono Player；每组独占端口，串行复用同一构建。

```powershell
python tools/embedded-minerals/test_network.py --player '<Mono Player绝对路径>/DarkNights.exe' --clients 1 --port 29440 --output-root artifacts/embedded-ore-development-20261004/network
python tools/embedded-minerals/test_network.py --player '<同一Mono Player绝对路径>/DarkNights.exe' --clients 3 --port 29450 --output-root artifacts/embedded-ore-development-20261004/network
python tools/embedded-minerals/test_network.py --player '<同一Mono Player绝对路径>/DarkNights.exe' --clients 1 --weak --port 29460 --output-root artifacts/embedded-ore-development-20261004/network
```

覆盖真实扣耐久、采空、货袋单次产出、冻结矿格一致性、暂停、晚加入、重连、v18 写盘、加载和进程重启。结果记录 Player、程序集及脚本摘要；日志和首次失败保留。弱网使用 200ms RTT、5% loss、25ms jitter。前置 Ready 失败时停止后续采集与同批四人弱网，不将未执行项写为通过。

图形窗口用于真实相机回执。自动截图仅作诊断；规则／矿层小样、正式组合画面与最终美术分别验收。该检查不覆盖全矿工路线、全航程弱网、前台性能、IL2CPP 或双机器。
