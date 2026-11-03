"use strict";
const fs=require("node:fs"),path=require("node:path"),assert=require("node:assert"),crypto=require("node:crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const base="C:/Users/Jobscn/.codex/visualizations/2026/10/30/01a18eaf-1b9d-7f23-a7d0-91286addbf55";
const folder=path.join(__dirname,"records",Date.now()+"-browser");
fs.mkdirSync(folder,{recursive:true});
const report={date:"2026-10-31",scope:"Browser shape study only; no Unity or actor traversal.",errors:[],cases:[],layouts:[]};
(async()=>{
  const browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try{
    const page=await browser.newPage({viewport:{width:768,height:1250}});
    page.on("pageerror",e=>report.errors.push(e.message));
    await page.goto("file:///"+base+"/dualgrid-wfc-shapes-browser.html");
    const frame=page.frames()[1],host=frame.locator("#dg-wfc-study");
    await frame.waitForFunction(()=>{const r=document.getElementById("dg-wfc-study");return r?.dataset.ready||r?.dataset.error;});
    assert(!await host.getAttribute("data-error"),"Initial render failed: "+await host.getAttribute("data-error"));
    const inspect=()=>frame.evaluate(()=>document.getElementById("dg-wfc-study")._inspect());
    const capture=name=>host.screenshot({path:path.join(folder,name+".png")});
    for(const scene of ["basin","shoulder","network"]){
      await frame.locator("#ds-scene").selectOption(scene);
      await frame.waitForFunction(scene=>document.getElementById("dg-wfc-study")._inspect().state.scene===scene&&!document.getElementById("ds-next").disabled,scene);
      for(const method of ["dual","wfc","hybrid"]){
        await frame.locator('[data-method="'+method+'"]').click();
        const value=await inspect();
        assert.equal(value.state.method,method);assert(value.sameOccupancy);
        assert.equal(value.dual.mismatches,0);assert.equal(value.wfc.mismatches,0);
        assert(value.stats.propagations>0&&value.stats.observations>0);
        assert(value.hybridStats.propagations>0&&value.hybridStats.observations>0);
        assert.equal(value.macroMismatches,0);assert.equal(value.macroLayout.length,6);
        report.cases.push({scene,method,...value});await capture(scene+"-"+method);
      }
      const observations=(await inspect()).stats.observations;
      const macroBefore=JSON.stringify((await inspect()).macroLayout);
      await frame.locator("#ds-dig").click();
      const dug=await inspect();assert.equal(dug.dual.changed,4);assert.equal(dug.wfc.changed,4);
      assert(dug.hybrid.changed>0&&dug.hybrid.changed<16);assert.equal(dug.stats.observations,observations);
      assert.equal(JSON.stringify(dug.macroLayout),macroBefore);
      await capture(scene+"-dig");await frame.locator("#ds-dig").click();
    }
    await frame.locator("#ds-scene").selectOption("basin");
    await frame.waitForFunction(()=>document.getElementById("dg-wfc-study")._inspect().state.scene==="basin"&&!document.getElementById("ds-next").disabled);
    await frame.locator("#ds-camera").selectOption("close");
    await frame.locator("#ds-overlay").check();await frame.locator("#ds-grid").check();await frame.locator("#ds-atlas").check();
    await capture("close-overlay-atlas");
    const box=await frame.locator("#ds-canvas").boundingBox();
    await page.mouse.click(box.x+box.width*.42,box.y+box.height*.55);
    assert((await inspect()).selected!==null);
    for(const width of [736,360,320])for(const theme of ["light","dark"]){
      await page.setViewportSize({width:width+32,height:1250});await page.emulateMedia({colorScheme:theme});
      await page.waitForTimeout(80);
      const layout=await frame.evaluate(()=>{
        const r=document.getElementById("dg-wfc-study"),bounds=r.getBoundingClientRect();
        return{width:bounds.width,overflow:document.documentElement.scrollWidth>innerWidth,
          clipped:[...r.querySelectorAll("button,input,select")].filter(node=>{
            const b=node.getBoundingClientRect();return b.width&&(b.left<bounds.left-1||b.right>bounds.right+1);
          }).map(n=>n.id||n.textContent)};
      });
      assert(!layout.overflow&&!layout.clipped.length,JSON.stringify(layout));
      assert(await frame.evaluate(()=>{
        const r=document.getElementById("dg-wfc-study");
        return r.querySelector("#ds-atlas").checked||r.querySelector("#ds-atlas-region").getBoundingClientRect().height===0;
      }),"Hidden atlas must not reserve blank space");
      report.layouts.push({width,theme,...layout});await capture(width+"-"+theme);
    }
    report.failedGenerationRetainsScene=await frame.evaluate(async()=>{
      const r=document.getElementById("dg-wfc-study"),before=r._inspect(),solve=window.CaveWB.solve;
      window.CaveWB.solve=async()=>{throw new Error("fixture contradiction");};
      try{
        const ok=await r._authoring.generate({...before.state,seed:999});
        return !ok&&r._inspect().geometryIdentity===before.geometryIdentity&&!r.querySelector("#ds-error").hidden;
      }finally{window.CaveWB.solve=solve;}
    });
    assert(report.failedGenerationRetainsScene);
    await frame.locator("#ds-next").click();
    await frame.waitForFunction(()=>!document.getElementById("ds-next").disabled);
    report.restored=await frame.evaluate(async()=>{
      const r=document.getElementById("dg-wfc-study"),value={...r._inspect().state,seed:902,scene:"shoulder",method:"wfc",dug:true};
      window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{privateContent:value}}}}));
      for(let i=0;i<100;i++){
        if(r._inspect().state.seed===902)return r._inspect();
        await new Promise(resolve=>setTimeout(resolve,10));
      }
      return r._inspect();
    });
    assert.equal(report.restored.state.seed,902);assert.equal(report.restored.state.method,"wfc");assert(report.restored.state.dug);
    assert(!report.errors.length,report.errors.join(","));
    const source=path.join(base,"dualgrid-wfc-shapes.html");
    report.fragment={path:source,sha256:crypto.createHash("sha256").update(fs.readFileSync(source)).digest("hex")};
    report.passed=true;
  }catch(error){report.failure=String(error.stack||error);report.passed=false;process.exitCode=1;}
  finally{
    fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n",{flag:"wx"});
    await browser.close();console.log(JSON.stringify({passed:report.passed,errors:report.errors,failure:report.failure,
      cases:report.cases.length,layouts:report.layouts.length,report:path.join(folder,"report.json")},null,2));
  }
})();
