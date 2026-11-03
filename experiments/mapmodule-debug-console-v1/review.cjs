const fs = require("node:fs");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { chromium } = require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const preview = path.resolve(process.argv[2]);
const evidence = path.resolve(process.argv[3], "display-" + Date.now());
fs.mkdirSync(evidence, { recursive: true });
const report = { scope: "UI layout and local interaction preview only; no game or Unity tests.", errors: [], layouts: [], interactions: [], screenshots: [] };
const check = (value, message) => { if (!value) throw new Error(message); };
async function inspect(frame) {
  return frame.evaluate(() => {
    const product = [...document.querySelectorAll(".dc-product")].find(p => p.getBoundingClientRect().width > 0);
    const box = product.getBoundingClientRect();
    const controls = [...product.querySelectorAll("button,select,input")].filter(el => el.getBoundingClientRect().width > 0);
    const clipped = controls.filter(el => { const r = el.getBoundingClientRect(); return r.left < box.left - 1 || r.right > box.right + 1; }).map(el => el.textContent || el.getAttribute("aria-label"));
    const overlap = [];
    for (let i = 0; i < controls.length; i++) for (let j = i + 1; j < controls.length; j++) {
      const a = controls[i], b = controls[j];
      if (a.contains(b) || b.contains(a)) continue;
      const r = a.getBoundingClientRect(), s = b.getBoundingClientRect();
      if (Math.min(r.right, s.right)-Math.max(r.left, s.left) > 1 && Math.min(r.bottom, s.bottom)-Math.max(r.top, s.top) > 1) overlap.push([a.textContent, b.textContent]);
    }
    const svg = product.querySelector(".dc-scene"), hero = product.querySelector("[data-hero]");
    const brokenText = [...product.querySelectorAll("p,span,label,h3,h4,dd,dt")].filter(el => {
      const r = el.getBoundingClientRect();
      return r.width > 0 && (r.left < box.left-1 || r.right > box.right+1);
    }).map(el => el.textContent);
    return {
      width: innerWidth, layout: product.dataset.layout, height: box.height,
      stageHeight: product.parentElement.getBoundingClientRect().height,
      overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
      clipped, overlap, brokenText,
      finite: !/NaN|Infinity|undefined/.test(svg.innerHTML),
      heroHeight: hero.getBoundingClientRect().height, scale: Number(svg.dataset.scale),
      state: JSON.parse(product.dataset.state)
    };
  });
}
async function layoutCheck(frame, config, name) {
  const state = await inspect(frame);
  report.layouts.push({ ...config, name, ...state });
  check(!state.overflow && !state.clipped.length && !state.overlap.length && !state.brokenText.length && state.finite,
    `Layout failed: ${JSON.stringify({ ...config, name, ...state })}`);
  check(Math.abs(state.heroHeight-state.scale*.48) < .1, "Hero scale changed");
  const expectedStage = config.width <= 480 ? 1850 : config.width <= 760 ? 1500 : 1200;
  check(Math.abs(state.stageHeight-expectedStage) < 1, "Shared carousel stage height");
}
async function screenshot(product, name) {
  const file = path.join(evidence, name + ".png");
  await product.screenshot({ path: file }); report.screenshots.push(file);
}
async function interact(frame, product, layout, width) {
  const action = name => product.locator(`[data-action="${name}"]`);
  const tab = name => product.locator(`[data-tab="${name}"]`);
  const state = () => product.evaluate(el => JSON.parse(el.dataset.state));
  check(await product.locator("[data-move]").first().isDisabled(), "F1 must block movement");
  await action("pick").click();
  await product.locator('[data-point="2"]').click();
  await product.locator('[data-layer="bounds"]').check();
  await product.locator('[data-layer="shape"]').check();
  await product.locator('[data-layer="ports"]').check();
  check((await state()).selected === 2, "Point picking");
  await action("record").click();
  await product.locator('[data-field="issue"]').selectOption("碰撞");
  await action("record").click();
  await tab("issues").click(); await action("report").click();
  check((await state()).notes.length === 2, "Issue recording");
  check((await product.locator("[data-report]").textContent()).includes("v03 示例"), "Record version");
  await layoutCheck(frame, { width, layout }, "issue-summary");
  if (width === 1024) await screenshot(product, layout + "-issues-1024");
  await tab("run").click();
  await action("start").click();
  check(!(await state()).hub && (await state()).running, "Start then close F1");
  const origin = (await state()).x;
  await product.locator('[data-move="1"]').click();
  check((await state()).x > origin, "Move preview");
  await action("jump").click();
  await frame.waitForFunction(layout => {
    const hero = document.querySelector(`[data-layout="${layout}"] [data-hero]`);
    return Number(hero.getAttribute("y")) < Number(hero.dataset.baseY)-.02;
  }, layout);
  await frame.waitForFunction(layout => {
    const hero = document.querySelector(`[data-layout="${layout}"] [data-hero]`);
    return Math.abs(Number(hero.getAttribute("y"))-Number(hero.dataset.baseY)) < .001;
  }, layout);
  await layoutCheck(frame, { width, layout }, "play-without-F1");
  if (width === 1024) await screenshot(product, layout + "-play-1024");
  await action("hub").click();
  check(await product.locator('[data-field="case"]').isDisabled(), "Case version must lock during run");
  await action("pause").click();
  check((await state()).paused, "Host pause");
  await action("restart").click();
  check((await state()).confirm, "Restart needs review");
  await layoutCheck(frame, { width, layout }, "restart-confirmation");
  await action("confirm-restart").click();
  check(!(await state()).paused && (await state()).notes.length === 2 && Math.abs((await state()).x-origin)<.001, "Restart preserves issue context");
  await tab("network").click();
  await tab("network").hover();
  check(await tab("network").evaluate(el => {
    const style = getComputedStyle(el), probe = document.createElement("span");
    probe.style.backgroundColor = "var(--dc-active)";
    probe.style.color = "var(--dc-active-text)";
    el.append(probe);
    const expected = getComputedStyle(probe);
    const matches = style.backgroundColor === expected.backgroundColor && style.color === expected.color;
    probe.remove(); return matches;
  }), "Selected tab hover contrast");
  check((await product.locator('[data-page="network"]').textContent()).includes("待接入"), "Must not invent network telemetry");
  await layoutCheck(frame, { width, layout }, "network");
  if (width === 1024) await screenshot(product, layout + "-network-1024");
  await tab("run").click(); await action("leave").click();
  await product.locator('[data-field="role"]').selectOption("client");
  await product.locator('[data-field="case"]').selectOption("stream");
  await action("start").click(); await action("hub").click();
  check(await action("pause").isDisabled() && await action("restart").isDisabled(), "Client authority controls");
  await layoutCheck(frame, { width, layout }, "client-run");
  await tab("observe").click();
  await action("ecology").click();
  if (!await product.evaluate(() => matchMedia("(prefers-reduced-motion: reduce)").matches)) {
    await frame.waitForFunction(layout => document.querySelector(`[data-layout="${layout}"] [data-plant]`).getAttribute("transform").includes("rotate("), layout);
    await action("ecology").click();
  }
  for (let i = 0; i < 5; i++) {
    await action("pick").click();
    if (!(await state()).picking) await action("pick").click();
    await product.locator('[data-point="2"]').click(); await action("record").click();
  }
  check((await state()).notes.length === 4, "Bounded issue history");
  await tab("issues").click(); await action("report").click();
  await layoutCheck(frame, { width, layout }, "full-issue-history");
  await tab("run").click(); await action("leave").click();
  await product.locator('[data-field="role"]').selectOption("host");
  await product.locator('[data-field="case"]').selectOption("lowpass");
  await tab("observe").click();
  report.interactions.push({
    layout, width, inputLeasePreview: true, pointPicking: true, localLayers: true, recordVersionBound: true,
    fixedCaseWhileRunning: true, hostRestartConfirmation: true, restartPreservesRecords: true,
    clientControlsDisabled: true, boundedIssues: true, finiteEcologyAnimation: true, noNetworkTelemetryInvented: true
  });
}
async function run() {
  const browser = await chromium.launch({ headless: true, executablePath: "C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe" });
  try {
    for (const config of [{ width: 1024, theme: "light" }, { width: 736, theme: "dark" }, { width: 320, theme: "light" }]) {
      const page = await browser.newPage({ viewport: { width: config.width, height: 2200 }, colorScheme: config.theme, hasTouch: config.width === 320 });
      page.on("pageerror", error => report.errors.push({ ...config, error: error.message }));
      await page.goto(pathToFileURL(preview).href);
      await page.evaluate(() => { document.body.style.padding = "0"; document.querySelector("iframe").style.height = "2200px"; });
      const frame = page.frames().find(f => f.parentFrame());
      await frame.waitForFunction(() => document.querySelector("#mapmodule-runtime-debug")?.dataset.ready === "true");
      await frame.evaluate(() => {
        window.__writes = [];
        window.openai.setWidgetState = snapshot => { window.__writes.push(snapshot); return Promise.resolve(); };
      });
      for (let index = 0; index < 2; index++) {
        const product = frame.locator(".dc-product:visible");
        const layout = await product.getAttribute("data-layout");
        await frame.waitForFunction(layout => {
          const svg = document.querySelector(`[data-layout="${layout}"] .dc-scene`);
          return Math.abs(svg.viewBox.baseVal.width-svg.parentElement.clientWidth) < 1;
        }, layout);
        await layoutCheck(frame, config, "initial");
        await screenshot(product, `${layout}-${config.width}-${config.theme}`);
        await interact(frame, product, layout, config.width);
        if (index === 0) await frame.locator(".viz-carousel").getByRole("button").last().click();
      }
      const writes = await frame.evaluate(() => window.__writes);
      check(writes.length > 0 && JSON.stringify(writes.at(-1)).length < 16384, "State snapshot budget");
      const count = writes.length;
      await frame.evaluate(() => window.dispatchEvent(new CustomEvent("openai:set_globals", {
        detail: { globals: { widgetState: { design: "mapmodule-runtime-debug-v1", layouts: { side: { caseId: "shelf", role: "client", tab: "run", layers: { bounds: true }, notes: [] } } } } }
      })));
      const restored = await frame.locator('[data-layout="side"]').evaluate(el => JSON.parse(el.dataset.state));
      check(restored.caseId === "shelf" && restored.role === "client" && !restored.running, "Restore without starting a session");
      check((await frame.evaluate(() => window.__writes.length)) === count, "State restore must not write back");
      await page.close();
    }
    check(!report.errors.length, "Runtime page errors");
    report.completed = true;
  } finally {
    await browser.close();
    const file = path.join(evidence, "report.json");
    fs.writeFileSync(file, JSON.stringify(report, null, 2) + "\n");
    console.log(JSON.stringify({ completed: !!report.completed, layouts: report.layouts.length, interactionGroups: report.interactions.length, screenshots: report.screenshots.length, report: file }));
  }
}
run().catch(error => { console.error(error.message); process.exitCode = 1; });
