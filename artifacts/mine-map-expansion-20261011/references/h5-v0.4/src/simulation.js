(function () {
  'use strict';
  const D = window.DN;
  D.newGame = function (layout='baseline') {
    return {
      map: D.createMap(layout), player: { x: 8.5 * D.T, y: 16 * D.T, vx: 0, vy: 0, w: 16, h: 30, facing: 1, grounded: true, coyote: .1, invulnerable: 0, fuel: 1 },
      keys: {}, mouse: { x: 600, y: 350, down: false, active: false }, camera: { x: 0, y: 90 },
      time: 0, started: false, panel: 'introScreen', zone: 0, zoneTime: 8,
      tool: 1, flashlight: true, fast: false, charges: 2, ladderKits: 4, buildProgress: 0, cargo: { iron: 0, gold: 0 }, bank: 0, delivered: { iron: 0, gold: 0 },
      jetpack: false, health: 5, target: null, blast: 0, hitTime: 0, particles: [], bags: [], lift: null, visited: new Set(['camp']),
      events: [], metrics: { firstOre: null, firstGold: null, deepAt: null, returns: 0, damage: 0, rescues: 0, fastSeconds: 0, mined: 0, liftUses: 0, gateOpened: [], wallsBroken: 0, hiddenRevealed: 0, nestsOpened: 0, laddersBuilt: 0, laddersRecovered: 0, cluesSeen: 0 },
      prompt: '', shake: 0, toastTime: 0, dustTime: 0
    };
  };
  D.aim=function(s){const p=s.player;return s.mouse.active?Math.atan2(s.mouse.y+s.camera.y-(p.y-20),s.mouse.x+s.camera.x-p.x):(p.facing>0?0:Math.PI);};
  D.record = function (s, kind, text, extra = {}) {
    s.events.push({ time: +s.time.toFixed(2), kind, text, ...extra });
    if (s.events.length > 512) s.events.shift();
    if (D.addJournal) D.addJournal(text);
  };
  D.notify = function (s, text) { s.toastTime = 3.2; if (D.showToast) D.showToast(text); };
  D.particles = function (s, x, y, color, count = 12) {
    for (let i = 0; i < count; i++) {
      const n = D.hash(Math.floor(s.time * 1000) + i, count);
      s.particles.push({ x, y, vx: (n % 180 - 90), vy: -(n % 130 + 35), life: .4 + (n % 35) / 100, color });
    }
    if (s.particles.length > 180) s.particles.splice(0, s.particles.length - 180);
  };
  function moveAxis(s, axis, amount) {
    const p = s.player;
    const count = Math.max(1, Math.ceil(Math.abs(amount) / 8)), delta = amount / count;
    for (let i = 0; i < count; i++) {
      p[axis] += delta;
      const rects = D.blockingRects(s.map, p.x - p.w / 2, p.y - p.h, p.w, p.h);
      rects.forEach(r => {
        if (axis === 'x') { p.x = delta > 0 ? Math.min(p.x, r.x - p.w / 2) : Math.max(p.x, r.x + r.w + p.w / 2); p.vx = 0; }
        else if (delta > 0) { p.y = Math.min(p.y, r.y); p.grounded = true; p.vy = 0; }
        else if (delta < 0) { p.y = Math.max(p.y, r.y + r.h + p.h); p.vy = 0; }
      });
    }
  }
  function motion(s, dt) {
    const p = s.player, k = s.keys;
    p.invulnerable = Math.max(0, p.invulnerable - dt);
    if (s.lift) {
      const l = s.lift;
      const target = l.path[l.index], dx = target.x - p.x, dy = target.y - p.y, d = Math.hypot(dx, dy), step = 570 * dt;
      if (d <= step) { p.x = target.x; p.y = target.y; l.index++; if (l.index >= l.path.length) { s.lift = null; D.notify(s, p.y < 40 * D.T ? '回到上层。左侧矿道通往入口。' : '抵达深层。随时可以搭车返回。'); } }
      else { p.x += dx / d * step; p.y += dy / d * step; }
      p.vx = 0; p.vy = 0; return;
    }
    if (D.climb && D.climb(s,dt)) return;
    const direction = (k.KeyD || k.ArrowRight ? 1 : 0) - (k.KeyA || k.ArrowLeft ? 1 : 0);
    const speed = k.ShiftLeft || k.ShiftRight ? 245 : 165;
    const desired = direction * speed;
    p.vx += D.clamp(desired - p.vx, -1300 * dt, 1300 * dt);
    if (direction) p.facing = direction;
    p.coyote = p.grounded ? .12 : Math.max(0, p.coyote - dt);
    if (s.jumpBuffer > 0) {
      s.jumpBuffer -= dt;
      if (p.coyote > 0) { p.vy = -432; p.coyote = 0; p.grounded = false; s.jumpBuffer = 0; s.jumpAge = 0; }
    }
    s.jumpAge = (s.jumpAge || 0) + dt;
    if (s.jetpack && k.Space && s.jumpAge > .2 && !p.grounded && p.fuel > 0) {
      p.vy = Math.max(-230, p.vy - 1600 * dt); p.fuel = Math.max(0, p.fuel - dt / 2.5);
      D.particles(s, p.x, p.y - 4, '#eba16b', 1);
    } else if (p.grounded) p.fuel = Math.min(1, p.fuel + dt * .9);
    p.vy = Math.min(570, p.vy + 1070 * dt);
    moveAxis(s, 'x', p.vx * dt);
    const oldY = p.y; p.grounded = false; moveAxis(s, 'y', p.vy * dt);
    if (!k.KeyS && p.vy >= 0) s.map.platforms.forEach(r => {
      if (p.x + p.w / 2 > r.x && p.x - p.w / 2 < r.x + r.w && oldY <= r.y + .1 && p.y >= r.y) { p.y = r.y; p.vy = 0; p.grounded = true; }
    });
    p.x = D.clamp(p.x, D.T + p.w, D.width - D.T - p.w);
  }
  D.findTarget = function (s) {
    if(s.tool===4)return{type:'ladder',object:D.planLadder(s)};
    const p = s.player, origin = { x: p.x, y: p.y - 20 };
    const mouse = { x: s.mouse.x + s.camera.x, y: s.mouse.y + s.camera.y };
    let best = null, score = Infinity;
    const wall = (s.tool===1||s.tool===3) && D.wallTarget ? D.wallTarget(s) : null;
    if(wall&&wall.type==='wall')return wall;
    if (s.tool === 1) s.map.ores.forEach(o => {
      if (!o.amount || (D.oreExposed&&!D.oreExposed(s.map,o)) || D.dist(origin, o) > 82 || !D.lineClear(s.map, origin, o)) return;
      const d = D.dist(mouse, o);
      if (d < 65 && d < score) { best = { type: 'ore', object: o }; score = d; }
    });
    if (s.tool === 3) s.map.gates.forEach(g => {
      if (g.open || g.id === 'hatch' || D.gateDistance(p, g) > 80) return;
      const point = { x: D.clamp(mouse.x, g.x, g.x + g.w), y: D.clamp(mouse.y, g.y, g.y + g.h) };
      const d = D.dist(mouse, point);
      if (d < 95 && d < score && D.lineClear(s.map, origin, point, 5)) { best = { type: 'gate', object: g }; score = d; }
    });
    // 触屏采矿按钮自动选最近可达矿物／裂岩；桌面始终以鼠标指向为准。
    if (s.touchMining && !best) {
      if (s.tool === 1) {
        const o = s.map.ores.filter(o => o.amount && (!D.oreExposed||D.oreExposed(s.map,o)) && D.dist(origin, o) < 82 && D.lineClear(s.map, origin, o)).sort((a,b) => D.dist(origin,a)-D.dist(origin,b))[0];
        if (o) best = { type: 'ore', object: o };
      } else if (s.tool === 3) {
        const g = s.map.gates.find(g => !g.open && g.id !== 'hatch' && D.gateDistance(p, g) < 80);
        if (g) best = { type: 'gate', object: g };
      }
    }
    return best || (s.tool===1 ? wall : null);
  };
  function mining(s, dt) {
    const target = D.findTarget(s);
    if (!target || !s.target || target.object.id !== s.target.object.id) { s.blast = 0; s.buildProgress = 0; }
    s.target = target;
    const operating = s.mouse.down && target && !s.lift;
    s.map.worms.forEach(w => { w.noise = Math.max(0, w.noise - dt * (operating ? .2 : 2.2)); });
    if (!operating) return;
    const o = target.object;
    if(target.type==='ladder'){
      if(!o.valid)return;s.buildProgress+=dt/.75;
      if(s.buildProgress>=1){D.placeLadder(s,o);s.buildProgress=0;}
      return;
    }
    if(target.type==='bedrock')return;
    if(target.type==='wall'){
      if(s.tool===3){
        if(s.charges<=0)return;s.blast+=dt;
        if(s.blast>=1.3){s.charges--;s.blast=0;s.shake=.3;D.excavate(s,o,true);}
      }else{
        o.progress+=dt/(s.fast?.36:.68);
        if(s.fast)s.metrics.fastSeconds+=dt;
        s.map.worms.forEach(w=>{if(Math.abs(o.y-w.floor)<70&&Math.abs(o.x-w.anchor)<230&&w.mode==='sleep')w.noise+=dt*(s.fast?3.5:1);});
        if(o.progress>=1)D.excavate(s,o);
      }
      return;
    }
    if (target.type === 'gate') {
      if (s.charges <= 0) { s.prompt = '炸药已用完；仍可沿原台阶返回。'; return; }
      s.blast += dt;
      if (s.blast < 1.3) return;
      o.open = true; s.charges--; s.blast = 0; s.shake = .35;
      D.particles(s, o.x + o.w / 2, s.player.y - 20, '#c4b9a0', 35);
      s.metrics.gateOpened.push(o.id);
      if (o.id === 'shortcut') {
        s.map.gates.find(g => g.id === 'hatch').open = true;
        s.map.platforms.push({ x: 18 * D.T, y: 16 * D.T, w: 5 * D.T });
        D.record(s, 'shortcut', '裂岩后是旧矿车。回程环已连通。');
        D.notify(s, '捷径已通！站在井口按 E 搭矿车回上层。');
      } else { D.record(s, 'secret', '晶脉没有骗人：裂岩后藏着安全富矿。'); D.notify(s, '隐藏晶脉室。这里没有敌人，可以安心开采。'); }
      s.map.worms.forEach(w => { if (D.dist(s.player, w) < 280) w.noise += 3; });
      return;
    }
    const cycle = s.fast ? .46 : .95;
    o.progress += dt / cycle; s.hitTime += dt;
    if (s.fast) s.metrics.fastSeconds += dt;
    s.map.worms.forEach(w => {
      if (Math.abs(o.y - w.floor) < 70 && Math.abs(o.x - w.anchor) < 230 && w.mode === 'sleep') w.noise = Math.min(5, w.noise + dt * (s.fast ? 3.5 : 1));
    });
    if (s.hitTime > .22) { s.hitTime = 0; D.particles(s, o.x, o.y, o.kind === 'gold' ? '#e5bd70' : '#86b7bc', 3); }
    if (o.progress < 1) return;
    o.progress -= 1; o.amount--; s.cargo[o.kind]++; s.metrics.mined++;
    if (s.metrics.firstOre === null) { s.metrics.firstOre = +s.time.toFixed(2); D.record(s, 'first-ore', '第一笔铁矿入袋。随时可以回入口交矿。'); }
    if (o.kind === 'gold' && s.metrics.firstGold === null) { s.metrics.firstGold = +s.time.toFixed(2); D.record(s, 'first-gold', '发现了金矿。现在可以选择再挖一点，或者撤回。'); }
    if (!o.amount) D.record(s, 'depleted', `${o.kind === 'gold' ? '金' : '铁'}矿簇采空，已装入货袋。`, { oreId: o.id });
  }
  function worms(s, dt) {
    const p = s.player;
    s.map.worms.forEach(w => {
      w.cooldown = Math.max(0, w.cooldown - dt);
      if (w.mode === 'sleep' && w.noise >= 3.7 && !w.cooldown) {
        w.mode = 'warning'; w.timer = 1.4; w.target = D.clamp(p.x, w.anchor - w.radius, w.anchor + w.radius);
        D.record(s, 'warning', '岩壁传来敲击。停工、换位置，别站在原处。'); D.notify(s, '敲击声越来越近……停工并退开！');
      } else if (w.mode === 'warning') {
        w.timer -= dt;
        if (w.timer <= 0) { w.mode = 'rush'; w.timer = 1.2; }
      } else if (w.mode === 'rush') {
        w.timer -= dt;
        const next=w.x+D.clamp(w.target-w.x,-270*dt,270*dt);
        if(!D.blockingRects(s.map,next-11,w.floor-16,22,16).length)w.x=next;
        if (!s.lift && !p.invulnerable && Math.abs(p.y - w.floor) < 30 && Math.abs(p.x - w.x) < 28) {
          s.metrics.damage++; s.health--; p.invulnerable = 2; p.vx = (p.x >= w.x ? 1 : -1) * 190; p.vy = -220; s.shake = .25;
          D.particles(s, p.x, p.y - 20, '#df8268', 15);
          D.notify(s, '被撞到了。矿还在，跳上台阶或离开这片矿簇。');
          if (s.health <= 0) {
            if (s.cargo.iron || s.cargo.gold) s.bags.push({ x: p.x, y: w.floor, ...s.cargo });
            s.cargo = { iron: 0, gold: 0 }; s.health = 5; p.x = 8.5 * D.T; p.y = 16 * D.T; p.vx = 0; p.vy = 0; s.metrics.rescues++;
            D.record(s, 'rescue', '救援将你带回入口。货袋留在原处，仍能拾回。'); D.notify(s, '已回入口；遗落货袋可按 E 拾回。');
          }
        }
        if (w.timer <= 0 || Math.abs(w.x - w.target) < 3) { w.mode = 'return'; w.noise = 0; w.cooldown = 4; }
      } else if (w.mode === 'return') {
        w.x += D.clamp(w.anchor - w.x, -80 * dt, 80 * dt);
        if (Math.abs(w.x - w.anchor) < 2) w.mode = 'sleep';
      }
    });
  }
  D.nearCamp = s => s.player.x < 17 * D.T && s.player.y < 18 * D.T;
  D.interact = function (s) {
    if (s.panel || !s.started || s.lift) return;
    const p = s.player;
    if (D.nearCamp(s)) { D.openCamp(s); return; }
    const bag = s.bags.find(b => D.dist(p, b) < 64);
    if (bag) { s.cargo.iron += bag.iron; s.cargo.gold += bag.gold; s.bags.splice(s.bags.indexOf(bag), 1); D.record(s, 'recover-bag', '遗落的货袋找回了。'); return; }
    if (s.metrics.gateOpened.includes('shortcut')) {
      const lower = { x: 123 * D.T, y: 67 * D.T }, upper = { x: 20.5 * D.T, y: 16 * D.T };
      const bottom = { x: lower.x, y: 75 * D.T };
      if (D.dist(p, lower) < 85 || D.dist(p, bottom) < 85 || D.dist(p, upper) < 80) {
        const up = D.dist(p, upper) >= 80;
        s.lift = { index: 0, path: up ? [D.dist(p,bottom)<85?bottom:lower, bottom, { x: upper.x, y: bottom.y }, upper] : [upper, { x: upper.x, y: bottom.y }, bottom, lower] };
        s.metrics.liftUses++; D.record(s, 'lift', up ? '搭矿车沿旧轨道回到上层。' : '搭矿车直接进入深层。'); return;
      }
    }
    if(D.inspectClue&&D.inspectClue(s))return;
    const sign = s.map.signs.find(v => D.dist(p, v) < 72);
    if (sign) { D.record(s, 'clue', sign.note); D.notify(s, sign.note); }
  };
  function prompt(s) {
    const p = s.player;
    s.prompt = '';
    if (s.lift) { s.prompt = '矿车正在运行 · 手电仍可转向'; return; }
    if(s.target&&s.target.type==='ladder'){
      const o=s.target.object,onLadder=s.map.ladders.some(l=>Math.abs(p.x-l.x)<27&&p.y>=l.top-18&&p.y<=l.bottom+10);
      s.prompt=onLadder&&!o.valid?'W/S 攀爬 · 空格 + 方向键跳离 · 落脚后 X 回收':o.valid?`${o.reason} · 按住左键搭建 ${Math.floor(s.buildProgress*100)}% · X 回收`:o.reason;
      return;
    }
    if (D.nearCamp(s)) { s.prompt = 'E 交矿 / 整备 · 可以随时继续探索'; return; }
    if (s.bags.some(b => D.dist(p, b) < 64)) { s.prompt = 'E 拾回遗落货袋'; return; }
    if (s.metrics.gateOpened.includes('shortcut') && (D.dist(p, {x:123*D.T,y:67*D.T}) < 85 || D.dist(p,{x:123*D.T,y:75*D.T}) < 85 || D.dist(p,{x:20.5*D.T,y:16*D.T}) < 80)) { s.prompt = 'E 搭乘矿车升降捷径'; return; }
    if (s.target) {
      const o = s.target.object;
      if(s.target.type==='wall')s.prompt=s.tool===3?`裂纹岩 · 按住左键爆破 ${Math.floor(s.blast/1.3*100)}%`:`裂纹岩 · 按住左键挖墙 ${Math.floor(o.progress*100)}%`;
      else if(s.target.type==='bedrock')s.prompt='基岩不可挖 · 试试有浅色裂纹的岩层';
      else s.prompt = s.target.type === 'ore' ? `${o.kind === 'gold' ? '金矿' : '铁矿'} · 剩余 ${o.amount} · 按住左键${s.fast ? '快速' : '稳妥'}开采` : `${o.title} · 按住左键装药 ${Math.min(100,Math.floor(s.blast/1.3*100))}%`;
      return;
    }
    const gate = s.map.gates.find(g => !g.open && g.id !== 'hatch' && D.gateDistance(p, g) < 105);
    if (gate) { s.prompt = `${gate.title} · 切换 3 号炸药，瞄准裂岩`; return; }
    if(D.nearClue&&D.nearClue(s)){s.prompt='E 观察岩壁痕迹 · 去向仍需自己探索';return;}
    const sign = s.map.signs.find(v => D.dist(p, v) < 64);
    if (sign) s.prompt = 'E 阅读路牌';
    else if(s.map.ladders.some(l=>Math.abs(p.x-l.x)<36&&p.y>=l.top-15&&p.y<=l.bottom+10))s.prompt='W/S 攀爬 · 空格 + 方向键跳上岩台';
  }
  D.update = function (s, dt) {
    if (!s.started || s.panel) return;
    s.time += dt; s.zoneTime = Math.max(0, s.zoneTime - dt); s.toastTime = Math.max(0, s.toastTime - dt); s.shake = Math.max(0, s.shake - dt);
    motion(s, dt); mining(s, dt); worms(s, dt); prompt(s);
    const zone = D.zoneAt(s.player,s.map);
    if (zone !== s.zone) { s.zone = zone; s.zoneTime = 5; D.record(s, 'zone', `进入${D.zones[zone].title}。`); }
    if (zone === 2 && s.metrics.deepAt === null) s.metrics.deepAt = +s.time.toFixed(2);
    const room = D.roomAt(s.player,s.map);
    if (room && !s.visited.has(room.id)) { s.visited.add(room.id); D.record(s, 'room', `发现${room.name.replace(/^\d+ /,'')}。`,{roomId:room.id,deadEnd:room.degree===1}); }
    s.particles.forEach(p => { p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 420 * dt; p.life -= dt; });
    s.particles = s.particles.filter(p => p.life > 0);
  };
})();
