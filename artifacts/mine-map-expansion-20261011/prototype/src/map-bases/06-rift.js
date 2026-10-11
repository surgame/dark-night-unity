// 固定地图定义；坐标以格为单位。运行状态由地图实例独立持有。
window.DN.registerLayout({
  "id": "rift",
  "number": "06",
  "title": "裂谷竖井",
  "tag": "竖向发现 / 攀高选择",
  "natural": true,
  "description": "收窄岩缝与较大的裂谷交替，矿台藏在高处。竖井里没有梯子，要自己选择搭在哪里。",
  "seed": 43,
  "profile": [
    [3,16],
    [24,16],
    [34,19],
    [43,32],
    [52,32],
    [65,48],
    [78,48],
    [91,63],
    [110,63],
    [128,68]
  ],
  "chambers": [31,49,71,96,121],
  "shafts": [40,66,102],
  "shaftDepths": [9,14,12],
  "bulge": 6,
  "spread": 6,
  "kind": "natural"
});
