(function (D) {
  "use strict";
  let lightCanvas, lightContext;
  function carveLight(c, w, light) {
    const angle = light.angle, cone = light.cone || Math.PI * 2;
    c.save(); c.beginPath(); c.moveTo(light.x, light.y);
    const rays = cone < 6 ? 40 : 44;
    for (let i = 0; i <= rays; i++) {
      const a = angle - cone / 2 + cone * i / rays;
      let distance = light.radius;
      for (let d = 10; d <= light.radius; d += 12) {
        if (w.solidAt(light.x + Math.cos(a) * d, light.y + Math.sin(a) * d)) { distance = d + 8; break; }
      }
      c.lineTo(light.x + Math.cos(a) * distance, light.y + Math.sin(a) * distance);
    }
    c.closePath(); c.clip();
    const gradient = c.createRadialGradient(light.x, light.y, 2, light.x, light.y, light.radius);
    gradient.addColorStop(0, "rgba(0,0,0,.94)"); gradient.addColorStop(.45, "rgba(0,0,0,.74)");
    gradient.addColorStop(1, "rgba(0,0,0,0)");
    c.fillStyle = gradient;
    c.fillRect(light.x - light.radius, light.y - light.radius, light.radius * 2, light.radius * 2);
    c.restore();
  }
  function darkness(ctx, w) {
    if (!lightCanvas) {
      lightCanvas = document.createElement("canvas"); lightCanvas.width = 960; lightCanvas.height = 540;
      lightContext = lightCanvas.getContext("2d");
    }
    const c = lightContext;
    c.setTransform(1, 0, 0, 1, 0, 0); c.clearRect(0, 0, 960, 540);
    c.fillStyle = w.reference && w.players[w.active].x < 1050 ? "rgba(4,13,15,.12)" : "rgba(4,13,15,.60)";
    c.fillRect(0, 0, 960, 540);
    c.scale(w.zoom || 1, w.zoom || 1);
    c.translate(-w.camera.x, -w.camera.y); c.globalCompositeOperation = "destination-out";
    const lights = [];
    w.players.forEach(function (p, i) {
      if (i && !w.coop) return;
      lights.push({ x: p.x, y: p.y - 12, radius: 85, angle: 0 });
      if (p.light && !p.down) lights.push({ x: p.x, y: p.y - 18, radius: 370,
        angle: Math.atan2(p.aim.y - (p.y - 18), p.aim.x - p.x), cone: 1.35 });
    });
    w.objects.forEach(function (o) {
      if (o.alive && o.light && w.visible(o)) lights.push({ x: o.x, y: o.y - 5, radius: o.light, angle: 0 });
    });
    lights.slice(0, 24).forEach(function (light) { carveLight(c, w, light); });
    c.globalCompositeOperation = "source-over"; c.setTransform(1, 0, 0, 1, 0, 0);
    ctx.drawImage(lightCanvas, 0, 0);
  }
  function annotations(ctx, w) {
    const names = { ore: "矿", crystal: "晶簇", sample: "活性样本", nest: "巢口", cocoon: "茧",
      mimic: "可疑矿样", heavy: "完整重矿", cache: "补给箱", resonance: "共鸣晶", pillar: "承重矿柱",
      winchBase: "绞盘底座", crate: "待提升矿箱", fungus: "发光菌" };
    w.objects.forEach(function (o) {
      if (!o.alive || !w.visible(o)) return;
      if (o.type === "collapse") {
        ctx.save(); ctx.setLineDash([6, 6]); ctx.strokeStyle = "#c4aa66"; ctx.globalAlpha = .5;
        ctx.strokeRect(o.x - 110, o.y - 10, 220, 245); ctx.restore();
        D.label(ctx, o.stage === "warning" ? "落灰 · " + Math.max(0, o.timer).toFixed(1) + "s" : "不稳定顶板", o.x, o.y - 25, "#d4be7d");
      } else if (o.type === "sign" || o.type === "mark") D.label(ctx, o.label, o.x, o.y - 46, "#bfccb5", 11);
      else if (names[o.type] && !["ore", "fungus", "crystal"].includes(o.type)) {
        D.label(ctx, names[o.type] + (o.type === "nest" && o.sealed > 0 ? " · 封堵" + Math.ceil(o.sealed) + "s" : ""),
          o.x, o.y - (o.type === "pillar" ? 150 : 47), "#c3cdb7", 11);
      }
    });
  }
  D.render = function (ctx, w) {
    ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.clearRect(0, 0, 960, 540);
    ctx.imageSmoothingEnabled = false;
    const shakeX = Math.sin(w.time * 75) * w.shake;
    ctx.save(); ctx.scale(w.zoom || 1, w.zoom || 1);
    ctx.translate(-Math.round(w.camera.x) + shakeX, -Math.round(w.camera.y));
    if (w.reference) D.drawReferenceBackdrop(ctx, w);
    else D.drawBackground(ctx, w);
    w.objects.forEach(function (o) {
      if (o.type === "crate" && o.linked) {
        ctx.strokeStyle = "#afa57f"; ctx.lineWidth = 2; ctx.beginPath();
        ctx.moveTo(540, 486); ctx.lineTo(o.x, 370); ctx.lineTo(o.x, o.y - 18); ctx.stroke();
      }
      D.drawObject(ctx, o, w);
    });
    if (w.reference) D.drawReferenceFront(ctx, w);
    else D.drawCaveSurface(ctx, w);
    D.drawFeatureGeometry(ctx, w);
    w.enemies.forEach(function (e) { D.drawEnemy(ctx, e, w); });
    w.players.forEach(function (p, i) { if (i === 0 || w.coop) D.drawPlayer(ctx, p, w); });
    w.projectiles.forEach(function (p) {
      if (p.type === "bomb") D.drawNative(ctx, "bomb", p.x, p.y + 12, 1);
      else if (["glow", "lure", "flare"].includes(p.type)) D.drawPixel(ctx, p.type === "lure" ? "seismic" : "glow", p.x, p.y, 1);
      else { ctx.fillStyle = D.items[p.type].color; ctx.fillRect(Math.round(p.x), Math.round(p.y), 4, 4); }
    });
    w.effects.forEach(function (e) { D.drawEffect(ctx, e); });
    ctx.restore();
    darkness(ctx, w);
    ctx.save(); ctx.scale(w.zoom || 1, w.zoom || 1); ctx.translate(-Math.round(w.camera.x), -Math.round(w.camera.y));
    D.drawScan(ctx, w);
    if (w.annotations) annotations(ctx, w);
    const player = w.players[w.active], hint = D.interactionHint(w, player);
    if (hint) D.label(ctx, hint, player.x, player.y - 68, "#e2c58a", 12);
    ctx.restore();
    drawOverlay(ctx, w);
    if (!w.paused) D.drawFeatureHud(ctx, w);
  };
  function drawOverlay(ctx, w) {
    const player = w.players[w.active];
    ctx.fillStyle = "#d6dfcd"; ctx.font = '12px "Bahnschrift", "Microsoft YaHei", sans-serif'; ctx.textAlign = "left";
    ctx.fillText("P" + (w.active + 1) + "  " + Math.ceil(player.hp) + " HP", 22, 27);
    ctx.fillStyle = "#364d43"; ctx.fillRect(22, 37, 100, 4);
    ctx.fillStyle = player.hp < 30 ? "#d68a73" : "#b7c79a"; ctx.fillRect(22, 37, player.hp, 4);
    ctx.fillStyle = "#d1bb8c"; ctx.fillText("随身货物  " + player.cargo + "   /   已交付  " + (w.stats.delivered || 0), 22, 61);
    const item = D.items[w.scene.kit[w.selected]];
    ctx.fillStyle = item.color; ctx.fillText(item.name, 22, 84);
    ctx.fillStyle = "#bac8bc"; ctx.textAlign = "right";
    ctx.fillText(String(Math.floor(w.time / 60)).padStart(2, "0") + ":" + String(Math.floor(w.time % 60)).padStart(2, "0"), 938, 27);
    ctx.fillStyle = "#435b4c"; ctx.fillRect(740, 43, 198, 4);
    w.players.forEach(function (p, i) {
      if (i && !w.coop) return;
      ctx.fillStyle = i ? "#83bfb7" : "#e2b575"; ctx.fillRect(740 + p.x / w.width * 198, 39, 4, 12);
    });
    if (w.messageUntil > w.time) {
      ctx.textAlign = "center"; ctx.font = '13px "Microsoft YaHei", sans-serif';
      ctx.fillStyle = "rgba(9,19,19,.88)"; ctx.fillRect(65, 486, 830, 35);
      ctx.fillStyle = "#d9d9bd"; ctx.fillText(w.message, 480, 504, 805);
    }
    if (w.finished) {
      ctx.fillStyle = "rgba(15,40,33,.9)"; ctx.fillRect(725, 70, 213, 31);
      ctx.fillStyle = "#b6d6b6"; ctx.textAlign = "center"; ctx.fillText("✓ 实验目标完成 · 可继续玩", 831, 90);
    }
    if (w.paused) {
      ctx.fillStyle = "rgba(9,19,19,.62)"; ctx.fillRect(0, 0, 960, 540);
      ctx.textAlign = "center"; ctx.fillStyle = "#e0d9bc"; ctx.font = '28px "Microsoft YaHei", sans-serif';
      ctx.fillText(w.started === false ? "点击画面，开始实验" : "实验已暂停", 480, 258);
      ctx.font = '14px "Microsoft YaHei", sans-serif';
      ctx.fillText(w.started === false ? "A / D 移动 · 鼠标瞄准 · 左键使用 · E 交互" :
        "Esc 或点击继续 · R 重置当前场景", 480, 293);
    }
  }
})(window.DN);
