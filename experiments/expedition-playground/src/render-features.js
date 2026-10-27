(function (D) {
  "use strict";
  const bridgeArt = [];
  function bridgeRock(index) {
    if (!bridgeArt[index]) {
      const mask = new Uint8Array(16 * 7);
      for (let y = 0; y < 7; y++) for (let x = 0; x < 16; x++) {
        mask[y * 16 + x] = y < 5 || (x + index * 3) % 7 > y - 5 ? 1 : 0;
      }
      bridgeArt[index] = D.bakePixelRock(16, 7, mask, D.rockToneField(16, 7));
    }
    return bridgeArt[index];
  }
  function particles(c, w, o, color, height, width, n) {
    c.fillStyle = color;
    for (let i = 0; i < n; i++) {
      const age = (w.time * (o.mode === "jet" ? 1.8 : .6) + i * .17) % 1;
      const x = Math.round(Math.sin(i * 13 + age * 4) * width * age / 2) * 2;
      const y = Math.round(-age * height / 2) * 2;
      c.globalAlpha = (1 - age) * .9;
      c.fillRect(x, y, i % 2 ? 4 : 6, 4);
    }
    c.globalAlpha = 1;
  }
  D.drawFeatureObject = function (c, o, w) {
    if (o.type === "vent") {
      D.drawPixel(c, "vent-prop", 0, 8, 2);
      if (o.mode === "frozen") {
        c.fillStyle = "#9ba29b";
        for (let i = 0; i < 7; i++) c.fillRect(-20 + i * 6, -1 - i % 2 * 2, 4, 2);
      } else {
        if (o.mode !== "cool") {
          c.fillStyle = o.mode === "jet" ? "#e8bc67" : "#c17858";
          c.fillRect(-14, 1, 28, 2);
        }
        particles(c, w, o, o.mode === "jet" ? "#e8bc67" : "#9ba29b",
          o.mode === "jet" ? 110 : o.mode === "warning" ? 40 : 14, 22, o.mode === "jet" ? 25 : 7);
      }
      return true;
    }
    if (o.type === "magcore") {
      D.drawPixel(c, "core-prop", 0, 0, 2);
      c.fillStyle = o.mode === "suppressed" || o.mode === "off" ? "#393c3b" : "#e8bc67";
      c.fillRect(-2, -2, 6, 4);
      if (o.mode === "pull" || o.mode === "charging") {
        for (let i = 0; i < 20; i++) {
          const t = (w.time * .6 + i * .09) % 1;
          const side = i % 2 ? 1 : -1;
          const x = Math.round(side * (24 + (1 - t) * 220) / 2) * 2;
          const y = Math.round((20 + i % 4 * 5) * (1 - t) / 2) * 2;
          c.fillStyle = i % 3 ? "#9ba29b" : "#b29566"; c.fillRect(x, y, 2, 2);
        }
      }
      return true;
    }
    if (o.type === "photoroot") {
      for (let i = -3; i <= 3; i++) {
        const length = Math.round((520 - o.y - Math.abs(i) * 5) * (1 - o.opening) / 2) * 2;
        for (let y = 0; y < length; y += 4) {
          const x = i * 5 + Math.round(Math.sin(y * .1 + i + w.time * .7)) * 2;
          c.fillStyle = "#393c3b"; c.fillRect(x - 2, y, 6, 4);
          c.fillStyle = i % 2 ? "#85835b" : "#69462e"; c.fillRect(x, y, 2, 4);
        }
      }
      D.drawPixel(c, "root-knot", 0, 0, 2);
      c.fillStyle = o.lit ? "#d1cebc" : "#668e94"; c.fillRect(-4, -2, 4, 4);
      return true;
    }
    if (o.type === "pressureValve") {
      c.fillStyle = "#393c3b"; c.fillRect(18, 8, 150, 5);
      c.fillStyle = "#656b68"; c.fillRect(18, 8, 150, 2);
      D.drawPixel(c, "valve-prop", 0, 26, 2); return true;
    }
    if (o.type === "amber") {
      if (!o.ruptured) {
        D.drawPixel(c, "amber-prop", 0, 0, 2);
        c.fillStyle = "#e8bc67";
        for (let i = 0; i < o.pressure; i++) c.fillRect(-10 + i * 8, -40, 5, 4);
        if (o.ruptureTimer !== undefined) particles(c, w, o, "#f4e2b5", 35, 22, 15);
      } else {
        D.drawNative(c, "rubble2", 0, 28, 2);
        if (o.jetTime > 0) particles(c, w, o, "#e8bc67", 80, 80, 30);
      }
      return true;
    }
    if (o.type === "echoPod") {
      D.drawNative(c, w.feature.awake ? "burrow-empty" : "burrow-occupied", 0, 19, 2);
      if (!w.feature.awake && w.feature.noise > 15) {
        c.fillStyle = "#b29566"; c.fillRect(-10, -18, 4, 2); c.fillRect(6, -18, 4, 2);
      }
      return true;
    }
    if (o.type === "echoShard") {
      D.drawMineral(c, "iron", -8, -10, o.id % 16, 2); return true;
    }
    return false;
  };
  D.drawFeatureGeometry = function (c, w) {
    if (!w.feature) return;
    w.solids.forEach(function (s) {
      if (s.kind !== "thinbridge" || s.hp <= 0) return;
      c.drawImage(bridgeRock(s.id % 3), s.x, s.y, s.w, 14);
      if (s.wear > 0) {
        c.fillStyle = "#261d14";
        c.fillRect(s.x + 12, s.y + 2, 2, 4); c.fillRect(s.x + 14, s.y + 6, 2, 8);
      }
      if (s.braced) {
        c.fillStyle = "#656b68"; c.fillRect(s.x + 2, s.y + 2, s.w - 4, 4);
        c.fillStyle = "#d1cebc"; c.fillRect(s.x + 4, s.y + 2, 2, 2);
        c.fillRect(s.x + s.w - 6, s.y + 2, 2, 2);
      } else if (s.decay > 0 && Math.floor(w.time * 7) % 2) {
        c.fillStyle = "#e8bc67"; c.fillRect(s.x + 12, s.y, 4, 2);
      }
    });
  };
  D.featureStatus = function (w) {
    if (!w.feature) return "";
    const type = w.feature.type, objects = w.objects;
    if (type === "echo") return w.feature.awake ? "眠巢已醒 · 本点共2只，允许引开或清理" :
      "共振 " + Math.round(w.feature.noise) + " / 45 · 慢走安静，冲刺与开火增振";
    if (type === "fracture") return "已加固 " + (w.stats.bridgesBraced || 0) + " / 6 · 坍塌 " + (w.stats.bridgesFallen || 0) + " · 裂纹不会恢复";
    if (type === "vents") return objects.filter(o => o.type === "vent").map(o =>
      o.frozen > 0 ? "冷凝" + o.frozen.toFixed(1) + "s" : ({ cool: "间歇", warning: "预警", jet: "喷发" })[o.mode]).join("  /  ");
    if (type === "magnet") {
      const core = objects.find(o => o.type === "magcore");
      return core.suppressed > 0 ? "消磁剩余 " + core.suppressed.toFixed(1) + "s" :
        ({ charging: "磁核聚能", pull: "磁场吸引 · 背矿越多吸力越强", off: "磁场间歇" })[core.mode];
    }
    if (type === "photoroot") return objects.filter(o => o.type === "photoroot").map((o, i) =>
      "根幕" + (i + 1) + (o.opening > .72 ? "退缩" : "垂下")).join("  /  ");
    if (type === "amber") {
      const amber = objects.find(o => o.type === "amber");
      return !amber ? "完整琥珀已收取 · 回入口交付" : amber.ruptured ? "囊体破裂 · 余3份碎料" :
        "囊内压力 " + amber.pressure + " / 3 · 完整价值6，破裂碎料3";
    }
    return "断层试采 · 地热 → 薄桥 → 畏光根幕 → 磁核富矿 → 原路返回";
  };
  D.drawFeatureHud = function (c, w) {
    if (!w.feature) return;
    c.fillStyle = "#101b19db"; c.fillRect(15, 101, 510, 28);
    c.font = '12px "Microsoft YaHei", sans-serif'; c.textAlign = "left"; c.fillStyle = "#c7b58b";
    c.fillText(D.featureStatus(w), 23, 119, 490);
    if (w.feature.type === "echo") {
      c.fillStyle = "#393c3b"; c.fillRect(23, 133, 160, 5);
      c.fillStyle = w.feature.noiseFlash > 0 ? "#e8bc67" : "#96704b";
      c.fillRect(23, 133, Math.min(160, w.feature.noise / 45 * 160), 5);
    }
  };
})(window.DN);
