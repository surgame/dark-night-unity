(function (D) {
  "use strict";
  D.useItem = function (w, p, type, dt) {
    if (type === "medkit") { D.revive(w, p, dt, 2.2); return; }
    if (p.cooldown > 0 || p.down) return;
    p.cooldown = D.items[type].rate;
    if (D.useFeatureItem(w, p, type)) return;
    if (["pistol", "shotgun", "nail", "charge", "bomb"].includes(type)) D.featureNoise(w, p, 22);
    if (D.useWeapon(w, p, type)) return;
    const origin = { x: p.x, y: p.y - 12 };
    if (type === "pick") {
      const hit = D.ray(w, origin, p.aim, 92, true, true);
      w.effect("swing", origin.x, origin.y, "#ddcfb2", { angle: Math.atan2(p.aim.y - origin.y, p.aim.x - origin.x), life: .2 });
      if (hit.kind === "solid") {
        if (hit.target.kind === "rootgate") w.note("根幕会随光照退缩，试着照亮上方根结。", "hint");
        else if (hit.target.kind === "bedrock") w.note("深色基岩不可挖，浅色裂岩可以开采。", "hint");
        else D.breakSolid(w, hit.target, 1, "pick");
      }
      if (hit.kind === "enemy") D.damageEnemy(w, hit.target, 14, hit.dx, 80, "pick");
      if (hit.kind === "object") D.mineObject(w, p, hit.target, "pick");
      return;
    }
    if (type === "scanner") {
      const wall = D.ray(w, origin, p.aim, 125, false, false);
      const mimic = w.near(p, "mimic", 230);
      if (wall.kind !== "solid" && !mimic && !w.near(p, "cocoon", 230)) {
        w.note("探测器需要靠近岩面、茧群或可疑矿样。", "hint"); return;
      }
      w.count("scans"); w.scanUntil = w.time + 5;
      w.scanOrigin = wall.kind === "solid" ? wall.point : origin;
      w.effect("ring", w.scanOrigin.x, w.scanOrigin.y, "#85cdd2", { r: 260, life: 1.3 });
      if (mimic) { mimic.identified = true; w.count("mimicIdentified"); w.note("回波异常：矿样内部有活动迹象。", "discovery"); }
      else w.note("局部回波：有矿物反射和空腔轮廓，无法确认全部内容。", "discovery");
      return;
    }
    if (["ladder", "support", "shield", "seismic"].includes(type) && !p.grounded) {
      w.note("需要站稳后部署这件装备。", "hint"); return;
    }
    if (type === "ladder") {
      const x = Math.round((p.x + D.clamp(p.aim.x - p.x, -100, 100)) / 16) * 16;
      let top = Math.max(130, p.y - 290);
      for (let y = p.y - 5; y > top; y -= 8) {
        if (w.solidAt(x, y)) { top = y + 8; break; }
      }
      if (p.y - top < 60) { w.note("此列被岩壁阻挡，请在高台侧面搭梯。", "hint"); return; }
      if (w.objects.filter(function (o) { return o.alive && o.type === "ladder"; }).length >= 6) {
        w.note("本场景最多6架梯具，靠近梯脚E回收。", "hint"); return;
      }
      w.addObject("ladder", x, p.y, { top: top, r: 15 });
      w.count("ladders"); w.note("梯具已架设；W/S攀爬，Space跳离，梯脚E回收。", "equipment");
    }
    if (type === "support") {
      w.addObject("support", p.x + p.facing * 35, p.y, { ttl: 18, maxTtl: 18, r: 14 });
      w.count("supports"); w.note("临时支撑已架设，开裂后会失效。", "equipment");
    }
    if (type === "shield") {
      const x = p.x + p.facing * 42;
      if (w.solidAt(x, p.y - 20)) { w.note("放置空间被岩壁占据。", "hint"); return; }
      w.addObject("shield", x, p.y, { hp: 100, maxHp: 100, r: 12 });
      w.count("shields"); w.note("挡板展开；会阻挡双方，靠近E回收。", "equipment");
    }
    if (type === "seismic") {
      w.addObject("seismic", p.x + p.facing * 32, p.y - 8, { ttl: 22, r: 18 });
      w.count("seismicPlaced"); w.note("地震诱饵开始脉冲，附近地面生物正在响应。", "equipment");
    }
    if (type === "winch") {
      const base = w.near(p, "winchBase", 105);
      const crate = w.objects.find(function (o) { return o.alive && o.type === "crate"; });
      if (base && crate) {
        base.attached = true; crate.linked = true;
        w.note("牵引索已连接，在底座附近按住E提升，松开保持高度。", "equipment");
      } else w.note("靠近固定绞盘底座才能挂接井底矿箱。", "hint");
    }
    if (type === "sample") {
      const sample = w.near(p, "sample", 90);
      if (sample) { D.collect(w, p, sample, true); w.note("活性样本已完整密封，回入口E交付。", "mining"); }
      else w.note("靠近蓝绿色活性样本才能密封收取。", "hint");
    }
    const limit = { support: 8, shield: 8, seismic: 6 };
    if (limit[type]) {
      const placed = w.objects.filter(function (o) { return o.alive && o.type === type; });
      if (placed.length > limit[type]) placed[0].alive = false;
    }
  };
  D.revive = function (w, p, dt, seconds) {
    const friend = w.players.find(function (other) {
      return other !== p && other.down && D.distance(p, other) < 85;
    });
    if (!friend) return;
    friend.revive += dt / seconds;
    if (friend.revive >= 1) {
      friend.down = false; friend.hp = 65; friend.revive = 0; friend.invulnerable = 4;
      w.count("revives"); w.note("队友已恢复行动；演示救援完成。", "rescue");
    }
  };
})(window.DN);
