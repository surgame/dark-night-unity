"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const folder=path.join(__dirname,"records",Date.now()+"-pressure-verification");
fs.mkdirSync(folder,{recursive:true});
const report={date:"2026-11-01",scope:"Grouped CPU throughput and independent batch completion; no Unity.",
  models:[],layouts:[],errors:[],measurements:[]};
const files=["geometry","solver","meshes","adapters","pressure-model"];
for(const file of files)vm.runInThisContext(fs.readFileSync(path.join(__dirname,file+".js"),"utf8"),{filename:file});
const T=TerrainFour;
(async()=>{
  let browser,ticker;
  try{
    for(const w of [96,320])for(const profile of T.pressureProfiles) {
      const field=T.field(w,w===320?192:60),edits=T.pressureEdits(field,256,profile,902);
      assert.equal(new Set(edits).size,256);let shape=null,wfc=null;
      for(const method of T.methods) {
        const base=T.adapter(field,method),a=T.forkAdapter(base),b=T.forkAdapter(base),frozen=T.signature(base.masks);
        const immediate=T.pressureBatch(a,edits,"immediate"),merged=T.pressureBatch(b,edits,"merged");
        const check=T.pressureCheck(a);assert.deepEqual(check,T.pressureCheck(b));
        for(const key of new Set([...immediate.chunks,...merged.chunks]))
          assert.deepEqual(a.meshes.get(key).data,b.meshes.get(key).data);
        assert.equal(T.signature(base.masks),frozen);assert(merged.rebuilds<=immediate.rebuilds);
        if(shape===null)shape=check.geometry;assert.equal(shape,check.geometry);
        if(method==="whole"||method==="hybrid"){if(wfc===null)wfc=check.variant;assert.equal(wfc,check.variant);}
        report.models.push({w,profile,method,valid:256,geometryEqual:true,mergedMeshEqual:true,
          immediateRebuilds:immediate.rebuilds,mergedRebuilds:merged.rebuilds});
      }
    }
    browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe",
      args:["--use-angle=d3d11","--enable-gpu"]});
    const page=await browser.newPage({viewport:{width:768,height:1400},reducedMotion:"reduce"});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto("file:///"+base+"/terrain-pressure-browser.html");
    const frame=page.frames()[1],host=frame.locator("#terrain-fourway");
    await frame.waitForFunction(()=>document.getElementById("terrain-fourway")?._pressure);
    assert(await frame.evaluate(()=>typeof d3==="object"),"Pinned D3 dependency did not load.");
    const target=await frame.evaluate(()=>document.getElementById("terrain-fourway")._target()),
      canvasBox=await frame.locator("#tf-overlay").boundingBox();
    await page.mouse.click(canvasBox.x+target.x/96*canvasBox.width,canvasBox.y+target.y/60*canvasBox.height);
    const before=await frame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().count);
    assert.equal(before,1);
    if(process.argv.includes("--measure")){
      const references={};let lastStatus="";
      ticker=setInterval(async()=>{
        try{const text=await frame.locator("#tp-progress").textContent();if(text!==lastStatus){
          lastStatus=text;console.log(JSON.stringify({progress:text}));}}
        catch(_){}
      },15000);
      for(const w of [96,320]){
        await frame.locator("#tf-size").selectOption(String(w));
        const measured=await frame.evaluate(()=>document.getElementById("terrain-fourway")._pressure({
          rounds:16,profiles:TerrainFour.pressureProfiles,policies:TerrainFour.pressurePolicies}));
        assert(measured.success&&!measured.software);assert.equal(measured.raw.length,16*4*2*4*4);
        fs.writeFileSync(path.join(folder,"pressure-"+w+"-raw.json"),JSON.stringify(measured,null,2)+"\n");
        const ref={...measured};delete ref.raw;delete ref.checks;
        ref.sources=["geometry","solver","meshes","adapters","gpu","benchmark","pressure-model","pressure-benchmark"].map(name=>({
          path:name+".js",sha256:crypto.createHash("sha256").update(fs.readFileSync(path.join(__dirname,name+".js"))).digest("hex")}));
        references[String(w)]=ref;
        report.measurements.push({w,renderer:ref.renderer,software:ref.software,raw:measured.raw.length,
          operations:ref.validOperations,elapsedMs:ref.elapsedMs,path:path.join(folder,"pressure-"+w+"-raw.json")});
        console.log(JSON.stringify({measured:w,rows:measured.raw.length,operations:ref.validOperations,seconds:ref.elapsedMs/1000}));
        await host.screenshot({path:path.join(folder,"measured-"+w+".png")});
      }
      fs.writeFileSync(path.join(__dirname,"pressure-references.json"),JSON.stringify(references,null,2)+"\n");
      clearInterval(ticker);ticker=null;
    }else if(process.argv.includes("--quick")){
      const measured=await frame.evaluate(()=>document.getElementById("terrain-fourway")._pressure({
        rounds:1,profiles:["holes"],policies:["merged"]}));
      assert(measured.success);report.quick={rows:measured.raw.length,operations:measured.validOperations,
        example:measured.summary.holes.merged[1024]};
    }else if(process.argv.includes("--button")){
      await frame.locator("#tp-profile").selectOption("tunnel");
      await frame.locator("#tp-policy").selectOption("merged");
      await frame.locator("#tp-rounds").selectOption("8");
      await frame.locator("#tp-run").click();
      await frame.waitForFunction(()=>document.getElementById("tp-progress").textContent==="压力评估完成",null,{timeout:120000});
      const measured=await frame.evaluate(()=>document.getElementById("terrain-fourway")._pressureInspect().last);
      assert(measured.success);assert.deepEqual(measured.profiles,["tunnel"]);assert.deepEqual(measured.policies,["merged"]);
      assert.equal(measured.rounds,8);assert.equal(measured.raw.length,128);
      fs.writeFileSync(path.join(folder,"button-raw.json"),JSON.stringify(measured,null,2)+"\n");
      report.button={rounds:8,rows:128,operations:measured.validOperations};
    }
    assert.equal(await frame.evaluate(()=>document.getElementById("terrain-fourway")._inspect().count),
      process.argv.includes("--measure")?0:before);
    for(const profile of T.pressureProfiles)for(const policy of T.pressurePolicies) {
      await frame.locator("#tp-profile").selectOption(profile);await frame.locator("#tp-policy").selectOption(policy);
      const rows=await frame.locator("#tp-results tr").count();assert.equal(rows,4);
    }
    for(const size of [320,96]) {
      await frame.locator("#tf-size").selectOption(String(size));
      const r=await frame.evaluate(()=>document.getElementById("terrain-fourway")._pressureInspect().report);
      if(r)assert.equal(r.width,size);
    }
    for(const width of [736,360,320])for(const theme of ["light","dark"]){
      await page.setViewportSize({width:width+32,height:1400});await page.emulateMedia({colorScheme:theme,reducedMotion:"reduce"});
      await page.waitForTimeout(40);
      const bounds=await frame.evaluate(()=>{
        const root=document.getElementById("terrain-fourway"),r=root.getBoundingClientRect();
        const bad=[...root.querySelectorAll("button,select")].filter(el=>{const b=el.getBoundingClientRect();
          return b.width&&(!el.closest("details")||el.closest("details").open)&&(b.left<r.left-1||b.right>r.right+1);});
        const labels=[...root.querySelectorAll("#tf-pressure svg text")].filter(el=>{
          const b=el.getBoundingClientRect(),s=el.closest("svg").getBoundingClientRect();
          return b.left<s.left-1||b.right>s.right+1||b.top<s.top-1||b.bottom>s.bottom+1;
        });
        return {overflow:document.documentElement.scrollWidth>innerWidth,clipped:bad.length,clippedLabels:labels.length,
          svg:[...root.querySelectorAll("#tf-pressure svg")].map(el=>({width:el.getBoundingClientRect().width,paths:el.querySelectorAll("path").length}))};
      });assert(!bounds.overflow&&!bounds.clipped&&!bounds.clippedLabels);report.layouts.push({width,theme,...bounds});
      await host.screenshot({path:path.join(folder,width+"-"+theme+".png")});
    }
    assert.equal(report.errors.length,0,report.errors.join("\n"));
    report.output={path:base+"/terrain-pressure.html",sha256:crypto.createHash("sha256").update(fs.readFileSync(base+"/terrain-pressure.html")).digest("hex")};
    report.passed=true;
  }catch(error){report.passed=false;report.failure=error.stack;process.exitCode=1;}
  finally{if(ticker)clearInterval(ticker);await browser?.close();
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
    console.log(JSON.stringify({folder,passed:report.passed,models:report.models.length,layouts:report.layouts.length,
      quick:report.quick,button:report.button,measurements:report.measurements,failure:report.failure,errors:report.errors}));
  }
})();
