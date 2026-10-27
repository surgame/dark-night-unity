(function (D) {
  "use strict";
  D.wakeCocoon = function (w, o) {
    if (!o.alive || !o.active) return;
    o.alive = false; w.enemy(o.x, 520); w.count("cocoonsAwake");
    w.effect("chips", o.x, o.y, "#b3b68b", { r: 35 });
    w.note("活茧破开，一只幼虫落地。", "hazard");
  };
  D.wakeMimic = function (w, o) {
    if (!o.alive) return;
    o.alive = false; w.enemy(o.x, 520, { mimic: true }); w.count("mimicAwake");
    w.note("拟态矿样展开了节肢！", "hazard");
  };
  D.catchNet = function (w, enemy, net) {
    if (enemy.netId === net.id) return;
    enemy.netId = net.id; enemy.stun = Math.max(enemy.stun, 4.5);
    if (!enemy.everNetted) { enemy.everNetted = true; w.count("netCatches"); }
    w.note("捕网困住幼虫；挣裂后会恢复移动。", "combat");
  };
  D.stepEnemies = function (w, dt) {
    w.enemies.forEach(function (e) {
      if (!e.alive) return;
      ["stun", "slow", "pin", "trapped", "hitCooldown"].forEach(function (k) { e[k] = Math.max(0, e[k] - dt); });
      const players = w.players.filter(function (p, i) { return !p.down && (i === 0 || w.coop); });
      let target = players.sort(function (a, b) { return D.distance(e, a) - D.distance(e, b); })[0];
      if (!target) return;
      const lures = w.objects.filter(function (o) {
        return o.alive && ["lure", "seismic"].includes(o.type) &&
          D.distance(e, o) < (o.type === "seismic" ? 650 : 450);
      }).sort(function (a, b) { return D.distance(e, a) - D.distance(e, b); });
      if (lures.length) {
        target = lures[0];
        if (e.luredBy !== target.id) {
          e.luredBy = target.id;
          const type = target.type;
          e.lureTypes = e.lureTypes || {};
          if (!e.lureTypes[type]) {
            e.lureTypes[type] = true; w.count(type === "seismic" ? "seismicLured" : "lured");
          }
        }
      } else e.luredBy = 0;
      const direction = Math.sign(target.x - e.x);
      const dist = Math.abs(target.x - e.x);
      const targetIsPlayer = target.h !== undefined;
      const blocked = e.stun > 0 || e.pin > 0 || e.trapped > 0;
      if (!blocked && !e.stationary) {
        e.attack -= dt;
        if (e.dashing > 0) { e.dashing -= dt; e.vx = e.dashDir * 340; }
        else if (e.charge > 0) {
          e.charge -= dt; e.vx = 0;
          if (e.charge <= 0) { e.dashing = .38; e.dashDir = direction; e.attack = 3; }
        } else if (targetIsPlayer && dist < 165 && e.attack <= 0) {
          e.charge = .85; e.vx = 0;
        } else e.vx = dist > 25 ? direction * (e.slow > 0 ? 20 : 66) : 0;
      } else e.vx *= Math.max(0, 1 - dt * 10);
      if (e.vx) e.facing = Math.sign(e.vx);
      D.moveBody(w, e, dt, false);
      w.objects.forEach(function (o) {
        if (!o.alive) return;
        if (o.type === "net" && D.distance(e, o) < 62) D.catchNet(w, e, o);
        if (o.type === "foam" && D.distance(e, o) < 65) e.slow = .7;
        if (o.type === "shield" && Math.abs(e.x - o.x) < 32 && Math.abs(e.y - o.y) < 50) {
          o.hitTimer = (o.hitTimer || 0) - dt;
          if (o.hitTimer <= 0) {
            o.hitTimer = .8; o.hp -= 12; w.count("shieldBlocks");
            w.effect("spark", o.x, o.y - 25, "#d7b486", { r: 17 });
            if (o.hp <= 0) { o.alive = false; w.note("挡板已被咬坏。", "hazard"); }
          }
        }
      });
      if (targetIsPlayer && !blocked && D.distance(e, target) < 34 && target.invulnerable <= 0) {
        target.hp = Math.max(0, target.hp - 10); target.invulnerable = 1.5;
        target.vx += direction * 100; w.count("playerHits");
        w.note("受到幼虫冲撞；HP仅是本演示的试验规则。", "hazard");
        if (target.hp <= 0) {
          if (w.coop) { target.down = true; w.note("角色倒地，可切换队友救援，或R重置本场景。", "rescue"); }
          else { target.hp = 100; target.x = 140; target.y = 520; target.cargo = 0; target.featureCargo = {}; w.count("recoveries"); w.note("已返回实验入口，场景进度保留；R可完全重置。", "rescue"); }
        }
      }
    });
  };
})(window.DN);
