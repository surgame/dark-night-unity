# AnyRuleD 接入评估

2026-09-17，应用户要求在合并前评估。审查输入：游戏 `aa007a8`；主框架 `12b253c`；AnyRuleD 三个独立包 `aa450a7` 加两份网络补丁。只读核验锁文件中的 442 个包文件，哈希不匹配 0 项。本次不修改游戏实现、人工资产或 YYGC 工作区，不新建 Player，不将历史测试写成本次重跑。

同日用户要求快速修复后，下述跨页缺陷已修复：隐藏离开条带后通过原生 `ShowRegion` 确认完整目标区域，恢复被 halo 连带隐藏的页，页大小取自地图描述符。新增实际预览回归覆盖 12 个位置（四方向、往返、对角及地图边缘），每个位置检查相机覆盖页可见及静止 120 次更新不重建；地图 Editor 测试 11/11、架构守卫 341 文件／12 自测／0 错误通过。见 [修复证据](evidence/terrain-visibility-fix-2026-09-17.json)。仅修改预览和测试，无 YYGC／资产变更，未新建 Player。

## 结论

确实使用了 YYGC 的 AnyRuleD 原生 DualGrid，规则、坐标、状态所有权和渲染后端的主要接法正确；当前仍是独立地图基座，不能称为正式联机可破坏地形已完整接入。发现的页面可见性调用缺陷已按上方跟进修复，下文保留最初复现依据。

## 已修复：P2，向左／向下跨页可隐藏仍在视野内的页

位置：`Game/Assets/DarkNights/Scripts/View/Terrain/TerrainPreview.cs` 的 `UpdateVisible`、`ChangeDifference` 和 `Change`（62–86 行）。

游戏在逻辑区域上求进入／离开条带，再分别调用 `ShowRegion`／`HideRegion`。但锁定框架 `Runtime/Unity/Facade/MapOptions.cs` 的 `MapRegions.Pages` 会把逻辑区域最小坐标减 1 后再除以页大小，以覆盖 DualGrid 的负向视觉依赖。`ARDMapController.HideRegion` 直接隐藏所得整页，不对重叠逻辑区域做引用计数。因此，逻辑条带互不重叠不代表实际操作的页面互不重叠。

本次按框架公式执行的一维页集合复现（页宽 16，Y 范围不变）：

| 操作 | 逻辑 X 范围（半开） | 页列 |
|---|---|---|
| 旧视野 | [96,144) | 5、6、7、8 |
| 向左移动后的目标 | [80,128) | 4、5、6、7 |
| 隐藏离开条带 | [128,144) | 7、8 |
| 显示进入条带 | [80,96) | 4、5 |
| 实际剩余 | — | 4、5、6，缺少 7 |

第 7 页覆盖 X=[112,128)，可包含新相机视野内的地形。Y 轴向下平移有同样问题。现有 `TerrainPresentationTests` 检查静止及局部破坏；历史相机探针只向右移动并统计构建数，没有断言四方向移动后的完整页面集合，因此历史通过结果不能排除此缺陷。这是源码和页集合复现结论，本次未执行 Unity 画面复现。

建议在视觉页集合上求差并调用适合的页级接口，或先实现正确的完整目标区域显示，再按测量优化。补充左／右／上／下及对角移动、边界裁剪的实际页面可见性回归，不能只检查构建计数。此次按评估范围记录，未代改实现。

## 已核实的正确接入

- `TerrainTestAssets` 创建原生 TerrainDefinition、AnyRuleD、TileVisualSet 和制作目录，通过 `RuleCatalogBuild.CompileToNewAssets` 生成运行目录及 ARDMapDefinition，没有自建并行规则求解器。
- NW/NE/SW/SE 位序为 1/2/4/8；逻辑格 `(x,y)` 转为 `(x,-y)`，对应视觉格 `(x,-y-1)`，与框架四角采样合同一致。原型明确使用 NW–SE 对角，9 连通、6 分离；图集切片转换纹理 Y，不反转规则位序。
- 单材料对空格使用 This/Empty 的精确规则、`cell` 通道、ExactCell 和 CanonicalMaterialEdgesV1；混合材料不会误匹配这些二值规则，而由框架 canonical topology 与各材料的 TileableFill 合成。拼接正确不等于有专用手绘材质过渡，本次也未对所有像素组合做画面验收。
- `TerrainPreview` 实际使用 ARDMapController；默认 SpritePages 后端执行页面求解与绘制。逻辑块 32×32、视觉页 16×16，不为每格创建对象或网络实体。
- `TerrainMapAuthority` 以活跃 ObjectSessionContext 限制唯一 ARDMap 权威写入，采用局部事务；蓝图只用于初始化，快照用于只读消费。网络复用 AnyRuleD 的 MapInterestService／ChunkReplicaStateMachine，内容版本与权限版本共同控制静态发布。
- 图集使用 Point、无 mipmap、无压缩、sRGB，项目继续保持 Linear。人工资产不会在普通导入时由初建工具覆盖。

## 接线与验证边界

1. 离线渲染读取 TerrainBlueprintSource；联机测试读取网络副本。生产用的“网络副本更新 → 本地渲染页”尚未接通，不能把两组分别通过的证据称为完整联机画面验收。
2. 预览未传稳定 WorldIdentity，框架每次创建生成新 GUID；变体哈希包含世界身份。因此同一导出地图重开时纹理变体不保证相同。生成种子用于生成逻辑，当前显示选样仍使用 MapOptions 默认种子 42；正式跨端／恢复接线应统一权威描述符的身份和选样种子，不应让各端各自生成。
3. 正式主角采矿、工具／距离／冷却／Ready／营地权限、耐久与奖励事务、碰撞及跨模块存档仍待接线。DestroyTrusted 是可信宿主 API，不能直接把任意客户端请求作为授权调用。
4. 既有地图证据是 Editor 10/10、独立 Mono 正常／弱网各 10/10；Bootstrap 修复另有 Editor 2/2、Mono 启动 6/6、会话 13/13。均保留原构建身份。本次仅源码、合同、依赖哈希、页集合与 Git 差异核验；完整游戏回归、前台性能、IL2CPP、双机器及 M5 未因此完成。

## 合并与保留范围

本切片从 `codex/dualgrid-map-generator` 合回游戏仓库 `main`，不操作用户另一个 YYGC master／AnyRule 工作区及其分支。当前七个未跟踪工具已在地图清理账本中列为保留探针，不纳入成果提交、不删除。历史 detached worktree 的目录已经不存在，只清理失效 Git 登记；本次没有可删除的独立开发 worktree 目录。其他历史分支不属于本次切片，保持原样。
