(async function (D) {
  "use strict";
  const byId = function (id) { return document.getElementById(id); };
  const canvas = byId("game"), context = canvas.getContext("2d");
  try { await D.loadProjectAssets(); }
  catch (error) { byId("loading").textContent = error.message + "。请使用完整离线入口。"; return; }
  D.makePixelArt();
  const input = new D.Input(canvas);
  let category = "new", query = "", lastHud = 0, noticeTimer, lastTime = 0, accumulator = 0;
  let soundContext, soundEnabled = false;
  D.notify = function (text) {
    byId("notification").textContent = text; byId("notification").classList.add("visible");
    clearTimeout(noticeTimer); noticeTimer = setTimeout(function () { byId("notification").classList.remove("visible"); }, 3000);
  };
  D.sound = function (type) {
    if (!soundEnabled || !soundContext) return;
    const frequencies = { combat: 140, hazard: 90, mining: 450, discovery: 660, complete: 880, blast: 65, equipment: 290 };
    if (!frequencies[type]) return;
    const oscillator = soundContext.createOscillator(), gain = soundContext.createGain(), now = soundContext.currentTime;
    oscillator.type = type === "blast" ? "triangle" : "sine"; oscillator.frequency.setValueAtTime(frequencies[type], now);
    oscillator.frequency.exponentialRampToValueAtTime(frequencies[type] * .65, now + .15);
    gain.gain.setValueAtTime(.045, now); gain.gain.exponentialRampToValueAtTime(.001, now + .2);
    oscillator.connect(gain); gain.connect(soundContext.destination); oscillator.start(); oscillator.stop(now + .22);
  };
  function renderCatalog() {
    const filtered = D.catalog.filter(function (s) {
      return (category === "all" || category === "new" && s.feature || s.category === category) &&
        (s.name + s.description + s.tags.join("")).toLowerCase().includes(query);
    });
    byId("scene-list").innerHTML = filtered.length ? filtered.map(function (s) {
      const run = D.runs[s.id], review = D.reviews[s.id];
      const done = run && run.everComplete && run.identity === (D.buildIdentity || D.version);
      const reviewed = review && review.decision !== "pending";
      return '<button class="scene-button' + (D.world && D.world.scene.id === s.id ? " active" : "") +
        '" data-scene="' + s.id + '" aria-current="' + (D.world && D.world.scene.id === s.id ? "true" : "false") + '">' +
        '<span class="scene-number">' + s.code + '</span><span class="scene-text"><strong>' + s.name +
        '</strong><small>' + s.tags.join(" / ") + '</small></span><span class="scene-state ' +
        (reviewed ? "reviewed" : done ? "done" : "") + '">' + (reviewed ? "◆" : done ? "✓" : "·") + "</span></button>";
    }).join("") : '<p class="empty-results">没有匹配的实验。换个关键词试试。</p>';
    byId("catalog-count").textContent = filtered.length;
    byId("review-count").textContent = Object.values(D.reviews).filter(function (r) { return r.decision !== "pending"; }).length + " 项已记录";
    byId("completion-count").textContent = Object.values(D.runs).filter(function (r) {
      return r.everComplete && r.identity === (D.buildIdentity || D.version);
    }).length + " / " + D.catalog.length + " 完成试玩目标";
  }
  function renderKit() {
    byId("loadout").innerHTML = D.world.scene.kit.map(function (key, i) {
      const item = D.items[key];
      return '<button class="item-button' + (D.world.selected === i ? " active" : "") +
        '" data-slot="' + i + '" aria-pressed="' + (D.world.selected === i) + '" title="' + item.name +
        '"><span class="item-number">' + (i + 1) + "</span>" + D.icon(key, item.color) +
        '<span class="item-name">' + item.name + "</span></button>";
    }).join("");
  }
  function saveCurrent() { if (D.world) D.rememberRun(D.world); }
  function selectScene(id, reset) {
    const scene = D.catalog.find(function (s) { return s.id === id; }) || D.catalog[0];
    saveCurrent(); input.clear(); D.world = new D.World(scene); D.world.annotations = false;
    D.world.paused = true; D.world.started = false;
    const group = D.categories.find(function (g) { return g.id === scene.category; });
    byId("scene-code").textContent = scene.code; byId("scene-category").textContent = group.label;
    byId("scene-name").textContent = scene.name; byId("scene-subtitle").textContent = scene.subtitle;
    byId("objective").textContent = scene.objective; byId("hint").textContent = scene.hint;
    byId("description").textContent = scene.description; byId("boundary").textContent = scene.boundary;
    byId("tags").innerHTML = scene.tags.map(function (t) { return "<span>" + t + "</span>"; }).join("");
    const review = D.reviews[scene.id] || { decision: "pending", notes: "" };
    byId("decision").value = review.decision; byId("notes").value = review.notes;
    byId("annotations").setAttribute("aria-pressed", "false"); byId("annotations").textContent = "标注 关";
    byId("save-state").textContent = D.storageAvailable ? "评审只保存在本浏览器，建议导出备份。" : "浏览器存储不可用；本次仍可评审，请及时导出。";
    if (review.identity && review.identity !== (D.buildIdentity || D.version)) {
      byId("save-state").textContent = "保留了旧候选的意见；本版已变化，建议重新试玩后更新评审。";
    }
    if (!reset && location.hash !== "#" + scene.id) history.replaceState(null, "", "#" + scene.id);
    renderCatalog(); renderKit(); updateHud();
    byId("loading").classList.add("hidden");
  }
  function updateHud() {
    const w = D.world, goal = w.scene.goal, value = w.stats[goal.key] || 0;
    byId("goal-status").textContent = w.finished ? "✓ 目标已完成" : "进行中 " + Math.min(value, goal.count) + "/" + goal.count;
    byId("goal-progress").firstElementChild.style.width = Math.min(100, value / goal.count * 100) + "%";
    byId("pause").textContent = w.started === false ? "开始" : w.paused ? "继续" : "暂停";
    byId("feature-state").textContent = w.feature ? D.featureStatus(w) : "已有机制对照 · 可从左侧切换本轮新特性";
    byId("coop").textContent = "双角色 " + (w.coop ? "开" : "关");
    byId("coop").setAttribute("aria-pressed", String(w.coop));
    byId("switch-player").textContent = "切换 P" + (w.active + 1);
  }
  function slot(index) {
    if (index >= D.world.scene.kit.length) return;
    D.world.selected = index; renderKit();
  }
  function toggleCoop() {
    const w = D.world;
    if (w.coop && w.players.some(function (p) { return p.down; })) {
      D.notify("此场景有倒地队友，救起后可关闭双角色。"); return;
    }
    w.coop = !w.coop;
    if (w.coop) {
      const p = w.players[w.active], q = w.players[1 - w.active];
      if (D.distance(p, q) > 350) { q.x = p.x - 45; q.y = p.y; }
    } else {
      const heavy = w.objects.find(function (o) { return o.alive && o.type === "heavy"; });
      if (heavy) heavy.attached = false;
      w.players.forEach(function (p) { p.carry = false; }); w.active = 0;
    }
    input.clear(); updateHud();
  }
  function switchPlayer() {
    if (!D.world.coop) { D.notify("先开启双角色，再用Tab切换操控。"); return; }
    D.world.active = 1 - D.world.active; input.clear(); updateHud();
  }
  function help(open) {
    if (open) { D.world.paused = true; input.clear(); byId("help-dialog").showModal(); }
    else { byId("help-dialog").close(); D.world.paused = false; canvas.focus({ preventScroll: true }); }
  }
  D.handleShortcut = function (code) {
    if (byId("help-dialog").open) return;
    if (["KeyA", "KeyD", "KeyW", "KeyS", "Space", "KeyE"].includes(code) && D.world.started === false) {
      D.world.started = true; D.world.paused = false;
    }
    if (/^Digit[1-4]$/.test(code)) slot(Number(code.slice(5)) - 1);
    if (code === "KeyR") selectScene(D.world.scene.id, true);
    if (code === "KeyF") D.world.players[D.world.active].light = !D.world.players[D.world.active].light;
    if (code === "Escape") { D.world.started = true; D.world.paused = !D.world.paused; input.clear(); updateHud(); }
    if (code === "Tab") switchPlayer();
  };
  D.onSceneComplete = function (w) { D.rememberRun(w); renderCatalog(); };
  byId("scene-list").addEventListener("click", function (e) {
    const button = e.target.closest("[data-scene]");
    if (button) {
      selectScene(button.dataset.scene);
      canvas.focus({ preventScroll: true });
      byId("canvas-wrap").scrollIntoView({ block: "nearest" });
    }
  });
  document.querySelectorAll("[data-category]").forEach(function (b) {
    b.addEventListener("click", function () {
      category = b.dataset.category;
      document.querySelectorAll("[data-category]").forEach(function (other) {
        other.classList.toggle("active", other === b); other.setAttribute("aria-pressed", String(other === b));
      }); renderCatalog();
    });
  });
  byId("search").addEventListener("input", function (e) { query = e.target.value.trim().toLowerCase(); renderCatalog(); });
  byId("loadout").addEventListener("click", function (e) { const b = e.target.closest("[data-slot]"); if (b) slot(Number(b.dataset.slot)); });
  byId("reset").onclick = function () { selectScene(D.world.scene.id, true); };
  byId("pause").onclick = function () { D.world.started = true; D.world.paused = !D.world.paused; updateHud(); };
  canvas.addEventListener("pointerdown", function () {
    if (D.world.started === false) { D.world.started = true; D.world.paused = false; input.mouseDown = false; }
  });
  document.querySelectorAll("[data-hold]").forEach(function (b) {
    b.addEventListener("pointerdown", function () { D.world.started = true; D.world.paused = false; });
  });
  byId("coop").onclick = toggleCoop; byId("switch-player").onclick = switchPlayer;
  byId("annotations").onclick = function () {
    D.world.annotations = !D.world.annotations;
    this.textContent = "标注 " + (D.world.annotations ? "开" : "关");
    this.setAttribute("aria-pressed", String(D.world.annotations));
  };
  byId("sound").onclick = function () {
    try {
      soundContext = soundContext || new (window.AudioContext || window.webkitAudioContext)();
      soundContext.resume(); soundEnabled = !soundEnabled;
      this.textContent = "声音 " + (soundEnabled ? "开" : "关"); this.setAttribute("aria-pressed", String(soundEnabled));
    } catch (error) { D.notify("此浏览器不支持声音，仍可正常试玩。"); }
  };
  byId("previous").onclick = function () { selectScene(D.catalog[(D.catalog.indexOf(D.world.scene) + D.catalog.length - 1) % D.catalog.length].id); };
  byId("next").onclick = function () { selectScene(D.catalog[(D.catalog.indexOf(D.world.scene) + 1) % D.catalog.length].id); };
  byId("reference-scene").onclick = function () { selectScene("m-reference"); };
  byId("fullscreen").onclick = async function () {
    try {
      if (document.fullscreenElement) await document.exitFullscreen();
      else await byId("canvas-wrap").requestFullscreen();
    } catch (error) { D.notify("此浏览器不支持全屏，可在独立浏览器打开离线文件。"); }
  };
  byId("help").onclick = byId("help-bottom").onclick = function () { help(true); };
  byId("close-help").onclick = byId("start-playing").onclick = function () { help(false); };
  byId("help-dialog").addEventListener("cancel", function (event) { event.preventDefault(); help(false); });
  function reviewChanged() {
    D.world.paused = true; input.clear();
    D.reviewScene(D.world, byId("decision").value, byId("notes").value);
    byId("save-state").textContent = D.storageAvailable ? "已保存在本浏览器。导出可保留副本。" : "本次意见在内存中，请导出备份。";
    renderCatalog();
  }
  byId("decision").addEventListener("change", reviewChanged);
  byId("notes").addEventListener("input", reviewChanged);
  byId("export").onclick = function () { D.exportReport(D.world); D.notify("已导出全部" + D.catalog.length + "项评审和最近试玩记录。"); };
  window.addEventListener("hashchange", function () { selectScene(location.hash.slice(1)); });
  window.addEventListener("pagehide", saveCurrent);
  byId("build-info").textContent = "PROJECT MATERIALS / OFFLINE\n" + (D.buildIdentity || D.version);
  selectScene(location.hash.slice(1) || "s-vents");
  function frame(timestamp) {
    const elapsed = lastTime ? Math.min(.1, (timestamp - lastTime) / 1000) : 1 / 60;
    lastTime = timestamp; accumulator += elapsed;
    while (accumulator >= 1 / 60) {
      D.step(D.world, input.sample(D.world, 1 / 60), 1 / 60); accumulator -= 1 / 60;
    }
    D.render(context, D.world);
    if (timestamp - lastHud > 200) { updateHud(); lastHud = timestamp; }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})(window.DN);
