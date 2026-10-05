# 远征调试页与 F1 唤出

2026-09-29，`fix-20260929-expedition-debug-hub`。协议 17／存档 v13 不变；按用户要求不运行测试或构建。

- YYGC `RuntimeDebugHub` 仅用 F1 唤出，新 Input System 与旧 Input Manager 都移除反引号；窗口标题同步为 `[ F1 ]`。Smart Console 的 F10 不变。
- 原常驻远征按钮块从正式画面隐藏，状态、卸货、舱段升级、派工、补充、驾驶等入口移到 Hub 的“远征”页。开发页仅在 Editor 或 Development Build 注册；非开发 Player 不提供该调试页。
- 调试页复用 `ExpeditionPanel` 的状态文本、按钮可见性／可用性计算及 `ExpeditionHud.Submit`，最终仍由可信服务端命令链核验，不能绕过距离、支付、权限或阶段条件。原生资源、引用及 GUID 保留，隐藏容器不拦截指针。
- 调试页激活时租用 YYGC Interaction Session，阻止键盘／世界／镜头输入穿透；关闭、切页、禁用或销毁会话时释放并注销。调试页转入目的地选择前先关闭 Hub，避免两层模态冲突。
- 正式驾驶台使用靠近后的 E 交互：太空待命时选择目的地，准备阶段取消航程，下降／着陆阶段接管或离开驾驶位。复用既有目的地选择页；购物和售矿仍使用原交互入口，驾驶台不与它们同时触发。未来正式流程的其他设备交互未在本批扩展。

## YYGC 落点与复现

实际包仍为 `file:D:/Developer/YYGC`，基线 `fee18645c997ed7529c4592917de6c412033c84e`。本批仅修改 `Runtime/Debugging/RuntimeDebugHub.cs`，不切换框架分支、不改 manifest／lock。活动文件保留为未提交改动；游戏仓库保存精确补丁及有基线保护的重放脚本：

```powershell
pwsh -NoProfile -File tools/runtime-debug-hub/Apply-F1Only.ps1 -FrameworkPath D:/Developer/YYGC
```

脚本只接受锁定基线或完整已应用状态，未知修改停止；换行规范化后比较源码，补丁文件另有 SHA-256 锁。它不执行测试或启动 Editor。当前本机路径仍不等于新机器完整依赖恢复已验证。

## 边界

只检查源码差异与引用，不运行测试、Play 验证或 Player 构建。用户当前 Play 未被中断；待退出并让 Unity 重新编译后自行检查 F1、反引号、远征页开关及驾驶台 E 交互。无新增 Unity 资产／GUID，无额外缓存或构建中间产物，复用 Local 缓存。
