(function (D) {
  "use strict";
  D.throwItem = function (w, p, type, speed) {
    const origin = { x: p.x, y: p.y - 12 };
    let dx = p.aim.x - origin.x, dy = p.aim.y - origin.y;
    const mag = Math.hypot(dx, dy) || 1; dx /= mag; dy /= mag;
    if (w.projectiles.length >= 100) return;
    w.projectiles.push({ type: type, x: origin.x, y: origin.y,
      vx: dx * speed, vy: dy * speed - (type === "flare" ? 0 : 80), age: 0, owner: p });
  };
  function bullet(w, p, type, angle) {
    const origin = { x: p.x, y: p.y - 12 };
    const base = Math.atan2(p.aim.y - origin.y, p.aim.x - origin.x) + (angle || 0);
    const end = { x: origin.x + Math.cos(base) * 650, y: origin.y + Math.sin(base) * 650 };
    const hit = D.ray(w, origin, end, type === "shotgun" ? 330 : 650, true, ["cocoon", "mimic", "amber"]);
    w.effect("beam", origin.x, origin.y, type === "nail" ? "#bcd2bf" : "#efc68c",
      { endX: hit.point.x, endY: hit.point.y, life: .13 });
    if (hit.kind === "enemy") {
      const e = hit.target;
      const damage = type === "shotgun" ? (hit.distance < 155 ? 19 : 8) : type === "nail" ? 20 : 24;
      D.damageEnemy(w, e, damage, hit.dx, type === "shotgun" ? 100 : 35, type);
      if (type === "shotgun" && !e.shotgunHit) { e.shotgunHit = true; w.count("shotgunHits"); }
      if (type === "nail" && e.alive) {
        const wall = D.ray(w, { x: e.x + hit.dx * 17, y: e.y - 10 },
          { x: e.x + hit.dx * 120, y: e.y - 10 + hit.dy * 120 }, 86, false, false);
        if (wall.kind === "solid") {
          e.pin = 4.5; e.stun = 4.5; w.count("pins");
          w.note("钉壁成功：后方岩壁限制了生物移动。", "combat");
        }
      }
    } else if (hit.kind === "solid" && hit.target.kind === "connector") {
      D.breakSolid(w, hit.target, 1, "bullet");
    } else if (hit.kind === "object") {
      if (hit.target.type === "mimic") D.wakeMimic(w, hit.target);
      if (hit.target.type === "cocoon" && hit.target.active) D.wakeCocoon(w, hit.target);
      if (hit.target.type === "amber") D.featureMine(w, p, hit.target, "bullet");
    }
  }
  D.useWeapon = function (w, p, type) {
    if (["pistol", "shotgun", "nail"].includes(type)) {
      if (type === "shotgun") [-.16, -.08, 0, .08, .16].forEach(function (a) { bullet(w, p, type, a); });
      else bullet(w, p, type, 0);
      w.effect("spark", p.x + p.facing * 15, p.y - 20, "#ffe3a6", { r: 10, life: .12 });
      return true;
    }
    if (type === "shock") {
      const hit = D.ray(w, { x: p.x, y: p.y - 12 }, p.aim, 85, true, false);
      w.effect("arc", p.x, p.y - 12, "#91dce8", { endX: hit.point.x, endY: hit.point.y, life: .22 });
      if (hit.kind === "enemy") {
        if (hit.target.charge > 0 || hit.target.dashing > 0) w.count("interrupts");
        hit.target.charge = 0; hit.target.dashing = 0; hit.target.stun = 2.8; hit.target.attack = 4;
        D.damageEnemy(w, hit.target, 8, hit.dx, 150, "shock");
        w.note("电击打断，短暂眩晕。", "combat");
      }
      return true;
    }
    if (["net", "foam", "flare", "lure", "bomb", "glow"].includes(type)) {
      D.throwItem(w, p, type, type === "flare" ? 620 : type === "foam" ? 390 : 410);
      return true;
    }
    if (type === "charge") {
      const hit = D.ray(w, { x: p.x, y: p.y - 12 }, p.aim, 98, false, false);
      if (hit.kind !== "solid" || hit.target.kind === "bedrock") {
        w.note("需要靠近浅色可挖岩面布置。", "hint"); return false;
      }
      w.addObject("charge", hit.point.x - hit.dx * 5, hit.point.y - hit.dy * 5,
        { direction: { x: hit.dx, y: hit.dy }, owner: p, r: 10 });
      w.count("chargesPlaced"); w.note("已布置定向装药，按E引爆；布置时的准星决定方向。", "equipment");
      return true;
    }
    return false;
  };
  D.stepProjectiles = function (w, dt) {
    w.projectiles.forEach(function (p) {
      if (p.dead) return;
      p.age += dt;
      const old = { x: p.x, y: p.y };
      if (p.type !== "flare") p.vy += 590 * dt;
      p.x += p.vx * dt; p.y += p.vy * dt;
      const hit = D.ray(w, old, p, Math.hypot(p.x - old.x, p.y - old.y), true, false);
      const collided = hit.kind !== "air";
      if (p.type === "bomb") {
        if (collided) {
          p.x = hit.point.x - hit.dx * 5; p.y = hit.point.y - hit.dy * 5;
          p.vx *= -.24; p.vy *= -.28;
        }
        if (p.age > 1.45) { D.blast(w, p.x, p.y, { x: 1, y: 0 }, p.owner, false); p.dead = true; }
      } else if (collided || p.age > 2 || p.y > 630) {
        p.dead = true;
        const x = collided ? hit.point.x - hit.dx * 5 : p.x;
        const y = collided ? hit.point.y - hit.dy * 5 : p.y;
        if (p.type === "flare") {
          w.addObject("flare", x, y, { light: 245, ttl: 35, r: 8 });
          w.count("flares");
          if (x > 780 && y < 400) w.count("highFlares");
        }
        if (p.type === "glow") w.addObject("glow", x, y, { light: 145, ttl: 90, falling: true, vy: 0, r: 8 });
        if (p.type === "lure") w.addObject("lure", x, y, { ttl: 12, falling: true, vy: 0, r: 12 });
        if (p.type === "net") {
          const trap = w.addObject("net", x, y, { ttl: 9, falling: true, vy: 0, r: 48 });
          if (hit.kind === "enemy") D.catchNet(w, hit.target, trap);
        }
        if (p.type === "foam") {
          w.addObject("foam", x, y, { ttl: 10, falling: true, vy: 0, r: 55 });
          if (hit.kind === "enemy") hit.target.slow = 5;
          w.objects.forEach(function (n) {
            if (n.alive && n.type === "nest" && Math.hypot(n.x - x, n.y - y) < 100) {
              n.sealed = 12; w.count("seals"); w.note("巢口已封，12秒后泡沫失效。", "equipment");
            }
          });
        }
      }
      if (p.x < 0 || p.x > w.width || p.age > 5) p.dead = true;
    });
    w.projectiles = w.projectiles.filter(function (p) { return !p.dead; });
  };
})(window.DN);
