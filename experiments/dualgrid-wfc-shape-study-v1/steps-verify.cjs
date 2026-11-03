"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const folder=path.join(__dirname,"records",Date.now()+"-steps");
fs.mkdirSync(folder,{recursive:true});
const report={date:"2026-10-31",scope:"Algorithm steps and independent browser only.",models:[],views:[],layouts:[],errors:[]};
for(const file of ["../cave-wfc-whitebox-v1/plan.js","../cave-wfc-whitebox-v1/catalog.js","../cave-wfc-whitebox-v1/solver.js",
  "model.js","macro.js","hybrid.js","steps-trace.js"])
  vm.runInThisContext(fs.readFileSync(path.join(__dirname,file),"utf8"),{filename:file});
(async()=>{
  let browser;
  try{
    for(const seed of [271,902,1643]){
      const d=await ShapeStudy.buildSteps(seed),e=d.trace.event;
      assert(d.trace.map.tiles.every((v,i)=>v===d.before.wfc.tiles[i]));
      assert(d.macro.selected.every((v,i)=>v.id===d.pair.hybridPlan.macroLayout.selected[i].id));
      assert(e.candidates.includes(e.chosen));assert.equal(e.observed[e.at].length,1);
      let minimum=Infinity,best=-1;
      e.entropies.forEach((h,i)=>{
        if(e.before[i].length<2)return;
        const priority=h+((i*.61803398875)%1)*.000001;
        if(priority<minimum){minimum=priority;best=i;}
      });
      assert.equal(best,e.at,"Recorded cell was not the actual minimum-entropy cell.");
      for(const change of e.changes){
        assert(change.before.length>change.after.length);
        assert(change.after.every(id=>change.before.includes(id)));
        const source=e.after[change.source],opposite=(change.direction+2)%4;
        assert(change.after.some(id=>source.some(s=>CaveWB.catalog[s].edges[change.direction]===CaveWB.catalog[id].edges[opposite])));
      }
      assert(e.changes.length>0);assert.equal(d.dug.dual.changed.length,4);assert.equal(d.dug.wfc.changed.length,4);
      assert(d.dug.hybrid.changed.length>0&&d.dug.hybrid.changed.length<16);assert(d.slopeAt>=0);
      const eventBefore=JSON.stringify(e.before),macroBefore=JSON.stringify(d.macro.selected);
      ShapeStudy.dig(d.pair,true);ShapeStudy.dig(d.pair,false);
      assert.equal(JSON.stringify(e.before),eventBefore);assert.equal(JSON.stringify(d.macro.selected),macroBefore);
      report.models.push({seed,observation:e.number,candidates:e.candidates.length,
        propagationChanges:e.changes.length,macroObservations:d.macro.stats.observations,
        outputSHA256:crypto.createHash("sha256").update(Buffer.from(d.before.wfc.tiles.buffer)).digest("hex"),
        changes:{dual:d.dug.dual.changed.length,wfc:d.dug.wfc.changed.length,hybrid:d.dug.hybrid.changed.length}});
    }
    browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
    const page=await browser.newPage({viewport:{width:768,height:1100}});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto("file:///"+base+"/dualgrid-wfc-steps-browser.html");
    const fragment=fs.readFileSync(base+"/dualgrid-wfc-steps.html","utf8");
    const embedded=await page.evaluate(html=>new DOMParser().parseFromString(html,"text/html")
      .querySelector("iframe").getAttribute("data-srcdoc"),fs.readFileSync(base+"/dualgrid-wfc-steps-browser.html","utf8"));
    assert(embedded.includes(fragment.replace(/\r\n/g,"\n").trim()),
      "Browser wrapper does not contain the current complete fragment.");
    const frame=page.frames()[1],host=frame.locator("#terrain-steps");
    await frame.waitForFunction(()=>{const r=document.getElementById("terrain-steps");return r?.dataset.ready||r?.dataset.error;});
    assert(!await host.getAttribute("data-error"),await host.getAttribute("data-error"));
    const inspect=()=>frame.evaluate(()=>document.getElementById("terrain-steps")._inspect());
    await frame.evaluate(()=>{
      window._saves=[];window.openai={widgetState:null,setWidgetState:value=>{window._saves.push(value);return Promise.resolve();}};
    });
    const layout=()=>frame.evaluate(()=>{
      const r=document.getElementById("terrain-steps"),b=r.getBoundingClientRect();
      return{width:b.width,overflow:document.documentElement.scrollWidth>innerWidth,
        clipped:[...r.querySelectorAll("button,select")].filter(n=>{
          const x=n.getBoundingClientRect();return x.left<b.left-1||x.right>b.right+1;
        }).map(n=>n.id||n.textContent)};
    });
    for(const [width,theme] of [[736,"light"],[320,"dark"]]){
      await page.setViewportSize({width:width+32,height:1100});await page.emulateMedia({colorScheme:theme});
      for(const method of ["dual","wfc","hybrid"]){
        await frame.locator('[data-method="'+method+'"]').click();
        for(let step=0;step<6;step++){
          await frame.locator("#st-stage").selectOption(String(step));
          const value=await inspect(),bounds=await layout();
          assert.equal(value.state.step,step);assert.equal(value.state.method,method);
          assert(value.tileIdentity&&value.macroIdentity);assert(!bounds.overflow&&!bounds.clipped.length,JSON.stringify(bounds));
          assert.equal(await frame.locator("#st-prev").isDisabled(),step===0);
          assert.equal(await frame.locator("#st-next").isDisabled(),step===5);
          await host.screenshot({path:path.join(folder,width+"-"+theme+"-"+method+"-"+step+".png")});
          report.views.push({width,theme,method,step,stage:value.stage});
        }
        for(let n=0;n<5;n++)await frame.locator("#st-prev").click();
        assert.equal((await inspect()).state.step,0);
        for(let n=0;n<5;n++)await frame.locator("#st-next").click();
        assert.equal((await inspect()).state.step,5);
      }
    }
    for(const width of [736,360,320])for(const theme of ["light","dark"]){
      await page.setViewportSize({width:width+32,height:1100});await page.emulateMedia({colorScheme:theme});
      await frame.locator("#st-stage").selectOption("3");await page.waitForTimeout(50);
      const bounds=await layout();assert(!bounds.overflow&&!bounds.clipped.length);
      report.layouts.push({width,theme,...bounds});
    }
    const savedBefore=await frame.evaluate(()=>window._saves.length);
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{
      detail:{globals:{widgetState:{privateContent:{version:2,method:"wfc",step:3}}}}
    })));
    assert.equal((await inspect()).state.method,"wfc");assert.equal((await inspect()).state.step,3);
    assert.equal(await frame.evaluate(()=>window._saves.length),savedBefore,"Restore wrote state back.");
    assert.equal(report.errors.length,0,report.errors.join("\n"));
    report.output={path:base+"/dualgrid-wfc-steps.html",
      sha256:crypto.createHash("sha256").update(fragment).digest("hex"),
      wrapperSHA256:crypto.createHash("sha256").update(fs.readFileSync(base+"/dualgrid-wfc-steps-browser.html")).digest("hex"),
      exactFragmentInWrapper:true};
    report.passed=true;
  }catch(error){report.passed=false;report.failure=error.stack;process.exitCode=1;}
  finally{
    await browser?.close();
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
    console.log(JSON.stringify({folder,passed:report.passed,models:report.models.length,views:report.views.length,
      layouts:report.layouts.length,failure:report.failure,errors:report.errors}));
  }
})();
