(function (root) {
  "use strict";
  const api = root.CaveWB = root.CaveWB || {};

  function random(seed) {
    let state = seed >>> 0;
    return function () {
      state += 0x6D2B79F5;
      let value = state;
      value = Math.imul(value ^ value >>> 15, value | 1);
      value ^= value + Math.imul(value ^ value >>> 7, value | 61);
      return ((value ^ value >>> 14) >>> 0) / 4294967296;
    };
  }

  function clamp(value, low, high) { return Math.max(low, Math.min(high, value)); }

  function distanceToSegment(x, y, a, b) {
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const t = clamp(((x - a.x) * dx + (y - a.y) * dy) / (dx * dx + dy * dy || 1), 0, 1);
    return Math.hypot(x - a.x - dx * t, y - a.y - dy * t);
  }

  function makePlan(seed, width = 96, height = 40) {
    const rng = random(seed);
    const nodes = [];
    const rooms = [];
    const corridors = [];
    const margin = 4;
    let y = height * .3;
    const step = 7;
    for (let x = margin; x < width - margin; x += step) {
      y = clamp(y + (rng() - .37) * 5, 7, height - 9);
      const node = { x, y, rx: 2.2 + rng() * 2.1, ry: 1.6 + rng() * 1.5 };
      if (nodes.length) corridors.push({ a: nodes[nodes.length - 1], b: node, radius: 1.15 + rng() * .35 });
      nodes.push(node);
      rooms.push(node);
    }
    nodes.forEach((node, i) => {
      if (i < 1 || i >= nodes.length - 1 || i % 2 === 0) return;
      const side = i % 4 === 1 ? 1 : -1;
      const other = {
        x: clamp(node.x + 3 + rng() * 4, margin + 1, width - margin - 1),
        y: clamp(node.y + side * (5 + rng() * 5), 5, height - 5),
        rx: 2.4 + rng() * 1.9, ry: 1.7 + rng() * 1.3
      };
      corridors.push({ a: node, b: other, radius: 1.05 + rng() * .3 });
      rooms.push(other);
      if (i + 2 < nodes.length && rng() > .48) {
        corridors.push({ a: other, b: nodes[i + 2], radius: 1.05 });
      }
    });
    function signedDistance(x, y) {
      let distance = 999;
      rooms.forEach(room => {
        const r = Math.min(room.rx, room.ry);
        distance = Math.min(distance, (Math.hypot((x - room.x) / room.rx, (y - room.y) / room.ry) - 1) * r);
      });
      corridors.forEach(corridor => {
        distance = Math.min(distance, distanceToSegment(x, y, corridor.a, corridor.b) - corridor.radius);
      });
      // A solid border closes every contour; the demo is underground, without a surface mouth.
      return Math.max(distance, 2 - Math.min(x, y, width - x, height - y));
    }
    const stride = width + 1;
    const target = new Float32Array(stride * (height + 1));
    const allowed = new Uint8Array(target.length);
    for (let sy = 0; sy <= height; sy++) {
      for (let sx = 0; sx <= width; sx++) {
        const index = sy * stride + sx;
        const distance = signedDistance(sx, sy);
        target[index] = clamp(distance * 3, -3, 3);
        if (Math.abs(distance) > .95) allowed[index] = distance > 0 ? 8 : 1;
        else if (Math.abs(distance) < .12) allowed[index] = 6;
        else allowed[index] = distance > 0 ? 12 : 3;
      }
    }
    return { seed, width, height, rooms, corridors, nodes, target, allowed, signedDistance };
  }

  Object.assign(api, { random, clamp, makePlan });
  if (typeof module !== "undefined") module.exports = api;
})(typeof globalThis !== "undefined" ? globalThis : this);
