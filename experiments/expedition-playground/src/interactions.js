(function (D) {
  "use strict";
  D.interact = function (w, p, dt, pressed) {
    const base = w.near(p, "winchBase", 110);
    if (base && base.attached) {
      const crate = w.objects.find(function (o) { return o.alive && o.type === "crate"; });
      if (crate && crate.linked && !crate.raised) {
        crate.y = Math.max(crate.targetY, crate.y - 75 * dt);
        if (crate.y <= crate.targetY) {
          crate.raised = true; w.count("winchRaised");
          w.note("矿箱抵达交接线，回入口可确认回收。", "transport");
        }
      }
      return;
    }
    const downed = w.players.some(function (q) { return q !== p && q.down && D.distance(p, q) < 85; });
    if (downed) { D.revive(w, p, dt, 6); return; }
    if (!pressed) return;
    const charges = w.objects.filter(function (o) { return o.alive && o.type === "charge" && o.owner === p; });
    if (charges.length) {
      charges.forEach(function (o) { o.alive = false; D.blast(w, o.x, o.y, o.direction, p, true); });
      return;
    }
    const exit = w.near(p, "exit", 95);
    if (exit) {
      const cargo = p.cargo;
      D.featureDeliver(w, p);
      w.count("delivered", cargo); w.count("samplesDelivered", p.samples); w.count("fungiDelivered", p.fungi);
      p.cargo = 0; p.samples = 0; p.fungi = 0;
      if (w.stats.heavyBroken && cargo) w.count("heavyDelivered");
      const heavy = w.objects.find(function (o) { return o.alive && o.type === "heavy" && o.x < 230; });
      if (heavy) { heavy.alive = false; w.count("heavyDelivered"); w.count("delivered", 8); }
      if (w.stats.winchRaised && w.stats.revives) w.count("cargoComplete");
      w.count("deliveries");
      w.note(cargo ? "已交付" + cargo + "份演示货物。" : "入口确认完成；所有收益仅属于本H5。", "delivery");
      return;
    }
    if (D.featureInteract(w, p)) return;
    const near = w.objects.filter(function (o) {
      return o.alive && w.visible(o) && D.distance(D.center(p), o) < 85 &&
        ["sign", "trace", "cache", "cocoon", "heavy", "ladder", "shield", "glow"].includes(o.type);
    }).sort(function (a, b) { return D.distance(p, a) - D.distance(p, b); })[0];
    if (!near) { w.note("附近没有可交互目标；装药、底座、队友、入口与箱子使用E。", "hint"); return; }
    if (near.type === "sign") {
      if (near.refuge) w.count("refugeRead");
      w.note(near.detail || near.label, "discovery");
    }
    if (near.type === "trace") {
      w.count("tracesRead"); near.read = true;
      w.effect("ring", near.points[0], near.points[1], "#d8bf7e", { r: 60, life: 2 });
      w.note("旧钻头指向右侧浅色岩面；标记写着：里层还有一团。", "discovery");
    }
    if (near.type === "cache") {
      near.alive = false; w.count("caches");
      p.cargo += near.damaged ? 1 : 3;
      w.count("collected", near.damaged ? 1 : 3);
      w.note(near.damaged ? "损坏补给箱：找回1份物资。" : "补给箱打开：找回3份物资，可继续试用装备。", "discovery");
    }
    if (near.type === "cocoon") {
      near.checked = true;
      if (near.active) w.note("壳内有规律脉动，靠近或攻击会惊醒。", "discovery");
      else { w.count("emptyCocoons"); w.note("这是一只空茧，内壁已经干枯。", "discovery"); }
    }
    if (near.type === "heavy") {
      if (near.attached) {
        near.attached = false; w.players.forEach(function (q) { q.carry = false; });
        w.note("重矿已放下。", "transport");
      } else {
        const helpers = w.players.filter(function (q) { return !q.down && D.distance(q, near) < 100; });
        if (w.coop && helpers.length === 2) {
          near.attached = true; w.players.forEach(function (q) { q.carry = true; });
          w.note("两人挂接完成，移动任一角色可缓慢拖运；E放下。", "transport");
        } else w.note("完整搬运需要两人靠近。可开启双人并Tab切换，或直接用镐拆碎。", "hint");
      }
    }
    if (["ladder", "shield", "glow"].includes(near.type)) {
      if (near.type === "ladder" && (!p.grounded || Math.abs(p.y - near.y) > 30)) {
        w.note("站在梯脚附近才能回收梯具。", "hint"); return;
      }
      near.alive = false; w.count("recovered"); w.note("装备已回收，可重新部署。", "equipment");
    }
  };
  D.interactionHint = function (w, p) {
    if (w.feature && w.near(p, "pressureValve", 75)) return "E · 释放一档压力";
    if (w.near(p, "winchBase", 110)) return "连接后按住 E · 提升矿箱";
    if (w.players.some(function (q) { return q !== p && q.down && D.distance(p, q) < 85; })) return "按住 E 慢速协助 / 救援包加速";
    if (w.near(p, "exit", 95)) return "E · 交付 / 确认回收";
    const near = w.near(p, null, 75);
    if (near && ["sign", "trace", "cache", "cocoon", "heavy", "ladder", "shield", "glow"].includes(near.type)) {
      return "E · " + ({ sign: "读路标", trace: "查看遗留物", cache: "打开补给箱", cocoon: "检查茧壳",
        heavy: "挂接 / 放下重矿", ladder: "回收梯具", shield: "回收挡板", glow: "回收荧光棒" })[near.type];
    }
    return "";
  };
})(window.DN);
