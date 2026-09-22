const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  const page = await browser.newPage();
  await page.goto('file:///' + path.resolve(__dirname, 'source-v16.1.html').replaceAll('\\', '/'));
  const data = await page.evaluate(() => ({ palette: materialPalette(), seed: state.seed,
    config: { stone: state.stone, group: state.group, width: state.width, coherence: state.coherence },
    foreground: Array.from(pixels(state.final)), occupancy: Array.from(pixels(state.terrain)).filter((_,i)=>i%4===3).map(v=>v?1:0),
    foregroundPng: state.final.toDataURL(), scene: sceneCanvas().toDataURL(),
    width: state.final.width, height: state.final.height }));
  const output = path.resolve(__dirname, '../../artifacts/contour');
  fs.mkdirSync(output, { recursive: true });
  for (const name of ['foregroundPng', 'scene']) {
    fs.writeFileSync(path.join(output, 'h5-' + name + '.png'), Buffer.from(data[name].split(',')[1], 'base64'));
    delete data[name];
  }
  for (const name of ['foreground', 'occupancy']) {
    fs.writeFileSync(path.join(__dirname, 'golden-' + name + '.bin'), Buffer.from(data[name])); delete data[name];
  }
  fs.writeFileSync(path.join(__dirname, 'visual-reference.json'), JSON.stringify(data, null, 2));
  console.log(JSON.stringify(data)); await browser.close();
})().catch(e=>{console.error(e); process.exit(1);});
