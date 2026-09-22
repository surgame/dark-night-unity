// Read-only export from the checked-in v16.1 reference. No Unity import.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { chromium } = require(process.env.DN_PLAYWRIGHT || 'C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async () => {
  const source = path.resolve(__dirname, '../../tools/contour-reference/source-v16.1.html');
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  try {
    const page = await browser.newPage();
    await page.goto('file:///' + source.replaceAll('\\', '/'));
    const data = await page.evaluate(() => {
      for (const [id, value] of Object.entries({ stoneSize: 4, outlineJitter: 3, outlineScale: 20, outlineQuant: 2,
        outlineMode: 'hybridB', outlineSeed: 'OUTLINE-0921', seed: 'DN-MATERIAL-0921', oreDensity: 0 })) $(id).value = value;
      rebuild({ resetMap: true });
      const empty = [0, 1, 2].map(() => canvas(state.final.width, state.final.height));
      return { width: state.final.width, height: state.final.height, top: backgroundStartY(),
        foreground: state.final.toDataURL(), layers: state.decorLayers.map(c => c.toDataURL()),
        base: sceneCanvas(empty, { foreground: false, ore: false, lighting: false }).toDataURL(),
        profile: { stone: 4, outline: 'hybridB', amplitude: 3, wavelength: 20, quantization: 2, middleSoftness: state.middleSoftness } };
    });
    data.sourceSha256 = crypto.createHash('sha256').update(fs.readFileSync(source)).digest('hex');
    fs.writeFileSync(path.join(__dirname, 'reference.json'), JSON.stringify(data));
    console.log(JSON.stringify({width:data.width,height:data.height,sourceSha256:data.sourceSha256}));
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
