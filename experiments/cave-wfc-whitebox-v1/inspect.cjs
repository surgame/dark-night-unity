const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");
const { pathToFileURL } = require("node:url");
const { chromium } = require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");

(async () => {
  const directory = path.resolve(process.argv[2]);
  const run = Date.now();
  const errors = [];
  const results = [];
  const browser = await chromium.launch({
    executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true
  });
  const page = await browser.newPage({ viewport: { width: 1000, height: 900 } });
  page.on("pageerror", error => errors.push(error.message));
  try {
    await page.goto(pathToFileURL(path.join(directory, "preview.html")).href);
    const frame = page.frameLocator("#codex-visualization");
    const root = frame.locator("#cave-wfc-whitebox");
    await root.locator('xpath=self::*[@data-state="ready"]').waitFor();
    const metrics = await root.evaluate(element => {
      const map = element.__caveDemo.map;
      return {
        seed: map.seed, cells: map.tiles.length, conflicts: map.check.mismatches,
        corners: map.check.cornerConflicts, observations: map.stats.observations,
        topology: Array.from(new Set(map.tiles)).length
      };
    });
    assert.equal(metrics.cells, 3840);
    assert.equal(metrics.conflicts, 0);
    assert.equal(metrics.corners, 0);
    assert(metrics.observations > 0);
    assert(await frame.locator("[data-progress-wrap]").isHidden());
    await page.screenshot({ path: path.join(directory, `local-initial-${run}.png`), fullPage: true });
    await frame.locator("[data-fit=all]").click();
    await page.screenshot({ path: path.join(directory, `overview-initial-${run}.png`), fullPage: true });
    for (const scheme of ["light", "dark"]) {
      await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
      for (const width of [736, 360, 320]) {
        await page.setViewportSize({ width, height: 1000 });
        for (const mode of ["scene", "seams", "atlas"]) {
          await frame.locator(`[data-mode=${mode}]`).click();
          const dimensions = await root.evaluate(element => ({
            overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
            canvasWidth: element.querySelector("canvas").clientWidth,
            selected: element.querySelector('[aria-pressed=true]').dataset.mode,
            detail: element.querySelector("[data-detail]").textContent
          }));
          assert(!dimensions.overflow);
          assert.equal(dimensions.selected, mode);
          assert(dimensions.canvasWidth >= 280);
          assert(dimensions.detail);
          results.push({ scheme, width, mode, passed: true });
        }
      }
    }
    await page.setViewportSize({ width: 1000, height: 900 });
    await page.emulateMedia({ colorScheme: "light" });
    await frame.locator("[data-mode=scene]").click();
    await frame.locator("[data-fit=local]").click();
    await frame.locator("[data-perspective]").uncheck();
    const flat = await root.evaluate(element => element.querySelector("canvas").toDataURL());
    await frame.locator("[data-perspective]").check();
    const depth = await root.evaluate(element => element.querySelector("canvas").toDataURL());
    assert.notEqual(flat, depth);
    const before = await root.evaluate(element => ({ ...element.__caveDemo.renderer.camera }));
    const box = await frame.locator("canvas").boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2 + 65, box.y + box.height / 2 + 20);
    await page.mouse.up();
    await page.mouse.wheel(0, -160);
    const after = await root.evaluate(element => ({ ...element.__caveDemo.renderer.camera }));
    assert.notEqual(before.x, after.x);
    assert(after.zoom > before.zoom);
    await frame.locator("[data-mode=seams]").click();
    await frame.locator("canvas").click();
    assert.match(await frame.locator("[data-detail]").textContent(), /四角/);
    await page.screenshot({ path: path.join(directory, `seams-${run}.png`), fullPage: true });
    const oldSnapshot = await root.evaluate(() => window.openai?.widgetState);
    await frame.locator("[data-mode=atlas]").click();
    await root.evaluate((element, snapshot) => {
      window.dispatchEvent(new CustomEvent("openai:set_globals", {
        detail: { globals: { widgetState: snapshot } }
      }));
    }, oldSnapshot);
    await root.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    assert.equal(await frame.locator('[aria-pressed=true]').getAttribute("data-mode"), "atlas");
    await page.screenshot({ path: path.join(directory, `atlas-${run}.png`), fullPage: true });
    await frame.locator("[data-next]").click();
    await root.locator('xpath=self::*[@data-state="ready" and @data-seed="20268948"]').waitFor();
    const newSeed = await root.evaluate(element => element.__caveDemo.map.seed);
    assert.equal(newSeed, 20268948);
    await frame.locator("[data-mode=scene]").click();
    await frame.locator("[data-fit=local]").click();
    await page.screenshot({ path: path.join(directory, `local-final-${run}.png`), fullPage: true });
    await page.emulateMedia({ colorScheme: "dark" });
    await page.screenshot({ path: path.join(directory, `dark-final-${run}.png`), fullPage: true });
    assert.deepEqual(errors, []);
    results.push({ perspectivePixelsChange: true, panAndZoom: true, selectedTile: true, nextSeed: true, staleStateEchoIgnored: true });
    console.log(JSON.stringify({ checks: results.length, errors: 0, metrics, run }));
  } catch (error) {
    await page.screenshot({ path: path.join(directory, `failed-${run}.png`), fullPage: true });
    errors.push(error.stack);
    throw error;
  } finally {
    fs.writeFileSync(path.join(directory, `browser-check-${run}.json`), JSON.stringify({ results, errors }, null, 2));
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
