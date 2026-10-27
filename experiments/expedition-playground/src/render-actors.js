(function (D) {
  "use strict";
  D.drawPlayer = function (c, p, w) { drawNativePlayer(c, p, w); };
  D.drawEnemy = function (c, e, w) { if (e.alive) drawNativeEnemy(c, e, w); };
  function drawNativePlayer(c, p, w) {
    const moving = Math.abs(p.vx) > 4 || p.onLadder;
    const frame = Math.floor(w.time * 12) % 12;
    const body = moving ? "body" + frame : "bodyIdle";
    const shirt = moving ? "shirt" + frame : "shirtIdle";
    c.save();
    if (p.invulnerable > 0 && Math.floor(w.time * 12) % 2) c.globalAlpha = .5;
    if (p.down) {
      c.translate(p.x, p.y - 2); c.rotate(-Math.PI / 2);
      D.drawNative(c, "bodyIdle", 0, 10, 2); D.drawNative(c, "shirtIdle", 0, 10, 2);
      c.restore();
      c.fillStyle = "#d9a88e"; c.fillRect(p.x - 7, p.y - 30, 14, 4); c.fillRect(p.x - 2, p.y - 35, 4, 14);
      if (p.revive) { c.fillStyle = "#8ec8b0"; c.fillRect(p.x - 22, p.y - 44, 44 * p.revive, 3); }
      return;
    }
    D.drawNative(c, body, p.x, p.y, 2, p.facing < 0);
    D.drawNative(c, shirt, p.x, p.y, 2, p.facing < 0);
    const type = w.scene.kit[w.selected], item = D.items[type];
    const angle = Math.atan2(p.aim.y - (p.y - 12), p.aim.x - p.x);
    c.translate(Math.round(p.x), Math.round(p.y - 12)); c.rotate(Math.round(angle / (Math.PI / 8)) * (Math.PI / 8));
    const native = type === "pick" ? "pickaxe" : type === "pistol" ? "pistol" : type === "bomb" ? "bomb" : null;
    if (Math.cos(angle) < 0) c.scale(1, -1);
    const kick = ["pistol", "shotgun", "nail", "flare"].includes(type) && p.cooldown > item.rate - .1 ? -2 : 0;
    if (type === "pick" && p.cooldown > 0) c.rotate(-.6 + p.cooldown / item.rate * 1.2);
    c.translate(kick, 0);
    if (native) c.drawImage(D.images[native], -16, -24, 48, 48);
    else D.drawPixel(c, type, 0, 0, 2);
    c.drawImage(D.images.hand, -16, -24, 48, 48);
    if (["pistol", "shotgun", "nail"].includes(type) && p.cooldown > item.rate - .07) {
      c.drawImage(D.images.muzzle, 8, -24, 48, 48);
    }
    c.restore();
    if (w.coop) {
      c.fillStyle = p.index ? "#8cc4bc" : "#d9b278";
      c.fillRect(p.x - 5, p.y - 35, 10, 3);
      if (w.active === p.index) { c.beginPath(); c.moveTo(p.x - 4, p.y - 42); c.lineTo(p.x + 4, p.y - 42); c.lineTo(p.x, p.y - 37); c.fill(); }
    }
    if (p.trapped > 0) { c.strokeStyle = "#b5c392"; c.strokeRect(p.x - 11, p.y - 24, 22, 24); }
  }
  function drawNativeEnemy(c, e, w) {
    const frame = e.hitCooldown > 0 ? "recoil" : e.charge > 0 ? (e.mimic ? "warning" : "alert") :
      e.dashing > 0 ? "lunge" : Math.abs(e.vx) > 4 ? (Math.floor(w.time * 8) % 2 ? "crawl_a" : "crawl_b") :
      e.mimic ? "reveal" : Math.floor(w.time * 3) % 2 ? "idle_a" : "idle_b";
    D.drawNative(c, (e.mimic ? "mimic-" : "grub-") + frame, e.x, e.y, 2, e.facing < 0);
    if (e.charge > 0) {
      c.strokeStyle = "#d9ad72"; c.beginPath(); c.arc(e.x, e.y - 12, 30, 0, Math.PI * 2); c.stroke();
    }
    const netted = e.netId && e.stun > 0 && w.objects.some(function (o) { return o.id === e.netId && o.alive; });
    if (netted) D.drawPixel(c, "net-prop", e.x, e.y - 7, 2);
    if (e.trapped > 0) {
      c.fillStyle = "#85835b"; c.fillRect(e.x - 8, e.y - 24, 2, 20); c.fillRect(e.x + 8, e.y - 24, 2, 20);
    }
    if (e.stun > 0 && !netted) {
      c.fillStyle = "#bbd8bf";
      for (let i = 0; i < 3; i++) {
        c.fillRect(e.x - 12 + i * 10, e.y - 30 - (Math.floor(w.time * 5) + i) % 2 * 4, 3, 3);
      }
    }
    if (e.pin > 0) {
      c.fillStyle = "#d1cebc"; c.fillRect(e.x - 24, e.y - 13, 58, 2);
      c.fillStyle = "#656b68"; c.fillRect(e.x - 25, e.y - 16, 3, 7);
    }
    if (e.hp < e.maxHp) {
      c.fillStyle = "#334333"; c.fillRect(e.x - 23, e.y - 36, 46, 3);
      c.fillStyle = "#c6b87b"; c.fillRect(e.x - 23, e.y - 36, 46 * Math.max(0, e.hp / e.maxHp), 3);
    }
    if (e.luredBy && w.annotations) {
      const target = w.objects.find(function (o) { return o.alive && o.id === e.luredBy; });
      if (target) {
        c.save(); c.strokeStyle = "#ac92b4"; c.globalAlpha = .6; c.setLineDash([4, 6]);
        c.beginPath(); c.moveTo(e.x, e.y - 12); c.lineTo(target.x, target.y); c.stroke(); c.restore();
      }
    }
  }
  D.drawEffect = function (c, e) {
    const t = e.age / e.life;
    c.save(); c.globalAlpha = 1 - t; c.fillStyle = e.color; c.strokeStyle = e.color; c.lineWidth = 2;
    if (e.type === "corpse") {
      D.drawNative(c, e.mimic ? "mimic-fallen" : "grub-fallen", e.x, e.y, 2, e.flip);
    } else if (e.type === "coldbeam") {
      for (let i = 0; i < 18; i++) {
        const r = i / 18;
        c.fillRect(Math.round(e.x + (e.endX - e.x) * r),
          Math.round(e.y + (e.endY - e.y) * r + Math.sin(i * 7 + t * 8) * 5 * r), 3, 3);
      }
    } else if (["beam", "arc"].includes(e.type)) {
      c.beginPath(); c.moveTo(e.x, e.y);
      if (e.type === "arc") c.lineTo((e.x + e.endX) / 2, e.y - 14);
      c.lineTo(e.endX, e.endY); c.stroke();
    } else if (e.type === "text") {
      c.font = 'bold 17px "Bahnschrift", sans-serif'; c.fillText(e.text, e.x, e.y - t * 30);
    } else if (e.type === "swing") {
      c.lineWidth = 3; c.beginPath(); c.arc(e.x, e.y, 38, e.angle - 1 + t, e.angle + .6 + t); c.stroke();
    } else if (e.type === "ring" || e.type === "blast") {
      c.lineWidth = e.type === "blast" ? 6 : 2;
      c.beginPath(); c.arc(e.x, e.y, Math.max(1, e.r * t), 0, Math.PI * 2); c.stroke();
      if (e.type === "blast") {
        D.drawNative(c, "explosion" + Math.min(3, Math.floor(t * 4)), e.x, e.y + 24, 2);
      }
    } else {
      for (let i = 0; i < 8; i++) {
        const angle = i * 2.4, radius = e.r * t;
        c.fillRect(e.x + Math.cos(angle) * radius, e.y + Math.sin(angle) * radius + t * t * 15, i % 2 ? 3 : 5, 3);
      }
    }
    c.restore();
  };
})(window.DN);
