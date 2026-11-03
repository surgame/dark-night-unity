const fs=require("fs"),path=require("path"),{pathToFileURL}=require("url");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
(async()=>{
  const folder=path.join(__dirname,"records",Date.now()+"-depth-review");
  fs.mkdirSync(folder);
  const report={scope:"Surface/depth design-display review only; no physics, gameplay, traversal, solver or Unity validation.",errors:[],layouts:[],modules:[]};
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try{
    const page=await browser.newPage({viewport:{width:736,height:1100}});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto(pathToFileURL(path.join(__dirname,"records/depth-preview.html")).href);
    const frame=page.frames().find(f=>f.parentFrame());
    const inspect=()=>frame.evaluate(()=>document.getElementById("cave-surface-depth")._inspect());
    await frame.waitForFunction(()=>document.getElementById("cave-surface-depth")?.dataset.ready==="true");
    report.initial=await inspect();
    for(const width of [736,360,320]){
      await page.setViewportSize({width,height:1100});
      await frame.waitForFunction(expected=>innerWidth<=expected && innerWidth>=expected-48,width);
      await page.emulateMedia({colorScheme:width===320?"dark":"light"});
      for(const view of ["all","surface","shallow","middle","deep","single"]){
        await frame.locator("#csd-view").selectOption(view);
        await frame.waitForFunction(()=>Math.abs(document.querySelector("#csd-map svg").width.baseVal.value-document.getElementById("csd-map").getBoundingClientRect().width)<=1);
        report.layouts.push(await frame.evaluate(()=>({
          width:innerWidth,scroll:document.documentElement.scrollWidth,
          state:document.getElementById("cave-surface-depth")._inspect().state,
          invalid:/NaN|undefined|Infinity/.test(document.getElementById("csd-map").innerHTML),
          labels:[...document.querySelectorAll("#csd-map text")].map(n=>{
            const b=n.getBoundingClientRect();
            return {text:n.textContent,left:b.left,right:b.right,top:b.top,bottom:b.bottom};
          })
        })));
        await frame.locator("#cave-surface-depth").screenshot({path:path.join(folder,`${width}-${view}.png`)});
      }
    }
    const ids=await frame.locator("#csd-module option").evaluateAll(nodes=>nodes.map(n=>n.value));
    for(const id of ids){
      await frame.locator("#csd-module").selectOption(id);
      report.modules.push(await frame.evaluate(()=>({
        id:document.getElementById("cave-surface-depth")._inspect().state.module,
        invalid:/NaN|undefined|Infinity/.test(document.getElementById("csd-map").innerHTML)
      })));
    }
    await frame.locator("#csd-bounds").check();
    await frame.locator("#csd-content").uncheck();
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:{kind:"cave-surface-depth-v1",view:"all",module:"M01",bounds:false,content:true}}}}})));
    report.restored=await inspect();
    report.portRelations=await frame.evaluate(()=>{
      const Q=globalThis.CaveDepthDesign;
      return {
        entranceFloor:Q.port("M01","entry"),entranceSurface:Q.surfaceY(38),
        entranceRoof:Q.world(Q.nodes[0],Q.shape(Q.byId("M01"))).roof[0],
        joins:Q.links.map(l=>({id:l.id||`${l.from}-${l.to}`,
          from:l.floor[0],source:Q.port(l.from,l.branch?"branch":"exit"),
          to:l.floor.at(-1),target:Q.port(l.to,"entry")})),
        lowerMine:Q.port("M12","entry")
      };
    });
    report.ok=!report.errors.length && report.layouts.every(x=>!x.invalid && x.scroll<=x.width+1 && x.labels.every(b=>b.left>=-1 && b.right<=x.width+1)) && report.modules.every(x=>!x.invalid);
  }finally{
    await browser.close();
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  }
  console.log(JSON.stringify({ok:report.ok,folder,layouts:report.layouts.length,modules:report.modules.length,errors:report.errors}));
  if(!report.ok)process.exitCode=1;
})().catch(e=>{console.error(e.stack);process.exitCode=1;});
