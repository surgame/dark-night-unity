// 固定地图定义；坐标以格为单位。运行状态由地图实例独立持有。
window.DN.registerLayout({
  "id": "karst",
  "number": "07",
  "title": "溶洞回环",
  "tag": "天然上层支路 / 回环",
  "natural": true,
  "description": "主洞与上层曲道围成岩柱回环。在两端搭梯后，上层支路就成为可双向返回的第二条路线。",
  "seed": 71,
  "profile": [
    [3,16],
    [24,16],
    [37,22],
    [48,25],
    [60,34],
    [73,38],
    [86,46],
    [100,57],
    [115,60],
    [128,66]
  ],
  "chambers": [36,58,81,106,123],
  "shafts": [103],
  "shaftDepths": [11],
  "bulge": 7,
  "spread": 7,
  "loop": true,
  "kind": "natural"
});
