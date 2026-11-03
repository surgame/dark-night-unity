const fs=require("fs"),path=require("path"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const root=__dirname,fragment=process.argv[2],wrapper=process.argv[3];
const folder=path.join(root,"records",Date.now()+"-browser-review");fs.mkdirSync(folder,{recursive:true});
const sha=file=>crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const report={date:"2026-11-01",scope:"Current reference identity, GPU parity/payload, controls, cancellation and layout only.",
  sources:{standalone:sha(path.join(root,"index.html")),fragment:fragment?sha(fragment):null,
    reference:sha(path.join(root,"reference.json"))},methods:[],layouts:[],errors:[]};
const check=(v,m)=>{if(!v)throw Error(m);};let browser;
(async()=>{
  browser=await chromium.launch({
    executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe",
    args:["--use-angle=d3d11","--enable-gpu"]});
  const page=await browser.newPage({viewport:{width:1000,height:1400},colorScheme:"light",reducedMotion:"reduce"});
  page.on("pageerror",e=>report.errors.push(e.message));
  await page.goto("file:///"+path.join(root,"index.html").replaceAll("\\","/"));
  await page.waitForFunction(()=>document.getElementById("cave-layout-performance")._inspect);
  const get=()=>page.evaluate(()=>document.getElementById("cave-layout-performance")._inspect());
  report.initial=await get();check(report.initial.ready&&!report.initial.error&&report.initial.resultRows===4,"Reference page not ready");
  check(!await page.locator("#cp-cancel").isVisible(),"Idle stop button visible");
  const rootSelector="#cave-layout-performance";
  await page.locator(rootSelector).screenshot({path:path.join(folder,"initial.png")});
  await page.locator("#cp-size").selectOption("128");
  await page.evaluate(()=>localStorage.setItem("dark-nights-macro-cave-comparison-v1","prior-comparison-sentinel"));
  const read=()=>page.evaluate(()=>{
    const canvas=document.getElementById("cp-scene"),gl=canvas.getContext("webgl2");
    const bytes=new Uint8Array(canvas.width*canvas.height*4);
    gl.finish();gl.readPixels(0,0,canvas.width,canvas.height,gl.RGBA,gl.UNSIGNED_BYTE,bytes);
    let hash=2166136261,min=255,max=0;
    for(let i=0;i<bytes.length;i++) {hash=Math.imul(hash^bytes[i],16777619);if(i%4===0){min=Math.min(min,bytes[i]);max=Math.max(max,bytes[i]);}}
    return {hash:hash>>>0,min,max,lost:gl.isContextLost(),error:gl.getError()};
  });
  for(const method of ["contour","blocks","bridges","wfc"]) {
    await page.locator('[data-method="'+method+'"]').click();
    await page.locator("#cp-policy").selectOption("chunks");const chunks=await read();
    await page.locator("#cp-policy").selectOption("instances");const instances=await read();
    const state=await get(),r=state.resources;
    check(chunks.hash===instances.hash&&chunks.min<chunks.max&&!chunks.lost&&!instances.lost&&
      chunks.error===0&&instances.error===0,"GPU paths differ or lost context");
    check(state.uploadBytes===r.chunkBytes+(method==="contour"?0:r.sourceBytes),"Uploaded bytes differ from resource table");
    check(state.resultRows===4&&!state.error,"Method result binding failed");
    report.methods.push({method,state,pixels:{chunks,instances}});
  }
  await page.locator("#cp-view").selectOption("all");
  await page.locator(rootSelector).screenshot({path:path.join(folder,"network.png")});
  await page.locator("#cp-profile").selectOption("standard");
  check((await get()).resultRows===4,"Standard-port reference missing");
  await page.locator("#cp-profile").selectOption("mixed");
  await page.locator("#cp-size").selectOption("2048");
  await page.locator("#cp-view").selectOption("local");
  await page.locator(rootSelector).screenshot({path:path.join(folder,"mixed-2048.png")});
  await page.locator("#cp-rounds").selectOption("32");
  await page.locator("#cp-run").click();
  await page.waitForFunction(()=>document.getElementById("cave-layout-performance")._inspect().job?.running===true);
  await page.locator("#cp-cancel").click();
  await page.waitForFunction(()=>document.getElementById("cave-layout-performance")._inspect().job?.running===false);
  report.cancel=await get();check(report.cancel.resultRows===4,"Cancel discarded the previous full result");
  await page.locator("#cp-seed").click();
  report.newSeed=await get();check(report.newSeed.choices.seed===372&&report.newSeed.resultRows===0,"Old results shown for a new seed");
  await page.reload();await page.waitForFunction(()=>document.getElementById("cave-layout-performance")._inspect);
  report.restored=await get();check(report.restored.choices.seed===372&&report.restored.choices.size===2048,"Choices not restored");
  check(await page.evaluate(()=>localStorage.getItem("dark-nights-macro-cave-comparison-v1"))==="prior-comparison-sentinel","Prior state changed");
  const fault=await page.evaluate(async()=>{
    const canvas=document.createElement("canvas"),gpu=globalThis.CavePerf.createGPU(canvas);
    const extension=canvas.getContext("webgl2").getExtension("WEBGL_lose_context");
    if(!extension)return {available:false};extension.loseContext();await new Promise(r=>setTimeout(r,30));
    let rejected=false;
    try{await globalThis.CavePerf.run(gpu,{sizes:[128],profiles:["mixed"],rounds:1});}catch{rejected=true;}
    gpu.dispose();return {available:true,rejected};
  });
  check(!fault.available||fault.rejected,"Lost context accepted as a measurement");report.contextLoss=fault;
  for(const [width,theme] of [[1000,"light"],[736,"dark"],[360,"light"],[320,"dark"]]) {
    await page.setViewportSize({width,height:1400});await page.emulateMedia({colorScheme:theme});await page.waitForTimeout(70);
    const layout=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,
      tables:[...document.querySelectorAll(".cp-table")].map(e=>({width:e.clientWidth,scroll:e.scrollWidth}))}));
    check(layout.scroll<=layout.width,"Global horizontal overflow");report.layouts.push({...layout,theme});
    await page.locator(rootSelector).screenshot({path:path.join(folder,width+"-"+theme+".png")});
  }
  if(wrapper) {
    await page.goto("file:///"+path.resolve(wrapper).replaceAll("\\","/"));
    const frame=page.frames().find(f=>f.parentFrame());
    await frame.waitForFunction(()=>document.getElementById("cave-layout-performance")._inspect);
    await frame.locator('[data-method="bridges"]').click();
    report.inline=await frame.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth,
      state:document.getElementById("cave-layout-performance")._inspect()}));
    check(report.inline.width>=report.inline.scroll&&report.inline.state.ready&&!report.inline.state.error,"Inline page failed");
    check(!await frame.locator("#cp-cancel").isVisible(),"Inline idle stop button visible");
    await frame.locator(rootSelector).screenshot({path:path.join(folder,"inline-320.png")});
  }
  check(!report.errors.length,"Browser script errors");report.ok=true;
})().catch(e=>{report.ok=false;report.failure=e.stack;process.exitCode=1;}).finally(async()=>{
  fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
  console.log(JSON.stringify({ok:report.ok,folder,methods:report.methods.length,layouts:report.layouts,
    contextLoss:report.contextLoss,errors:report.errors,failure:report.failure}));
  if(browser)await browser.close();
});
