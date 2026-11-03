(function (root) {
  "use strict";
  const api = root.CaveWB = root.CaveWB || {};
  const binaryPolygons = [
    [], [[[0, 0], [.5, 0], [0, .5]]], [[[.5, 0], [1, 0], [1, .5]]],
    [[[0, 0], [1, 0], [1, .5], [0, .5]]], [[[1, .5], [1, 1], [.5, 1]]],
    [[[0, 0], [.5, 0], [1, .5], [1, 1], [.5, 1], [0, .5]]],
    [[[.5, 0], [1, 0], [1, 1], [.5, 1]]],
    [[[0, 0], [1, 0], [1, 1], [.5, 1], [0, .5]]],
    [[[0, .5], [.5, 1], [0, 1]]], [[[0, 0], [.5, 0], [.5, 1], [0, 1]]],
    [[[.5, 0], [1, 0], [1, .5]], [[0, .5], [.5, 1], [0, 1]]],
    [[[0, 0], [1, 0], [1, .5], [.5, 1], [0, 1]]],
    [[[0, .5], [1, .5], [1, 1], [0, 1]]],
    [[[0, 0], [.5, 0], [1, .5], [1, 1], [0, 1]]],
    [[[.5, 0], [1, 0], [1, 1], [0, 1], [0, .5]]],
    [[[0, 0], [1, 0], [1, 1], [0, 1]]]
  ];

  function polygon(ctx, points, fill) {
    if (!points.length) return;
    ctx.beginPath();
    points.forEach((point, i) => { if (i) ctx.lineTo(point.x, point.y); else ctx.moveTo(point.x, point.y); });
    ctx.closePath();
    ctx.fillStyle = fill;
    ctx.fill();
  }

  function makeRenderer(canvas, container) {
    const ctx = canvas.getContext("2d");
    const probe = document.createElement("span");
    probe.hidden = true;
    container.appendChild(probe);
    let colors = {};
    let map = null;
    let rockPath = null;
    let width = 736, height = 440;
    const camera = { x: 20, y: 15, zoom: 70 };
    const state = { mode: "scene", perspective: true, selection: null };

    function palette() {
      for (const name of ["background", "far", "face", "top", "side", "under", "line", "joint", "ink", "fog"]) {
        probe.style.color = `var(--cave-${name})`;
        colors[name] = getComputedStyle(probe).color;
      }
    }
    function resize() {
      const box = canvas.getBoundingClientRect();
      width = Math.max(280, box.width);
      height = box.height;
      const ratio = Math.min(2, window.devicePixelRatio || 1);
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(height * ratio);
      ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
      palette();
      draw();
    }
    function setMap(next) {
      map = next;
      rockPath = new Path2D();
      rockPath.rect(0, 0, map.width, map.height);
      map.geometry.contours.forEach(contour => {
        contour.points.forEach((point, i) => {
          if (i) rockPath.lineTo(point.x, point.y); else rockPath.moveTo(point.x, point.y);
        });
        rockPath.closePath();
      });
      fit("local");
    }
    function fit(kind) {
      if (!map) return;
      if (kind === "all") {
        camera.zoom = Math.min(width / map.width, height / map.height) * .94;
        camera.x = map.width / 2;
        camera.y = map.height / 2;
      } else {
        const room = map.plan.nodes[Math.min(4, map.plan.nodes.length - 1)];
        camera.zoom = Math.min(width / 9, height / 6.2);
        camera.x = room.x;
        camera.y = room.y + .2;
      }
      draw();
    }
    function toWorld(x, y) {
      return { x: (x - width / 2) / camera.zoom + camera.x, y: (y - height / 2) / camera.zoom + camera.y };
    }
    function drawBands() {
      if (!state.perspective) return;
      const dx = .34, dy = -.30;
      map.geometry.contours.forEach(contour => {
        for (let i = 1; i < contour.points.length; i++) {
          const a = contour.points[i - 1], b = contour.points[i];
          const length = Math.hypot(b.x - a.x, b.y - a.y);
          if (!length) continue;
          const ny = contour.normals[i - 1].y;
          const fill = ny < -.22 ? colors.top : ny > .22 ? colors.under : colors.side;
          polygon(ctx, [a, b, { x: b.x + dx, y: b.y + dy }, { x: a.x + dx, y: a.y + dy }], fill);
        }
      });
      ctx.strokeStyle = colors.line;
      ctx.lineWidth = .8 / camera.zoom;
      map.geometry.contours.forEach(contour => {
        ctx.beginPath();
        contour.points.forEach((point, i) => {
          if (i) ctx.lineTo(point.x + dx, point.y + dy); else ctx.moveTo(point.x + dx, point.y + dy);
        });
        ctx.stroke();
      });
    }
    function drawGrid() {
      const low = toWorld(0, 0), high = toWorld(width, height);
      const x0 = Math.max(0, Math.floor(low.x)), x1 = Math.min(map.width, Math.ceil(high.x));
      const y0 = Math.max(0, Math.floor(low.y)), y1 = Math.min(map.height, Math.ceil(high.y));
      ctx.strokeStyle = colors.line;
      ctx.lineWidth = .65 / camera.zoom;
      ctx.globalAlpha = .6;
      ctx.beginPath();
      for (let x = x0; x <= x1; x++) { ctx.moveTo(x, y0); ctx.lineTo(x, y1); }
      for (let y = y0; y <= y1; y++) { ctx.moveTo(x0, y); ctx.lineTo(x1, y); }
      ctx.stroke();
      ctx.globalAlpha = 1;
      if (camera.zoom < 24) return;
      for (let y = y0; y <= y1; y++) {
        for (let x = x0; x <= x1; x++) {
          const value = map.check.samples[y * (map.width + 1) + x];
          ctx.beginPath();
          ctx.arc(x, y, 3.3 / camera.zoom, 0, Math.PI * 2);
          ctx.fillStyle = value > 0 ? colors.ink : colors.background;
          ctx.fill();
          ctx.strokeStyle = colors.ink;
          ctx.lineWidth = 1 / camera.zoom;
          ctx.stroke();
        }
      }
    }
    function drawAtlas() {
      const gap = 14;
      const size = Math.min((width - gap * 5) / 4, (height - 32) / 4 - gap);
      const left = (width - (size * 4 + gap * 3)) / 2;
      for (let mask = 0; mask < 16; mask++) {
        const x = left + (mask % 4) * (size + gap);
        const y = 9 + Math.floor(mask / 4) * (size + gap);
        ctx.save();
        ctx.translate(x, y);
        ctx.scale(size, size);
        binaryPolygons[mask].forEach(points => polygon(ctx, points.map(p => ({ x: p[0], y: p[1] })), colors.face));
        ctx.strokeStyle = colors.line;
        ctx.lineWidth = 1 / size;
        ctx.strokeRect(0, 0, 1, 1);
        [[0, 0], [1, 0], [1, 1], [0, 1]].forEach(([px, py], bit) => {
          ctx.beginPath();
          ctx.arc(px, py, 3 / size, 0, Math.PI * 2);
          ctx.fillStyle = mask & (1 << bit) ? colors.ink : colors.background;
          ctx.fill(); ctx.stroke();
        });
        ctx.restore();
        ctx.font = "12px sans-serif";
        ctx.fillStyle = colors.ink;
        ctx.textAlign = "center";
        ctx.fillText(mask.toString(2).padStart(4, "0"), x + size / 2, y + size / 2 + 4);
      }
    }
    function draw() {
      ctx.fillStyle = colors.background || "transparent";
      ctx.fillRect(0, 0, width, height);
      if (state.mode === "atlas") { drawAtlas(); return; }
      if (!map) return;
      ctx.save();
      ctx.translate(width / 2, height / 2);
      ctx.scale(camera.zoom, camera.zoom);
      ctx.translate(-camera.x, -camera.y);
      ctx.beginPath();
      ctx.rect(0, 0, map.width, map.height);
      ctx.clip();
      ctx.fillStyle = colors.far;
      ctx.fillRect(0, 0, map.width, map.height);
      // Neutral background prisms provide scale and depth without authored art.
      const rng = api.random(map.seed ^ 0xCA7E);
      for (let i = 0; i < 100; i++) {
        const x = rng() * map.width, y = rng() * map.height;
        const w = .2 + rng() * .7, h = .6 + rng() * 1.4;
        polygon(ctx, [{ x, y }, { x: x + w * .4, y: y - h }, { x: x + w, y }, { x: x + w, y: y + h }], colors.fog);
      }
      ctx.fillStyle = colors.face;
      ctx.fill(rockPath, "evenodd");
      if (state.mode === "scene") drawBands();
      if (state.mode === "seams") drawGrid();
      if (state.selection && state.mode === "seams") {
        ctx.strokeStyle = colors.joint;
        ctx.lineWidth = 2 / camera.zoom;
        ctx.strokeRect(state.selection.x, state.selection.y, 1, 1);
      }
      ctx.restore();
      ctx.fillStyle = colors.ink;
      ctx.font = "12px sans-serif";
      ctx.textAlign = "left";
      ctx.fillText(state.mode === "seams" ? "● 实心角点   ○ 空气角点" : "正面岩体 · 顶面 · 暗色底面", 12, 23);
      const bar = Math.min(120, camera.zoom * 2);
      ctx.strokeStyle = colors.ink;
      ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(14, height - 24); ctx.lineTo(14 + bar, height - 24); ctx.stroke();
      ctx.fillText(`${(bar / camera.zoom).toFixed(1)} 大格`, 14, height - 32);
    }
    palette();
    return { state, camera, resize, setMap, fit, toWorld, draw, refreshPalette: () => { palette(); draw(); } };
  }
  Object.assign(api, { makeRenderer });
})(typeof globalThis !== "undefined" ? globalThis : this);
