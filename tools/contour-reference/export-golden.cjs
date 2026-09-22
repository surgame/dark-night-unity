// Run with NODE_PATH pointing at the bundled Playwright installation.
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  const page = await browser.newPage();
  await page.goto('file:///' + path.resolve(__dirname, 'source-v16.1.html').replaceAll('\\', '/'));
  const data = await page.evaluate(() => {
    const layers = rawBackgroundLayers();
    return { width: state.final.width, height: state.final.height, seed: state.seed,
      base: materialPalette()[4], source: Array.from(ensureFrozenBackgroundSource().occupancy),
      layers: layers.map(c => Array.from(pixels(c))),
      soft: [0, 2, 4].map(w => Array.from(pixels(softenMiddleEdge(layers[1], w)))) };
  });
  fs.writeFileSync(path.join(__dirname, 'golden-source.bin'), Buffer.from(data.source));
  data.layers.forEach((v, i) => fs.writeFileSync(path.join(__dirname, `golden-layer-${i}.rgba`), Buffer.from(v)));
  data.soft.forEach((v, i) => fs.writeFileSync(path.join(__dirname, `golden-soft-${[0,2,4][i]}.rgba`), Buffer.from(v)));
  delete data.source; delete data.layers; delete data.soft;
  fs.writeFileSync(path.join(__dirname, 'golden.json'), JSON.stringify(data, null, 2));
  console.log(JSON.stringify(data));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
