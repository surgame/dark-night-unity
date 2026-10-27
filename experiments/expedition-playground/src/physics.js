(function (D) {
  "use strict";
  D.solidTop = function (s, x) {
    return s.slope ? s.slope[0] + (s.slope[1] - s.slope[0]) * D.clamp((x - s.x) / s.w, 0, 1) : s.y;
  };
  D.overlap = function (body, s) {
    return body.x + body.r > s.x && body.x - body.r < s.x + s.w &&
      body.y > s.y && body.y - body.h < s.y + s.h;
  };
  D.moveBody = function (w, b, dt, climb) {
    b.grounded = false;
    const solids = w.solids.filter(function (s) { return s.hp > 0; });
    w.objects.forEach(function (o) {
      if (o.alive && o.type === "shield") solids.push({ x: o.x - 10, y: o.y - 58, w: 20, h: 58, hp: 1 });
    });
    b.x += b.vx * dt;
    solids.forEach(function (s) {
      if (s.slope) return;
      if (!D.overlap(b, s)) return;
      if (b.vx > 0) b.x = s.x - b.r;
      if (b.vx < 0) b.x = s.x + s.w + b.r;
      b.vx = 0;
    });
    if (!climb) b.vy += 1080 * dt;
    b.y += b.vy * dt;
    solids.forEach(function (s) {
      if (s.slope) {
        const top = D.solidTop(s, b.x);
        if (b.x >= s.x && b.x < s.x + s.w && b.vy >= 0 &&
            b.y >= top && b.y - b.h < s.y + s.h) {
          b.y = top; b.grounded = true; b.vy = 0;
        }
        return;
      }
      if (!D.overlap(b, s)) return;
      if (b.vy >= 0) { b.y = s.y; b.grounded = true; }
      else b.y = s.y + s.h + b.h;
      b.vy = 0;
    });
    b.x = D.clamp(b.x, 42, w.width - 42);
    if (b.y > w.height + 100) { b.x = 140; b.y = w.reference ? D.referenceFloor(140) : 520; b.vy = 0; }
  };
  D.stepPlayer = function (w, p, input, dt) {
    p.cooldown = Math.max(0, p.cooldown - dt);
    p.invulnerable = Math.max(0, p.invulnerable - dt);
    p.trapped = Math.max(0, p.trapped - dt);
    if (p.down) return;
    const ladder = w.objects.find(function (o) {
      return o.alive && o.type === "ladder" && Math.abs(p.x - o.x) < 23 &&
        p.y >= o.top - 8 && p.y - p.h < o.y + 5;
    });
    if (!ladder || input.jump) p.onLadder = false;
    if (ladder && (input.up || input.down) && !input.jump) p.onLadder = true;
    const climbing = ladder && p.onLadder && !input.jump;
    const speed = p.carry ? 90 : input.sprint ? 245 : 180;
    p.vx = p.trapped ? 0 : ((input.right ? 1 : 0) - (input.left ? 1 : 0)) * speed;
    if (w.feature) {
      if (!p.trapped) p.vx += p.fieldVx || 0;
      if (input.sprint && (input.left || input.right) && p.grounded) D.featureNoise(w, p, dt * 34);
    }
    if (p.vx) p.facing = Math.sign(p.vx);
    if (climbing) { p.vy = ((input.down ? 1 : 0) - (input.up ? 1 : 0)) * 145; }
    if (input.jump && (p.grounded || ladder) && !p.trapped) { p.vy = -435; w.count("jumps"); }
    D.moveBody(w, p, dt, !!climbing);
    if (input.aim) p.aim = input.aim;
    if (input.fire) D.useItem(w, p, w.scene.kit[w.selected], dt);
    if (input.interact) D.interact(w, p, dt, !!input.interactPressed);
    if (p.x > 830 && p.y <= 305 && !p.down) w.count("highVisits", w.stats.highVisits ? 0 : 1);
    const glow = w.near(p, "glow", 80);
    if (glow && glow.y > 540) w.count("glowVisits", w.stats.glowVisits ? 0 : 1);
    if (w.reference) {
      if (p.x > 650) w.stats.referenceValley = 1;
      if (p.x > 1160 && p.y > 270) w.stats.referenceEntrance = 1;
      if (p.x > 1950 && p.y > 450) w.stats.referenceCave = 1;
      if (p.x < 200 && w.stats.referenceCave && w.stats.referenceEntrance) w.stats.referenceReturned = 1;
    }
  };
  D.ray = function (w, origin, aim, length, hitEnemy, hitObject) {
    let dx = aim.x - origin.x, dy = aim.y - origin.y;
    const mag = Math.hypot(dx, dy) || 1;
    dx /= mag; dy /= mag;
    for (let t = 0; t <= length; t += 4) {
      const point = { x: origin.x + dx * t, y: origin.y + dy * t };
      const solid = w.solidAt(point.x, point.y);
      if (solid) return { kind: "solid", target: solid, point: point, dx: dx, dy: dy, distance: t };
      if (hitEnemy) {
        const enemy = w.enemies.find(function (e) {
          return e.alive && Math.hypot(e.x - point.x, e.y - e.h / 2 - point.y) < e.r + 4;
        });
        if (enemy) return { kind: "enemy", target: enemy, point: point, dx: dx, dy: dy, distance: t };
      }
      if (hitObject) {
        const object = w.objects.find(function (o) {
          return o.alive && w.visible(o) && (!Array.isArray(hitObject) || hitObject.includes(o.type)) &&
            ["ore", "crystal", "fungus", "sample", "pillar", "resonance", "vine", "cocoon", "mimic", "heavy", "hanging", "nest"].concat(D.featureTargets || []).includes(o.type) &&
            Math.hypot(o.x - point.x, (o.type === "vine" ? o.tipY : o.y) - point.y) < o.r;
        });
        if (object) return { kind: "object", target: object, point: point, dx: dx, dy: dy, distance: t };
      }
    }
    return { kind: "air", target: null, point: { x: origin.x + dx * length, y: origin.y + dy * length },
      dx: dx, dy: dy, distance: length };
  };
})(window.DN);
