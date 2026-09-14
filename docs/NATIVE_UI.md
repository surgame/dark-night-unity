# 原生 UI 与可操作切片

2026-09-14：[M5 UI 校准](M5_UI_CALIBRATION.md)已补齐 30 个按钮的文字四态、原 Label 的顶部字排／多行间距、五处来源阴影，以及新增菜单控件区域。UIFont 使用实际解析的 Microsoft YaHei／HintedSmooth，原 Georgia Bold 保留；正式 YYGC 根 Canvas 启用像素对齐。原控件几何／文字／绑定及 UI 相关 22/22、真实鼠标 19/19 通过；新 Mono 启动／独立双进程／两分辨率捕获共 31 项通过，十张图已复核，完整同状态世界画面对照仍待完成。输入、产物哈希和边界在校准文档单独记录。下方为旧批次历史。

2026-09-12 Player 续验：新主题已进入成功的干净 Mono 构建，两分辨率昼夜／暂停／帮助／主菜单捕获各 6/6。图像复核仍发现字体粗细与基线、禁用文字、新增槽位／控制策略按钮位于暂停面板外等问题，尚未签署画面通过。详见 [Mono 验收与后续校准](MONO_ACCEPTANCE.md)。下方为此前实施和验证时点。

2026-09-12 后续校准：保留五页既有 Image、按钮绑定与 GUID，显式有限编辑补齐原主题圆角、内边框及 hover／pressed／disabled 背景和边框色；新增 `NativePanelTheme` 只改网格，不改布局。两种分辨率各 162 控件重开检查通过，完整 Editor 55/55，真实鼠标 13/13。后台 Editor 的探针输入路由在 finally 恢复；曾因默认 GameView 焦点策略未收到测试鼠标的失败报告不算产品故障。十槽位存档产品已由 M4 接通，后文是首版历史边界。新主题等待干净目录 Mono 画面验收。

2026-09-12，基于 `bfb094a` 的 A3/B 批次。五个正式 UGUI Prefab 已接入 YYGC 定义、Addressables、生成字段／事件绑定和现有 UGUIManager。当前可从主菜单建立 LAN Host，另一客户端通过地址加入；选择、框选、镜头和建造预览均为本地状态，操作通过同一个 SessionClient 请求权威端执行。

## 来源与资源维护

`tools/prepare-ui.py` 只向空 staging 目录提取原版 UI／Theme 依赖，保留 15 个源文件的 SHA-256；`tools/capture-ui.gd` 在隔离副本中去除业务脚本后，用锁定 Godot 导出 1280×800、1600×900 五页共 162 控件的实际几何、文字和样式。冻结结果为 `UiLayoutInput.json`、`UiSourceHashes.json`。运行、普通导入和构建均不调用这些工具，不依赖旧项目或 Godot。

`Dark Nights/Content/Install Initial Native UI` 只允许 `Res/UI` 为空，已执行，不可重跑覆盖正式资源。后续修正通过有限的 Prefab 编辑完成，GUID／绑定保留。原生控件的 RectTransform 是可编辑来源；两种视口之间使用锚点和偏移适配，不在运行时重排或覆盖人工布局。

原版使用系统字体；UIFont 保留 Microsoft YaHei UI／Microsoft YaHei／Noto Sans CJK SC 回退，标题保留 Georgia／Times New Roman。原生 Font 资产保存动态字体的 Material 和空 Atlas 子资源，字体字节不复制到项目；已验证重开后中文／英文实际字符可用。Windows 字体环境与跨 GPU 画面对照仍须随交付记录。

## 已接入的实际行为

- Chrome、MainMenu、PauseMenu、Help、Result 五页，新增明确的 LAN 地址／加入入口和房主控制模式按钮。15 类实体头像从原 VisualDefinition 映射，HUD 费用读取唯一规则目录。
- 本地单选、Shift 追加／取消、框选、G 守卫／I 空闲工人、镜头移动／缩放／Home、小地图点击；右键移动、采集、攻击，建造、训练、招募、修缮和时间操作发送显式参数。耗尽和派生农田工位不能直接选中。
- 通过同一定义工厂创建建造预览，客户端与权威端共用 `PlacementGeometry` 占地公式；预览不支付，异步结果检查连接、epoch 和当前模式，取消和退出释放对象。
- 小地图、选择环、生命／施工／生产／训练状态与框选使用 UGUI 网格；显示只读取冻结投影和显式 `EntityView` 锚点。世界销毁先于 Entry 销毁时也能安全清理。
- 选择、放置和菜单互斥使用 YYGC Interaction Sessions；没有另建全局事件总线或输入锁框架。

联机语义调整：菜单／帮助只阻挡本机操作，不自动暂停所有玩家；房主通过暂停按钮或空格修改权威暂停状态，关闭菜单保留既有暂停状态。此行为避免来宾打开帮助便暂停共享营地。存档／加载／继续按钮目前禁用，F5/F9 产品接入与完整槽位流程留在 M4，不能视为已完成。

HUD 需要“已经生成的敌人”数量，冻结投影增加 `NextSpawn`，正式会话协议升为 3；握手跟随协议版本，旧 Player 不能混用。实际 Editor 编码器测量初始 1367 B，256 实体／1024 箭矢支持上限 260574 B，限制仍为 262144 B；这些是编码测量，不代表大载荷网络已经验收。

## 本批验证与边界

- 独立规则回归 1316/1316；架构守卫 181 文件、10 自测、0 错误。
- 完整 Editor 首轮 48 项中 46 通过，两项仅因原源 CRLF 与 Unity LF 的文字换行比较失败。统一比较换行表示后 UI 3/3 通过；补充头像和 CanvasRenderer 断言后 Native 5/5 通过，未重生成期望布局或规则夹具。其他已通过项目复用，新增 NextSpawn 非法范围与 roundtrip 已通过。
- `Dark Nights/Verify/Native UI Runtime`：正式 Play 内有限鼠标探针，13/13 通过。实际点击主菜单、选工人、右键派工、预览、建造一次扣款、取消、地图定位、菜单输入互斥、退出清理及重开。临时 Unity 鼠标设备在 finally 释放，恢复原设备。
- Mono Player 本批实际构建一次，独立启动 6/6，Host＋独立客户端 13/13 通过，包含真实原生视图及生成 UGUI 暂停／招募事件，运行日志无异常。双进程首轮发现报告原子替换时读取基线的脚本竞态，修正等待条件后复用同一 Player 通过，不重建程序。
- 运行证据：`artifacts/migration/ui-runtime.json`、`native-ui-host-1280.png`、`run-20260912-010708-579-mono/result.json` 和 `session-20260912-010837-332/result.json`。归档摘要包含实际游戏 DLL 哈希，见[本批证据](evidence/native-ui-2026-09-12.json)；Unity 通用启动 EXE 哈希不能单独标识脚本版本。

M2 仍待四人并发／工位／弱网及上限传输测量；M3 仍待完整环境、箭矢／死亡／效果／音频、插值、完整三夜及两分辨率昼夜画面对照。当前主题圆角和部分表现细节尚未校准，不把几何一致写成像素验收。M4 存档编排／重连凭据与 M5 干净构建、性能、双机器及另行授权的 IL2CPP 均未据本批签署完成。

YYGC 必要修正及授权记录见 [逐项改动账本](YYGC_CHANGES.md)。
