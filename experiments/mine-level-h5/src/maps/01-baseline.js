(function(){
  const D=window.DN;
  D.rooms = [
    { id: 'camp', name: '飞船入口', x: 3, y: 6, w: 22, h: 10, zone: 0 },
    { id: 'shallow', name: '01 试采矿道', x: 29, y: 11, w: 22, h: 10, zone: 0 },
    { id: 'echo', name: '02 回声岔口', x: 58, y: 22, w: 20, h: 10, zone: 1 },
    { id: 'secret', name: '隐藏晶脉室', x: 45, y: 36, w: 15, h: 11, zone: 1 },
    { id: 'pit', name: '03 折岩深井', x: 86, y: 39, w: 22, h: 11, zone: 2 },
    { id: 'core', name: '深层金矿室', x: 116, y: 56, w: 13, h: 11, zone: 2 }
  ];
  D.createBaselineMap = function () {
    const tiles = new Uint8Array(D.cols * D.rows).fill(1);
    const carve = (x, y, w, h) => {
      for (let j = Math.max(1, y); j < Math.min(D.rows - 1, y + h); j++)
        for (let i = Math.max(1, x); i < Math.min(D.cols - 1, x + w); i++) tiles[j * D.cols + i] = 0;
    };
    const stairs = (x1, f1, x2, f2, headroom = 6) => {
      for (let x = x1; x <= x2; x++) {
        const floor = Math.round(f1 + (f2 - f1) * (x - x1) / (x2 - x1));
        carve(x, floor - headroom, 1, headroom);
      }
    };
    D.rooms.forEach(r => carve(r.x, r.y, r.w, r.h));
    stairs(24, 16, 30, 21);
    stairs(50, 21, 59, 32);
    stairs(77, 32, 86, 50);
    stairs(106, 50, 116, 67);
    // 左下支线：入口附近的真实晶脉线索绑定隐藏矿室，完整岩层保留。
    stairs(60, 47, 73, 32);
    // 回程环：下层轨道与上升井。上端实体盖板在下端爆破后释放。
    carve(18, 12, 5, 63); carve(20, 70, 105, 5); carve(121, 61, 4, 14);
    // 回程轨道位于主路下方，避免拆掉既有台阶；深层乘车井保留可下穿的木盖板。
    const platforms = [{ x: 121 * D.T, y: 67 * D.T, w: 4 * D.T }, { x: 68 * D.T, y: 32 * D.T, w: 5 * D.T }];
    const gates = [
      { id: 'secret', x: 65 * D.T, y: 35 * D.T, w: D.T, h: 6 * D.T, open: false, title: '漏光裂岩', hint: '晶脉从裂缝延伸进去。3 号炸药可打开矿室。' },
      { id: 'shortcut', x: 121 * D.T, y: 67 * D.T, w: 4 * D.T, h: D.T, open: false, title: '旧升降井裂岩', hint: '下方轨道通往上层升降井。3 号炸药可恢复捷径。' },
      { id: 'hatch', x: 18 * D.T, y: 16 * D.T, w: 5 * D.T, h: D.T, open: false, title: '封闭升降口' }
    ];
    const raw = [
      ['iron', 15, 15, 3], ['iron', 34, 20, 4], ['iron', 43, 20, 5],
      ['iron', 62, 31, 5], ['gold', 75, 31, 3],
      ['gold', 48, 46, 6], ['gold', 56, 46, 5], ['iron', 46, 46, 3],
      ['iron', 89, 49, 4], ['gold', 102, 49, 4],
      ['gold', 116, 66, 6], ['gold', 125, 66, 7]
    ];
    const ores = raw.map((r, i) => ({ id: `ore-${i}`, kind: r[0], x: (r[1] + .5) * D.T, y: (r[2] + 1) * D.T - 8, amount: r[3], initial: r[3], progress: 0 }));
    const signs = [
      { x: 21 * D.T, y: 16 * D.T, text: '主矿道 →', note: '第一笔矿就在入口。沿木牌与旧轨道下探，台阶始终能跳回来。' },
      { x: 68 * D.T, y: 32 * D.T, text: 'S ↓ 晶脉 / 主路 →', note: 'S 下穿木桥，沿晶脉向左下可找到漏光裂岩；右侧旧轨道通往更深处。' },
      { x: 79 * D.T, y: 36 * D.T, text: '敲击 = 停工', note: '岩鸣虫只回应附近的开采。先出现敲击，再冲向声音；停工或退到另一侧台阶。' },
      { x: 118 * D.T, y: 67 * D.T, text: '↓ 旧回程井', note: '金矿室脚下有可爆破裂岩。修复后按 E 搭矿车沿下层轨道回上层。' }
    ];
    const worms = [
      { id: 'echo-worm', x: 72 * D.T, y: 32 * D.T, anchor: 72 * D.T, floor: 32 * D.T, radius: 180, noise: 0, mode: 'sleep', timer: 0, target: 0, cooldown: 0 },
      { id: 'core-worm', x: 123 * D.T, y: 67 * D.T, anchor: 123 * D.T, floor: 67 * D.T, radius: 164, noise: 0, mode: 'sleep', timer: 0, target: 0, cooldown: 0 }
    ];
    const lights = [
      { x: 11 * D.T, y: 12.5 * D.T, radius: 330, power: .96, color: '#edc68a' },
      { x: 24 * D.T, y: 13 * D.T, radius: 136, power: .72, color: '#c2d4b2' },
      { x: 45 * D.T, y: 17 * D.T, radius: 120, power: .4, color: '#90b9b2' },
      { x: 53 * D.T, y: 42 * D.T, radius: 175, power: .6, color: '#74c7c8' }
    ];
    return { id: 'baseline', title: '原版下探洞网', rooms: D.rooms, tiles, gates, ores, signs, worms, lights, platforms, ladders: [], walls: new Map(), nests: [], background: null };
  };
})();
// 固定地图定义；坐标以格为单位。运行状态由地图实例独立持有。
window.DN.registerLayout({
  "id": "baseline",
  "number": "01",
  "title": "原版下探洞网",
  "tag": "原版对照",
  "description": "清楚主路、隐藏支线、矿车回程。保留首版的表面矿，作为比较基准。",
  "kind": "baseline"
});
