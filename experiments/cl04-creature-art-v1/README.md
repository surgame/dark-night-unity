# CL-04 生物遭遇 · 首批像素素材

2026-10-15，用户授权先为CL-04准备若干符合现有画风的素材。本目录保存独立美术候选及唯一重建来源；模块状态由[DEVELOPMENT](../../docs/DEVELOPMENT.md#stage-cl)维护。尚未导入Unity，不代表生物逻辑、动画控制器、战斗或事件验收完成。

## 查看与编辑

- [总览图](preview/contact-sheet.png)、[状态姿势GIF](preview/states.gif)、[原生比例对照](preview/scale-scene-1x.png)、[4倍比例对照](preview/scale-scene-4x.png)。比例图是离线摆放，使用原始12×12角色；不是游戏运行截图，不模拟光照。
- [本地审阅页](preview/index.html)包含逐帧查看及透明边缘背景切换，可离线打开；浏览器交互未做自动验收，PNG／GIF为已检查的主交付。
- `editable/`包含5份逐帧、命名Tag的原生Aseprite文件，单一`Pixels`图层，每帧像素可直接编辑。`native/`是25张RGBA PNG，`sheets/`为按状态顺序横排的分组图集及全素材图集。

| 素材 | 原生画布 | 帧数 | 表现内容 |
| --- | --- | --- | --- |
| [拟态矿虫](editable/ore-mimic.aseprite) | 32×32 | 8 | 伪装、前兆、显形、两帧爬行、攻击姿势、退缩、倒伏；暗岩壳与赭金矿纹，显形后保留同一外壳 |
| [伏击幼虫](editable/ambush-grub.aseprite) | 32×24 | 8 | 两帧休眠、警觉、两帧爬行、攻击姿势、退缩、倒伏；暖褐甲节、小型口器与触须 |
| [虫卵簇](editable/egg-clutch.aseprite) | 24×24 | 4 | 完整、局部蠕动、开裂、空壳；有连续变化线索 |
| [巢穴口](editable/burrow.aseprite) | 32×24 | 2 | 空巢、占据；洞口外形不变，细小眼点仅作造型提案 |
| [环境线索](editable/clues.aseprite) | 16×16 | 3 | 抓痕、黏液痕、蜕壳；蜕壳不据此成为奖励或可拾取物 |

共25张原生状态帧、5份可编辑Aseprite、5张分组图集。`all-assets-32-cell.png`按表格与帧顺序进入32×32格，8列4行，末7格透明；不同规格帧左上对齐。接入优先使用独立分组图集或PNG，避免从总图集猜测原点。

## 制作方式与来源

最终素材由`authoring/`中的整数坐标轮廓、色块、矿纹和步态绘制，固定24色，alpha仅0或255。不由大图缩小、像素滤镜或模型网格生成，也没有从现有角色或矿石贴图裁剪出怪物。

现有原始矿工、僵尸、铁矿和洞穴矿层预览只用于画风、比例及配色参考。原件路径及SHA-256见[manifest.json](manifest.json)；引用原件保持原位。`source/style-reference.png`是这些现有内容的对照排版，不是AI输出。岩层比例图只复用现有`cave-rock.png`的一小条作为背景，未改变原素材。

尝试按项目指定的imagegen-codex-provider调用`custom` provider（`https://sub.1qq.xyz/v1`）、`gpt-image-2.5`；适配器发现`auth.json`没有`OPENAI_API_KEY`，在API请求前退出，没有返回生图或设计参考。提示词及结果留在`source/concept-prompt.txt`和`source/generation-attempt.json`；本批成果应标注为原生像素绘制，不能标为AI生图。

## 后续Unity接入合同

- 像素密度沿现有PPU100，16像素为一地形格；不得为匹配画布尺寸单独拉伸。拟态矿虫是较大的矿石伏击造型，透明画布不等同于身体占地。
- 建议Sprite／RGBA、Point、Clamp、无mipmap、无压缩、sRGB开启；项目继续Linear。亮眼和高光是作者颜色，不自带灯光、Bloom或伤害标记。
- `manifest.json`记录每帧文件、原生尺寸、非透明边界、颜色数、图集切片矩形及锚点。切片矩形采用左上原点；Unity切片Y应转换为`图集高度 - y - h`。
- 所有角色朝右，后续左向可镜像；没有独立左向帧。PNG无碰撞轮廓，不能自动生成碰撞并据此结算攻击。
- 同组固定锚点，不逐帧裁切或重新居中。以下锚点采用画布边界坐标，左上原点；Unity的归一化Pivot为`(x / width, 1 - y / height)`。

| 分组 | 固定锚点 px | Unity Pivot |
| --- | --- | --- |
| 拟态矿虫 | (16,30) | (0.5,0.0625) |
| 伏击幼虫 | (16,23) | (0.5,0.0416667) |
| 虫卵簇 | (12,23) | (0.5,0.0416667) |
| 巢穴口 | (16,23) | (0.5,0.0416667) |
| 环境线索 | (8,15) | (0.5,0.0625) |

帧名仅表达美术意图。Aseprite每帧160ms、GIF每帧180ms均是审阅默认值，不定义实际前兆窗口、伤害帧、追击或恢复规则。正式接入后需独立确认轮廓／碰撞、原生Prefab绑定、Definition及Addressables；在单一Local通道导入，由Unity生成新meta，不能手工分配GUID。

## 重建与手工编辑保护

环境：Python 3.13、Pillow 12.0.0、Aseprite 1.3.18.5。仓库根目录执行：

```powershell
python experiments/cl04-creature-art-v1/authoring/build.py --aseprite 'D:/Program Files (x86)/Steam/steamapps/common/Aseprite/Aseprite.exe'
```

重建先检查manifest登记的所有输出SHA-256；已有PNG、预览或Aseprite被手工修改时拒绝覆盖。手工编辑后的文件即为作者维护成果，不应改写哈希强行绕过保护。需要对比新配方时通过`--output`指定新的空目录，保留原成果。`editable/import-native.lua`由构建器生成，只供构建器在哈希核对后调用，不单独运行。普通Unity导入／构建不会执行本配方，也不会发起生图。

## 本批核对与待审内容

最终核对覆盖25张PNG的尺寸、固定调色板、二值alpha、四边至少1像素透明余量、分组图集逐帧像素一致性；5份Aseprite分别重导出，与PNG图集像素及25个Tag名称一致。最终身份与回执见[美术核对记录](../../artifacts/cl04-art-20261015/final/review.json)。检查范围仅美术文件，不计Unity编译或玩法测试。

需用户审阅造型是否符合基本画风、拟态显形与幼虫口器是否足够可辨。真实镜头、编辑保存重开、实际暗处／手电受光、背景融合和前兆可读性尚待接入后验证，Q-06仍保留；尚未选定首个正式遭遇、未决定数值／声音／伤害／奖励／噪声／同步和存档。CL-02已有工作、正式场景、规则、YYGC和用户存档均不属于本批写入范围。
