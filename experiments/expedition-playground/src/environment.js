(function (D) {
  "use strict";
  D.surfaceBelow = function (w, x, y) {
    let floor = 640;
    w.solids.forEach(function (s) {
      if (s.hp > 0 && x >= s.x && x <= s.x + s.w && s.y >= y - 5) floor = Math.min(floor, s.y);
    });
    return floor;
  };
  function falling(w, o, dt) {
    o.vy = (o.vy || 0) + 700 * dt;
    const floor = D.surfaceBelow(w, o.x, o.y);
    o.y += o.vy * dt;
    if (o.y >= floor - (o.type === "rockfall" ? 16 : o.type === "hanging" ? 40 : 5)) {
      o.y = floor - (o.type === "rockfall" ? 16 : o.type === "hanging" ? 40 : 5);
      o.falling = false; o.vy = 0;
      if (o.type === "rockfall") {
        o.alive = false;
        w.addSolid(o.x - 16, floor - 32, 32, 32, "rubble", { hp: 2, maxHp: 2 });
        w.effect("dust", o.x, floor - 15, "#bba887", { r: 42, life: .8 });
      }
      if (o.type === "hanging") {
        o.landed = true; w.shake = 7;
        if (o.ore) w.count("hangingSolved");
        w.enemies.forEach(function (e) {
          if (e.alive && Math.abs(e.x - o.x) < 80) D.damageEnemy(w, e, 200, 1, 100, "boulder");
        });
        if (!o.ore) {
          w.addSolid(o.x - 36, floor - 48, 72, 48, "rubble", { hp: 5, maxHp: 5 }); o.alive = false;
        }
        w.effect("dust", o.x, floor - 25, "#bba887", { r: 70, life: 1.2 });
      }
    }
  }
  function collapse(w, o, dt) {
    const nearby = w.players.some(function (p, i) { return (i === 0 || w.coop) && !p.down && Math.abs(p.x - o.x) < 125; });
    const pillar = w.objects.find(function (p) { return p.id === o.pillarId; });
    if (o.stage === "idle" && (o.pillar ? pillar && !pillar.alive : nearby)) {
      o.stage = "warning"; o.timer = 3.5; w.count("collapseTriggered");
      w.note("顶板落灰！有撤离窗口，支撑柱只能推迟坠落。", "hazard");
    }
    if (o.stage !== "warning") return;
    const support = w.objects.find(function (s) { return s.alive && s.type === "support" && Math.abs(s.x - o.x) < 145; });
    o.timer -= dt * (support ? .15 : 1);
    if (Math.sin(w.time * 14) > .95 && w.effects.length < 100) {
      w.effect("dust", o.x + Math.sin(w.time * 19) * 85, o.y, "#c5b390", { r: 13, life: .8 });
    }
    if (o.timer <= 0) {
      o.stage = "fallen";
      for (let i = 0; i < 7; i++) {
        w.addObject("rockfall", o.x - 85 + i * 27, o.y - i % 3 * 25,
          { falling: true, vy: 0, r: 16 });
      }
      w.count("collapses"); w.note("顶板坠落，等碎岩落稳再清障。", "hazard");
    }
  }
  D.stepEnvironment = function (w, dt) {
    w.objects.slice().forEach(function (o) {
      if (!o.alive) return;
      if (o.ttl !== undefined) { o.ttl -= dt; if (o.ttl <= 0) { o.alive = false; return; } }
      if (o.falling) falling(w, o, dt);
      if (o.type === "collapse") collapse(w, o, dt);
      if (o.type === "hanging" && !o.falling && !o.landed) {
        const connectors = w.solids.filter(function (s) { return s.kind === "connector"; });
        const connected = connectors.length && connectors.every(function (s) { return s.hp > 0; });
        if (!connected) {
          connectors.forEach(function (s) { s.hp = 0; });
          if (!o.warning) w.note("连接岩失稳，矿团落灰晃动；立即离开下方！", "hazard");
          o.warning = (o.warning || 0) + dt;
          const dustStep = Math.floor(o.warning * 12);
          if (dustStep !== o.dustStep) {
            o.dustStep = dustStep;
            w.effect("chips", o.x + Math.sin(dustStep * 7) * 18, o.y - 35, "#b58b62", { r: 20, life: .5 });
          }
          if (o.warning > .9) { o.falling = true; w.note("连接岩已断，悬块开始坠落！", "hazard"); }
        }
      }
      if (o.type === "nest") {
        if (o.stage === "hidden" && w.visible(o)) {
          o.stage = "warning"; o.timer = 2.5; w.count("nestsRevealed");
          w.note("巢穴揭露，壳内传来刮擦声。", "hazard");
        }
        o.sealed = Math.max(0, o.sealed - dt);
        if (o.stage === "warning" && o.sealed <= 0) {
          o.timer -= dt;
          if (o.timer <= 0 && o.spawned < o.quota) {
            w.enemy(o.x - 30, 520); o.spawned++; o.timer = 6;
            w.note("一只幼虫出巢，本点最多" + o.quota + "只。", "hazard");
          }
          if (o.spawned >= o.quota) o.stage = "spent";
        }
      }
      if (o.type === "cocoon" && o.active) {
        if (w.players.some(function (p, i) { return (i === 0 || w.coop) && !p.down && D.distance(p, o) < 65; })) D.wakeCocoon(w, o);
      }
      if (o.type === "vine") {
        w.enemies.forEach(function (e) {
          if (e.alive && Math.abs(e.x - o.x) < 25 && Math.abs(e.y - o.tipY) < 45) {
            e.trapped = .4;
            if (!o.caught) { o.caught = e.id; w.count("vineSolved"); w.note("吊丝缠住了幼虫。", "hazard"); }
          }
        });
        w.players.forEach(function (p, i) {
          if ((i === 0 || w.coop) && !p.down && Math.abs(p.x - o.x) < 22 && Math.abs(p.y - o.tipY) < 45) p.trapped = .4;
        });
      }
      if (o.type === "heavy" && o.attached) {
        const p = w.players[w.active], q = w.players[1 - w.active];
        if (p.down || q.down) { o.attached = false; p.carry = false; q.carry = false; return; }
        o.x = p.x + p.facing * 28; o.y = p.y - 14; q.x = p.x - p.facing * 42; q.y = p.y;
      }
    });
    const aliveCount = w.objects.filter(function (o) { return o.alive; }).length;
    if (aliveCount > 150) w.note("场景对象已较多，可R重置。", "hint");
    w.objects = w.objects.filter(function (o) {
      return o.alive || ["pillar", "nest", "heavy"].includes(o.type);
    });
    w.effects.forEach(function (e) { e.age += dt; });
    w.effects = w.effects.filter(function (e) { return e.age < e.life; });
    w.shake = Math.max(0, w.shake - dt * 20);
  };
})(window.DN);
