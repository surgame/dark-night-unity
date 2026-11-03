const fs=require("node:fs"),path=require("node:path"),crypto=require("node:crypto");
const {pathToFileURL}=require("node:url");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const source=process.argv[2],records=process.argv[3];
if(!source||!records)throw new Error("Provide fragment directory and evidence directory.");
const folder=path.resolve(records,"display-"+Date.now());fs.mkdirSync(folder,{recursive:true});
const identity=file=>{const data=fs.readFileSync(file);return {path:path.resolve(file),bytes:data.length,sha256:crypto.createHash("sha256").update(data).digest("hex")};};
const report={scope:"UI prototype layout and local interactions only; no Unity, FishNet or game execution.",sources:["workbench","lab"].map(kind=>identity(path.join(source,`mapmodule-${kind}-ui.html`))),errors:[],layouts:[],interactions:[],screenshots:[]};
function check(value,message){if(!value)throw new Error(message);}
async function inspect(frame,kind,config) {
  return await frame.evaluate(({kind,config})=>{
    const product=[...document.querySelectorAll(".mm-product")].find(el=>el.getBoundingClientRect().width>0);
    const box=product.getBoundingClientRect();
    const controls=[...product.querySelectorAll("button,select,input[type=text],summary")].filter(el=>{
      const closed=el.closest("details:not([open])");
      if(closed&&!closed.querySelector("summary").contains(el))return false;
      return el.getBoundingClientRect().width>0;
    });
    const clipped=controls.filter(el=>{const r=el.getBoundingClientRect();return r.left<box.left-1||r.right>box.right+1;}).map(el=>el.textContent||el.getAttribute("aria-label"));
    const overlap=[];
    for(let i=0;i<controls.length;i++)for(let j=i+1;j<controls.length;j++){
      const a=controls[i],b=controls[j];if(a.contains(b)||b.contains(a))continue;
      const r=a.getBoundingClientRect(),s=b.getBoundingClientRect();
      if(Math.min(r.right,s.right)-Math.max(r.left,s.left)>1&&Math.min(r.bottom,s.bottom)-Math.max(r.top,s.top)>1)overlap.push([a.textContent,b.textContent]);
    }
    const svg=product.querySelector(".mm-scene"),scale=Number(svg.dataset.scale);
    return {kind,...config,layout:product.dataset.layout,productHeight:box.height,rootWidth:box.width,overflow:document.documentElement.scrollWidth>document.documentElement.clientWidth,clipped,overlap,scale,heroHeights:[...svg.querySelectorAll("[data-mm-hero]")].map(el=>el.getBoundingClientRect().height),finite:!/NaN|Infinity/.test(svg.innerHTML),state:JSON.parse(product.dataset.state)};
  },{kind,config});
}
async function workbenchInteractions(frame,product) {
  const find=action=>product.locator(`[data-action="${action}"]`);
  if(!(await product.locator('[data-fold="inspector"]').evaluate(el=>el.open)))await product.locator('[data-fold="inspector"] summary').click();
  check(await find("run").isDisabled(),"Run initially disabled");
  await find("check").click();await find("bake").click();
  check(await find("run").isEnabled(),"Matching pair bake");
  const baked=await product.evaluate(el=>JSON.parse(el.dataset.state));
  await product.locator('[data-toggle="ecology"]').uncheck();
  check((await product.evaluate(el=>JSON.parse(el.dataset.state))).phase==="baked","Display dirtied source");
  await product.locator('[data-field="second"]').selectOption("descent");
  check(await find("run").isDisabled(),"Changed source enabled old run");
  await find("check").click();
  check((await product.evaluate(el=>JSON.parse(el.dataset.state))).phase==="conflict","Mismatch not rejected");
  check(await find("bake").isDisabled(),"Conflict baked");
  await product.locator('[data-field="second"]').selectOption("lowpass");
  await find("check").click();
  check(await find("bake").isDisabled(),"Unsaved input baked");
  await find("save").click();await find("bake").click();await find("run").click();
  check((await product.locator("[data-notice]").textContent()).includes("本机Host"),"Run handoff");
  await product.locator('[data-task="module"]').click();
  check((await product.evaluate(el=>JSON.parse(el.dataset.state))).scope==="single","Module task view");
  await product.locator('[data-task="assembly"]').click();
  await product.locator('[data-scope="map"]').click();
  check(await product.locator(".mm-empty").count()===1,"Whole-map empty state");
  await product.locator('[data-scope="fragment"]').click();
  await product.locator('[data-field="theme"]').selectOption("fragment");
  check(await find("run").isDisabled(),"Empty theme starts");
  await product.locator('[data-field="theme"]').selectOption("entrance");
  await product.locator('[data-field="variant"]').selectOption("shelf");
  check((await product.evaluate(el=>JSON.parse(el.dataset.state))).second==="shelf","Sample pair choice");
  return {matchingPair:true,sourceInvalidatesBake:true,conflictRejects:true,saveAndBakeSeparate:true,displayDoesNotDirty:true,taskViews:true,emptyTheme:true,hostHandoffOnly:true,firstBakeVersion:baked.version};
}
async function labInteractions(frame,product) {
  const find=action=>product.locator(`[data-action="${action}"]`);
  check(await find("pause").isDisabled(),"Idle pause");
  check(await product.locator("[data-address]").isHidden(),"Host address visible");
  await find("start").click();
  check(await product.locator('[data-field="variant"]').isDisabled(),"Running version not locked");
  await find("pause").click();
  check((await product.evaluate(el=>JSON.parse(el.dataset.state))).paused,"Host pause");
  await find("restart").click();
  check(!(await product.evaluate(el=>JSON.parse(el.dataset.state))).paused,"Restart pause reset");
  await product.locator('[data-scope="single"]').click();
  check((await product.evaluate(el=>JSON.parse(el.dataset.state))).scope==="single","Local camera");
  await find("leave").click();
  await product.locator('[data-field="role"]').selectOption("client");
  check(await product.locator("[data-address]").isVisible(),"Client address absent");
  await find("start").click();
  check(await find("pause").isDisabled()&&await find("restart").isDisabled(),"Client authority controls");
  await find("leave").click();
  check(await product.locator('[data-field="variant"]').isEnabled(),"Leaving kept selector locked");
  const readyText=await product.locator(".mm-readiness").textContent();
  check(readyText.includes("待回执"),"Invented network receipt");
  return {hostControls:true,clientControlsDisabled:true,versionLocksWhileRunning:true,cameraLocal:true,leaveUnlocks:true,noInventedReceipts:true};
}
async function run(){
  const browser=await chromium.launch({headless:true,executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  try{
    for(const kind of ["workbench","lab"])for(const config of [{width:1024,theme:"light"},{width:736,theme:"dark"},{width:320,theme:"light"}]){
      const page=await browser.newPage({viewport:{width:config.width,height:1600},colorScheme:config.theme,hasTouch:config.width===320});
      page.on("pageerror",error=>report.errors.push({kind,...config,error:error.message}));
      await page.goto(pathToFileURL(path.resolve(source,`mapmodule-${kind}-ui-browser.html`)).href);
      await page.evaluate(()=>{document.body.style.padding="0";document.querySelector("iframe").style.height="1600px";});
      const frame=page.frames().find(f=>f.parentFrame());
      await frame.waitForSelector(".mm-product .mm-scene image");
      await frame.evaluate(()=>{window.__writes=[];window.openai.setWidgetState=snapshot=>{window.__writes.push(snapshot);return Promise.resolve();};});
      for(let index=0;index<2;index++){
        const product=frame.locator(".mm-product:visible");
        await frame.waitForFunction(()=>{
          const svg=[...document.querySelectorAll(".mm-product .mm-scene")].find(el=>el.getBoundingClientRect().width>0);
          return svg&&Math.abs(svg.viewBox.baseVal.width-Math.max(260,svg.parentElement.clientWidth))<1;
        });
        const layout=await inspect(frame,kind,config);
        report.layouts.push(layout);
        check(!layout.overflow&&!layout.clipped.length&&!layout.overlap.length&&layout.finite,`Layout ${kind}/${config.width}/${layout.layout}`);
        check(layout.heroHeights.every(h=>Math.abs(h-.48*layout.scale)<.1),"Role proportion");
        if(kind==="lab")check(layout.heroHeights.length===1,"Multiple fake players");
        const screenshot=path.join(folder,`${kind}-${layout.layout}-${config.width}-${config.theme}.png`);
        await product.screenshot({path:screenshot});report.screenshots.push(identity(screenshot));
        if(index===0){
          const interactions=kind==="workbench"?await workbenchInteractions(frame,product):await labInteractions(frame,product);
          report.interactions.push({kind,...config,...interactions});
          await frame.locator(".viz-carousel").getByRole("button").last().click();
          check((await frame.locator(".mm-product:visible").getAttribute("data-layout"))!==(kind==="workbench"?"canvas":"play"),"Carousel navigation");
        }
      }
      const writes=await frame.evaluate(()=>window.__writes);
      check(writes.length>0,"State not persisted");
      check(JSON.stringify(writes.at(-1)).length<16384,"State too large");
      const count=writes.length;
      await frame.evaluate(({kind})=>{
        const layouts=kind==="workbench"?{canvas:{variant:"steps",second:"steps",scope:"single"}}:{play:{variant:"steps",role:"client",scope:"single"}};
        window.dispatchEvent(new CustomEvent("openai:set_globals",{detail:{globals:{widgetState:{design:kind==="workbench"?"mapmodule-workbench-ui-v1":"mapmodule-lab-ui-v1",layouts}}}}));
      },{kind});
      check((await frame.evaluate(()=>window.__writes.length))===count,"State event wrote snapshot");
      const first=frame.locator(".mm-product").first();
      check((await first.evaluate(el=>JSON.parse(el.dataset.state))).variant==="steps","State restoration");
      await page.close();
    }
    check(report.errors.length===0,"Page errors");
    report.completed=true;
  }finally{await browser.close();}
}
run().catch(error=>{report.completed=false;report.failure=error.stack;process.exitCode=1;}).finally(()=>{
  const output=path.join(folder,"report.json");fs.writeFileSync(output,JSON.stringify(report,null,2)+"\n");
  console.log(JSON.stringify({completed:report.completed,layouts:report.layouts.length,screenshots:report.screenshots.length,interactionGroups:report.interactions.length,errors:report.errors,failure:report.failure,report:output}));
});
