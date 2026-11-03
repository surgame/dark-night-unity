const fs=require("fs"),path=require("path"),{pathToFileURL}=require("url");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
(async()=>{
  const folder=path.join(__dirname,"records",Date.now()+"-elevation-review");
  fs.mkdirSync(folder);
  const report={scope:"Elevation design-display review; no physics, gameplay, solver or Unity test.",errors:[],layouts:[],modules:[]};
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try{
    const page=await browser.newPage({viewport:{width:736,height:1000}});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto(pathToFileURL(path.join(__dirname,"records/elevation-preview.html")).href);
    const frame=page.frames().find(f=>f.parentFrame());
    const inspect=()=>frame.evaluate(()=>document.getElementById("cave-macroblock-elevation")._inspect());
    await frame.waitForFunction(()=>document.getElementById("cave-macroblock-elevation")?.dataset.ready==="true");
    report.initial=await inspect();
    await frame.locator("#cbe-elevation").uncheck();
    await frame.waitForFunction(()=>document.getElementById("cave-macroblock-elevation")._inspect().amount===0);
    report.flat=await inspect();
    await frame.locator("#cave-macroblock-elevation").screenshot({path:path.join(folder,"736-middle-flat.png")});
    await frame.locator("#cbe-elevation").check();
    await frame.waitForFunction(()=>document.getElementById("cave-macroblock-elevation")._inspect().amount===1);
    report.fixedCamera=JSON.stringify(report.flat.camera)===JSON.stringify(report.initial.camera);
    for(const width of [736,360,320]){
      await page.setViewportSize({width,height:1000});
      await frame.waitForFunction(expected=>innerWidth<=expected && innerWidth>=expected-48,width);
      await page.emulateMedia({colorScheme:width===320?"dark":"light"});
      for(const view of ["middle","all","shallow","deep","module"]){
        await frame.locator("#cbe-view").selectOption(view);
        await frame.waitForFunction(()=>Math.abs(document.querySelector("#cbe-map svg").width.baseVal.value-document.getElementById("cbe-map").getBoundingClientRect().width)<=1);
        report.layouts.push(await frame.evaluate(()=>({
          width:innerWidth,scroll:document.documentElement.scrollWidth,
          state:document.getElementById("cave-macroblock-elevation")._inspect().state,
          invalid:/NaN|undefined|Infinity/.test(document.getElementById("cbe-map").innerHTML),
          labels:[...document.querySelectorAll("#cbe-map text")].map(n=>{
            const b=n.getBoundingClientRect();
            return {text:n.textContent,left:b.left,right:b.right};
          })
        })));
        await frame.locator("#cave-macroblock-elevation").screenshot({path:path.join(folder,`${width}-${view}.png`)});
      }
    }
    for(let i=1;i<=16;i++){
      await frame.locator("#cbe-module").selectOption("M"+String(i).padStart(2,"0"));
      report.modules.push(await inspect());
    }
    await frame.locator("#cbe-bounds").check();
    await frame.locator("#cbe-content").uncheck();
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:{kind:"cave-macroblock-elevation-v1",view:"middle",module:"M04",elevation:true,bounds:false,content:true}}}}})));
    report.restored=await inspect();
    report.ok=!report.errors.length && report.fixedCamera && report.layouts.every(x=>!x.invalid && x.scroll<=x.width+1 && x.labels.every(b=>b.left>=-1 && b.right<=x.width+1)) && report.modules.length===16;
  }finally{
    await browser.close();
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  }
  console.log(JSON.stringify({ok:report.ok,folder,layouts:report.layouts.length,modules:report.modules.length,errors:report.errors}));
  if(!report.ok)process.exitCode=1;
})().catch(e=>{console.error(e.stack);process.exitCode=1;});
