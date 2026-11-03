const fs=require("fs"),path=require("path"),{pathToFileURL}=require("url");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
(async()=>{
  const folder=path.join(__dirname,"records",Date.now()+"-display-review");
  fs.mkdirSync(folder);
  const report={scope:"Compact design display, suggested polyline and state only; no gameplay, actual traversal, elapsed play time or Unity test.",errors:[],goals:[],layouts:[]};
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try{
    const page=await browser.newPage({viewport:{width:736,height:1050}});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto(pathToFileURL(path.join(__dirname,"records/preview.html")).href);
    const frame=page.frames().find(f=>f.parentFrame());
    const inspect=()=>frame.evaluate(()=>document.getElementById("compact-natural-mine")._inspect());
    await frame.waitForFunction(()=>document.getElementById("compact-natural-mine")?.dataset.ready==="true");
    report.initial=await inspect();
    for(const id of ["A","B","C"]){
      await frame.locator("#cnm-goal").selectOption(id);
      await frame.waitForFunction(()=>!document.getElementById("compact-natural-mine")._inspect().moving);
      const info=await inspect();
      info.displayPath=await frame.evaluate(()=>{
        const p=document.querySelector("#cnm-map .route"),length=p.getTotalLength(),end=p.getPointAtLength(length);
        return {length,end:{x:end.x,y:end.y}};
      });
      report.goals.push(info);
      await frame.locator("#compact-natural-mine").screenshot({path:path.join(folder,`736-${id}.png`)});
    }
    report.sameCamera=report.goals.every(g=>JSON.stringify(g.camera)===JSON.stringify(report.goals[0].camera));
    report.routeDisplay=await frame.evaluate(()=>{
      const path=document.querySelector("#cnm-map .route"),length=path.getTotalLength();
      const cavities=[...document.querySelectorAll("#cnm-map g.air path")],outside=[];
      for(let i=0;i<=500;i++){
        const p=path.getPointAtLength(length*i/500);
        if(!cavities.some(c=>c.isPointInFill(p)))outside.push({x:p.x,y:p.y});
      }
      return {samples:501,outside};
    });
    for(const width of [736,360,320]){
      await page.setViewportSize({width,height:1050});
      await frame.waitForFunction(expected=>innerWidth<=expected && innerWidth>=expected-48,width);
      await page.emulateMedia({colorScheme:width===320?"dark":"light"});
      for(const view of ["all","shallow","middle","deep"]){
        await frame.locator("#cnm-view").selectOption(view);
        await frame.waitForFunction(()=>Math.abs(document.querySelector("#cnm-map svg").width.baseVal.value-document.getElementById("cnm-map").getBoundingClientRect().width)<=1);
        report.layouts.push(await frame.evaluate(()=>({
          width:innerWidth,scroll:document.documentElement.scrollWidth,
          view:document.getElementById("compact-natural-mine")._inspect().state.view,
          invalid:/NaN|undefined|Infinity/.test(document.getElementById("cnm-map").innerHTML),
          labels:[...document.querySelectorAll("#cnm-map text")].map(n=>{
            const b=n.getBoundingClientRect();
            return {text:n.textContent,left:b.left,right:b.right,top:b.top,bottom:b.bottom};
          })
        })));
        await frame.locator("#compact-natural-mine").screenshot({path:path.join(folder,`${width}-${view}.png`)});
      }
    }
    await frame.locator("#cnm-bounds").check();
    await frame.locator("#cnm-route").uncheck();
    await frame.locator("#cnm-content").uncheck();
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:{kind:"compact-natural-mine-v1",view:"all",goal:"B",selected:"06",bounds:false,route:true,content:true}}}}})));
    report.restored=await inspect();
    report.independentDistances=await frame.evaluate(()=>{
      const C=globalThis.CompactNaturalMine;
      return C.goals.map(g=>({
        id:g.id,distance:C.route.slice(1,g.index+1).reduce((sum,p,i)=>sum+Math.hypot(p[0]-C.route[i][0],p[1]-C.route[i][1]),0)
      }));
    });
    report.ok=!report.errors.length && report.sameCamera && report.goals.every(g=>Math.abs(g.displayPath.length-g.shown)<.02) && !report.routeDisplay.outside.length && report.layouts.every(x=>!x.invalid && x.scroll<=x.width+1 && x.labels.every(b=>b.left>=-1 && b.right<=x.width+1)) && report.restored.state.goal==="B";
  }finally{
    await browser.close();
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  }
  console.log(JSON.stringify({ok:report.ok,folder,layouts:report.layouts.length,sameCamera:report.sameCamera,errors:report.errors}));
  if(!report.ok)process.exitCode=1;
})().catch(e=>{console.error(e.stack);process.exitCode=1;});
