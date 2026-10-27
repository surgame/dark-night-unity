(function (D) {
  "use strict";
  D.version = "expedition-playground-v1";
  D.categories = [
    { id: "weapon", label: "武器实验", code: "W", color: "#edaa68" },
    { id: "tool", label: "探索道具", code: "T", color: "#7dc9bf" },
    { id: "site", label: "场景玩点", code: "S", color: "#c7ba81" },
    { id: "mix", label: "组合远征", code: "M", color: "#a8b7dd" }
  ];
  D.items = {
    pick: { name: "矿镐", icon: "pick", color: "#bac6be", rate: .34 },
    pistol: { name: "手枪", icon: "gun", color: "#b8c4c0", rate: .32 },
    bomb: { name: "普通炸药", icon: "bomb", color: "#df876b", rate: .65 },
    shotgun: { name: "短管霰弹枪", icon: "gun", color: "#e8a56a", rate: .85 },
    nail: { name: "工业射钉枪", icon: "gun", color: "#c8d5c2", rate: .52 },
    shock: { name: "电击棒", icon: "shock", color: "#8bd7e4", rate: .65 },
    net: { name: "捕网发射器", icon: "net", color: "#bed2a0", rate: .9 },
    foam: { name: "泡沫喷射器", icon: "foam", color: "#c6e2d4", rate: .55 },
    flare: { name: "照明弹发射器", icon: "flare", color: "#ffa576", rate: .8 },
    charge: { name: "定向爆破器", icon: "charge", color: "#e8b164", rate: .7 },
    lure: { name: "诱饵投射器", icon: "lure", color: "#c8a4dc", rate: .75 },
    glow: { name: "投掷荧光棒", icon: "flare", color: "#91e5b4", rate: .5 },
    scanner: { name: "贴壁探测器", icon: "scanner", color: "#81cbd7", rate: 1.2 },
    ladder: { name: "伸缩梯具", icon: "ladder", color: "#c5ac80", rate: .5 },
    support: { name: "临时支撑柱", icon: "support", color: "#dfb97c", rate: .6 },
    winch: { name: "牵引绞盘", icon: "winch", color: "#d0b285", rate: .5 },
    shield: { name: "折叠挡板", icon: "shield", color: "#97bdb3", rate: .5 },
    seismic: { name: "地震诱饵", icon: "lure", color: "#cfacda", rate: .7 },
    sample: { name: "密封采样罐", icon: "sample", color: "#b1d1c3", rate: .6 },
    medkit: { name: "救援包", icon: "medkit", color: "#e2a29a", rate: .5 }
  };
  D.catalog = [];
  D.register = function (category, entries) {
    const offset = D.catalog.filter(function (s) { return s.category === category; }).length;
    entries.forEach(function (entry, i) {
      const group = D.categories.find(function (x) { return x.id === category; });
      D.catalog.push(Object.assign({
        category: category, code: group.code + String(offset + i + 1).padStart(2, "0"),
        color: group.color, kit: ["pick", "pistol", "glow"], tags: [],
        boundary: "仅离线机制候选；所有数值、伤害、物资与场景为本演示专用。"
      }, entry));
    });
  };
})(window.DN = window.DN || {});
