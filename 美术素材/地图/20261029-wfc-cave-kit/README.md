# WFC 洞穴美术包

本包已制作真实 PNG 美术源、WFC 生成地图、透明前景分块及可重建装配器。当前为独立美术候选，供视觉评审与后续接入；正式 Unity 资源与碰撞未修改。

## 本轮调整

- 灰绿手绘大岩块、土黄色可见路面、冷青色深洞后壁、垂根与暖色矿簇。
- 用户指出道路太规整、太窄后，重新生成 `sources/floor-wide-v2.png`；宽路面前后沿不等宽，装配时再以连续低频变化调整可见厚度。旧版 `floor.png` 保留。
- 用户圈出的尖锐坡折改为沿前后坡段各展开40%的二次圆弧过渡，在完整共享轮廓上统一处理。原WFC socket约束保留在逻辑层，最终可见边界以导出的平滑轮廓与同源分块为准；这会轻微移动原折点，不改变游戏权威坡形或碰撞。
- 初版路面贴层中断来自线性轮廓与双线性符号查询不一致，现改为使用同一实际绘制轮廓确定朝向；初版截图保留在本聊天的 visualizations 目录。

## 内容与使用

| 路径 | 内容 |
| --- | --- |
| `sources/` | 5 张现用原生美术源、1 张旧地面源及只读原角色比例图；PNG 原字节保留 |
| `sources-manifest.json`、`prompts.json` | 现用源尺寸、alpha、SHA-256、完整生成提示与来源 |
| `geometry-snapshot/` | 对白模聊天当时 `catalog.js`、`plan.js`、`solver.js` 的冻结只读快照；SHA-256 见验证回执 |
| `renderer/` | 独立美术装配与预览源；世界坐标材质、地表带、悬顶带、共享平滑轮廓、矿簇和比例人物 |
| `exports/maps/` | 3 个种子的布局 JSON、5120×3072 整图、同尺寸透明前景、1536×864 局部图与分块清单 |
| `exports/chunks/` | 每图 60 张 512×512 RGBA 前景分块，按对应 `*-chunks.json` 的像素坐标放置 |
| `exports/socket-catalog.json` | 256 个角点组合、16 类拓扑、N/E/S/W socket、顶点与图集矩形 |
| `exports/geometry-alpha-atlas.png` | 4096×4096 形状遮罩图集，16×16 布局，每项256像素 |
| `exports/geometry-painted-reference.png` | 同版岩体形状的零相位美术参考图集 |
| `verification.json` | 同批地图接边、交点、导出重组、浏览器操作、响应式及来源身份 |

地图范围对应现行 `TerrainGenerationSettings` 的 320×192 格与 `PlayableTerrain.CellPixels=16`，整图为5120×3072像素。每个 WFC 模块对应4×4地形格，布局为80×48模块；洞形与洞室位置由独立白模生成，并非复制现行作者地图。角色仅作约29游戏像素高的视觉比例标记，不运行角色物理或通行判断。

## 拼接合同

角点顺序为 TL/TR/BR/BL，取值 `[-3,-1,1,3]`，正数为岩体，负数为空气。边顺序 N/E/S/W，相邻边的有序角点对必须完全一致。WFC 实际执行加权最小熵选择及约束传播，有限失败后报错；不以预制结果冒充求解。

材质由世界坐标采样；岩面使用交叠带连续化，地表和悬顶使用各自源图。圆化在已经连接的整条轮廓上统一计算，转折向相邻两段各延伸40%，以旧折点为二次曲线控制点，采样12个间隔。不是各块分别倒角后强行拼接；最终跨块轮廓和像素由同一次整体装配产生。最初强行穿过每个旧折点的三次拟合在短坡产生局部陡折，已撤出，截图保留。形状遮罩、绘制轮廓和图集参考的职责分开，不能把平滑美术当成现有游戏碰撞。

**直接复用已有地图**：在冷色后壁上，按 `maps/<seed>-chunks.json` 平铺其60张RGBA前景图，顺序不影响结果；原生精度、无重叠。这些是真实导出的地图素材，已逐字节验证分块解码并重组后与整张前景一致。

**生成新地图**：使用冻结的 socket 目录与求解器，再运行本包美术装配器并烘焙新分块。可复用源图和装配规则，不需要逐张重画完整地图。

**单独使用形状图集**：alpha图集用于形状，岩面以世界UV采样，地表／悬顶消费完整共享轮廓。`geometry-painted-reference.png` 只是零相位展示，不能当成任意摆放即可无缝的完整绘景tile；仅有一个“出口点相同”不足以保证纹理、路面和根须连续。本包采用源图＋约束几何＋连续装配方式解决这一点。

## 重建

本机 Node 使用 `build.cjs` 指定的 bundled sharp／Playwright 路径。按顺序运行：

```powershell
node build.cjs
python "C:/Users/Jobscn/.codex/plugins/cache/openai-bundled/visualize/1.0.57/skills/visualize/scripts/render.py" "C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1885a-d455-7a51-ad10-5e3b12d43271/painted-wfc-cave.html" "C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1885a-d455-7a51-ad10-5e3b12d43271/browser-preview.html" --force
node export-and-check.cjs
```

聊天预览仅压缩编码为WebP，保留原生尺寸；正式PNG导出加载原生PNG，未用聊天压缩图充当生产源。脚本不启动Unity，不访问游戏存档，也不读取网络数据。

## 验证与后续

每图核对7552对原始socket相邻边、共享角点与轮廓闭合，并记录圆化后的折点位移；实际结果见 `verification.json`。三图共180张透明分块做无混合的RGBA逐像素重组。第一次用图像合成接口进行重组时未满足字节一致，已保留 `exports/first-reassembly-failure.json`，现采用分块原始RGBA按位置复制核对，避免把重采样／混合差异误判为素材接缝。

完成的是独立素材、WFC装配和浏览器预览。素材重复感、局部立面纹理拉伸、垂根密度及美术偏好仍可继续评审；真实Unity采样、Linear光照、碰撞／采挖新边缘、存档、联机与Player均未验收。此包不替代16像素采掘tile合同，不修改正式生成器；CL阶段保持进行中。
