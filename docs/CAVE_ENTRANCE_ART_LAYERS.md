# 复用现有三层背景的地表衔接

日期：2026-10-03。发布分支：`ft-20261003-existing-cave-layers`；Local 原分支保留并行评估提交。

用户要求以已有美术表现和绘制层级衔接天空与矿洞，明确纠正不应重绘已经存在的三层点缀。本批复用 BackgroundContourBaker、BackgroundPageBaker、SourceMaskAtlas 及原 modifier 配置，原三层 RGBA 输出逐字节保持。没有新背景 PNG 接入或原素材覆盖。

正式渲染退出环境包络遮罩，不再按入口深度改变 Alpha、底色或基础亮度。素材原有的透明边缘和设备局部光照保留；不是把同一段渐变搬到 CPU。地图、碰撞、游戏协议、存档格式和 YYGC 锁定依赖均不因本批改变。

## 最终表现与复用方式

天空铺满视口，远山／云层保持原航程表现。原近、中、远三层各自使用独立材质和绘制对象，以原岩片形状形成遮挡与空间层次。最远基础后壁接住地下空隙，深井不再整列露天空；天空不会逐渐变成棕黑色。

基础后壁只是原地下底板的覆盖改造：沿冻结岩缘定位，优先采用已有岩片的上缘；没有点缀轮廓的深处使用同一个 BackgroundMaskAtlas 拼接封底。它复用原地下底板的配色范围和噪声尺度，不增加美术图集，不应用高度渐隐或 1:1 包络坡度。左右相邻片段共用岩缘定位端点，像素覆盖由已有图集掩码决定。该定位是视觉近似，不等同于完整天空可见性或洞穴光照模拟。

采挖继续只更新当前前景与光场。三层点缀及基础后壁只读初始 BackgroundBakeDescriptor，不随逐格采挖重排；挖空深井不会让地下重新变成天空。

默认从后向前：天空 −110、远景 −107、完整基础后壁 −100、原远层 −99、原中层 −98、原近层 −97、完整前景外轮廓 −96、AnyRuleD 前景 0。三层原 Near／Middle／Deep 开关继续有效；关闭全部点缀仍保留完整地下后壁。

## 配置与代码

现有 CaveBackgroundStyle 增加分层 Shader 引用、绘制排序、基础后壁的岩缘定位范围及封底深度、固定前景／背景亮度。三个点缀层的作者资源、轮廓生成器、密度、范围、modifier 和 MiddleSoftness 均沿用。旧 EntranceDepth、UndergroundAmbient、WeatheredDepth 隐藏并退出正式表现，只保留旧诊断字段。

调用链：RandomLevelEntry → TerrainPreview → CaveVisualSource → CaveBackgroundCache → CaveEntranceBackdrop.Bake → CaveBackgroundPage → CaveBackgroundLayer.shader。CaveStrata.shader 处理前景、完整外轮廓及独立地下预览固定底板，不再实现入口渐变。背景仍沿用已有串行后台任务、可见页缓存及每帧最多上传一页的节奏；没有新增通用调度器。背景表现参数进入 VisualIdentity，防止不同视觉输入混用。

每个 256×256 背景页由三张原点缀纹理加一张基础后壁纹理构成；相对原三张纹理每页增加 256 KiB。通常缓存 24 页为 24 MiB，全图 60 页为 60 MiB 背景贴图。增加每页的独立绘制对象，容量有界不代表前台性能已通过。

## 未采用的素材草稿

最初误扩大范围，绘制了 FarWall、MiddleRock、NearRock 三张 512×128 样板。用户指出已有三层后，全部退出资源引用，并连同源脚本、来源记录和专属配置代码保留到 `artifacts/待清理/20261003-entrance-art-unused/`。清单记录原绝对路径、归档路径、字节数和处理条件；没有永久删除，释放空间计 0。草稿不提交或发布，也不作为本批美术成果。

## 验证及边界

最终机器摘要保存到 `docs/evidence/cave-entrance-art-20261003.json`。专项覆盖原三层完整 RGBA 对照、窄井、宽谷、空图、两轴分页一致性、地图边界、冻结背景、正式坐标缩放、四层顺序、天空颜色变化不污染地下像素及关闭点缀层后的完整覆盖。Editor、正式 Play、Mono 与独立进程结果在完成后补齐。

扩大检查发现旧背景恢复用例假设一次爆破立刻清空岩格，而当前网格按耐久扣伤。夹具现按正式 BombDamage 计算有限次爆破，保留原清空、初始参考不可变、存档恢复和损坏输入拒绝断言；没有改玩法规则或重生成冻结证据。

任务开始时 main 已有其他任务的未提交改动，原始文件与哈希备份到 `artifacts/art-layer-entrance-20261003/before`。仅提交本批渲染、匹配测试和文档；角色运动、WorldSession 作者改动、存档规则及旧环境包络候选不混入提交。并行会话新增的氧气评估提交单独保护，不随本批推送。

IL2CPP、双机器与前台性能不在已验范围；不引用历史数字作为本批验收。

## 本批最终结果

- Unity 6000.4.9f1 导入／Editor 编译、真实 GPU 专项 **36/36** 通过，包含原三层 RGBA 对照。最终报告：`artifacts/art-layer-entrance-20261003/editor-010615.json`。
- 将仅属于本任务的源码从独立 Git 树导出，排除其他会话未提交改动；Core／Runtime／View／Entry／Editor／Tests 六个程序集编译 **6/6**，C# 9 输入来自锁定 Local 引用。结果：`candidate-compile.json`。此检查不替代 Unity 或 Player。
- 正式 Mono `player-mono-existing-r1/DarkNights.exe` 构建成功，独立进程正常网络 **31/31**，覆盖航程、地表 Ready、着陆、第三人晚加入和重连。Editor Bootstrap 的真实快速局 Ready／Landed 与 9 个背景页已确认，截图 `editor-main-quick.png`，结束后恢复原 Bootstrap 并退出 Play。
- **弱网未全通过**：`200 ms RTT + 5% loss + 25 ms jitter` 的三进程检查在着陆后第三人晚加入超时，已完成的 14 个断言通过；双进程复测在首次客户端加入超时，没有完成断言。失败时地图数据未就绪，保留全部日志和失败报告，不宣称弱网已验收，也不因本视觉任务扩大修改网络代码。
- 现有 **577 张 PNG** 与任务基线逐文件 SHA-256 相同；没有新图接入。三张未采用草稿集中保留，不永久删除。独立候选源码副本、编译中间输出按清单集中保留到 `artifacts/待清理/20261003-existing-layer-checks/`，归档释放空间计 0。

首次 Player 构建发现纹理内容哈希接口只在 Editor 可用，导致 Player 编译失败；已移除该接口并复用现有视觉身份参数，最终 Mono 成功。首次失败报告保留在 `build.json`，最终结果为 `build-existing-r1.json`。没有用旧素材草稿的通过数字替代最终复用方案。

Player 与 Editor 使用 Local 当时的其他未提交输入；本批六程序集独立候选编译另行验证提交源码没有依赖这些输入。其他会话工作仍保留，不把 Local 的全部改动一起提交。
