(function (D) {
  "use strict";
  function activePlayers(w) {
    return w.players.filter(function (p, i) { return !p.down && (i === 0 || w.coop); });
  }
  function hazard(w, p, x, y, range, height) {
    if (Math.abs(p.x - x) > range || p.y < y - height || p.y - p.h > y + 10) return;
    p.fieldVx += (Math.sign(p.x - x) || -1) * 180;
    if (p.invulnerable > 0) return;
    p.hp = Math.max(1, p.hp - 8); p.invulnerable = 1;
    w.count("featureHits"); w.note("喷流推退！等间歇、绕开或使用对应工具。", "hazard");
  }
  function stepVent(w, o, players, dt) {
    o.frozen = Math.max(0, o.frozen - dt);
    const cycle = (w.time + o.phase) % 7.2;
    o.mode = o.frozen > 0 ? "frozen" : cycle < 2.7 ? "cool" : cycle < 4 ? "warning" : cycle < 5.6 ? "jet" : "cool";
    if (o.mode === "jet") players.forEach(function (p) { hazard(w, p, o.x, 520, 25, 115); });
  }
  function stepBridge(w, s, players, dt) {
    if (s.hp <= 0 || s.braced) return;
    const on = players.some(function (p) {
      return p.grounded && Math.abs(p.y - s.y) < 3 && p.x + p.r > s.x && p.x - p.r < s.x + s.w;
    });
    if (on) s.wear += dt;
    if (s.wear > .12 && s.decay < 0) {
      s.decay = 1.55;
      w.effect("chips", s.x + s.w / 2, s.y + 5, "#b29566", { r: 17 });
      w.note("薄桥出现裂纹，离开也不会停止扩裂；仍可打钉加固。", "hazard");
    }
    if (s.decay >= 0) {
      s.decay -= dt;
      if (s.decay <= 0) {
        s.hp = 0; w.count("bridgesFallen");
        w.effect("dust", s.x + 16, s.y + 16, "#9ba29b", { r: 30 });
      }
    }
  }
  function stepCore(w, o, players, dt) {
    o.suppressed = Math.max(0, o.suppressed - dt);
    const cycle = w.time % 6;
    o.mode = o.suppressed > 0 ? "suppressed" : cycle < 1.4 ? "charging" : cycle < 4.4 ? "pull" : "off";
    if (o.mode !== "pull") return;
    players.forEach(function (p) {
      if (Math.abs(p.x - o.x) > 330) return;
      const strength = Math.min(158, 12 + p.cargo * 32);
      p.fieldVx += Math.sign(o.x - p.x) * strength;
      if (p.cargo && !p.magneticVisit) { p.magneticVisit = true; w.count("loadedMagnetVisits"); }
    });
  }
  D.featureLit = function (w, target) {
    const point = { x: target.x, y: target.y };
    const playerLight = activePlayers(w).some(function (p) {
      if (!p.light) return false;
      const origin = { x: p.x, y: p.y - 18 };
      const dx = target.x - origin.x, dy = target.y - origin.y;
      const a = Math.atan2(dy, dx), aim = Math.atan2(p.aim.y - origin.y, p.aim.x - origin.x);
      const delta = Math.atan2(Math.sin(a - aim), Math.cos(a - aim));
      return Math.hypot(dx, dy) < 370 && Math.abs(delta) < .63 && w.clearLine(origin, point, target.doorId);
    });
    return playerLight || w.objects.some(function (o) {
      return o.alive && o.light && D.distance(o, point) < o.light &&
        w.clearLine({ x: o.x, y: o.y - 5 }, point, target.doorId);
    });
  };
  function stepRoot(w, o, players, dt) {
    const door = w.solids.find(function (s) { return s.id === o.doorId; });
    const lit = D.featureLit(w, o);
    o.opening = D.clamp(o.opening + dt * (lit ? 2.7 : -.85), 0, 1);
    const occupied = players.some(function (p) {
      return Math.abs(p.x - o.x) < p.r + 18 && p.y > door.y && p.y - p.h < door.y + door.h;
    });
    if (o.opening > .72) {
      door.hp = 0;
      if (!o.everOpened) { o.everOpened = true; w.count("rootsOpened"); }
    } else if (o.opening < .3 && !occupied) door.hp = 3;
    if (occupied && o.opening < .73) o.opening = .73;
    o.lit = lit;
  }
  function stepAmber(w, o, players, dt) {
    if (o.ruptureTimer !== undefined && !o.ruptured) {
      o.ruptureTimer -= dt;
      if (o.ruptureTimer <= 0) {
        o.ruptured = true; o.jetTime = 2.5;
        for (let i = 0; i < 3; i++) w.addObject("ore", o.x - 36 + i * 38, 485,
          { value: 1, hp: 2, r: 16, featureLoot: "amber" });
        w.note("压力囊破裂，完整度损失；喷流退去后可采3份碎料。", "hazard");
      }
    }
    if (o.jetTime > 0) {
      o.jetTime -= dt;
      players.forEach(function (p) { hazard(w, p, o.x + 40, 507, 78, 75); });
    }
  }
  D.stepFeatures = function (w, dt) {
    if (!w.feature) return;
    const players = activePlayers(w);
    players.forEach(function (p) { p.fieldVx = 0; });
    w.solids.forEach(function (s) { if (s.kind === "thinbridge") stepBridge(w, s, players, dt); });
    w.objects.forEach(function (o) {
      if (!o.alive) return;
      if (o.type === "vent") stepVent(w, o, players, dt);
      if (o.type === "magcore") stepCore(w, o, players, dt);
      if (o.type === "photoroot") stepRoot(w, o, players, dt);
      if (o.type === "amber") stepAmber(w, o, players, dt);
    });
    const f = w.feature;
    f.noise = Math.max(0, f.noise - dt * 8);
    f.noiseFlash = Math.max(0, f.noiseFlash - dt);
    if (f.type === "echo" && f.noise >= 45 && !f.awake) {
      f.awake = true; w.count("echoAwakened");
      w.enemy(910, 520); w.enemy(980, 520);
      w.effect("chips", 870, 490, "#b29566", { r: 50 });
      w.note("共振达到阈值，眠巢唤醒两只幼虫；本洞室不再追加出虫。", "hazard");
    }
  };
  D.featureSnapshot = function (w) {
    if (!w.feature) return null;
    return {
      type: w.feature.type, noise: +w.feature.noise.toFixed(2), awake: w.feature.awake,
      stations: w.objects.filter(function (o) {
        return ["vent", "magcore", "photoroot", "amber"].includes(o.type);
      }).map(function (o) {
        return { id: o.id, type: o.type, mode: o.mode, frozen: o.frozen,
          suppressed: o.suppressed, opening: o.opening, pressure: o.pressure, ruptured: o.ruptured };
      }),
      bridges: w.solids.filter(function (s) { return s.kind === "thinbridge"; }).map(function (s) {
        return { id: s.id, present: s.hp > 0, braced: s.braced, wear: +s.wear.toFixed(3) };
      })
    };
  };
})(window.DN);
