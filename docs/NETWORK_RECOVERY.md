# 正式会话恢复与文件接入

2026-09-12，基线 `d427fc7`，协议 5。保存、读取、重开和恢复凭据已实现；九组弱网矩阵与重复启停已通过；M5 干净交付及外部条件继续验收。

- `Save` 与 `BeginLoad`／`Restart` 经同一可信命令入口授权和去重。一次最多一个文件任务，保存捕获命令执行时刻的冻结快照；后台只处理冻结数据／文件文本，完成回到权威线程提交。坏档或缺失槽位保留原世界，原子保存保留已有文件。
- 现有两个菜单新增原生槽位按钮，十个槽位循环选择；保存、读取与主菜单继续已接通。F5/F9 使用当前槽位。重开保留房间和当前共享控制策略，加载／重开增加 epoch、清除 Ready，等待新快照确认。
- 服务端为来宾随机签发 32 字节恢复凭据，只在定向可靠回执中发送；断线槽位保留 120 秒，重连轮换凭据并增加 ConnectionGeneration。活跃槽位不可抢占，旧／过期凭据不可冒领，凭据只存在当前进程内并绑定房间。
- 已修复 Ready 截止时间未随 epoch 重置的问题。基础四进程 22/22，覆盖超过 30 秒后读档、四端状态、坏档、原槽位重连、重开、退出和继续；独立回归 1355，Editor 55，新增凭据边界另行复测通过。证据路径见本批最终报告。

## 真实弱网发现的第三方缺陷

首组 0 ms 标称 RTT、双向 25 ms 抖动、0% 丢包产生 187 次真实 UDP 乱序。在重新连接时，客户端的完整投影反序列化失败。源码与实际 Editor 探针确认：FishNet 4.7.2 在断线后仍持有 `SplitReader`（已收 1 片／预期 2 片），只有收齐时才释放；旧连接残片可被拼入新连接。

修复仅向客户端非 Started 状态分支加入 `ResettableObjectCaches<SplitReader>.StoreAndDefault`。依赖版本保持 `de19b5d66459f60400ffd0edc443c4da173a01e7`，隔离 checkout 为 `.deps/FishNet`，UPM 引用其 `Assets/FishNet`；`tools/prepare-fishnet.ps1` 核验基线及完整补丁差异，拒绝未知改动。补丁单独存为 `tools/fishnet-patch/ResetClientSplitOnDisconnect.patch`，不直接改 Library 包缓存。

分片修复属于 FishNet。随后关闭 Domain Reload 的实际启动还发现 YYGC 池化单例的 Instance 没有重新登记，造成 Interaction Sessions 为空。新增 `RestoreSingletonOnPooledReentry.patch` 将本地／网络单例登记移至每次执行的 InitializeCore，首次业务初始化仍只运行一次；实际服务重入回归与完整 Editor 55/55 已通过，连续无 Domain Reload 两次 Play、每次三轮会话均为 10/10，九组四进程弱网均为 22/22。

用户维护的 YYGC 仓库保持不变；本次三项 YYGC 改动见 [改动账本](YYGC_CHANGES.md)，FishNet 单独计列。首次分片探针等待条件过早（只看游戏视图），补充客户端 transport 停止后，常规 Domain Reload 的三次会话共 9 项通过；随后独立记录的两份无 Domain Reload 报告补齐完整启动检查。

## 已完成的矩阵与连接超时

九组标称 RTT 0/100/200 ms × 丢包 0/1/5%，每方向抖动 25 ms，均通过四进程恢复 22/22。0 ms RTT 组的负抖动截为零，实际延迟并非恒零。真实 UDP 乱序和丢包计数随报告保存，最差组记录 1058 次乱序、234 次丢弃。另通过真实网络并发 13/13：三人同时付款、业务重发去重、序号冲突、非法目标、共享工位唯一占用、旧策略、HostOnly 训练和旧 epoch。

第二轮弱网中，Host 退出包丢失时默认 60 秒客户端超时超过退出等待窗口；正式适配现通过 FishNet 现有超时接口统一配置为 15 秒，网络心跳仍在暂停时运行。修正后全九组通过，不另建心跳或恢复协议。报告与各项 DLL 身份见[恢复证据](evidence/network-recovery-2026-09-12.json)。
