const fs = require("node:fs");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { chromium } = require("playwright");

async function main() {
  const output = process.argv[2];
  if (!output) throw new Error("Usage: node export-landscape.cjs <new-output.png>");
  if (fs.existsSync(output)) throw new Error("Refusing to overwrite an existing asset.");
  const browser = await chromium.launch({
    executablePath: process.env.CHROME_PATH || "C:/Program Files/Google/Chrome/Application/chrome.exe",
    headless: true
  });
  try {
    const page = await browser.newPage({ viewport: { width: 768, height: 480 } });
    await page.goto(pathToFileURL(path.join(__dirname, "landscape-source.html")).href);
    const png = await page.locator("canvas").evaluate(canvas => canvas.toDataURL("image/png"));
    fs.mkdirSync(path.dirname(path.resolve(output)), { recursive: true });
    fs.writeFileSync(output, Buffer.from(png.split(",")[1], "base64"));
    console.log(JSON.stringify({ output: path.resolve(output), width: 768, height: 480 }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
