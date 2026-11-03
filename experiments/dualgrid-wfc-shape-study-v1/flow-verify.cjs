"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const folder=path.join(__dirname,"records",Date.now()+"-flow");
fs.mkdirSync(folder,{recursive:true});
const report={date:"2026-10-31",scope:"Continuous map explanation only; no Unity.",models:[],views:[],layouts:[],errors:[]};
for(const file of ["../cave-wfc-whitebox-v1/plan.js","../cave-wfc-whitebox-v1/catalog.js","../cave-wfc-whitebox-v1/solver.js",
  "model.js","macro.js","hybrid.js","steps-trace.js","flow-model.js"])
  vm.runInThisContext(fs.readFileSync(path.join(__dirname,file),"utf8"),{filename:file});
(async()=>{
  let browser;
  try{
    for(const seed of [271,902,1643]){
      const data=await ShapeStudy.buildFlow(seed),trace=data.trace,frames=data.micro.frames;
      assert.equal(trace.timeline.events.length,trace.stats.observations);
      assert.equal(frames.length,trace.stats.observations+1);
      for(let n=1;n<frames.length;n++)assert(frames[n].counts.every((v,i)=>v<=frames[n-1].counts[i]),"Replay increased a domain.");
      const final=frames.at(-1);
      data.before.wfc.tiles.forEach((id,i)=>{
        assert.equal(final.counts[i],1);CaveWB.catalog[id].values.forEach((v,j)=>assert.equal(final.values[i*4+j],v));
      });
      const observed=data.trace.event.observed;
      assert(data.micro.observed.counts.every((v,i)=>v===observed[i].length));
      assert.equal(data.dug.dual.changed.length,4);assert.equal(data.dug.wfc.changed.length,4);
      const macro=data.macro.selected;
      macro.slice(1).forEach((c,i)=>{
        assert.equal(c.left,macro[i].right);
        for(const name of ["roof","floor"]){
          const a=data.macroCurves[macro[i].id][i][name].at(-1),b=data.macroCurves[c.id][i+1][name][0];
          assert(Math.abs(a[0]-b[0])<1e-6&&Math.abs(a[1]-b[1])<1e-6);
        }
      });
      report.models.push({seed,history:trace.timeline.events.length,finalIdentity:true,macroPorts:true});
    }
    browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
    const page=await browser.newPage({viewport:{width:768,height:1100},reducedMotion:"reduce"});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto("file:///"+base+"/dualgrid-wfc-continuous-browser.html");
    const fragment=fs.readFileSync(base+"/dualgrid-wfc-continuous.html","utf8");
    const embedded=await page.evaluate(html=>new DOMParser().parseFromString(html,"text/html").querySelector("iframe").getAttribute("data-srcdoc"),
      fs.readFileSync(base+"/dualgrid-wfc-continuous-browser.html","utf8"));
    assert(embedded.includes(fragment.replace(/\r\n/g,"\n").trim()));
    const frame=page.frames()[1],host=frame.locator("#terrain-flow");
    await frame.waitForFunction(()=>{const r=document.getElementById("terrain-flow");return r?.dataset.ready||r?.dataset.error;});
    assert(!await host.getAttribute("data-error"),await host.getAttribute("data-error"));
    const inspect=()=>frame.evaluate(()=>document.getElementById("terrain-flow")._inspect());
    const settle=step=>frame.waitForFunction(step=>{
      const r=document.getElementById("terrain-flow")._inspect();return r.state.step===step&&!r.animating&&Math.abs(r.position-step)<1e-6;
    },step);
    await frame.evaluate(()=>{window._saves=[];window.openai={setWidgetState:value=>{window._saves.push(value);return Promise.resolve();}};});
    for(const [width,theme] of [[736,"light"],[320,"dark"]]){
      await page.setViewportSize({width:width+32,height:1100});await page.emulateMedia({colorScheme:theme,reducedMotion:"reduce"});
      let camera=null,anchors=null,canvasOrigin=null;
      for(const method of ["dual","wfc","hybrid"]){
        await frame.locator('[data-method="'+method+'"]').click();
        for(let step=0;step<6;step++){
          await frame.locator("#fl-stage").selectOption(String(step));await settle(step);
          const value=await inspect(),matrix=JSON.stringify(value.frame.viewport),points=JSON.stringify(value.frame.anchors);
          const origin=await frame.evaluate(()=>{
            const b=document.getElementById("fl-canvas").getBoundingClientRect();return JSON.stringify([b.x,b.y,b.width,b.height]);
          });
          if(camera===null){camera=matrix;anchors=points;}
          if(canvasOrigin===null)canvasOrigin=origin;
          assert.equal(matrix,camera,"Camera changed during steps or methods.");assert.equal(points,anchors);
          assert.equal(origin,canvasOrigin,"Canvas moved in the page during the sequence.");
          assert(value.finalTileIdentity);assert.equal(value.historyLength,value.observations);
          await host.screenshot({path:path.join(folder,width+"-"+theme+"-"+method+"-"+step+".png")});
          report.views.push({width,theme,method,step,camera:value.frame.viewport,anchors:value.frame.anchors});
        }
        for(let n=0;n<5;n++){await frame.locator("#fl-prev").click();await settle(4-n);}
        for(let n=1;n<=5;n++){await frame.locator("#fl-next").click();await settle(n);}
      }
    }
    for(const width of [736,360,320])for(const theme of ["light","dark"]){
      await page.setViewportSize({width:width+32,height:1100});await page.emulateMedia({colorScheme:theme,reducedMotion:"reduce"});
      await page.waitForTimeout(60);
      const bounds=await frame.evaluate(()=>{
        const r=document.getElementById("terrain-flow"),b=r.getBoundingClientRect();
        return{width:b.width,overflow:document.documentElement.scrollWidth>innerWidth,
          clipped:[...r.querySelectorAll("button,select")].filter(n=>{const x=n.getBoundingClientRect();return x.left<b.left-1||x.right>b.right+1;}).length};
      });
      assert(!bounds.overflow&&!bounds.clipped);report.layouts.push({width,theme,...bounds});
    }
    await page.setViewportSize({width:768,height:1100});await page.emulateMedia({colorScheme:"light",reducedMotion:"no-preference"});
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{
      widgetState:{privateContent:{version:3,method:"wfc",step:2}}}}})));
    report.stateBridge=await frame.evaluate(()=>({mockSurvivedGlobals:String(window.openai?.setWidgetState).includes("_saves")}));
    await frame.evaluate(()=>{
      window.openai={setWidgetState:value=>{window._saves.push(value);return Promise.resolve();}};
    });
    const savedBefore=await frame.evaluate(()=>window._saves.length);
    const initial=await inspect();await frame.locator("#fl-next").click();
    await page.waitForTimeout(260);
    const moving=await inspect();assert(moving.position>2&&moving.position<3);
    assert.deepEqual(moving.frame.viewport,initial.frame.viewport);
    await host.screenshot({path:path.join(folder,"moving-wfc-propagation.png")});await settle(3);
    assert.equal(await frame.evaluate(()=>window._saves.length),savedBefore+1);
    await page.emulateMedia({reducedMotion:"reduce"});
    await frame.locator('[data-method="hybrid"]').click();
    await frame.locator("#fl-stage").selectOption("0");await settle(0);
    await frame.locator("#fl-play").click();
    await frame.waitForFunction(()=>{
      const r=document.getElementById("terrain-flow")._inspect();return r.state.step===5&&!r.playing&&!r.animating;
    });
    await frame.locator("#fl-play").click();await page.waitForTimeout(180);
    assert((await inspect()).playing);await frame.locator("#fl-play").click();
    const stopped=await inspect();assert(!stopped.playing);
    await page.waitForTimeout(1000);assert.equal((await inspect()).state.step,stopped.state.step);
    assert.equal(report.errors.length,0,report.errors.join("\n"));
    report.animation={fixedCamera:true,continuousPosition:true,autoplayCompletes:true,pauseStops:true,reducedMotion:true};
    report.output={path:base+"/dualgrid-wfc-continuous.html",sha256:crypto.createHash("sha256").update(fragment).digest("hex"),
      wrapperSHA256:crypto.createHash("sha256").update(fs.readFileSync(base+"/dualgrid-wfc-continuous-browser.html")).digest("hex")};
    report.passed=true;
  }catch(error){report.passed=false;report.failure=error.stack;process.exitCode=1;}
  finally{await browser?.close();fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
    console.log(JSON.stringify({folder,passed:report.passed,models:report.models.length,views:report.views.length,layouts:report.layouts.length,
      failure:report.failure,errors:report.errors}));}
})();
