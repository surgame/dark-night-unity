const fs = require("fs");
const path = require("path");
const {pathToFileURL} = require("url");
const {chromium} = require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");

(async () => {
  const folder = path.join(__dirname, "records", Date.now() + "-display-review");
  fs.mkdirSync(folder);
  const browser = await chromium.launch({
    executablePath: "C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"
  });
  const report = {scope:"Design-display inspection only. No gameplay, solver, physical traversal or Unity test.", errors:[], modules:[], layouts:[]};
  try {
    const page = await browser.newPage({viewport:{width:736,height:1000}});
    page.on("pageerror", error => report.errors.push(error.message));
    await page.goto(pathToFileURL(path.join(__dirname,"records/preview.html")).href);
    const frame = page.frames().find(f=>f.parentFrame());
    await frame.waitForFunction(()=>document.getElementById("cave-macroblock-design")?.dataset.ready==="true");
    report.initial = await frame.evaluate(()=>document.getElementById("cave-macroblock-design")._inspect());
    await frame.locator("#cbd-view").selectOption("module");
    for (let i=1;i<=16;i++) {
      const id="M"+String(i).padStart(2,"0");
      await frame.locator("#cbd-module").selectOption(id);
      report.modules.push(await frame.evaluate(()=>({
        selection:document.getElementById("cbd-selected").textContent,
        invalid:/NaN|undefined|Infinity/.test(document.getElementById("cbd-map").innerHTML)
      })));
    }
    await frame.locator("#cbd-module").selectOption("M04");
    for (const width of [736,360,320]) {
      await page.setViewportSize({width,height:1000});
      await frame.waitForFunction(expected=>innerWidth<=expected && innerWidth>=expected-48,width);
      for (const view of ["assembly","module","port"]) {
        await frame.locator("#cbd-view").selectOption(view);
        await page.emulateMedia({colorScheme:width===320?"dark":"light"});
        await frame.waitForFunction(()=>Math.abs(document.querySelector("#cbd-map svg").width.baseVal.value-document.getElementById("cbd-map").getBoundingClientRect().width)<=1);
        const info=await frame.evaluate(()=>({
          width:innerWidth,scroll:document.documentElement.scrollWidth,
          state:document.getElementById("cave-macroblock-design")._inspect().state
        }));
        report.layouts.push(info);
        await frame.locator("#cave-macroblock-design").screenshot({path:path.join(folder,`${width}-${view}.png`)});
      }
    }
    await frame.locator("#cbd-bounds").uncheck();
    await frame.locator("#cbd-content").uncheck();
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:{kind:"cave-macroblock-design-v1",view:"module",module:"M15",bounds:true,content:true}}}}})));
    report.restored=await frame.evaluate(()=>document.getElementById("cave-macroblock-design")._inspect().state);
    report.ok=report.errors.length===0 && report.modules.every(x=>!x.invalid) && report.layouts.every(x=>x.scroll<=x.width+1) && report.restored.module==="M15";
  } finally {
    await browser.close();
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  }
  console.log(JSON.stringify({ok:report.ok,folder,modules:report.modules.length,layouts:report.layouts.length,errors:report.errors}));
  if (!report.ok) process.exitCode=1;
})().catch(error=>{console.error(error.stack);process.exitCode=1;});
