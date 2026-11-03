(function () {
  "use strict";
  const clone = value => JSON.parse(JSON.stringify(value));
  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  const overlap = (a, b) => a.x < b.x + b.w && a.x + a.w > b.x &&
    a.y < b.y + b.h && a.y + a.h > b.y;

  // Offline authoring demo only; all coordinates are logical pixels, with Y down.
  class EcologyEngine {
    constructor(world) {
      this.source = clone(world);
      this.restart();
    }

    restart() {
      this.world = clone(this.source);
      this.surfaces = this.world.surfaces || [];
      this.ceilings = this.world.ceilings || [];
      this.walls = this.world.walls || [];
      this.objects = this.world.objects || [];
      this.flags = { miningCollected: false, bridgeBuilt: false, cargoDelivered: false,
        hasBeam: false, primaryCollected: false, primaryDelivered: false };
      this.time = 0;
      this.dropSequence = 0;
      this.transferSequence = 0;
      this.eventSequence = 0;
      this.delivered = 0;
      this.completed = false;
      this.logs = [];
      this.events = [];
      this.transfers = [];
      this.previousInput = { jump: false, interact: false };
      this.checkpoint = { ...this.world.spawn };
      this.hero = {
        ...this.world.spawn, w: 24, h: 41, vx: 0, vy: 0,
        grounded: false, facing: 1, carry: null
      };
      this.objects.forEach(object => {
        if (object.type !== "bridge" && object.type !== "beam") return;
        const linked = this.linkedSurfaces(object);
        object.built = object.built === undefined
          ? linked.every(surface => surface.enabled !== false) : object.built;
        linked.forEach(surface => { surface.enabled = object.built; });
      });
      const ground = this.groundAt(this.hero.x, this.hero.y, 8);
      if (ground) { this.hero.y = ground.y; this.hero.grounded = true; }
      this.addLog("\u4ece\u8425\u5730\u51fa\u53d1\uff0c\u6cbf\u751f\u6001\u6d1e\u7a74\u8bd5\u8d70\u3002");
      return this.snapshot();
    }

    addLog(text) {
      this.logs.push({ time: this.time, text });
      if (this.logs.length > 6) this.logs.shift();
    }

    emit(type, object) {
      this.events.push({ id: ++this.eventSequence, time: this.time, type, x: object.x, y: object.y });
      if (this.events.length > 20) this.events.shift();
    }

    linkedSurfaces(object) {
      const ids = object.surfaceIds || [object.surfaceId || object.id];
      return this.surfaces.filter(surface => ids.includes(surface.id));
    }

    surfaceY(surface, x) {
      if (surface.enabled === false) return null;
      const points = surface.points || [];
      for (let index = 1; index < points.length; index++) {
        const a = points[index - 1], b = points[index];
        if (x >= a[0] && x <= b[0] && b[0] > a[0]) {
          return a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0]);
        }
      }
      return null;
    }

    groundAt(x, feetY, tolerance) {
      let best = null;
      this.surfaces.forEach(surface => {
        const y = this.surfaceY(surface, x);
        if (y !== null && Math.abs(y - feetY) <= tolerance &&
          (!best || Math.abs(y - feetY) < Math.abs(best.y - feetY))) best = { surface, y };
      });
      return best;
    }

    ceilingAt(x) {
      const left = x - 9, right = x + 9;
      let lowest = null;
      this.ceilings.forEach(ceiling => {
        const points = ceiling.points || [];
        if (points.length < 2 || points[0][0] > right || points[points.length - 1][0] < left) return;
        for (let index = 1; index < points.length; index++) {
          const a = points[index - 1], b = points[index];
          if (b[0] < left || a[0] > right || b[0] <= a[0]) continue;
          const at = value => a[1] + (b[1] - a[1]) * (value - a[0]) / (b[0] - a[0]);
          const y = Math.max(at(Math.max(left, a[0])), at(Math.min(right, b[0])));
          lowest = lowest === null ? y : Math.max(lowest, y);
        }
      });
      return lowest;
    }

    bodyAt(x, y, cargo) {
      const h = this.hero.h + (cargo ? 18 : 0);
      return { x: x - this.hero.w / 2, y: y - h, w: this.hero.w, h };
    }

    horizontalMove(dx) {
      const hero = this.hero, oldX = hero.x, oldY = hero.y;
      const bodyHeight = hero.h + (hero.carry ? 18 : 0);
      let nextX = oldX + dx;
      const support = hero.grounded ? this.groundAt(nextX, oldY, 7) : null;
      const nextY = support ? support.y : oldY;
      this.walls.forEach(wall => {
        if (!overlap(this.bodyAt(nextX, nextY, false), wall)) return;
        if (dx > 0 && oldX + hero.w / 2 <= wall.x + 1)
          nextX = Math.min(nextX, wall.x - hero.w / 2);
        else if (dx < 0 && oldX - hero.w / 2 >= wall.x + wall.w - 1)
          nextX = Math.max(nextX, wall.x + wall.w + hero.w / 2);
        else nextX = oldX;
      });
      if (!hero.grounded && dx) this.surfaces.forEach(surface => {
        if (["ledge", "crumble", "bridge"].includes(surface.kind)) return;
        const nextFloor = this.surfaceY(surface, nextX), oldFloor = this.surfaceY(surface, oldX);
        const entersSide = nextFloor !== null && nextFloor < oldY - 0.1 &&
          nextFloor >= oldY - bodyHeight &&
          (oldFloor === null || oldFloor >= oldY - 0.1);
        if (entersSide) nextX = oldX;
      });
      const nextSupport = hero.grounded ? this.groundAt(nextX, oldY, 7) : null;
      const feetY = nextSupport ? nextSupport.y : oldY, ceiling = this.ceilingAt(nextX);
      if (ceiling !== null && feetY - bodyHeight < ceiling) {
        const oldCeiling = this.ceilingAt(oldX);
        const oldOverlap = oldCeiling === null ? 0 : Math.max(0, oldCeiling - (oldY - bodyHeight));
        const nextOverlap = ceiling - (feetY - bodyHeight);
        if (oldOverlap <= 0.01 || nextOverlap > oldOverlap + 0.01) nextX = oldX;
      }
      if (Number.isFinite(this.world.width))
        nextX = clamp(nextX, hero.w / 2, this.world.width - hero.w / 2);
      hero.x = nextX;
      if (hero.grounded) {
        const ground = this.groundAt(nextX, oldY, 7);
        if (ground) hero.y = ground.y;
        else hero.grounded = false;
      }
    }

    verticalMove(dt) {
      const hero = this.hero;
      if (hero.grounded) { hero.vy = 0; return; }
      const oldY = hero.y;
      hero.vy += 320 * dt;
      let nextY = oldY + hero.vy * dt;
      if (hero.vy >= 0) {
        this.surfaces.forEach(surface => {
          const y = this.surfaceY(surface, hero.x);
          if (y !== null && oldY <= y + 0.1 && nextY >= y && y <= nextY) {
            nextY = y; hero.grounded = true;
          }
        });
      }
      this.walls.forEach(wall => {
        if (!overlap(this.bodyAt(hero.x, nextY, false), wall)) return;
        if (hero.vy < 0 && oldY - hero.h >= wall.y + wall.h - 1) {
          nextY = Math.max(nextY, wall.y + wall.h + hero.h); hero.vy = 0;
        } else if (hero.vy >= 0 && oldY <= wall.y + 1) {
          nextY = Math.min(nextY, wall.y); hero.grounded = true;
        }
      });
      if (hero.vy < 0) {
        const ceiling = this.ceilingAt(hero.x);
        const bodyHeight = hero.h + (hero.carry ? 18 : 0);
        if (ceiling !== null && nextY - bodyHeight < ceiling) {
          nextY = ceiling + bodyHeight;
          hero.vy = 0;
        }
      }
      hero.y = nextY;
      if (hero.grounded) hero.vy = 0;
    }

    canSee(object) {
      const x1 = this.hero.x, y1 = this.hero.y - this.hero.h / 2;
      const x2 = object.x, y2 = object.y - (object.targetOffset || 10);
      return !this.walls.some(wall => {
        for (let i = 1; i < 12; i++) {
          const t = i / 12, x = x1 + (x2 - x1) * t, y = y1 + (y2 - y1) * t;
          if (x > wall.x + 1 && x < wall.x + wall.w - 1 &&
            y > wall.y + 1 && y < wall.y + wall.h - 1) return true;
        }
        return false;
      });
    }

    cargoSpaceBlocked() {
      const ceiling = this.ceilingAt(this.hero.x);
      return ceiling !== null && this.hero.y - this.hero.h - 18 < ceiling;
    }

    pickupBlockedNearby() {
      return !this.hero.carry && this.cargoSpaceBlocked() && this.objects.some(object =>
        !object.collected && (object.type === "ore" || object.type === "looseOre") &&
        this.hero.y - object.y <= 32 &&
        Math.hypot(object.x - this.hero.x, (object.y - this.hero.y) * 0.7) <= (object.reach || 62));
    }

    target() {
      const hero = this.hero;
      const pickupFits = !this.cargoSpaceBlocked();
      const candidates = this.objects.filter(object =>
        !object.collected && ["ore", "looseOre", "bridge", "beam", "stash", "supply", "chute"].includes(object.type) &&
        (!(object.type === "ore" || object.type === "looseOre") ||
          (pickupFits && hero.y - object.y <= 32)) &&
        (!(object.type === "bridge" || object.type === "beam") || (!object.built && !object.building)));
      if (this.world.exit) candidates.push({ id: "__exit", type: "stash", ...this.world.exit });
      return candidates.map(object => ({
        object, distance: Math.hypot(object.x - hero.x, (object.y - hero.y) * 0.7)
      })).filter(item => item.distance <= (item.object.reach || item.object.radius ||
        (item.object.type === "chute" ? 50 : 62)) && this.canSee(item.object))
        .sort((a, b) => {
          const priority = item => hero.carry
            ? ({ stash: -1000, chute: -500 }[item.object.type] || 0) : 0;
          return priority(a) - priority(b) || a.distance - b.distance;
        })[0]?.object || null;
    }

    dropCargo() {
      const hero = this.hero;
      let x = hero.x, y = hero.y;
      if (!hero.grounded) {
        const below = this.surfaces.map(surface => this.surfaceY(surface, hero.x))
          .filter(value => value !== null && value >= hero.y).sort((a, b) => a - b);
        if (below.length) y = below[0];
        else { x = this.checkpoint.x; y = this.checkpoint.y; }
      }
      this.objects.push({
        ...hero.carry, id: "dropped-" + (++this.dropSequence), type: "looseOre",
        cargoId: hero.carry.cargoId || hero.carry.id, x, y, collected: false, name: hero.carry.name
      });
      hero.carry = null;
      this.addLog("\u5df2\u653e\u4e0b\u77ff\u5757\uff0c\u7a7a\u624b\u53ef\u901a\u8fc7\u4f4e\u9876\u3002");
    }

    interact() {
      const hero = this.hero, target = this.target();
      if (hero.carry && target?.type === "chute" && target.target) {
        if (this.transfers.length >= 16) {
          this.addLog("\u8d27\u69fd\u8fd0\u8f93\u4e2d\uff0c\u8bf7\u7a0d\u5019\u518d\u6295\u5165\u3002");
          return;
        }
        this.transfers.push({
          id: "transfer-" + (++this.transferSequence),
          cargo: { ...hero.carry, id: "dropped-" + (++this.dropSequence),
            cargoId: hero.carry.cargoId || hero.carry.id, type: "looseOre", collected: false },
          from: { x: target.x, y: target.y }, to: { ...target.target },
          startedAt: this.time, duration: 0.85
        });
        hero.carry = null;
        this.emit("chute", target);
        this.addLog("\u77ff\u5757\u6b63\u5728\u6cbf\u8d27\u69fd\u8fd0\u8f93\uff0c\u62b5\u8fbe\u540e\u53ef\u53d6\u56de\u3002");
        return;
      }
      if (hero.carry && (!target || target.type !== "stash")) { this.dropCargo(); return; }
      if (!target) return;
      if (target.type === "stash") {
        if (!hero.carry) return;
        const primary = !!hero.carry.primary;
        hero.carry = null;
        this.delivered++;
        this.flags.cargoDelivered = true;
        if (primary) { this.flags.primaryDelivered = true; this.completed = true; }
        this.emit("delivery", target);
        this.addLog(primary ? "\u4e3b\u77ff\u5df2\u4ea4\u4ed8\uff0c\u672c\u6b21\u8bd5\u8d70\u5b8c\u6210\uff01"
          : "\u5df2\u4ea4\u4ed8\u4e00\u5757\u652f\u8def\u77ff\u77f3\uff0c\u6df1\u5904\u4e3b\u77ff\u4ecd\u9700\u53d6\u56de\u3002");
      } else if (target.type === "chute") {
        this.addLog("\u5148\u624b\u6301\u77ff\u5757\uff0c\u518d\u6309E\u6cbf\u8d27\u69fd\u9001\u51fa\u3002");
      } else if (target.type === "supply") {
        target.collected = true;
        this.flags.hasBeam = true;
        this.emit("supply", target);
        this.addLog("\u5df2\u53d6\u5f97\u4e00\u4efd\u67b6\u6865\u6728\u6599\u3002");
      } else if (target.type === "ore" || target.type === "looseOre") {
        target.collected = true;
        hero.carry = { id: target.id, cargoId: target.cargoId || target.id, type: "ore", primary: !!target.primary,
          name: target.name || "\u77ff\u5757" };
        this.flags.miningCollected = true;
        if (target.primary) this.flags.primaryCollected = true;
        this.emit(target.type === "ore" ? "mining" : "pickup", target);
        this.addLog(target.type === "ore"
          ? "\u5df2\u91c7\u4e0b\u9732\u77ff\uff0c\u624b\u6301\u8fd4\u56de\u3002"
          : "\u5df2\u91cd\u65b0\u62fe\u8d77\u77ff\u5757\u3002");
      } else {
        if (target.requireSupply && !this.flags.hasBeam) {
          this.addLog("\u9700\u5148\u53d6\u5f97\u5cb8\u8fb9\u7684\u67b6\u6865\u6728\u6599\u3002");
          return;
        }
        target.building = { startedAt: this.time, duration: 0.65 };
        if (target.requireSupply) this.flags.hasBeam = false;
        this.linkedSurfaces(target).forEach(surface => { surface.enabled = false; });
        this.emit("bridge-start", target);
        this.addLog("\u6b63\u5728\u642d\u5efa\u6865\u9762\uff0c\u8bf7\u7b49\u5f85\u5b8c\u5de5\u540e\u901a\u884c\u3002");
      }
    }

    updatePhases() {
      this.objects.filter(object => object.building).forEach(object => {
        const phase = object.building;
        if (this.time + 1e-9 < phase.startedAt + phase.duration) return;
        object.building = null;
        object.built = true;
        this.linkedSurfaces(object).forEach(surface => { surface.enabled = true; });
        this.flags.bridgeBuilt = true;
        this.emit("bridge", object);
        this.addLog("\u6865\u9762\u5df2\u642d\u5efa\u5b8c\u6210\uff0c\u73b0\u5728\u53ef\u4ee5\u901a\u8fc7\u3002");
      });
      this.transfers = this.transfers.filter(transfer => {
        if (this.time + 1e-9 < transfer.startedAt + transfer.duration) return true;
        this.objects.push({ ...transfer.cargo, ...transfer.to });
        this.emit("chute-arrive", transfer.to);
        this.addLog("\u77ff\u5757\u5df2\u62b5\u8fbe\u8d27\u69fd\u53e6\u4e00\u7aef\uff0c\u53ef\u4ee5\u53d6\u56de\u3002");
        return false;
      });
    }

    updateCrumbles(dt) {
      this.objects.filter(object => object.type === "crumble" && !object.broken).forEach(object => {
        const surfaces = this.linkedSurfaces(object);
        const standing = this.hero.grounded && surfaces.some(surface => {
          const y = this.surfaceY(surface, this.hero.x);
          return y !== null && Math.abs(y - this.hero.y) < 1;
        });
        if (!standing && !object.warning) { object.elapsed = 0; return; }
        object.elapsed = (object.elapsed || 0) + dt;
        if (!object.warning && object.elapsed >= (object.warningAfter || 0.55)) {
          object.warning = true;
          this.emit("crumble-warning", object);
          this.addLog("\u811a\u4e0b\u5ca9\u53f0\u5f00\u59cb\u6389\u6e23\uff0c\u7559\u610f\u4e0b\u65b9\u5e72\u5ca9\u53f0\u3002");
        }
        if (object.elapsed >= (object.breakAfter || 1.2)) {
          object.broken = true;
          surfaces.forEach(surface => { surface.enabled = false; });
          if (standing) this.hero.grounded = false;
          this.emit("crumble", object);
          this.addLog("\u5ca9\u53f0\u5df2\u5c40\u90e8\u5d29\u843d\uff0c\u4e0b\u65b9\u56fa\u5b9a\u5ca9\u53f0\u4ecd\u53ef\u843d\u811a\u3002");
        }
      });
    }

    step(dt, input) {
      input = input || {};
      dt = clamp(Number.isFinite(dt) ? dt : 0, 0, 0.1);
      const hero = this.hero, jump = !!input.jump && !this.previousInput.jump;
      if (input.interact && !this.previousInput.interact) this.interact();
      if (jump && hero.grounded) {
        hero.vy = -160 * (hero.carry ? 0.84 : 1);
        hero.grounded = false;
      }
      const direction = Number(!!input.right) - Number(!!input.left);
      if (direction) hero.facing = direction;
      hero.vx = direction * 112 * (hero.carry ? 0.8 : 1);
      const count = Math.max(1, Math.ceil(dt * 120)), subDt = dt / count;
      for (let index = 0; index < count; index++) {
        this.horizontalMove(hero.vx * subDt);
        this.verticalMove(subDt);
      }
      this.time += dt;
      this.updatePhases();
      this.updateCrumbles(dt);
      this.objects.filter(object => object.type === "checkpoint").forEach(object => {
        if (Math.hypot(hero.x - object.x, hero.y - object.y) < (object.reach || 38) &&
          this.checkpoint.id !== object.id) {
          this.checkpoint = { id: object.id, x: object.x, y: object.y };
          this.addLog("\u5df2\u5230\u8fbe" + (object.name || "\u4e2d\u9014\u505c\u9760\u70b9") + "\u3002");
        }
      });
      if (hero.y > (this.world.fallY || this.world.height + 100 || 900)) {
        hero.x = this.checkpoint.x; hero.y = this.checkpoint.y;
        hero.vx = 0; hero.vy = 0; hero.grounded = false;
        this.addLog("\u5df2\u8fd4\u56de\u6700\u8fd1\u505c\u9760\u70b9\uff0c\u624b\u6301\u8d27\u7269\u4fdd\u7559\u3002");
      }
      this.previousInput = { jump: !!input.jump, interact: !!input.interact };
      return this.snapshot();
    }

    snapshot() {
      const target = this.target(), hero = this.hero;
      let prompt = "";
      if (hero.carry) prompt = target?.type === "stash"
        ? "E \u4ea4\u4ed8\u77ff\u5757" : "E \u653e\u4e0b\u77ff\u5757";
      else if (target) prompt = {
        ore: "E \u91c7\u4e0b\u5e76\u62fe\u8d77\u9732\u77ff",
        looseOre: "E \u62fe\u8d77\u77ff\u5757", beam: "E \u67b6\u8bbe\u6728\u6881",
        bridge: "E \u4fee\u590d\u6865\u9762", stash: "\u5e26\u4e00\u5757\u77ff\u77f3\u56de\u6765\u4ea4\u4ed8",
        supply: "E \u62fe\u53d6\u67b6\u6865\u6728\u6599", chute: "\u624b\u6301\u77ff\u5757\u65f6E\u6295\u5165\u8d27\u69fd"
      }[target.type];
      if (hero.carry && target?.type === "chute") prompt = "E \u5c06\u77ff\u5757\u6cbf\u8d27\u69fd\u9001\u5230\u53e6\u4e00\u7aef";
      if (!target && this.pickupBlockedNearby())
        prompt = "\u8bf7\u5148\u8d70\u5230\u5f00\u9614\u5904\uff0c\u518d\u62fe\u53d6\u77ff\u5757";
      if (!prompt && this.objects.some(object => object.building &&
        Math.hypot(object.x - hero.x, object.y - hero.y) < 90))
        prompt = "\u6865\u9762\u642d\u5efa\u4e2d\uff0c\u5b8c\u5de5\u540e\u624d\u53ef\u901a\u884c";
      return {
        hero: { ...hero, carry: hero.carry && { ...hero.carry } },
        objects: this.objects.map(object => ({ ...object, building: object.building && { ...object.building } })),
        surfaces: this.surfaces.map(surface => ({ ...surface, points: surface.points.map(p => p.slice()) })),
        checkpoint: { ...this.checkpoint }, flags: { ...this.flags },
        delivered: this.delivered, completed: this.completed, time: this.time,
        prompt, target: target && { ...target }, logs: this.logs.map(log => ({ ...log })),
        events: this.events.map(event => ({ ...event })), transfers: clone(this.transfers)
      };
    }

    exportState() {
      return clone({
        version: 1, worldId: this.source.id || null, surfaceIds: this.surfaces.map(s => s.id),
        hero: this.hero, checkpoint: this.checkpoint, flags: this.flags, objects: this.objects,
        enabled: this.surfaces.map(s => s.enabled !== false), time: this.time,
        delivered: this.delivered, completed: this.completed, logs: this.logs,
        events: this.events, eventSequence: this.eventSequence, dropSequence: this.dropSequence,
        transfers: this.transfers, transferSequence: this.transferSequence
      });
    }

    validPhases(state) {
      const validPhase = (phase, duration) => phase && Number.isFinite(phase.startedAt) &&
        phase.startedAt >= 0 && phase.startedAt <= state.time && phase.duration === duration &&
        phase.startedAt + phase.duration > state.time - 1e-9;
      const transfers = state.transfers || [];
      if (!Array.isArray(transfers) || transfers.length > 16 ||
        !Number.isInteger(state.transferSequence || 0) || (state.transferSequence || 0) < 0) return false;
      const ids = new Set();
      for (const transfer of transfers) {
        if (!validPhase(transfer, 0.85) || !transfer.cargo || typeof transfer.id !== "string" ||
          typeof transfer.cargo.id !== "string" || typeof transfer.cargo.cargoId !== "string" ||
          transfer.cargo.type !== "looseOre" || transfer.cargo.collected !== false ||
          ids.has(transfer.cargo.id) || state.objects.some(object => object.id === transfer.cargo.id) ||
          !Number.isFinite(transfer.from?.x) || !Number.isFinite(transfer.from?.y) ||
          !Number.isFinite(transfer.to?.x) || !Number.isFinite(transfer.to?.y)) return false;
        ids.add(transfer.cargo.id);
      }
      for (const object of state.objects.filter(value => value.building)) {
        if (!["bridge", "beam"].includes(object.type) || object.built ||
          !validPhase(object.building, 0.65)) return false;
        const linked = this.linkedSurfaces(object);
        if (!linked.length || linked.some(surface => state.enabled[this.surfaces.indexOf(surface)])) return false;
      }
      return true;
    }

    restoreState(state) {
      if (!state || state.version !== 1 || state.worldId !== (this.source.id || null) ||
        JSON.stringify(state.surfaceIds) !== JSON.stringify(this.surfaces.map(s => s.id)) ||
        !Number.isFinite(state.hero?.x) || !Number.isFinite(state.hero?.y) ||
        !Number.isFinite(state.hero?.vx) || !Number.isFinite(state.hero?.vy) ||
        !Number.isFinite(state.checkpoint?.x) || !Number.isFinite(state.checkpoint?.y) ||
        !Array.isArray(state.enabled) || state.enabled.length !== this.surfaces.length ||
        !state.enabled.every(enabled => typeof enabled === "boolean") ||
        !Number.isFinite(state.time) || state.time < 0 ||
        !Number.isInteger(state.delivered) || state.delivered < 0 ||
        !Array.isArray(state.logs) || !Array.isArray(state.events) ||
        !Array.isArray(state.objects) || state.objects.length > 256 ||
        state.objects.some(object => !object || !Number.isFinite(object.x) ||
          !Number.isFinite(object.y)) || !this.validPhases(state)) return false;
      const saved = clone(state);
      this.hero = { ...saved.hero, w: 24, h: 41 };
      this.checkpoint = saved.checkpoint;
      this.flags = { ...this.flags, ...saved.flags };
      this.objects = saved.objects;
      this.surfaces.forEach((surface, index) => { surface.enabled = saved.enabled[index] !== false; });
      this.time = saved.time || 0;
      this.delivered = saved.delivered || 0;
      this.completed = !!saved.completed;
      this.logs = (saved.logs || []).slice(-6);
      this.events = (saved.events || []).slice(-20);
      this.eventSequence = saved.eventSequence || 0;
      this.dropSequence = saved.dropSequence || 0;
      this.transfers = saved.transfers || [];
      this.transferSequence = saved.transferSequence || 0;
      this.previousInput = { jump: false, interact: false };
      return true;
    }
  }
  window.EcologyEngine = EcologyEngine;
})();
