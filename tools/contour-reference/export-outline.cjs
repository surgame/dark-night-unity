const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  const page = await browser.newPage();
  await page.goto('file:///' + path.join(__dirname, 'source-v16.1.html').replaceAll('\\', '/'));
  fs.mkdirSync(path.join(__dirname, '../../artifacts/contour'), { recursive: true });
  const metadata = { source: 'source-v16.1.html', profile: { stone: 4, amplitude: 3, wavelength: 20, quantization: 2 }, modes: {} };
  for (const mode of ['none', 'wave', 'pixelB', 'pixelC', 'hybridB', 'hybridC']) {
    const data = await page.evaluate(mode => {
      for (const [id, value] of Object.entries({ stoneSize: 4, outlineJitter: 3, outlineScale: 20, outlineQuant: 2,
        outlineMode: mode, outlineSeed: 'OUTLINE-0921', seed: 'DN-MATERIAL-0921', oreDensity: 0 })) $(id).value = value;
      rebuild({ resetMap: true });
      const mask = c => Array.from(pixels(c)).filter((_, i) => i % 4 === 3).map(v => v ? 1 : 0);
      return { width: state.final.width, height: state.final.height, raw: mask(state.terrainRaw), outline: mask(state.terrain),
        foreground: Array.from(pixels(state.final)), masks: Array.from(state.masks), times: state.times,
        png: state.final.toDataURL() };
    }, mode);
    fs.writeFileSync(path.join(__dirname, `outline-${mode}.bin`), Buffer.from(data.outline));
    if (mode === 'hybridB') {
      for (const name of ['raw', 'foreground', 'masks']) fs.writeFileSync(path.join(__dirname, `profile-${name}.bin`), Buffer.from(data[name]));
      fs.writeFileSync(path.join(__dirname, '../../artifacts/contour/h5-profile-rock.png'), Buffer.from(data.png.split(',')[1], 'base64'));
    }
    metadata.modes[mode] = { width: data.width, height: data.height, times: data.times };
  }
  fs.writeFileSync(path.join(__dirname, 'outline-reference.json'), JSON.stringify(metadata, null, 2));
  await browser.close(); console.log('Exported independent six-mode H5 outline fixtures and 4/3/20/2 profile.');
})().catch(e => { console.error(e); process.exit(1); });
