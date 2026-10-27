(function (D) {
  "use strict";
  // Read-only H5 translation of Core CaveRockBaker / ToneField / BackgroundPixelMath.
  // Native pixel = 2 world px. No Unity shader or grain modifier equivalence is claimed.
  const palette = [[27,21,15], [38,29,20], [53,41,29], [70,53,38],
    [92,70,50], [115,88,62], [146,112,79], [181,139,98]];
  function hash(x, y, seed) {
    let a = Math.imul(x, 374761393) ^ Math.imul(y, 668265263) ^ Math.imul(seed, 1442695041);
    a = Math.imul(a ^ a >>> 13, 1274126177);
    return ((a ^ a >>> 16) >>> 0) / 4294967296;
  }
  function noise(x, y, seed) {
    const ix = Math.floor(x), iy = Math.floor(y), fx = x - ix, fy = y - iy;
    const u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
    return (hash(ix, iy, seed) * (1 - u) + hash(ix + 1, iy, seed) * u) * (1 - v) +
      (hash(ix, iy + 1, seed) * (1 - u) + hash(ix + 1, iy + 1, seed) * u) * v;
  }
  const seed = 2166136261 >>> 0;
  const toneCache = new Map();
  D.rockToneField = function (width, height) {
    const key = width + "x" + height;
    if (toneCache.has(key)) return toneCache.get(key);
    const size = 4, columns = Math.ceil(width / size) + 3, facets = [];
    for (let y = -1; y < Math.ceil(height / size) + 2; y++) {
      for (let x = -1; x < Math.ceil(width / size) + 2; x++) {
        const s = seed + 316, v = hash(x, y, s), v2 = hash(x, y, s + 155);
        const individual = 2 + Math.floor(hash(x, y, s + 331) * 4);
        const regional = 2.6 + noise((x + .5) * size / 30, (y + .5) * size / 30, s + 665) * 1.6;
        facets.push({ x: (x + .2 + v * .62) * size, y: (y + .2 + v2 * .64) * size,
          tone: Math.floor(individual * .45 + regional * .55 + .5) });
      }
    }
    const tones = new Uint8Array(width * height);
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      const column = Math.floor(x / size) + 1, row = Math.floor(y / size) + 1;
      let first = Infinity, second = Infinity, nearest;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        const f = facets[(row + dy) * columns + column + dx];
        const distance = (x - f.x) ** 2 + (y - f.y) ** 2 * 1.18;
        if (distance < first) { second = first; first = distance; nearest = f; }
        else if (distance < second) second = distance;
      }
      let tone = D.clamp(nearest.tone + (y < nearest.y - 1 ? 1 : 0) - (y > nearest.y + 1 ? 1 : 0), 0, 7);
      if (Math.sqrt(second) - Math.sqrt(first) < .36) tone = 0;
      tones[y * width + x] = tone;
    }
    if (toneCache.size >= 4) toneCache.delete(toneCache.keys().next().value);
    toneCache.set(key, tones); return tones;
  };
  D.bakePixelRock = function (width, height, mask, tones, rear) {
    const canvas = document.createElement("canvas"); canvas.width = width; canvas.height = height;
    const c = canvas.getContext("2d"), image = c.createImageData(width, height), bytes = image.data;
    const distance = new Uint16Array(mask.length);
    for (let i = 0; i < mask.length; i++) distance[i] = mask[i] ? 4095 : 0;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      const i = y * width + x;
      distance[i] = Math.min(distance[i], x ? distance[i - 1] + 1 : 4095, y ? distance[i - width] + 1 : 4095);
    }
    for (let y = height - 1; y >= 0; y--) for (let x = width - 1; x >= 0; x--) {
      const i = y * width + x;
      distance[i] = Math.min(distance[i], x + 1 < width ? distance[i + 1] + 1 : 4095,
        y + 1 < height ? distance[i + width] + 1 : 4095);
    }
    function at(x, y) { return x >= 0 && x < width && y >= 0 && y < height && mask[y * width + x]; }
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      const i = y * width + x;
      if (!mask[i]) continue;
      const depth = Math.min(25, distance[i]), tone = palette[tones[i]];
      const exposure = (!at(x, y - 1) ? 1 : 0) + (!at(x, y - 2) ? .55 : 0) +
        (!at(x - 1, y - 1) ? .25 : 0) + (!at(x + 1, y - 1) ? .25 : 0);
      const shade = rear ? .37 : (.22 + .88 * Math.exp(-(depth - 1) / 1.9 / 7)) *
        (.94 + (hash(x, y, seed + 777) - .5) * .08) * (depth === 1 ? 1.14 : depth === 2 ? 1.06 : 1) *
        (1 + Math.min(exposure, .8) * .18 * Math.exp(-(depth - 1) / 3.5));
      for (let j = 0; j < 3; j++) bytes[i * 4 + j] = Math.min(255, Math.floor(tone[j] * shade + .5));
      bytes[i * 4 + 3] = 255;
    }
    c.putImageData(image, 0, 0); return canvas;
  };
  D.drawCaveSurface = function (c, w) {
    const signature = w.solids.filter(function (s) { return s.hp > 0 && !s.dynamic; }).map(function (s) { return s.id; }).join(",");
    if (!w.rockCache || w.rockCache.signature !== signature) {
      const width = w.width / 2, height = w.height / 2, mask = new Uint8Array(width * height);
      const tones = w.rockCache ? w.rockCache.tones : D.rockToneField(width, height);
      w.solids.forEach(function (s) {
        if (s.hp <= 0 || s.dynamic) return;
        for (let y = Math.max(0, Math.ceil(s.y / 2)); y < Math.min(height, Math.ceil((s.y + s.h) / 2)); y++) {
          mask.fill(1, y * width + Math.max(0, Math.ceil(s.x / 2)),
            y * width + Math.min(width, Math.ceil((s.x + s.w) / 2)));
        }
      });
      w.rockCache = { signature: signature, tones: tones, canvas: D.bakePixelRock(width, height, mask, tones) };
    }
    c.drawImage(w.rockCache.canvas, 0, 0, w.width, w.height);
    w.solids.forEach(function (s) {
      if (s.hp <= 0 || s.dynamic || s.kind === "bedrock") return;
      if (s.kind === "connector" || s.hp < s.maxHp) {
        const x = s.x + Math.floor(s.w / 4) * 2;
        c.fillStyle = "#b58b62"; c.fillRect(x, s.y, 2, Math.min(s.h, 10));
        c.fillStyle = "#261d14";
        for (let y = 0; y < s.h; y += 4) c.fillRect(x + (y % 12 ? -2 : 2), s.y + y, 2, 4);
      }
      if (s.kind === "mycelium") {
        c.fillStyle = "#627a61";
        for (let y = 0; y < s.h; y += 4) c.fillRect(s.x + 12 + Math.round(Math.sin(y / 8) * 5) * 2, s.y + y, 2, 4);
      }
    });
  };
})(window.DN);
