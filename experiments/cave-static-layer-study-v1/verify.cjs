"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const folder=path.join(__dirname,"records",Date.now()+"-verification");
fs.mkdirSync(folder,{recursive:true});
const report={date:"2026-10-31",scope:"Independent static-terrain comparison; no game digging or Unity changes.",models:[],views:[],layouts:[],errors:[]};
for(const file of ["../cave-wfc-whitebox-v1/plan.js","../cave-wfc-whitebox-v1/catalog.js",
  "../dualgrid-wfc-shape-study-v1/model.js","model.js"])
  vm.runInThisContext(fs.readFileSync(path.join(__dirname,file),"utf8"),{filename:file});
(async()=>{
  let browser;
  try{
    for(const form of [0,1])for(const seed of [271,902,1643]){
      const d=StaticCave.generate(form,seed);
      assert.equal(d.whole.signature,d.hybrid.signature);
      assert.equal(d.whole.geometry,d.scene.geometry);assert.equal(d.hybrid.geometry,d.scene.geometry);
      assert(d.whole.stats.observations>0&&d.hybrid.stats.observations>0);
      const check=CaveWB.verify({...d.scene.map,tiles:d.whole.tiles});
      assert.equal(check.mismatches,0);assert.equal(check.cornerConflicts,0);
      let jump=0,previous=ShapeStudy.groundAt(d.scene.map,1);
      for(let x=1.125;x<=41;x+=.125){
        const y=ShapeStudy.groundAt(d.scene.map,x);jump=Math.max(jump,Math.abs(y-previous));previous=y;
      }
      assert(jump<.6,"Ecology stroke crossed a discontinuous floor branch: "+jump);
      for(const mode of ["whole","hybrid"])assert.throws(()=>StaticCave.solve(d.scene,mode,seed,true),/四次/);
      report.models.push({form,seed,equivalent:true,geometryPreserved:true,bothRejectConflict:true,
        maxFloorSampleJump:jump,wholeStats:d.whole.stats,hybridStats:d.hybrid.stats});
    }
    browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
    const page=await browser.newPage({viewport:{width:768,height:1100},reducedMotion:"reduce"});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto("file:///"+base+"/cave-static-layers-browser.html");
    const fragment=fs.readFileSync(base+"/cave-static-layers.html","utf8");
    const embedded=await page.evaluate(html=>new DOMParser().parseFromString(html,"text/html").querySelector("iframe").getAttribute("data-srcdoc"),
      fs.readFileSync(base+"/cave-static-layers-browser.html","utf8"));
    assert(embedded.includes(fragment.replace(/\r\n/g,"\n").trim()));
    const frame=page.frames()[1],host=frame.locator("#static-cave-study");
    await frame.waitForFunction(()=>{const r=document.getElementById("static-cave-study");return r?.dataset.ready||r?.dataset.error;});
    assert(!await host.getAttribute("data-error"),await host.getAttribute("data-error"));
    const inspect=()=>frame.evaluate(()=>document.getElementById("static-cave-study")._inspect());
    const pixels=()=>frame.evaluate(()=>document.getElementById("sc-canvas").toDataURL());
    let camera=null;
    for(const form of [0,1]){
      if(form)await frame.locator("#sc-form").click();
      let fullPixels=null;
      for(const mode of ["base","whole","hybrid"]){
        await frame.locator('[data-mode="'+mode+'"]').click();
        const value=await inspect();assert.equal(value.state.form,form);
        assert.equal(value.geometry,value.wholeGeometry);assert.equal(value.geometry,value.hybridGeometry);
        assert.deepEqual(value.wholeEco,value.hybridEco);
        const matrix=JSON.stringify(value.viewport);if(camera===null)camera=matrix;assert.equal(matrix,camera);
        if(mode==="whole")fullPixels=await pixels();
        if(mode==="hybrid")assert.equal(await pixels(),fullPixels,"Equivalent encodings produced different pixels.");
        await host.screenshot({path:path.join(folder,"form"+form+"-"+mode+".png")});
        report.views.push({form,mode,viewport:value.viewport});
      }
    }
    const old=await inspect();await frame.locator("#sc-shuffle").click();const shuffled=await inspect();
    assert.equal(shuffled.geometry,old.geometry);assert.notDeepEqual(shuffled.hybridEco,old.hybridEco);
    const beforeConflict=await pixels();await frame.locator("#sc-conflict").click();
    const failed=await inspect();assert(failed.state.conflict&&failed.conflictRejected);
    assert.equal(failed.geometry,shuffled.geometry);assert.deepEqual(failed.hybridEco,shuffled.hybridEco);
    assert.equal(await frame.locator("#sc-error").isVisible(),true);
    await host.screenshot({path:path.join(folder,"conflict-rejected.png")});
    await frame.locator("#sc-conflict").click();assert.equal(await pixels(),beforeConflict);
    await frame.locator("#sc-layers").check();
    for(const mode of ["whole","hybrid"]){
      await frame.locator('[data-mode="'+mode+'"]').click();
      await host.screenshot({path:path.join(folder,"layers-"+mode+".png")});
    }
    for(const width of [736,360,320])for(const theme of ["light","dark"]){
      await page.setViewportSize({width:width+32,height:1100});await page.emulateMedia({colorScheme:theme,reducedMotion:"reduce"});
      await page.waitForTimeout(60);
      const bounds=await frame.evaluate(()=>{
        const r=document.getElementById("static-cave-study"),b=r.getBoundingClientRect();
        return{width:b.width,overflow:document.documentElement.scrollWidth>innerWidth,
          clipped:[...r.querySelectorAll("button,input")].filter(n=>{const x=n.getBoundingClientRect();return x.left<b.left-1||x.right>b.right+1;}).length};
      });
      assert(!bounds.overflow&&!bounds.clipped);report.layouts.push({width,theme,...bounds});
      await host.screenshot({path:path.join(folder,width+"-"+theme+".png")});
    }
    await page.setViewportSize({width:768,height:1100});await frame.locator("#sc-layers").uncheck();
    report.animation=await frame.evaluate(()=>{
      const r=document.getElementById("static-cave-study"),a=r._authoring,canvas=document.getElementById("sc-canvas"),c=canvas.getContext("2d");
      a.draw(0);const first=c.getImageData(0,0,canvas.width,canvas.height).data;
      a.draw(.7);const second=c.getImageData(0,0,canvas.width,canvas.height).data,v=r._inspect().viewport,slot=a.data.scene.slots[5];
      const rectCount=(x,y,w,h)=>{
        let count=0;
        for(let py=Math.max(0,Math.floor(v.oy+y*v.scale));py<Math.min(canvas.height,v.oy+(y+h)*v.scale);py++)
          for(let px=Math.max(0,Math.floor(v.ox+x*v.scale));px<Math.min(canvas.width,v.ox+(x+w)*v.scale);px++){
            const i=(py*canvas.width+px)*4;if([0,1,2,3].some(j=>first[i+j]!==second[i+j]))count++;
          }
        return count;
      };
      a.draw(0);const steady=canvas.toDataURL();a.draw(0);
      return{water:rectCount(29,10,2,12),mushroom:rectCount(slot.x-1,slot.y-1.8,2,2),
        deterministicAtSameTime:steady===canvas.toDataURL()};
    });
    assert(report.animation.water>10&&report.animation.mushroom>0&&report.animation.deterministicAtSameTime);
    await page.emulateMedia({reducedMotion:"no-preference"});await page.waitForTimeout(60);
    const movingPixels=await pixels();await page.waitForTimeout(300);
    assert.notEqual(await pixels(),movingPixels,"Actual animation loop did not advance.");
    await page.emulateMedia({reducedMotion:"reduce"});await page.waitForTimeout(50);
    const stillPixels=await pixels();await page.waitForTimeout(150);
    assert.equal(await pixels(),stillPixels,"Reduced-motion view kept animating.");
    report.animation.actualLoopAdvances=true;report.animation.reducedMotionStops=true;
    await frame.evaluate(()=>window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{
      widgetState:{privateContent:{version:1,mode:"whole",form:0,seed:1643,layers:true,conflict:false}}}}})));
    const restored=await inspect();assert.equal(restored.state.mode,"whole");assert.equal(restored.state.seed,1643);assert(restored.identical);
    assert.equal(report.errors.length,0,report.errors.join("\n"));
    report.output={path:base+"/cave-static-layers.html",sha256:crypto.createHash("sha256").update(fragment).digest("hex")};
    report.passed=true;
  }catch(error){report.passed=false;report.failure=error.stack;process.exitCode=1;}
  finally{await browser?.close();fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
    console.log(JSON.stringify({folder,passed:report.passed,models:report.models.length,views:report.views.length,
      layouts:report.layouts.length,animation:report.animation,failure:report.failure,errors:report.errors}));}
})();
