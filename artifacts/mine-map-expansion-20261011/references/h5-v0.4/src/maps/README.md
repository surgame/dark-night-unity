# 地图定义与扩展

每张地图独占一个 `.js` 文件，以 `window.DN.registerLayout({...})` 注册。这里只写固定布局、内容与验证路线；运动、碰撞、光照、挖掘和梯子规则在共用模块。`01–04` 为旧房间洞网，`05–07` 为旧自然洞穴，`08–10` 为本轮三种岔路方案。旧定义保留作对照。

## 新增或修改地图

1. 复制 `08-folded.js`、`09-shaft-loop.js` 或 `10-vein-maze.js`，改成新的排序文件名，例如 `11-my-cave.js`。
2. 修改唯一 `id`、`number`、`title`、`tag`、`description`；编辑 `rooms`、`paths`、`shafts`、`clues` 和对应 `testPlan`。
3. 在实验目录运行 `node build.mjs`。生成器自动按文件名发现 `src/maps/*.js`，更新地图选择器与两个 HTML 入口，无需修改 UI 或手写脚本清单。重复 ID、未知生成器、洞室引用错误、非法格点和无效井长会报错。
4. 双击根目录 `dev.html` 试玩。修改已存在的地图文件后刷新即可；新增文件后先重建，生成新的脚本标签。分享或离线单文件测试使用 `dist/index.html`，修改源文件后须重建。
5. 运行 `node check-concepts.mjs`，对带 `testPlan` 的洞网检查实际运动与梯具、封岩、矿／巢穴、线索。这是无界面的逻辑核对；完成后仍要手动试玩手电可读性和路线手感。

`dev.html` 使用普通本地 `<script src>` 和 CSS；没有 fetch、ES Module、服务器、CDN 或包依赖。所有游戏状态只在本局内存。不要直接编辑 `dist/index.html` 或旧版本单文件。

## `kind:'cave'` 定义合同

世界为 132×78 格，每格 24 px，左上为原点，向下为正。定义中的坐标单位为格，`floor` 和通路点的第二项是角色脚底地面；运行时生成器才转换为像素。所有点使用整数。

| 字段 | 用途与注意事项 |
| --- | --- |
| `id / number / title / tag / description / seed` | 稳定地图身份、显示顺序文字、名称、比较重点、描述和确定性外形变化；`seed` 不随机改变拓扑 |
| `rooms` | `{id,name,x,floor,radius?,height?,surface?,nest?}`；洞室横向中心和地面，默认半径4格、拱高5格；`camp` 保持 x10、floor16，以匹配固定出生与飞船 |
| `paths` | `{id,from,to,points:[[x,floor],...],sealX?,sealWidth?}`；`from/to` 必须引用洞室。每一段横向移动，每列落差不超过2格；大落差用竖井拆段 |
| `shafts` | `{id,from,to,x,top,bottom,side?,lower?,upper?,sealedTop?}`；`from` 为下洞，`to` 为上洞；井长2–14格。`side:-1` 为梯顶左侧岩台，默认右侧。井口没有预搭梯子 |
| `lower / upper` | 竖井两端步行接道的格点数组；`lower` 从下洞到井底，`upper` 从梯顶岩台到上洞。默认根据洞室中心计算；复杂折返应显式提供 |
| `sealX / sealedTop` | 封住横道或井口的可挖裂纹岩；覆盖真实碰撞／光照，挖开前不能穿过。普通基岩仍不可挖 |
| `surface / nest` | 指定洞室增加表面矿或墙后巢穴。共用内容器在洞室旁布置局部裂纹岩和更多埋矿；巢穴只在覆盖格拆开后一次生成，并先给活动前兆 |
| `clues` | `{name,type,x,y,dx?,dy?,note}`；`y` 为痕迹位置，不是脚底。`type` 可用 `glint / vein / wind / echo / shell`，走近并照到后按 E 观察，记入沿途记事与导出记录 |

洞室不能靠名称保证可达。通路与洞室合流处要留足头部空间，斜坡须在大洞边缘前接近该洞的地面高度，避免拱厅把坡脚挖成高台；同层竖井口留出至少三格的跳越空间。不要让不同楼层的斜坡、洞室或竖井交叠出悬空岩片。生成器和实际碰撞共用 tile 数据，不通过绘制隐藏通行问题。

## 验证路线随地图保存

`testPlan` 可选；08–10 已提供主路和第二种回程。新图复制它时同步改通路／竖井 ID：

```js
testPlan: {
  primary: ['entry', 'first-fork', {shaft:'middle-shaft'}, 'deep-route'],
  alternateReturn: [
    {path:'short-cut', reverse:true, excavate:true},
    {shaft:'side-shaft', up:true},
    {path:'entry', reverse:true}
  ]
}
```

字符串为按格点方向行走；`{path,reverse:true}` 为反向行走；`{shaft}` 从上口自建梯并下井，`{shaft,up:true}` 从下口攀爬，缺梯时实际搭建；`excavate:true` 先拆该通路封岩再测试。主路核对使用开局四套梯具，走到深部后原路返回；第二回程也从入口完整下探后执行。测试不会自动解锁喷气辅助或瞬移到后续节点，单段支路的独立起点核对另行列出。

没有 `testPlan` 的地图仍会由 `build.mjs` 核对生成、出生点和结构合同，但不据此声称路线已通过。地图源码、生成器、游戏、界面及现有测试共享 `source-files.mjs` 中的加载顺序。
