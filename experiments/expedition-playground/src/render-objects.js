(function (D) {
  "use strict";
  let hangingRock;
  function drawRock(c, o, w) {
    if (o.type === "rockfall") { D.drawNative(c, "rubble" + o.id % 4, 0, 16, 2); return; }
    if (!hangingRock) {
      const size = 46, mask = new Uint8Array(size * size);
      for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
        const dx = x - 22, dy = y - 22;
        mask[y * size + x] = dx * dx * .9 + dy * dy < (20 + Math.sin(y * .37) * 2) ** 2 ? 1 : 0;
      }
      hangingRock = D.bakePixelRock(size, size, mask, D.rockToneField(size, size));
    }
    if (o.warning && !o.falling) c.translate(Math.round(Math.sin(w.time * 40) * 2), 0);
    c.drawImage(hangingRock, -46, -46, 92, 92);
    if (o.ore) {
      const remaining = 3 - (w.stats.hangingMined || 0);
      for (let i = 0; i < remaining; i++) D.drawMineral(c, "gold", -28 + i * 16, -10 + i % 2 * 18, i + 8, 2);
    }
    c.fillStyle = "#261d14";
    for (let y = -34; y < 21; y += 4) c.fillRect(Math.round(Math.sin(y * .15) * 4) * 2, y, 2, 4);
  }
  function pillar(c, o) {
    c.fillStyle = "#35291d"; c.fillRect(-19, -185, 38, 250);
    c.fillStyle = "#5c4632"; c.fillRect(-17, -184, 5, 246);
    c.fillStyle = "#463526"; c.fillRect(9, -184, 8, 246);
    for (let y = -170; y < 48; y += 24) D.drawMineral(c, "gold", -10 + y % 16, y, Math.abs(y) % 16, 2);
    c.fillStyle = "#261d14";
    for (let y = -180; y < 60; y += 4) c.fillRect(Math.round(Math.sin(y * .09) * 3) * 2, y, 2, 4);
  }
  function ladder(c, o) {
    const top = Math.round((o.top - o.y) / 2) * 2;
    c.fillStyle = "#393c3b"; c.fillRect(-13, top, 6, -top); c.fillRect(7, top, 6, -top);
    c.fillStyle = "#96704b"; c.fillRect(-11, top, 2, -top); c.fillRect(9, top, 2, -top);
    for (let y = -10; y > top; y -= 16) {
      c.fillStyle = "#393c3b"; c.fillRect(-12, y, 24, 5);
      c.fillStyle = "#9ba29b"; c.fillRect(-9, y, 18, 2);
      c.fillStyle = "#656b68"; c.fillRect(-9, y + 2, 18, 2);
    }
  }
  function support(c, o) {
    const lean = o.ttl < 5 ? Math.round(Math.sin(o.ttl * 14)) * 2 : 0;
    c.fillStyle = "#393c3b"; c.fillRect(-6, -166, 12, 166);
    c.fillStyle = "#9ba29b"; c.fillRect(-4, -165, 2, 160);
    c.fillStyle = "#656b68"; c.fillRect(-2, -165, 5, 160);
    c.fillStyle = "#69462e"; c.fillRect(-10, -86, 20, 22);
    c.fillStyle = "#b29566"; c.fillRect(-10, -86, 20, 3);
    c.fillStyle = "#393c3b"; c.fillRect(-28 + lean, -175, 56, 10); c.fillRect(-18, -6, 36, 6);
    c.fillStyle = "#9ba29b"; c.fillRect(-26 + lean, -175, 52, 2); c.fillRect(-16, -6, 32, 2);
    if (o.ttl < 6) { c.fillStyle = "#c17858"; c.fillRect(6, -80, 3, 4); }
  }
  D.drawObject = function (c, o, w) {
    if (!o.alive || !w.visible(o)) return;
    c.save(); c.translate(Math.round(o.x), Math.round(o.y));
    if (D.drawFeatureObject(c, o, w)) { c.restore(); return; }
    const t = w.time;
    if (o.type === "torch") D.drawNative(c, "torch" + Math.floor(t * 9) % 7, 0, 0, 2);
    else if (o.type === "nest") {
      D.drawNative(c, o.stage === "spent" ? "burrow-empty" : "burrow-occupied", 0, 29, 2);
      if (o.sealed > 0) D.drawPixel(c, "foam-prop", 0, 8, 2);
    } else if (o.type === "cocoon") {
      const frame = o.active ? (Math.floor(t * 2) % 3 ? "intact" : "pulse") : o.hp < 3 ? "cracked" : "empty";
      D.drawNative(c, "egg-" + frame, 0, 30, 2);
    } else if (o.type === "mimic") {
      D.drawNative(c, o.identified && Math.floor(t * 2) % 2 ? "mimic-warning" : "mimic-dormant", 0, 29, 2);
    } else if (["ore", "crystal", "resonance"].includes(o.type)) {
      D.drawNative(c, "rubble" + o.id % 4, 0, 28, 2);
      const mineral = o.type === "ore" ? "gold" : "diamond";
      D.drawMineral(c, mineral, -16, -6, o.id % 16, 2);
      D.drawMineral(c, mineral, 0, 8, (o.id + 7) % 16, 2);
      if (o.type === "resonance") { c.fillStyle = "#84768c"; c.fillRect(0, 0, 2, 8); }
    } else if (o.type === "sample") D.drawPixel(c, "sample-prop", 0, 28, 2);
    else if (o.type === "fungus") D.drawPixel(c, "fungus" + o.id % 3, 0, 25, 2);
    else if (o.type === "exit") D.drawPixel(c, "exit-prop", 0, 32, 2);
    else if (o.type === "sign") D.drawPixel(c, "sign-prop", 0, 28, 2);
    else if (o.type === "trace") D.drawPixel(c, "trace-prop", 0, 22, 2);
    else if (o.type === "ladder") ladder(c, o);
    else if (o.type === "support") support(c, o);
    else if (o.type === "pillar") pillar(c, o);
    else if (["hanging", "rockfall"].includes(o.type)) drawRock(c, o, w);
    else if (o.type === "vine") {
      c.fillStyle = "#393c3b";
      const top = -220, bottom = o.tipY - o.y;
      for (let y = top; y < bottom; y += 4) {
        const x = Math.round(Math.sin(y * .04 + t * .6)) * 2;
        c.fillRect(x, y, 4, 4);
        if (y % 12 === 0) { c.fillStyle = "#99b18c"; c.fillRect(x, y, 2, 2); c.fillStyle = "#393c3b"; }
      }
      D.drawPixel(c, "vine-mouth", 0, bottom, 2);
    } else if (["cache", "crate"].includes(o.type)) {
      D.drawPixel(c, "crate-prop", 0, 32, 2);
      if (o.damaged) { c.fillStyle = "#261d14"; c.fillRect(-8, -8, 4, 28); c.fillRect(-4, 8, 8, 4); }
    } else if (o.type === "heavy") {
      drawRock(c, Object.assign({}, o, { ore: true }), w);
      D.drawMineral(c, "iron", -12, -24, 7, 2);
    } else if (o.type === "winchBase") D.drawPixel(c, "winch", 0, 0, 2);
    else if (o.type === "shield") {
      c.fillStyle = "#393c3b"; c.fillRect(-10, -58, 20, 58);
      c.fillStyle = "#656b68"; c.fillRect(-8, -56, 16, 54);
      c.fillStyle = "#b29566"; c.fillRect(-8, -42, 16, 4); c.fillRect(-8, -16, 16, 4);
    } else if (o.type === "spores") {
      c.globalAlpha = Math.min(.5, o.ttl * .12); c.fillStyle = "#99b18c";
      for (let i = 0; i < 28; i++) {
        const x = Math.sin(i * 7.1 + t * .6) * o.r, y = Math.cos(i * 3.7 + t * .4) * o.r * .6;
        c.fillRect(Math.round(x / 2) * 2, Math.round(y / 2) * 2, 4, 4);
      }
    } else if (o.type === "net") D.drawPixel(c, "net-prop", 0, 0, 2);
    else if (o.type === "foam") { c.globalAlpha = Math.min(1, o.ttl); D.drawPixel(c, "foam-prop", 0, 0, 2); }
    else if (o.type === "lamp") D.drawPixel(c, "lamp-prop", 0, 16, 2);
    else if (["glow", "flare", "lure", "seismic", "charge"].includes(o.type)) {
      const id = o.type === "lure" ? "seismic" : o.type === "flare" ? "glow" : o.type;
      D.drawPixel(c, id, 0, -10, 2);
      if (o.type === "flare") { c.fillStyle = "#f4e2b5"; c.fillRect(-1, -24, 2, 6); }
      if (["lure", "seismic"].includes(o.type)) {
        c.globalAlpha = .6 * (1 - t % 1); c.strokeStyle = "#b29566";
        c.strokeRect(-16 - t % 1 * 25, -16, 32 + t % 1 * 50, 24);
      }
    }
    c.restore();
  };
})(window.DN);
