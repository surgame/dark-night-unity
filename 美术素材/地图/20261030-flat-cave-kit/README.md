# 清爽手绘洞穴美术包

本包把用户认可的[原主角地图概念](../20261030-hero-led-cave-concepts/README.md)制作成独立素材与可重建WFC装配候选。当前候选输出在`exports-v3/`，使用有可见宽度的地表顶面、厚岩壁、不规则悬顶、冷蓝远景及嵌岩矿斑。原人物只作比例参考，没有改绘原Spine或原PNG。

执行状态与人工评审唯一记录于[DEVELOPMENT](../../../docs/DEVELOPMENT.md)。美术装配和静态核对不代表正式Unity接入、碰撞、通行、采集或照明通过。

## 当前内容

| 路径 | 内容与使用边界 |
| --- | --- |
| `sources/rock.png` | 1254×1254岩面原图，实色大岩块及简洁描边 |
| `sources/floor-shelf.png` | 1536×1024透明地表，宽顶面、前沿岩块与厚岩壁 |
| `sources/ceiling-edge.png` | 1536×1024透明悬顶，不规则肩部与短垂岩 |
| `sources/backdrop.png` | 1536×1024蓝色远景；采掘新开口另有稳定近后壁 |
| `sources/ore.png` | 1254×1254透明矿斑，四象限取样，不承担矿种／储量状态 |
| `geometry-snapshot/` | 旧包冻结的catalog／plan／solver只读副本，来源哈希见`geometry-provenance.json` |
| `renderer/` | 当前装配源，实际入口依赖列表见`build.cjs`；旧`surfaces.js`仅保留失败路径，不再加载 |
| `exports-v3/maps/` | 种子271／902／1643的5120×3072整图、同尺寸透明前景、1536×864局部、布局及分块清单 |
| `exports-v3/chunks/` | 每图60张512×512透明前景分块，共180张，按清单坐标放置 |
| `exports-v3/geometry-alpha-atlas.png` | 2048×2048形状遮罩，256项、每项128×128 |
| `exports-v3/geometry-painted-reference.png` | 零相位材质形状参考，不保证任意独立拼贴的纹理相位 |
| `exports-v3/socket-catalog.json` | 角点、socket、形状多边形及图集矩形 |
| `exports-v3/mining/` | 作者局部样板的三个采掘阶段、照明示意与透明前景；不是正式WFC关卡或游戏模拟 |

完整生成提示分别在`prompts.json`和`surface-prompts.json`，制作用途、尺寸请求、透明／共边与精度要求见`brief.json`。`sources-manifest.json`记录原生尺寸、alpha范围和SHA-256；原生生成文件保持原字节。原角色和旧包局部对照仅引用原位置，不重复保存作者源。

## 拼接与尺度

三张WFC图均为80×48模块，一个模块对应4×4游戏格，完整范围对应320×192格。导出按每游戏格16像素得到5120×3072。渲染内部每模块128单位，整图输出倍率为0.5；这些内部单位不能直接当作Unity世界坐标。

共用冻结WFC边界和完整轮廓，沿相邻坡段各40%作共享二次圆化，再进行材质采样和分块。圆化后的美术轮廓与原始socket几何有偏移，不能视为当前游戏权威碰撞。新地图需要重跑装配，不应随意混用不同地图的已烘焙分块。

岩面固定世界UV。地表／悬顶采用沿轮廓实际弧长的取样和切线／法线映射；直坡横纵均按0.4等比，不再按水平X逐列剪切，也不再附加纵向8%波动。急弯中的曲面贴层仍会弯曲，不能把直坡比例检查扩大成“任何曲率均无形变”。

图层衔接沿原图深色岩缝选取不透明裁切边界；岩面周期源使用不透明镜像寻址，避免先前最小误差拼接尝试的硬色块断点，代价是仍能辨认镜像重复。贴层保持不透明，不再随坡度降低alpha或用不同岩块的宽透明渐变隐藏接缝。原始生成透明光晕仅在渲染准备阶段做alpha-test，源PNG不改写。

## 已保留的问题与证据

- `exports/`和`verification.json`是首轮平面填充版本，用户认为未达到地面质感要求；不作为当前美术接受证据。
- `revisions/flat-fill-v1/`保留首轮源码／回执；`revisions/column-shear-v2/`保留恢复顶面后仍有逐列剪切与渐变重影的源码。
- `exports-v2/`及`verification-v2.json`保留中断前输出，不能代表最后一次源码修正；`revisions/resume-20261030/`冻结续接时的源码、回执和局部画面。被否定的不透明拼接尝试及截图继续保留。
- `review/`保存用户指出地面质感、拉扯和发糊的截图，以及同一输入、同一镜头的`mapping-before.png`／`mapping-after.png`。
- `mapping-verification.json`记录0／15／30／45／60／65／70度直坡的局部比例、正交性、准备纹理的中间alpha像素数量，以及实际绘制的60／65／70度岩面不透明像素。它不证明急弯、物理碰撞或Unity效果。
- `verification-v3.json`记录当前整图接边、分块RGBA重组、开挖局部变化、预览控件与窄屏布局核对；若存在失败，原记录保留在对应导出目录。
- `continuation-verification.json`记录本次续接的输出／源图／渲染源码哈希复核、原生评审图与最终局部逐像素一致、重建脚本身份及Node／sharp／Playwright版本。v3共209项、208,124,533字节；续接输入checkpoint约1.67 MiB，预览缓存约0.55 MiB，无Unity中间产物。

当前仍需人工评审镜像材质重复感、不同尺寸岩块衔接、急弯与陡壁上的贴层轮廓；陡壁处贴层结束仍可能显得突然。矿窝切口仅为固定格子裁切，工具姿势和局部照明仅示意，不结算采矿收益、不模拟碰撞或队友光照依赖。

## 重建

在本目录运行：

```powershell
node build.cjs
python "C:/Users/Jobscn/.codex/plugins/cache/openai-bundled/visualize/1.0.57/skills/visualize/scripts/render.py" "C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1891b-2542-7ea1-a69e-12cf28b0c785/flat-wfc-cave.html" "C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1891b-2542-7ea1-a69e-12cf28b0c785/flat-wfc-browser.html" --force
node inspect-mapping.cjs
node export.cjs
```

预览使用保留原尺寸的WebP编码；整图与分块使用原生PNG重新装配。当前脚本输出`exports-v3/`，首轮`exports/`和中断前`exports-v2/`保持。只改采掘样板／控件且不影响地图材质时，`node export.cjs --sample-only`可复用本版已核对地图；材质、轮廓或贴层改变后需要完整重导。

脚本依赖本机已有Node、sharp与Playwright，以及旧包保留的原角色／对照局部。均不启动Unity、不访问玩家存档、不修改正式地图和其他聊天源码。
