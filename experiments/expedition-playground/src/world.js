(function (D) {
  "use strict";
  D.distance = function (a, b) { return Math.hypot(a.x - b.x, a.y - b.y); };
  D.clamp = function (v, a, b) { return Math.max(a, Math.min(b, v)); };
  D.center = function (a) { return { x: a.x, y: a.y - (a.h || 0) / 2 }; };
  D.World = function (scene) {
    this.scene = scene; this.width = 1536; this.height = 672; this.time = 0;
    this.serial = 0; this.solids = []; this.objects = []; this.enemies = [];
    this.projectiles = []; this.effects = []; this.events = []; this.stats = {};
    this.players = []; this.active = 0; this.coop = false; this.flashlight = true;
    this.finished = false; this.paused = false; this.zoom = 1.5;
    this.viewY = ["hanging", "boulder", "collapse", "pillar", "miner", "high", "explore"].includes(scene.layout) ? 340 : 300;
    this.camera = { x: 280, y: 520 - this.viewY };
    this.message = ""; this.messageUntil = 0; this.shake = 0; this.selected = 0;
    this.addSolid(0, 520, this.width, 152, "bedrock");
    this.addSolid(0, 0, 30, 672, "bedrock");
    this.addSolid(this.width - 30, 0, 30, 672, "bedrock");
    for (let x = 0; x < this.width; x += 32) {
      const edge = 108 + Math.round((Math.sin(x * .008) * 22 + Math.sin(x * .031) * 8) / 2) * 2;
      this.addSolid(x, 0, 32, edge, "bedrock");
    }
    this.addObject("exit", 100, 488, { label: "入口 / 交付" });
    this.addObject("lamp", 110, 436, { light: 200, color: "#e9b675" });
    this.addObject("torch", 430, 520, { light: 175 });
    this.players.push(D.makePlayer(430, 520, 0));
    this.players.push(D.makePlayer(480, 520, 1));
    D.buildScene(this);
    if (scene.layout === "rescue" || scene.layout === "cargo") {
      this.coop = true; this.players[1].x = 440; this.players[1].down = true;
      this.players[1].hp = 0; this.players[1].y = 520;
    }
    this.note("进入实验：" + scene.name, "scene");
  };
  D.makePlayer = function (x, y, index) {
    return { x: x, y: y, vx: 0, vy: 0, r: 6, h: 22, grounded: true,
      facing: 1, hp: 100, down: false, trapped: 0, invulnerable: 0,
      cooldown: 0, cargo: 0, fungi: 0, samples: 0, revive: 0,
      aim: { x: x + 150, y: y - 20 }, index: index, light: true, carry: false };
  };
  D.World.prototype.addSolid = function (x, y, w, h, kind, extra) {
    const solid = Object.assign({ id: ++this.serial, x: x, y: y, w: w, h: h,
      kind: kind || "rock", hp: 2, maxHp: 2 }, extra || {});
    this.solids.push(solid); return solid;
  };
  D.World.prototype.wall = function (x, y, cols, rows, kind, extra) {
    for (let row = 0; row < rows; row++) {
      for (let col = 0; col < cols; col++) {
        this.addSolid(x + col * 32, y + row * 32, 32, 32, kind || "rock", extra);
      }
    }
  };
  D.World.prototype.addObject = function (type, x, y, extra) {
    const object = Object.assign({ id: ++this.serial, type: type, x: x, y: y,
      r: 18, hp: 3, alive: true, hidden: false }, extra || {});
    this.objects.push(object);
    const limits = { glow: 24, flare: 12, net: 12, foam: 16, lure: 8, seismic: 6, support: 8, shield: 8, charge: 12, spores: 12 };
    if (limits[type]) {
      const live = this.objects.filter(function (o) { return o.alive && o.type === type; });
      if (live.length > limits[type]) live[0].alive = false;
    }
    return object;
  };
  D.World.prototype.enemy = function (x, y, extra) {
    const enemy = Object.assign({ id: ++this.serial, x: x, y: y, vx: 0, vy: 0,
      r: 20, h: 24, hp: 65, maxHp: 65, grounded: false, facing: -1,
      stun: 0, slow: 0, charge: 0, attack: 1.5, pin: 0, alive: true,
      luredBy: 0, trapped: 0, hitCooldown: 0 }, extra || {});
    this.enemies.push(enemy); return enemy;
  };
  D.World.prototype.count = function (key, n) {
    this.stats[key] = (this.stats[key] || 0) + (n === undefined ? 1 : n);
  };
  D.World.prototype.note = function (text, type) {
    this.message = text; this.messageUntil = this.time + 4;
    this.events.push({ at: +this.time.toFixed(2), type: type || "info", text: text });
    if (this.events.length > 180) this.events.shift();
    if (D.sound) D.sound(type || "info");
  };
  D.World.prototype.effect = function (type, x, y, color, extra) {
    if (this.effects.length >= 170) this.effects.shift();
    this.effects.push(Object.assign({ type: type, x: x, y: y, color: color,
      age: 0, life: .6, r: 10 }, extra || {}));
  };
  D.World.prototype.solidAt = function (x, y) {
    if (this.reference) return D.referenceSolidAt(this, x, y);
    return this.solids.find(function (s) {
      return s.hp > 0 && x >= s.x && x <= s.x + s.w && y >= D.solidTop(s, x) && y <= s.y + s.h;
    });
  };
  D.World.prototype.clearLine = function (a, b, ignoreId) {
    const length = D.distance(a, b);
    for (let t = 4; t < length - 6; t += 6) {
      const ratio = t / length;
      const solid = this.solidAt(a.x + (b.x - a.x) * ratio, a.y + (b.y - a.y) * ratio);
      if (solid && solid.id !== ignoreId) return false;
    }
    return true;
  };
  D.World.prototype.visible = function (o) {
    if (!o.hidden) return true;
    return !this.solidAt(o.x, o.y);
  };
  D.World.prototype.near = function (p, type, range) {
    return this.objects.filter(function (o) {
      return o.alive && (!type || o.type === type) && this.visible(o) &&
        D.distance(D.center(p), o) < range;
    }, this).sort(function (a, b) { return D.distance(p, a) - D.distance(p, b); })[0];
  };
  D.World.prototype.snapshot = function () {
    return { scene: this.scene.id, time: +this.time.toFixed(2), complete: this.finished,
      stats: Object.assign({}, this.stats), player: this.players.map(function (p) {
        return { x: +p.x.toFixed(1), y: +p.y.toFixed(1), hp: p.hp, down: p.down, cargo: p.cargo };
      }), feature: D.featureSnapshot ? D.featureSnapshot(this) : null,
      events: this.events.slice(), activeEnemies: this.enemies.filter(function (e) { return e.alive; }).length };
  };
})(window.DN);
