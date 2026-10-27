(function (D) {
  "use strict";
  D.updateGoals = function (w) {
    const s = w.stats;
    if (s.pillarMined && w.players.some(function (p) { return !p.down && Math.abs(p.x - 700) > 170; })) s.pillarEscaped = 1;
    if (s.emptyCocoons && s.collected >= 1) s.cocoonSolved = 1;
    if (s.collected >= 1 && (s.mimicIdentified || (s.mimicAwake && s.kills))) s.mimicSolved = 1;
    if (s.refugeRead && s.collected >= 1) s.refugeVisited = 1;
    if (s.nestsRevealed && s.kills >= 2) s.nestCleared = 1;
    if (s.delivered >= 3 && (s.netCatches || s.vineSolved || s.pins)) s.huntComplete = 1;
    if (s.delivered >= 4 && s.scans && s.highVisits) s.exploreComplete = 1;
    const goal = w.scene.goal;
    if (!w.finished && (s[goal.key] || 0) >= goal.count) {
      w.finished = true;
      w.note("实验目标完成。可以继续尝试另一种处理方式，或记录人工评审。", "complete");
      if (D.onSceneComplete) D.onSceneComplete(w);
    }
  };
  D.step = function (w, inputs, dt) {
    if (w.paused) return;
    dt = Math.min(dt, 1 / 30); w.time += dt;
    D.stepFeatures(w, dt);
    w.players.forEach(function (p, i) {
      if (i === 0 || w.coop) D.stepPlayer(w, p, inputs[i] || {}, dt);
    });
    D.stepProjectiles(w, dt);
    D.stepEnemies(w, dt);
    D.stepEnvironment(w, dt);
    D.updateGoals(w);
    const p = w.players[w.active];
    const zoom = w.zoom || 1;
    w.camera.x += (D.clamp(p.x - (w.reference ? 240 : 150), 0, w.width - 960 / zoom) - w.camera.x) * Math.min(1, dt * 8);
    w.camera.y += (D.clamp(p.y - (w.reference ? 250 : w.viewY), 0, w.height - 540 / zoom) - w.camera.y) * Math.min(1, dt * 8);
  };
})(window.DN);
