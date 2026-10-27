(function (D) {
  "use strict";
  D.damageEnemy = function (w, enemy, damage, dx, power, cause) {
    if (!enemy.alive) return;
    enemy.hp -= damage; enemy.vx += dx * (power || 0);
    enemy.hitCooldown = .22;
    w.effect("spark", enemy.x, enemy.y - 10, "#f4ba7f", { r: 18 });
    if (enemy.hp <= 0) {
      enemy.alive = false; w.count("kills");
      w.effect("corpse", enemy.x, enemy.y, "#d1cebc",
        { mimic: !!enemy.mimic, flip: enemy.facing < 0, life: 1.2 });
      if (cause === "boulder") w.count("boulderHits");
      w.note("幼虫已清理", "combat");
    }
  };
  D.breakSolid = function (w, solid, amount, cause) {
    if (solid.kind === "bedrock" || solid.kind === "rootgate" || solid.hp <= 0) return false;
    solid.hp -= amount;
    w.effect("chips", solid.x + solid.w / 2, solid.y + solid.h / 2,
      solid.kind === "mycelium" ? "#a9cdb2" : "#bdaa87", { r: 20 });
    if (solid.hp > 0) return false;
    w.count("rocksBroken");
    if (solid.kind === "rubble") w.count("rubbleCleared");
    if (solid.kind === "mycelium" && cause === "blast") {
      w.addObject("spores", solid.x + 16, solid.y + 16, { ttl: 5, r: 115 });
      w.note("菌丝爆裂，孢子暂时遮蔽视线。", "hazard");
    }
    return true;
  };
  D.collect = function (w, p, o, full) {
    o.alive = false;
    const value = full === false ? 1 : (o.value || 1);
    p.cargo += value; w.count("collected", value);
    if (o.featureLoot) {
      p.featureCargo = p.featureCargo || {};
      p.featureCargo[o.featureLoot] = (p.featureCargo[o.featureLoot] || 0) + value;
    }
    if (o.type === "fungus") { p.fungi++; w.count("fungi"); }
    if (o.type === "sample" && full) { p.samples++; w.count("samples"); }
    if (o.type === "crystal") w.count("crystals");
    if (o.fromMycelium) w.count("myceliumLoot");
    if (w.stats.scans && o.hidden) w.count("scanDiscovery");
    if (w.stats.resonances && o.hidden) w.count("resonanceDiscovery");
    if (w.stats.tracesRead && o.hidden) w.count("traceDiscovery");
    const sealed = w.objects.some(function (n) { return n.alive && n.type === "nest" && n.sealed > 0; });
    if (sealed) w.count("foamMining");
    const supported = w.objects.some(function (s) {
      return s.alive && s.type === "support" && Math.abs(s.x - p.x) < 150;
    });
    if (supported) w.count("supportedMining");
    w.effect("text", o.x, o.y - 20, "#d8e8be", { text: "+" + value, life: 1.1 });
    w.note(full === false ? "取得普通碎料；完整度较低。" : "货物已采入演示货袋。", "mining");
  };
  D.mineObject = function (w, p, o, cause) {
    if (!o.alive) return;
    if (D.featureMine(w, p, o, cause)) return;
    if (["ore", "crystal", "fungus", "sample"].includes(o.type)) {
      o.hp--;
      w.effect("chips", o.x, o.y, "#d9c080", { r: 15 });
      if (o.hp <= 0) D.collect(w, p, o, o.type !== "sample");
    } else if (o.type === "pillar") {
      o.hp--; p.cargo++; w.count("collected");
      w.effect("chips", o.x, o.y, "#d4ac6b", { r: 25 });
      if (o.hp <= 0) { o.alive = false; w.count("pillarMined"); w.note("承重矿柱已拆，上方开始落灰！", "hazard"); }
    } else if (o.type === "resonance") {
      w.count("resonances"); w.scanUntil = w.time + 5; w.scanOrigin = { x: o.x, y: o.y };
      w.effect("ring", o.x, o.y, "#b9addb", { r: 330, life: 1.5 });
      w.note("回声勾勒出右侧墙后的一组轮廓。", "discovery");
    } else if (o.type === "vine") {
      o.hp--; w.effect("chips", o.x, o.tipY, "#b7c78c", { r: 25 });
      if (o.hp <= 0) { o.alive = false; w.count("vineSolved"); w.note("吊丝已切断，束缚解除。", "hazard"); }
    } else if (o.type === "cocoon") {
      o.hp--; o.checked = true;
      if (o.active) D.wakeCocoon(w, o);
      else { w.count("emptyCocoons"); w.note("空茧壳；没有生物。", "discovery"); if (o.hp <= 0) o.alive = false; }
    } else if (o.type === "mimic") {
      D.wakeMimic(w, o);
    } else if (o.type === "heavy") {
      o.hp--; w.effect("chips", o.x, o.y, "#d2ba8b", { r: 26 });
      if (o.hp <= 0) {
        o.alive = false; p.cargo += 3; w.count("collected", 3); w.count("heavyBroken");
        w.note("重矿拆成3份碎矿；完整搬运价值为8。", "mining");
      }
    } else if (o.type === "hanging" && o.ore) {
      p.cargo++; w.count("collected"); w.count("hangingMined");
      if (w.stats.hangingMined >= 3) { o.alive = false; w.count("hangingSolved"); }
    } else if (o.type === "nest") {
      o.hp--;
      if (o.hp <= 0) { o.alive = false; w.count("nestsDestroyed"); w.note("巢口已清理，不再出虫。", "combat"); }
    }
  };
  D.blast = function (w, x, y, direction, owner, directional) {
    D.featureNoise(w, owner, 35);
    const radius = directional ? 150 : 120;
    let broken = 0;
    w.solids.forEach(function (s) {
      const cx = s.x + s.w / 2, cy = s.y + s.h / 2;
      const dx = cx - x, dy = cy - y, distance = Math.hypot(dx, dy);
      const projection = distance ? (dx * direction.x + dy * direction.y) / distance : 1;
      if (distance < radius && (!directional || projection > .35)) {
        if (D.breakSolid(w, s, 10, "blast")) broken++;
      }
    });
    w.enemies.forEach(function (e) {
      if (e.alive && Math.hypot(e.x - x, e.y - 10 - y) < radius) D.damageEnemy(w, e, 90, Math.sign(e.x - x), 260, "blast");
    });
    w.objects.forEach(function (o) {
      if (!o.alive || Math.hypot(o.x - x, o.y - y) > radius) return;
      if (o.type === "crystal" && w.visible(o)) { D.collect(w, owner, o, false); w.count("crystals"); }
      if (o.type === "cocoon" && o.active) D.wakeCocoon(w, o);
      if (o.type === "mimic") D.wakeMimic(w, o);
      if (o.type === "amber") D.featureMine(w, owner, o, "blast");
    });
    if (directional && broken) w.count("directionalBreaks");
    w.shake = 10;
    w.effect("blast", x, y, "#f4ba72", { r: radius, life: .65 });
    w.note((directional ? "定向爆破" : "炸药爆破") + "：破开" + broken + "块岩层。", "blast");
  };
})(window.DN);
