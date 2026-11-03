"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const folder=path.join(__dirname,"records",Date.now()+"-verification");
fs.mkdirSync(folder,{recursive:true});
const report={date:"2026-11-01",models:[],views:[],layouts:[],errors:[],scope:"Independent four-way WebGL2 prototype."};
for(const name of ["geometry","solver","meshes","adapters"])
  vm.runInThisContext(fs.readFileSync(path.join(__dirname,name+".js"),"utf8"),{filename:name});
const T=TerrainFour;
(async()=>{
  let browser;
  try {
    for(const [w,h,seed] of [[64,40,271],[96,60,902],[320,192,271]]) {
      const field=T.field(w,h,seed),adapters=T.methods.map(m=>T.adapter(field,m));
      assert.equal(T.signature(adapters[1].variants),T.signature(adapters[2].variants),"Initial WFC factorisations disagree.");
      let hits=0;
      for(const kind of ["scatter","tunnel"])for(const i of T.workload(adapters[0].field,20,kind)) {
        for(const a of adapters) {
          assert(T.breakCell(a,i));assert.equal(T.checkPorts(a.masks,a.variants,w,h),0);
          assert.equal(T.signature(a.masks),T.signature(T.masks(a.field)));
        }
        const signatures=adapters.map(a=>T.signature(a.masks));assert(signatures.every(s=>s===signatures[0]));
        assert.equal(T.signature(adapters[1].variants),T.signature(adapters[2].variants),"Local WFC factorisations disagree.");
        hits++;
      }
      for(const a of adapters)for(const mesh of a.meshes.values()) {
        assert(mesh.data.every(Number.isFinite));assert.equal(mesh.data.length%(T.stride*3),0);
      }
      report.models.push({w,h,seed,hits,geometryEqual:true,ports:0,equivalentWfc:true});
    }
    const masks=new Uint8Array(9).fill(3),previous=new Uint8Array(9);
    previous[1]=4;previous[5]=8;previous[7]=1;
    const stats={propagations:0,backtracks:0,observations:0,contradictions:0};
    assert.equal(T.solveGraph([4],masks,3,3,previous,271,stats),null);
    report.finiteCatalogueRejectsThreeWay=true;
    browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe",
      args:["--use-angle=d3d11","--enable-gpu"]});
    const page=await browser.newPage({viewport:{width:768,height:1400},reducedMotion:"reduce"});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto("file:///"+base+"/terrain-fourway-browser.html");
    const frame=page.frames()[1],host=frame.locator("#terrain-fourway");
    await frame.waitForFunction(()=>{const r=document.getElementById("terrain-fourway");return r?.dataset.ready||r?.dataset.error;});
    assert(!await host.getAttribute("data-error"),await host.getAttribute("data-error"));
    const inspect=()=>frame.evaluate(()=>document.getElementById("terrain-fourway")._inspect());
    const initial=await inspect();report.renderer=initial.renderer;
    for(const method of T.methods) {
      await frame.locator('[data-method="'+method+'"]').click();
      const info=await inspect();assert.equal(info.state.method,method);assert.deepEqual(info.camera,initial.camera);
      const visual=await frame.evaluate(()=>{
        const p=document.getElementById("terrain-fourway")._pixels();let n=0;
        for(let i=4;i<p.length;i+=4)if(p[i]!==p[0]||p[i+1]!==p[1]||p[i+2]!==p[2])n++;
        return {variation:n,signature:TerrainFour.signature(p)};
      });assert(visual.variation>20000,"Terrain did not render.");
      await host.screenshot({path:path.join(folder,method+".png")});report.views.push({method,...visual});
    }
    assert.equal(report.views[1].signature,report.views[2].signature,"Equivalent WFC outputs rendered differently.");
    const target=await frame.evaluate(()=>document.getElementById("terrain-fourway")._target()),box=await frame.locator("#tf-overlay").boundingBox();
    await page.mouse.click(box.x+target.x/initial.camera.w*box.width,box.y+target.y/initial.camera.h*box.height);
    const after=await inspect();assert.equal(after.count,1,"Real mouse did not remove a solid cell.");
    assert(Object.values(after.methods).every(m=>m.geometry===after.methods.dual.geometry&&m.ports===0));
    for(const method of T.methods) {
      await frame.locator('[data-method="'+method+'"]').click();const info=await inspect();
      assert.equal(info.count,1);assert.deepEqual(info.camera,initial.camera);
    }
    await frame.locator("#tf-debug").check();
    await host.screenshot({path:path.join(folder,"destruction-debug.png")});
    await frame.locator("#tf-zoom").selectOption("2");
    const zoomed=(await inspect()).camera,rect=await frame.locator("#tf-overlay").boundingBox();
    await page.mouse.move(rect.x+rect.width*.5,rect.y+rect.height*.5);await page.mouse.down({button:"right"});
    await page.mouse.move(rect.x+rect.width*.6,rect.y+rect.height*.55,{steps:5});await page.mouse.up({button:"right"});
    const panned=await inspect();assert.notEqual(panned.camera.x,zoomed.x);assert.equal(panned.count,1);
    await frame.locator("#tf-zoom").selectOption("1");
    await frame.locator("#tf-reset").click();assert.equal((await inspect()).count,0);
    await frame.locator("#tf-debug").uncheck();
    const start=await frame.evaluate(()=>document.getElementById("terrain-fourway")._target());
    const r=await frame.locator("#tf-overlay").boundingBox();
    await page.mouse.move(r.x+start.x/96*r.width,r.y+start.y/60*r.height);await page.mouse.down();
    await page.mouse.move(r.x+(start.x+5)/96*r.width,r.y+(start.y+2)/60*r.height,{steps:20});await page.mouse.up();
    const dragged=await inspect();assert(dragged.count>2,"Drag did not break successive cells.");
    report.mouse={clickCount:after.count,dragCount:dragged.count,allMethodsSame:true,panWithoutBreaking:true};
    await page.waitForTimeout(220);
    await page.reload();
    const restoredFrame=page.frames()[1];await restoredFrame.waitForFunction(()=>document.getElementById("terrain-fourway")?.dataset.ready);
    assert.equal(await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().count),dragged.count);
    report.restore=true;
    for(const width of [736,360,320])for(const scheme of ["light","dark"]) {
      await page.setViewportSize({width:width+32,height:1400});await page.emulateMedia({colorScheme:scheme,reducedMotion:"reduce"});
      await restoredFrame.waitForTimeout(40);
      const bounds=await restoredFrame.evaluate(()=>{
        const root=document.getElementById("terrain-fourway"),r=root.getBoundingClientRect();
        return {width:r.width,overflow:document.documentElement.scrollWidth>innerWidth,
          clipped:[...root.querySelectorAll("button,select,input")].filter(el=>{const b=el.getBoundingClientRect();return b.left<r.left-1||b.right>r.right+1;}).length};
      });assert(!bounds.overflow&&!bounds.clipped);report.layouts.push({width,scheme,...bounds});
      await restoredFrame.locator("#terrain-fourway").screenshot({path:path.join(folder,width+"-"+scheme+".png")});
    }
    const references=await restoredFrame.evaluate(()=>Object.keys(TerrainFour.references));
    if(references.includes("320")) {
      await restoredFrame.locator("#tf-size").selectOption("320");
      assert.equal(await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().report.width),320);
      const target=await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._target()),
        box=await restoredFrame.locator("#tf-overlay").boundingBox();
      await page.mouse.click(box.x+target.x/320*box.width,box.y+target.y/192*box.height);
      assert.equal(await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().count),1);
      await restoredFrame.locator("#tf-size").selectOption("96");
      report.referenceCases=references;
    }
    const recoveryBefore=await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().count);
    await restoredFrame.evaluate(()=>{
      const ext=document.getElementById("tf-gpu").getContext("webgl2").getExtension("WEBGL_lose_context");
      ext.loseContext();setTimeout(()=>ext.restoreContext(),120);
    });
    await restoredFrame.waitForFunction(()=>!!document.getElementById("terrain-fourway").dataset.error);
    await restoredFrame.waitForFunction(()=>!document.getElementById("terrain-fourway").dataset.error);
    assert.equal(await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().count),recoveryBefore);
    report.contextRestored=true;
    const mobile=await browser.newContext({viewport:{width:360,height:1000},hasTouch:true,isMobile:true,reducedMotion:"reduce"});
    const mobilePage=await mobile.newPage();mobilePage.on("pageerror",e=>report.errors.push(e.message));
    await mobilePage.goto("file:///"+base+"/terrain-fourway-browser.html");const mf=mobilePage.frames()[1];
    await mf.waitForFunction(()=>document.getElementById("terrain-fourway")?.dataset.ready);
    const mt=await mf.evaluate(()=>document.getElementById("terrain-fourway")._target()),mb=await mf.locator("#tf-overlay").boundingBox();
    await mobilePage.touchscreen.tap(mb.x+mt.x/96*mb.width,mb.y+mt.y/60*mb.height);
    assert.equal(await mf.evaluate(()=>document.getElementById("terrain-fourway")._inspect().count),1);
    await mf.locator("#tf-zoom").selectOption("2");
    const mc=await mf.evaluate(()=>document.getElementById("terrain-fourway")._inspect().camera);
    const session=await mobile.newCDPSession(mobilePage),touches=[
      {x:mb.x+mb.width*.4,y:mb.y+mb.height*.5,id:1},
      {x:mb.x+mb.width*.6,y:mb.y+mb.height*.55,id:2}];
    await session.send("Input.dispatchTouchEvent",{type:"touchStart",touchPoints:touches});
    await session.send("Input.dispatchTouchEvent",{type:"touchMove",touchPoints:touches.map(p=>({...p,x:p.x+20}))});
    await session.send("Input.dispatchTouchEvent",{type:"touchEnd",touchPoints:[]});
    const mi=await mf.evaluate(()=>document.getElementById("terrain-fourway")._inspect());
    assert.equal(mi.count,1);assert.notEqual(mi.camera.x,mc.x);
    report.touch={tapCount:1,panWithoutBreaking:true};await mobile.close();
    if(process.argv.includes("--bench")) {
      report.benchmark=[];const references={};
      await page.emulateMedia({colorScheme:"light",reducedMotion:"reduce"});
      for(const size of [96,320]) {
        if(size===320)await restoredFrame.locator("#tf-size").selectOption("320");
        let measured;
        if(size===96) {
          await restoredFrame.locator("#tf-bench").click();
          await restoredFrame.waitForFunction(()=>document.getElementById("tf-progress").textContent==="评估完成",null,{timeout:60000});
          measured=await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().report);
        }else measured=await restoredFrame.evaluate(()=>document.getElementById("terrain-fourway")._bench());
        fs.writeFileSync(path.join(folder,"benchmark-"+size+"-raw.json"),JSON.stringify(measured,null,2)+"\n");
        assert(measured.success);assert.equal(measured.raw.length,4*3*2*32);
        const reference={...measured};delete reference.raw;delete reference.static;delete reference.initial;delete reference.checks;
        reference.sources=["geometry","solver","meshes","adapters","gpu","benchmark"].map(name=>({
          path:name+".js",sha256:crypto.createHash("sha256").update(fs.readFileSync(path.join(__dirname,name+".js"))).digest("hex")}));
        references[String(size)]=reference;
        report.benchmark.push({size,raw:path.join(folder,"benchmark-"+size+"-raw.json"),renderer:measured.renderer,
          software:measured.software,samples:measured.raw.length,summary:measured.summary});
        await restoredFrame.locator("#terrain-fourway").screenshot({path:path.join(folder,"benchmark-"+size+".png")});
      }
      fs.writeFileSync(path.join(__dirname,"references.json"),JSON.stringify(references,null,2)+"\n");
    }
    assert.equal(report.errors.length,0,report.errors.join("\n"));
    const fragment=fs.readFileSync(base+"/terrain-fourway.html");
    report.output={path:base+"/terrain-fourway.html",sha256:crypto.createHash("sha256").update(fragment).digest("hex")};
    report.passed=true;
  }catch(error){report.passed=false;report.failure=error.stack;process.exitCode=1;}
  finally{await browser?.close();fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
    console.log(JSON.stringify({folder,passed:report.passed,models:report.models,views:report.views.length,
      layouts:report.layouts.length,mouse:report.mouse,benchmark:report.benchmark,failure:report.failure,errors:report.errors}));}
})();
