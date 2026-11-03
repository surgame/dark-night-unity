const design = "mapmodule-runtime-debug-v1";
const products = [...root.querySelectorAll(".dc-product")];
const template = root.querySelector("[data-template]");
const issueKinds = ["接缝", "碰撞", "空间尺度", "生态动态"];
const instances = [];
let focused = null;
function restoredLayouts(input) {
  return input && input.design === design && input.layouts ? input.layouts : {};
}
function remember() {
  if (!window.openai || !window.openai.setWidgetState) return;
  const layouts = Object.fromEntries(instances.map(instance => [instance.layout, instance.snapshot()]));
  window.openai.setWidgetState({ modelContent: { design, layouts }, privateContent: null }).catch(() => {});
}
function floorAt(scheme, x) {
  const points = scheme.floor;
  for (let i = 1; i < points.length; i++) {
    const a = points[i - 1], b = points[i];
    if (x <= b[0]) return a[1] + (b[1] - a[1]) * Math.max(0, Math.min(1, (x - a[0]) / (b[0] - a[0] || 1)));
  }
  return points.at(-1)[1];
}
function create(product, saved) {
  product.append(template.content.cloneNode(true));
  const layout = product.dataset.layout;
  const q = selector => product.querySelector(selector);
  const all = selector => [...product.querySelectorAll(selector)];
  const state = {
    caseId: "lowpass", role: "host", camera: "hero", hub: true, tab: "observe",
    layers: { bounds: false, shape: false, ports: false, ecology: true },
    picking: false, selected: -1, issue: "接缝", notes: [],
    running: false, paused: false, confirm: false, x: 4.3
  };
  const appearance = { compact: false, hints: true, observer: "top" };
  let ecologyFrame = 0, jumpFrame = 0, size = null;
  const scheme = () => schemes.find(value => value.id === state.caseId);
  const version = () => `ENTRY.${state.caseId.toUpperCase()} / v03 示例`;
  function snapshot() {
    return {
      caseId: state.caseId, role: state.role, camera: state.camera, hub: state.hub, tab: state.tab,
      layers: { ...state.layers }, selected: state.selected, issue: state.issue, notes: state.notes
    };
  }
  function restore(input) {
    if (!input || typeof input !== "object") return;
    if (schemes.some(value => value.id === input.caseId)) state.caseId = input.caseId;
    if (["host", "client"].includes(input.role)) state.role = input.role;
    if (["hero", "fragment"].includes(input.camera)) state.camera = input.camera;
    if (["observe", "issues", "network", "run"].includes(input.tab)) state.tab = input.tab;
    if (typeof input.hub === "boolean") state.hub = input.hub;
    for (const key of Object.keys(state.layers)) if (typeof input.layers?.[key] === "boolean") state.layers[key] = input.layers[key];
    if (Number.isInteger(input.selected) && input.selected >= -1 && input.selected <= 4) state.selected = input.selected;
    if (issueKinds.includes(input.issue)) state.issue = input.issue;
    state.notes = Array.isArray(input.notes) ? input.notes.filter(note =>
      note && schemes.some(value => value.id === note.caseId) && Number.isInteger(note.point) &&
      note.point >= 0 && note.point < 5 && issueKinds.includes(note.kind)
    ).slice(-4).map(note => ({ caseId: note.caseId, point: note.point, kind: note.kind })) : [];
    state.running = false; state.paused = false; state.confirm = false; state.picking = false;
    state.x = scheme().heroes[0].x;
  }
  restore(saved);
  const select = q('[data-field="case"]');
  for (const value of schemes) {
    const option = document.createElement("option");
    option.value = value.id; option.textContent = value.name; select.append(option);
  }
  function points() {
    return [2.25, 4.3, 6.4, 8.6, 10.8].map((x, i) => ({
      x, y: floorAt(scheme(), x), label: ["洞檐下行", "入口落脚", "两片接口", "第二片低点", "深入出口"][i],
      module: x < 6.4 ? "M01" : "M02", localX: x < 6.4 ? x : x - 6.4
    }));
  }
  function say(message) { q("[data-notice]").textContent = message; }
  function draw() {
    const svg = q(".dc-scene"), current = scheme();
    const width = Math.max(240, q("[data-view]").clientWidth);
    const height = layout === "center" ? 298 : 378;
    const range = state.camera === "hero" ? Math.min(7.5, Math.max(6.4, width / 108)) : 12.8;
    const from = state.camera === "hero" ? Math.max(0, Math.min(12.8 - range, state.x - range * .43)) : 0;
    const scale = width / range;
    const left = (width - range * scale) / 2 - from * scale, top = (height - 3.2 * scale) / 2;
    size = { width, height, scale, left, top, from, range };
    const clip = `dc-${layout}-clip`;
    svg.setAttribute("viewBox", `0 0 ${width} ${height}`);
    svg.dataset.scale = scale;
    let ecology = "";
    if (state.layers.ecology) {
      ecology = current.plants.map((p, i) => p.kind === "grass"
        ? `<g data-plant="${i}" data-base="translate(${p.x} ${p.y}) scale(${p.scale})" transform="translate(${p.x} ${p.y}) scale(${p.scale})"><path class="dc-grass" d="M-.13 0Q-.2 -.13 -.14 -.21L-.04 -.06Q-.08 -.23 0 -.32Q.05 -.2 .03 -.06Q.11 -.22 .18 -.2Q.13 -.05 .1 0Z"/></g>`
        : `<g data-plant="${i}" data-base="translate(${p.x} ${p.y})" transform="translate(${p.x} ${p.y})"><path class="dc-grass" d="M-.018 0V-.18H.018V0ZM-.16 -.17Q-.13 -.33 0 -.34Q.13 -.32 .16 -.18Z"/></g>`).join("");
      ecology += current.roots.map(p => `<path class="dc-grass" fill="none" stroke="var(--dc-grass)" stroke-width=".025" d="M${p.x} ${p.y}q-.08 .1 0 ${p.length}"/>`).join("");
      if (current.water) {
        const w = current.water;
        ecology += `<path class="dc-water" opacity=".65" stroke-width="0" d="M${w.x-.04} ${w.top}h.08V${w.bottom}h-.08Z M${w.left} ${w.surface}H${w.right}V3.2H${w.left}Z"/>
          <path data-water-line class="dc-water" fill="none" stroke-width=".02" d="M${w.x} ${w.top+.08}v.35M${w.x+.025} ${w.top+.6}v.45"/>`;
      }
    }
    let bounds = "";
    if (state.layers.bounds) for (const x of [0, 6.4]) bounds +=
      `<rect class="dc-bound" x="${left+x*scale}" y="${top}" width="${6.4*scale}" height="${3.2*scale}"/>`;
    const shapes = state.layers.shape ? `<polyline class="dc-shape" points="${current.floor.map(p=>p.join(",")).join(" ")}"/><polyline class="dc-shape" points="${current.ceiling.map(p=>p.join(",")).join(" ")}"/>` : "";
    const port = state.layers.ports ? `<path class="dc-port" d="M6.4 ${current.port.ceiling}V${current.port.floor}m-.13 0h.26m-.26 ${current.port.ceiling-current.port.floor}h.26"/>` : "";
    const y = floorAt(current, state.x);
    svg.innerHTML = `<title>${current.name}的游戏内调试界面草案</title><desc>原主角素材与扁平地形。界面动作是本地预演，碰撞线不是正式权威地形。</desc>
      <defs><clipPath id="${clip}"><rect width="${width}" height="${height}"/></clipPath></defs>
      <g clip-path="url(#${clip})"><g transform="translate(${left} ${top}) scale(${scale})">
      <path class="dc-far" d="M0 2.8L1.5 1.25L2.6 2.2L3.9 1.3L5.1 2.4L6.3 1.55L8.3 2.55L9.4 1.7L11.6 2.4L12.8 1.45V3.2H0Z"/>
      <path class="dc-rock" d="M-30 ${current.floor[0][1]}H0V3.2H12.8V${current.floor.at(-1)[1]}H40V30H-30Z"/>
      <rect class="dc-rock" x="${current.ceiling[0][0]}" y="-20" width="40" height="20"/>
      <path class="dc-rock" d="${current.floorSolid}"/><path class="dc-rock" d="${current.ceilingSolid}"/>
      ${current.shelf?`<path class="dc-rock" d="${current.shelf}"/><path class="dc-rim" d="${current.shelf}"/>`:""}
      <path class="dc-rim" d="${current.floorPath}"/><path class="dc-rim" d="${current.ceilingPath}"/>${ecology}${shapes}${port}
      <image data-hero data-base-y="${y-.48}" x="${state.x-.48*232/251/2}" y="${y-.48}" width="${.48*232/251}" height=".48" href="${heroImage}"/>
      </g>${bounds}</g>`;
    const holder = q(".dc-points"); holder.replaceChildren();
    if (state.hub && state.picking) for (const [index, point] of points().entries()) {
      const x = left + point.x * scale, y = top + point.y * scale - 24;
      if (x < 25 || x > width - 25) continue;
      const button = document.createElement("button");
      button.type = "button"; button.className = "dc-point cursor-interaction";
      button.textContent = String(index + 1); button.dataset.point = index;
      button.setAttribute("aria-label", `选择${point.label}`);
      button.setAttribute("aria-pressed", String(index === state.selected));
      button.style.left = `${x}px`; button.style.top = `${Math.max(30, Math.min(height - 42, y))}px`;
      button.addEventListener("click", () => { state.selected = index; say(`已选 ${point.module} · ${point.label}，可记录问题。`); update(); remember(); });
      holder.append(button);
    }
  }
  function renderNotes() {
    const list = q("[data-issues]"); list.replaceChildren();
    if (!state.notes.length) {
      const empty = document.createElement("p"); empty.className = "dc-subtle";
      empty.textContent = "尚无记录。到观察页点选位置后记录。"; list.append(empty);
    }
    state.notes.forEach((note, index) => {
      const item = document.createElement("div"); item.className = "dc-issue";
      const title = document.createElement("strong"); title.textContent = `${index+1}. ${note.kind}`;
      const detail = document.createElement("span"); detail.className = "dc-subtle";
      detail.textContent = `${schemes.find(s=>s.id===note.caseId).name} · v03 示例 · 位置 ${note.point+1}`;
      item.append(title, detail); list.append(item);
    });
    q("[data-issue-count]").textContent = `${state.notes.length} / 4`;
    q('[data-action="report"]').disabled = !state.notes.length;
  }
  function update() {
    product.dataset.open = String(state.hub);
    product.dataset.state = JSON.stringify({ ...snapshot(), running: state.running, paused: state.paused, confirm: state.confirm, picking: state.picking, x: state.x });
    product.dataset.compact = String(appearance.compact); product.dataset.observer = appearance.observer;
    q("[data-hint]").hidden = !appearance.hints;
    q('[data-action="hub"]').textContent = state.hub ? "F1 · 收起调试" : "F1 · 打开调试";
    q("[data-case-title]").textContent = `${scheme().name} · 入口＋第二片`;
    q("[data-version]").textContent = version();
    q("[data-session]").textContent = !state.running ? "尚未开始预演" : state.paused ? "预演已暂停" : "试跑预演中";
    q("[data-input-state]").textContent = state.hub ? "F1 已接管输入" : state.paused ? "预演暂停" : "本端角色输入";
    q("[data-position]").textContent = `参考 x ${state.x.toFixed(2)}`;
    select.value = state.caseId; select.disabled = state.running;
    q('[data-field="role"]').value = state.role; q('[data-field="role"]').disabled = state.running;
    q('[data-field="issue"]').value = state.issue;
    all("[data-layer]").forEach(input => { input.checked = state.layers[input.dataset.layer]; });
    all("[data-camera]").forEach(button => button.setAttribute("aria-pressed", String(button.dataset.camera === state.camera)));
    all("[data-tab]").forEach(button => {
      const active = button.dataset.tab === state.tab;
      button.id = `${layout}-tab-${button.dataset.tab}`;
      button.setAttribute("aria-selected", String(active));
      button.setAttribute("aria-controls", `${layout}-page-${button.dataset.tab}`);
    });
    all("[data-page]").forEach(page => {
      page.hidden = page.dataset.page !== state.tab; page.id = `${layout}-page-${page.dataset.page}`;
      page.setAttribute("aria-labelledby", `${layout}-tab-${page.dataset.page}`);
    });
    all("[data-move], [data-action='jump']").forEach(button => { button.disabled = !state.running || state.paused || state.hub; });
    all('[data-action="start"], [data-action="quick-start"]').forEach(button => { button.disabled = state.running; });
    q('[data-action="pause"]').disabled = !state.running || state.role !== "host";
    q('[data-action="pause"]').textContent = state.paused ? "继续预演" : "暂停预演";
    q('[data-action="restart"]').disabled = !state.running || state.role !== "host";
    q('[data-action="leave"]').disabled = !state.running;
    q("[data-confirm]").hidden = !state.confirm;
    q("[data-role-text]").textContent = state.role === "host" ? "房主预演" : "普通客户端预演";
    q("[data-net-case]").textContent = version();
    q("[data-bound-version]").textContent = version();
    q("[data-lock]").textContent = state.running ? "当前预演已锁定；结束后才可换样例" : "开始后锁定样例与版本";
    q("[data-permission]").textContent = state.role === "host" ? "暂停与重开归房主；正式操作由服务端校验。" : "客户端可观察、记录、结束本端预演；暂停与重开由房主执行。";
    q('[data-action="pick"]').setAttribute("aria-pressed", String(state.picking));
    q('[data-action="pick"]').textContent = state.picking ? "结束点选" : "点选地形位置";
    q('[data-action="record"]').disabled = state.selected < 0;
    const point = points()[state.selected];
    q("[data-pick-label]").textContent = point ? point.module : "未选中";
    q("[data-selection]").textContent = point ? `${point.label} · 局部草案坐标 (${point.localX.toFixed(2)}, ${(3.2-point.y).toFixed(2)})；地形单元待原生映射。` : "打开点选后，选择画面上的位置标记。";
    renderNotes(); draw();
  }
  function stopMotion() {
    cancelAnimationFrame(ecologyFrame); cancelAnimationFrame(jumpFrame); ecologyFrame = 0; jumpFrame = 0;
    q('[data-action="ecology"]').textContent = "预演生态动态 · 5 秒"; draw();
  }
  function move(direction) {
    if (!state.running || state.paused || state.hub) return;
    cancelAnimationFrame(jumpFrame);
    state.x = Math.max(.45, Math.min(12.35, state.x + direction * .35)); update();
  }
  function jump() {
    if (!state.running || state.paused || state.hub || jumpFrame) return;
    const start = performance.now();
    function frame(now) {
      const t = Math.min(1, (now-start)/650), hero = q("[data-hero]");
      hero.setAttribute("y", String(Number(hero.dataset.baseY) - Math.sin(t*Math.PI)*.42));
      if (t < 1 && product.isConnected && !product.closest("[data-variant]").hidden) jumpFrame = requestAnimationFrame(frame);
      else { jumpFrame = 0; draw(); }
    }
    jumpFrame = requestAnimationFrame(frame);
  }
  function ecology() {
    if (ecologyFrame) { stopMotion(); return; }
    if (!state.layers.ecology) { say("先开启生态表现。"); return; }
    if (matchMedia("(prefers-reduced-motion: reduce)").matches) { say("系统已减少动态；保留静态生态。"); return; }
    const start = performance.now();
    q('[data-action="ecology"]').textContent = "停止生态预演";
    function frame(now) {
      if (now-start >= 5000 || product.closest("[data-variant]").hidden || !product.isConnected) { stopMotion(); return; }
      all("[data-plant]").forEach((plant, i) => plant.setAttribute("transform", `${plant.dataset.base} rotate(${Math.sin((now-start)/470+i)*5})`));
      const water = q("[data-water-line]");
      if (water) water.setAttribute("transform", `translate(0 ${((now-start)%650)/650*.25})`);
      ecologyFrame = requestAnimationFrame(frame);
    }
    ecologyFrame = requestAnimationFrame(frame);
  }
  function toggleHub(value) { state.hub = value; if (!value) state.picking = false; state.confirm = false; update(); remember(); }
  function act(action) {
    focused = instance;
    if (action === "hub") return toggleHub(!state.hub);
    if (action === "close") return toggleHub(false);
    if (action === "jump") return jump();
    if (action === "ecology") return ecology();
    if (action === "start" || action === "quick-start") {
      if (state.running) return;
      state.running = true; state.paused = false; state.hub = false; state.picking = false;
      say("已开始界面预演；正式对象、地图与绘制回执仍待接入。");
    }
    if (action === "pause" && state.running && state.role === "host") {
      state.paused = !state.paused; stopMotion();
      say(state.paused ? "玩法暂停预演；正式网络与 UI 将继续工作。" : "已继续预演。");
    }
    if (action === "restart" && state.running && state.role === "host") state.confirm = true;
    if (action === "cancel-restart") state.confirm = false;
    if (action === "confirm-restart" && state.confirm && state.running && state.role === "host") {
      state.x = scheme().heroes[0].x; state.paused = false; state.confirm = false;
      stopMotion(); say(`同版本重开预演：${version()}，问题记录保留。`);
    }
    if (action === "leave") {
      state.running = false; state.paused = false; state.confirm = false; state.x = scheme().heroes[0].x;
      stopMotion(); say("已结束本端预演，可以选择其他样例。");
    }
    if (action === "pick") {
      state.picking = !state.picking;
      if (state.picking) state.camera = "fragment";
      say(state.picking ? "选择画面上的编号；本端观察不会改变地形。" : "已结束点选。");
    }
    if (action === "record" && state.selected >= 0) {
      state.notes.push({ caseId: state.caseId, point: state.selected, kind: state.issue });
      state.notes = state.notes.slice(-4);
      q("[data-report]").hidden = true;
      say("已在此预演中记录问题；到“问题点”查看。");
    }
    if (action === "report" && state.notes.length) {
      q("[data-report]").hidden = false;
      q("[data-report]").textContent = state.notes.map((note, i) => `${i+1}. ${note.kind} / ENTRY.${note.caseId.toUpperCase()} / v03 示例 / 位置 ${note.point+1}`).join("\n") + "\n截图、epoch 与 tick：正式接入后采集";
      say("这是问题摘要预览，未写入工程文件。");
    }
    update(); remember();
  }
  all("[data-action]").forEach(button => button.addEventListener("click", () => act(button.dataset.action)));
  all("[data-move]").forEach(button => button.addEventListener("click", () => { focused = instance; move(Number(button.dataset.move)); }));
  all("[data-tab]").forEach(button => button.addEventListener("click", () => { state.tab = button.dataset.tab; state.confirm = false; update(); remember(); }));
  all("[data-camera]").forEach(button => button.addEventListener("click", () => { state.camera = button.dataset.camera; update(); remember(); }));
  all("[data-layer]").forEach(input => input.addEventListener("change", () => { stopMotion(); state.layers[input.dataset.layer] = input.checked; update(); remember(); }));
  select.addEventListener("change", () => {
    if (state.running) return;
    stopMotion(); state.caseId = select.value; state.selected = -1; state.x = scheme().heroes[0].x;
    say("已选择另一份样例制作版本；尚未开始预演。"); update(); remember();
  });
  q('[data-field="role"]').addEventListener("change", event => { if (!state.running) { state.role = event.target.value; update(); remember(); } });
  q('[data-field="issue"]').addEventListener("change", event => { state.issue = event.target.value; update(); remember(); });
  const instance = { layout, snapshot, restore(input) { stopMotion(); restore(input); q("[data-report]").hidden = true; update(); }, key(event) {
    if (event.key === "F1") { event.preventDefault(); toggleHub(!state.hub); }
    if (!/^(INPUT|SELECT|TEXTAREA)$/.test(event.target.tagName)) {
      if (["a", "A", "d", "D"].includes(event.key)) { event.preventDefault(); move(event.key.toLowerCase() === "a" ? -1 : 1); }
      if (event.code === "Space" && event.target.tagName !== "BUTTON") { event.preventDefault(); jump(); }
    }
  }};
  product.addEventListener("pointerdown", () => { focused = instance; });
  new ResizeObserver(() => { if (product.getBoundingClientRect().width > 0) draw(); }).observe(q("[data-view]"));
  update();
  if (globalThis.Tweak) {
    const tweak = new Tweak({ container: product, onChange: update });
    tweak.addToggle(appearance, "compact", { label: "紧凑诊断间距" });
    tweak.addToggle(appearance, "hints", { label: "显示操作提示" });
    tweak.addSelect(appearance, "observer", { label: "观察栏位置", options: [{ label: "画面上方", value: "top" }, { label: "画面下方", value: "bottom" }] });
  }
  return instance;
}
const saved = restoredLayouts(window.openai?.widgetState);
for (const product of products) instances.push(create(product, saved[product.dataset.layout]));
document.addEventListener("keydown", event => {
  const visible = products.find(product => !product.closest("[data-variant]").hidden);
  const active = focused && focused.layout === visible?.dataset.layout ? focused : instances.find(instance => instance.layout === visible?.dataset.layout);
  if (active) active.key(event);
});
window.addEventListener("openai:set_globals", event => {
  const next = restoredLayouts(event.detail?.globals?.widgetState);
  for (const instance of instances) if (next[instance.layout]) instance.restore(next[instance.layout]);
});
root.dataset.ready = "true";
root._inspect = () => ({ design, layouts: Object.fromEntries(products.map(product => [product.dataset.layout, JSON.parse(product.dataset.state)])) });
