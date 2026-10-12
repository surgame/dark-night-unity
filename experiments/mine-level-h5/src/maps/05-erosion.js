// 固定地图定义；坐标以格为单位。运行状态由地图实例独立持有。
window.DN.registerLayout({
  "id": "erosion",
  "number": "05",
  "title": "侵蚀长廊",
  "tag": "自然曲廊 / 玩家搭梯",
  "natural": true,
  "description": "起伏长廊串起大小不一的岩腔。先沿地面深入，再用自己搭的梯子探索高处侧洞。",
  "seed": 17,
  "profile": [
    [3,16],
    [24,16],
    [33,21],
    [43,25],
    [55,31],
    [68,38],
    [79,42],
    [92,50],
    [109,60],
    [128,67]
  ],
  "chambers": [34,53,76,100,122],
  "shafts": [45,89],
  "shaftDepths": [9,12],
  "bulge": 4.5,
  "spread": 5,
  "kind": "natural"
});
