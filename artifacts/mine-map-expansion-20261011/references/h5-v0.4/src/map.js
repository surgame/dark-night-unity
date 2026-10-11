(function () {
  'use strict';
  const D = window.DN = { T: 24, cols: 132, rows: 78, version: 'mine-concept-pack-v0.4' };
  D.width = D.cols * D.T; D.height = D.rows * D.T;
  D.clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  D.dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
  D.hash = (x, y) => { let n = Math.imul(x + 739, 374761393) ^ Math.imul(y + 317, 668265263); n = Math.imul(n ^ n >>> 13, 1274126177); return (n ^ n >>> 16) >>> 0; };
  D.zones = [
    { title: '入口试采区', eyebrow: '01 / SHALLOW', text: '有光、有矿、有清楚的回程。', color: '#93b6ad' },
    { title: '回声岔口', eyebrow: '02 / THE FORK', text: '晶脉有指向；矿镐声也会被听见。', color: '#baac71' },
    { title: '折岩深井', eyebrow: '03 / THE DEEP', text: '台阶仍能返回，先留意另一条退路。', color: '#b77976' }
  ];
  D.tileSolid = function (m, x, y) {
    const tx = Math.floor(x / D.T), ty = Math.floor(y / D.T);
    return tx < 0 || ty < 0 || tx >= D.cols || ty >= D.rows || m.tiles[ty * D.cols + tx] !== 0;
  };
  D.pointSolid = function (m, x, y) {
    return D.tileSolid(m, x, y) || m.gates.some(g => !g.open && x >= g.x && x < g.x + g.w && y >= g.y && y < g.y + g.h);
  };
  D.blockingRects = function (m, x, y, w, h) {
    const rects = [], minX = Math.floor(x / D.T), maxX = Math.floor((x + w - .01) / D.T);
    const minY = Math.floor(y / D.T), maxY = Math.floor((y + h - .01) / D.T);
    for (let ty = minY; ty <= maxY; ty++) for (let tx = minX; tx <= maxX; tx++) {
      if (tx < 0 || ty < 0 || tx >= D.cols || ty >= D.rows || m.tiles[ty * D.cols + tx]) rects.push({ x: tx * D.T, y: ty * D.T, w: D.T, h: D.T });
    }
    m.gates.forEach(g => { if (!g.open && x < g.x + g.w && x + w > g.x && y < g.y + g.h && y + h > g.y) rects.push(g); });
    return rects;
  };
  D.ray = function (m, x, y, angle, range) {
    const dx = Math.cos(angle), dy = Math.sin(angle);
    for (let r = 0; r <= range; r += 3) {
      if (D.pointSolid(m, x + dx * r, y + dy * r)) return { x: x + dx * Math.max(0, r - 1), y: y + dy * Math.max(0, r - 1), distance: r };
    }
    return { x: x + dx * range, y: y + dy * range, distance: range };
  };
  D.lineClear = function (m, a, b, tolerance = 8) {
    const range = D.dist(a, b);
    return D.ray(m, a.x, a.y, Math.atan2(b.y - a.y, b.x - a.x), range).distance >= range - tolerance;
  };
  D.gateDistance = function (p, g) {
    return Math.hypot(p.x - D.clamp(p.x, g.x, g.x + g.w), p.y - 15 - D.clamp(p.y - 15, g.y, g.y + g.h));
  };
  D.zoneAt = (p,m) => m && m.id !== 'baseline' ? (p.y > 45 * D.T ? 2 : p.y > 23 * D.T ? 1 : 0) : p.x > 44 * D.T && p.x < 74 * D.T && p.y > 32 * D.T && p.y < 48 * D.T ? 1 : p.y > 39 * D.T ? 2 : p.y > 23 * D.T ? 1 : 0;
  D.roomAt = (p,m) => (m ? m.rooms : D.rooms).find(r => p.x >= r.x * D.T && p.x < (r.x + r.w) * D.T && p.y >= r.y * D.T && p.y <= (r.y + r.h + 1) * D.T);
  D.oreExposed = (m,o) => !o.buried || m.tiles[o.coverIndex]===0;
})();
