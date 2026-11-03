(function () {
  "use strict";
  const root = document.getElementById("cave-wfc-whitebox");
  const api = globalThis.CaveWB;
  const canvas = root.querySelector("canvas");
  const renderer = api.makeRenderer(canvas, root);
  const seedInput = root.querySelector("[data-seed]");
  const status = root.querySelector("[data-status]");
  const detail = root.querySelector("[data-detail]");
  const progress = root.querySelector("[role=progressbar]");
  const progressWrap = root.querySelector("[data-progress-wrap]");
  const generateButton = root.querySelector("[data-generate]");
  const saved = window.openai?.widgetState?.modelContent;
  const owner = `cave-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  let map = null;
  let serial = 0;
  let drag = null;
  let view = "local";

  function remember() {
    if (!map) return;
    window.openai?.setWidgetState?.({
      modelContent: { seed: map.seed, mode: renderer.state.mode, perspective: renderer.state.perspective },
      privateContent: { camera: { ...renderer.camera }, view, owner }
    })?.catch(() => {});
  }
  function mode(next) {
    renderer.state.mode = ["scene", "seams", "atlas"].includes(next) ? next : "scene";
    root.querySelectorAll("[data-mode]").forEach(button => {
      button.setAttribute("aria-pressed", String(button.dataset.mode === renderer.state.mode));
    });
    renderer.draw();
    describe();
  }
  function describe() {
    const selected = renderer.state.selection;
    if (renderer.state.mode === "atlas") {
      detail.textContent = "16 个实空拓扑；实际求解库为 256 个离散角点配置。";
    } else if (renderer.state.mode === "seams" && selected && map) {
      const tile = api.catalog[map.tiles[selected.y * map.width + selected.x]];
      detail.textContent = `格 (${selected.x}, ${selected.y}) · ${tile.mask.toString(2).padStart(4, "0")} · 四角 ${tile.values.join(" / ")} · 边 ${tile.edges.join(" / ")}`;
    } else {
      detail.textContent = renderer.state.mode === "seams"
        ? "共点数据只存一份；点选大格查看四角与 N/E/S/W 边编码。"
        : "拖动平移 · 滚轮缩放 · 顶面沿统一斜投影展开。";
    }
  }
  async function generate(seed, restoreCamera) {
    if (!Number.isInteger(seed) || seed < 0 || seed > 4294967295) {
      status.textContent = "种子须为 0 到 4294967295 的整数。";
      status.setAttribute("role", "alert");
      return;
    }
    const task = ++serial;
    root.dataset.state = "generating";
    status.setAttribute("role", "status");
    generateButton.disabled = true;
    progressWrap.hidden = false;
    progress.setAttribute("aria-valuenow", "0");
    progress.querySelector("div").style.width = "0%";
    seedInput.value = String(seed);
    status.textContent = "规划洞室与廊道…";
    await new Promise(resolve => requestAnimationFrame(resolve));
    try {
      const plan = api.makePlan(seed);
      const next = await api.solve(plan, {
        cancelled: () => task !== serial,
        pause: () => new Promise(resolve => requestAnimationFrame(resolve)),
        progress: value => {
          if (task !== serial) return;
          const percent = Math.round(value.resolved / value.total * 100);
          progress.setAttribute("aria-valuenow", String(percent));
          progress.querySelector("div").style.width = `${percent}%`;
          status.textContent = `WFC ${value.resolved} / ${value.total} 格 · 观察 ${value.observations} 次`;
        }
      });
      if (task !== serial) return;
      map = next;
      renderer.setMap(map);
      if (restoreCamera && Number.isFinite(restoreCamera.x) && Number.isFinite(restoreCamera.y) &&
          restoreCamera.zoom > .1 && restoreCamera.zoom < 300) {
        Object.assign(renderer.camera, restoreCamera);
      }
      if (!restoreCamera && view === "all") renderer.fit("all");
      renderer.draw();
      root.dataset.state = "ready";
      root.dataset.seed = String(seed);
      status.textContent = `${map.width} × ${map.height} 大格 · ${map.check.comparisons.toLocaleString()} 条共边，冲突 ${map.check.mismatches} · ${Math.round(map.stats.elapsedMs)} ms`;
      describe();
      remember();
    } catch (error) {
      if (task !== serial) return;
      root.dataset.state = "error";
      status.setAttribute("role", "alert");
      status.textContent = `生成未安装：${error.message}`;
    } finally {
      if (task === serial) { generateButton.disabled = false; progressWrap.hidden = true; }
    }
  }
  generateButton.addEventListener("click", () => generate(Number(seedInput.value)));
  root.querySelector("[data-next]").addEventListener("click", () => {
    generate(((Number(seedInput.value) || 0) + 7919) >>> 0);
  });
  seedInput.addEventListener("keydown", event => {
    if (event.key === "Enter") generate(Number(seedInput.value));
  });
  root.querySelectorAll("[data-mode]").forEach(button => button.addEventListener("click", () => {
    mode(button.dataset.mode); remember();
  }));
  root.querySelectorAll("[data-fit]").forEach(button => button.addEventListener("click", () => {
    view = button.dataset.fit;
    renderer.fit(view);
    remember();
  }));
  root.querySelector("[data-perspective]").addEventListener("change", event => {
    renderer.state.perspective = event.target.checked;
    renderer.draw();
    remember();
  });
  canvas.addEventListener("pointerdown", event => {
    if (renderer.state.mode === "atlas") return;
    drag = { x: event.clientX, y: event.clientY, startX: event.clientX, startY: event.clientY };
    canvas.setPointerCapture(event.pointerId);
  });
  canvas.addEventListener("pointermove", event => {
    if (!drag) return;
    renderer.camera.x -= (event.clientX - drag.x) / renderer.camera.zoom;
    renderer.camera.y -= (event.clientY - drag.y) / renderer.camera.zoom;
    drag.x = event.clientX; drag.y = event.clientY;
    renderer.draw();
  });
  canvas.addEventListener("pointerup", event => {
    if (!drag) return;
    if (map && renderer.state.mode === "seams" &&
        Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) < 5) {
      const box = canvas.getBoundingClientRect();
      const point = renderer.toWorld(event.clientX - box.left, event.clientY - box.top);
      renderer.state.selection = {
        x: api.clamp(Math.floor(point.x), 0, map.width - 1),
        y: api.clamp(Math.floor(point.y), 0, map.height - 1)
      };
      describe();
      renderer.draw();
    }
    drag = null;
    remember();
  });
  canvas.addEventListener("pointercancel", () => { drag = null; });
  window.addEventListener("blur", () => { drag = null; });
  canvas.addEventListener("wheel", event => {
    if (renderer.state.mode === "atlas") return;
    event.preventDefault();
    const box = canvas.getBoundingClientRect();
    const x = event.clientX - box.left, y = event.clientY - box.top;
    const before = renderer.toWorld(x, y);
    renderer.camera.zoom = api.clamp(renderer.camera.zoom * Math.exp(-event.deltaY * .0015), 2, 220);
    const after = renderer.toWorld(x, y);
    renderer.camera.x += before.x - after.x;
    renderer.camera.y += before.y - after.y;
    renderer.draw();
    remember();
  }, { passive: false });
  new ResizeObserver(() => renderer.resize()).observe(canvas);
  new MutationObserver(() => renderer.refreshPalette()).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme", "style", "class"] });
  window.addEventListener("openai:set_globals", event => {
    const snapshot = event.detail?.globals?.widgetState;
    const content = snapshot?.modelContent;
    if (!content || snapshot.privateContent?.owner === owner) return;
    renderer.state.perspective = content.perspective !== false;
    root.querySelector("[data-perspective]").checked = renderer.state.perspective;
    mode(content.mode);
    if (Number.isInteger(content.seed) && content.seed !== Number(seedInput.value)) {
      generate(content.seed, snapshot.privateContent?.camera);
    }
  });
  root.__caveDemo = { get map() { return map; }, renderer, generate };
  renderer.state.perspective = saved?.perspective !== false;
  root.querySelector("[data-perspective]").checked = renderer.state.perspective;
  mode(saved?.mode || "scene");
  renderer.resize();
  generate(Number.isInteger(saved?.seed) ? saved.seed : 20261029, window.openai?.widgetState?.privateContent?.camera);
})();
