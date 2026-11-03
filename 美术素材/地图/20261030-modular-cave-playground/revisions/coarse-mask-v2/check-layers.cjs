const fs=require("fs"),path=require("path"),assert=require("assert"),crypto=require("crypto");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const out="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1891b-2542-7ea1-a69e-12cf28b0c785";
const review=path.join(__dirname,"review-v2"),exportsDir=path.join(__dirname,"exports-v2/foreground");
fs.mkdirSync(exportsDir,{recursive:true});fs.mkdirSync(review,{recursive:true});
let browser;
(async()=>{
  browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const page=await browser.newPage({viewport:{width:736,height:1100}});
  const errors=[];page.on("pageerror",e=>errors.push(e.message));
  await page.route(/^https?:/,route=>route.abort());
  await page.goto("file:///"+out+"/modular-cave-roam-browser.html");
  const frame=page.frames()[1];
  await frame.waitForFunction(()=>document.getElementById("macro-cave-playable")?.dataset.ready==="true");
  const evaluate=(fn,arg)=>frame.evaluate(fn,arg);
  const inspect=()=>evaluate(()=>document.getElementById("macro-cave-playable")._inspect());
  const root=frame.locator("#macro-cave-playable");
  await evaluate(()=>document.getElementById("macro-cave-playable")._teleport(240));
  const backgroundBefore=await evaluate(()=>document.getElementById("macro-cave-playable")._backgroundSignature());
  const alpha=await evaluate(()=>{
    const root=document.getElementById("macro-cave-playable");
    return {air:root._foregroundAlpha(0,100,220),rock:root._foregroundAlpha(0,100,20),
      oldOre1:root._foregroundAlpha(3,445,191),oldOre2:root._foregroundAlpha(4,130,190)};
  });
  assert.deepStrictEqual(alpha,{air:0,rock:255,oldOre1:0,oldOre2:0});
  const sources=await evaluate(()=>document.getElementById("macro-cave-playable")._layerSources());
  const outputs=[];
  for(let i=0;i<sources.length;i++) {
    const bytes=Buffer.from(sources[i].split(",")[1],"base64"),file=`module-${i+1}.png`;
    fs.writeFileSync(path.join(exportsDir,file),bytes);
    outputs.push({file:"exports-v2/foreground/"+file,bytes:bytes.length,
      sha256:crypto.createHash("sha256").update(bytes).digest("hex")});
  }
  const initial=await inspect();
  let selected;
  for(const ore of initial.terrain.ore.filter(value=>value.site===0)) {
    const [x,y]=ore.id.split(":").map(Number).map(n=>n*16+8);
    const hand={x:initial.player.x+13,y:initial.player.y-26};
    if(Math.hypot(x-hand.x,y-hand.y)>63)continue;
    const buried=await evaluate(point=>{
      const root=document.getElementById("macro-cave-playable");
      return [[-7,-7],[7,-7],[-7,7],[7,7]].every(([dx,dy])=>root._solid({x:point.x+dx,y:point.y+dy}));
    },{x,y});
    if(buried){selected={x,y,oreId:ore.id};break;}
  }
  assert(selected,"A nearby buried mineral cell must exist.");
  await root.screenshot({path:path.join(review,"layers-before-mining.png")});
  let firstTarget=await evaluate(point=>document.getElementById("macro-cave-playable")._target(point),selected);
  let exposedRemoved=0;
  while(firstTarget&&firstTarget.type==="ore"&&exposedRemoved<12) {
    assert.notStrictEqual(firstTarget.id,selected.oreId,"Buried ore must not be reachable through foreground.");
    for(let i=0;i<2;i++) {
      assert(await evaluate(point=>document.getElementById("macro-cave-playable")._mine(point),selected));
      await evaluate(()=>document.getElementById("macro-cave-playable")._advance(.55));
    }
    exposedRemoved++;
    firstTarget=await evaluate(point=>document.getElementById("macro-cave-playable")._target(point),selected);
  }
  assert(firstTarget&&firstTarget.type==="rock","Exposed ore may be cleared first, then foreground must block the buried target.");
  selected.rockId=firstTarget.id;
  const beforeRock=await inspect(),mineralBefore=beforeRock.terrain.ore.find(value=>value.id===selected.oreId).hp;
  for(let attempt=0;attempt<2;attempt++) {
    const accepted=await evaluate(point=>document.getElementById("macro-cave-playable")._mine(point),selected);
    assert(accepted,"Rock strike must be accepted.");
    await evaluate(()=>document.getElementById("macro-cave-playable")._advance(.55));
  }
  let revealed=await inspect();
  assert(revealed.terrain.clear.includes(selected.rockId));
  assert.strictEqual(revealed.terrain.ore.find(value=>value.id===selected.oreId).hp,mineralBefore,
    "Removing foreground must not harvest or damage the rear mineral layer.");
  let target=await evaluate(point=>document.getElementById("macro-cave-playable")._target(point),selected);
  let extraRockCells=0;
  while(target&&target.type==="rock"&&extraRockCells<4) {
    for(let i=0;i<2;i++) {
      await evaluate(point=>document.getElementById("macro-cave-playable")._mine(point),selected);
      await evaluate(()=>document.getElementById("macro-cave-playable")._advance(.55));
    }
    extraRockCells++;
    target=await evaluate(point=>document.getElementById("macro-cave-playable")._target(point),selected);
  }
  assert(target&&target.type==="ore","The next strike after exposure must target a mineral cell.");
  await root.screenshot({path:path.join(review,"layers-ore-revealed.png")});
  const oreHit=target.id;
  for(let attempt=0;attempt<2;attempt++) {
    assert(await evaluate(point=>document.getElementById("macro-cave-playable")._mine(point),selected));
    await evaluate(()=>document.getElementById("macro-cave-playable")._advance(.55));
  }
  const mined=await inspect();
  assert.strictEqual(mined.terrain.ore.find(value=>value.id===oreHit).hp,0);
  assert.strictEqual(mined.terrain.harvested,exposedRemoved+1);
  assert.deepStrictEqual(await evaluate(()=>document.getElementById("macro-cave-playable")._backgroundSignature()),backgroundBefore,
    "Mining must never repaint the stable backwall.");
  assert(!(await evaluate(point=>document.getElementById("macro-cave-playable")._mine(point),
    {x:mined.player.x+900,y:mined.player.y})));
  await root.screenshot({path:path.join(review,"layers-ore-depleted.png")});
  for(const layer of ["background","mineral","foreground","all"]) {
    await frame.locator("#mc-layer").selectOption(layer);
    await root.screenshot({path:path.join(review,"layer-"+layer+".png")});
  }
  await evaluate(()=>document.getElementById("macro-cave-playable")._save());
  const saved=await evaluate(()=>window.openai.widgetState);
  assert(Buffer.byteLength(JSON.stringify(saved))<16384);
  await frame.locator("#mc-seed").click();
  assert.strictEqual((await inspect()).terrain.clear.length,0);
  await evaluate(saved=>window.openai.setWidgetState(saved).catch(()=>{}),saved);
  const restored=await inspect();
  assert.deepStrictEqual(restored.terrain.serialized,mined.terrain.serialized);
  assert.strictEqual(restored.terrain.harvested,exposedRemoved+1);
  const revisionBeforeKey=restored.terrain.revision;
  await evaluate(()=>document.getElementById("macro-cave-playable")._teleport(176));
  await frame.locator("#mc-play").click();
  await page.keyboard.down("KeyJ");await page.waitForTimeout(400);await page.keyboard.up("KeyJ");
  assert((await inspect()).terrain.revision>revisionBeforeKey,"Real J input must perform a mining impact.");
  for(const width of [360,320]) {
    await page.setViewportSize({width,height:1300});
    for(const layer of ["background","mineral","foreground","all"]) {
      await frame.locator("#mc-layer").selectOption(layer);
      assert(!(await evaluate(()=>document.documentElement.scrollWidth>innerWidth+1)));
    }
  }
  assert.strictEqual(errors.length,0,errors.join("\n"));
  const result={ok:true,identity:"Current manifest.json, layered browser candidate only.",
    checked:["foreground alpha","baked mineral exclusion","foreground before rear mineral",
      "no dual-layer damage on one impact","mineral depletion","stable background RGBA fingerprint",
      "range rejection","four layer views","sparse state restoration","real mining keyboard input","320/360 layer layout"],alpha,
    selected,exposedRemoved,extraRockCells,harvested:mined.terrain.harvested,cleared:mined.terrain.clear,
    backgroundFingerprints:backgroundBefore,outputs,errors};
  fs.writeFileSync(path.join(__dirname,"layer-verification.json"),JSON.stringify(result,null,2));
  await browser.close();
  console.log(JSON.stringify({ok:true,exports:outputs.length,harvested:mined.terrain.harvested,cleared:mined.terrain.clear.length,errors}));
})().catch(async error=>{
  fs.writeFileSync(path.join(review,`layer-failure-${Date.now()}.json`),JSON.stringify({message:error.message,stack:error.stack},null,2));
  if(browser)await browser.close();
  console.error(error);process.exit(1);
});
