# 随机地图 Debug Bootstrap

2026-09-17。用于直接检查随机地表、地下房间和 DualGrid 表现的独立离线入口。

## 打开与操作

Unity 菜单选择 **Dark Nights / Debug / 打开随机地图 Bootstrap**，然后点击 Play。也可以直接打开 `Game/Assets/DarkNights/Res/Terrain/DebugBootstrap/TerrainDebugBootstrap.unity`。该场景不经过正式 Bootstrap、主菜单或 Ready，会自动生成地图并把观察角色放入入口洞室。

| 操作 | 效果 |
|---|---|
| W / A / S / D | 二维飞行；不受重力、墙体和地形碰撞影响 |
| Shift | 飞行速度临时乘 3；面板基础速度可调 2–100 格/秒 |
| 滚轮／镜头距离滑杆 | 调整正交镜头距离；默认半高 12 格，范围 5–100，越小越近 |
| R／新随机种子 | 重新生成新种子地图 |
| 重建同种子 | 复用当前所有参数重建 |
| F／入口按钮 | 返回入口洞室 |
| 8 个房间按钮 | 直接飞到对应洞室 |
| F1 | 收起／显示调试面板 |

左侧面板和 `Terrain Debug Bootstrap` Inspector 均可调整种子、六类地表、自然洞穴、起伏和矿脉密度。默认开启实时重建：停止调整约 0.35 秒后后台生成，并用真实 DualGrid 页面替换预览。文本框输入时暂停角色键盘控制；点击面板外恢复。重建成功后角色回到入口，镜头距离与飞行速度保留。无效参数保留上一次成功地图并显示错误。

独立 Mono 构建菜单为 **Dark Nights / Debug / 构建随机地图 Bootstrap Mono**，每次写入新的时间戳目录。本批产物为 `artifacts/terrain-debug/player-mono/TerrainDebug.exe`。

## 原平地与生成算法

原 `Res/Scenes/Pinewatch/Pinewatch.unity` 及其 `.meta` 对照 `a1cd509` 没有差异，因此无需覆盖恢复。此前改变的是正式默认开局选用的 `RandomPinewatch` 模板；其 `PlayableTerrainGenerator` 在随机蓝图前 72 列加了原营地平地覆盖。

新 Debug Bootstrap 不加载两个正式关卡、不包含旧营地建筑或平地节点，也不调用该 72 列覆盖步骤。它直接使用 `TerrainGenerator.Generate` 的原始完整蓝图；HTML 本身定义的三个着陆平台仍属于参考算法，未擅自删除。正式 Bootstrap、固定关卡、随机营地模式及规则保持原样。

参考文件实际路径为 `tools/terrain-reference/terrain_generator.html`。算法使用入口、矿洞、树根、长廊、熔炉、首领、密室、遗迹 **8 个房间模板和固定拓扑的 7 条连接**。种子改变房间中心的小范围偏移、地表和矿脉，可选择叠加自然洞穴；它不是任意房间数量、任意拓扑的拼接算法。现有 24 组 HTML 冻结向量继续精确匹配。

## 状态归属与验收边界

`TerrainDebugFlyer` 是离线地图工作台的观察角色，使用原 worker 精灵和独立原生 Prefab，不创建正式 YYGC 游戏实体、经济、AI、波次、网络或存档。地图蓝图只用于初始化表现；退出、重复生成和过期候选释放对应页面。正式玩家仍通过原来的权威输入与碰撞路径。

本批 Editor 地图检查 11/11 通过（含 24 组冻结生成向量）；取消加载保护修改后，受影响的表现检查 3/3 复验。实际 Play 验证 22/22，覆盖直接出生、原始蓝图、真实 WASD 输入、二维穿墙、斜向等速、输入阻塞、镜头调节、同种子重建、参数自动生成、最新候选和 8 房间跳转。原生场景与角色 Prefab 已保存、关闭重开并运行，中文面板截图已核对。字体使用运行时加载的系统字体，避免序列化 UIFont 在 IMGUI 中缺少字面数据导致重复文字。

Mono 构建与独立进程结果、输入哈希和清理盘点见 [本批证据](evidence/terrain-debug-bootstrap-2026-09-17.json)。本批不计为正式多人验收、IL2CPP 或前台帧率验收；协议 9／存档 v4、YYGC 锁定与 M5 状态不变。
