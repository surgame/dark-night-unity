const fs=require("fs"),path=require("path"),assert=require("assert");
const {chromium}=require("C:/Users/Jobscn/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");
const api=require("./solver.js");
const profiles=JSON.parse(fs.readFileSync(path.join(__dirname,"art-profiles.json"),"utf8"));
const tiles=api.catalog(profiles);
const out="C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a1891b-2542-7ea1-a69e-12cf28b0c785";
const review=path.join(__dirname,"review");
fs.mkdirSync(review,{recursive:true});
const inspect=frame=>frame.evaluate(()=>document.getElementById("macro-cave-playable")._inspect());
let browser;
(async()=>{
  const solverResults=[];
  for(let seed=0;seed<32;seed++) {
    const a=api.solve(tiles,seed),b=api.solve(tiles,seed);
    assert.deepStrictEqual(a.ids,b.ids);
    for(let i=1;i<a.ids.length;i++)assert(api.compatible(tiles[a.ids[i-1]],tiles[a.ids[i]]));
    solverResults.push({seed,stats:a.stats});
  }
  const demo=api.solve(tiles,309);
  assert.strictEqual(new Set(demo.ids.map(id=>tiles[id].panel)).size,6);
  assert(demo.ids.some(id=>tiles[id].right==="B"));
  browser=await chromium.launch({executablePath:"C:/Users/Jobscn/AppData/Local/ms-playwright/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe"});
  const page=await browser.newPage({viewport:{width:736,height:1100}});
  const errors=[];
  page.on("pageerror",e=>errors.push(e.message));
  await page.route(/^https?:/,route=>route.abort());
  await page.goto("file:///"+out+"/modular-cave-roam-browser.html");
  const frame=page.frames()[1];
  await frame.waitForFunction(()=>document.getElementById("macro-cave-playable")?.dataset.ready==="true");
  const root=frame.locator("#macro-cave-playable");
  let initial=await inspect(frame);
  assert.strictEqual(initial.socketErrors,0);
  assert(initial.seamErrors.every(error=>error<0.001));
  await root.screenshot({path:path.join(review,"walk-initial.png")});
  await frame.locator("#mc-play").click();
  await page.keyboard.down("KeyD");
  await page.waitForTimeout(450);
  await page.keyboard.up("KeyD");
  let moved=await inspect(frame);
  assert(moved.player.x>initial.player.x+30,"Real keyboard movement must move the character.");
  await page.keyboard.press("Space");
  await page.waitForTimeout(110);
  let airborne=await inspect(frame);
  assert(!airborne.player.grounded,"Space must initiate a jump.");
  await root.screenshot({path:path.join(review,"walk-jumping.png")});
  await page.waitForTimeout(900);
  assert((await inspect(frame)).player.grounded,"Character must land.");
  await frame.evaluate(()=>document.getElementById("macro-cave-playable")._teleport(590));
  await frame.evaluate(()=>document.getElementById("macro-cave-playable")._advance(.9,1));
  const crossed=await inspect(frame);
  assert(crossed.player.x>623&&crossed.player.grounded);
  await root.screenshot({path:path.join(review,"walk-first-joint.png")});
  const points=[];
  for(let i=0;i<16;i++) {
    await frame.evaluate(i=>document.getElementById("macro-cave-playable")._teleport(i*623+280),i);
    await frame.evaluate(()=>document.getElementById("macro-cave-playable")._advance(.3,1));
    const current=await inspect(frame);
    assert(current.player.grounded&&Number.isFinite(current.camera.y));
    points.push({module:i+1,x:current.player.x,y:current.player.y});
  }
  await frame.evaluate(()=>document.getElementById("macro-cave-playable")._teleport(7*623-16));
  await frame.locator("#mc-bounds").check();
  await root.screenshot({path:path.join(review,"walk-low-socket-overlay.png")});
  await frame.locator("#mc-light").uncheck();
  await root.screenshot({path:path.join(review,"walk-low-socket-unlit.png")});
  await frame.locator("#mc-view").selectOption("atlas");
  await root.screenshot({path:path.join(review,"module-atlas.png")});
  await frame.locator("#mc-view").selectOption("solve");
  const total=(await inspect(frame)).trace.length;
  for(const fraction of [0,.3,.7,1]) {
    await frame.locator("#mc-trace").evaluate((input,value)=>{
      input.value=String(value);input.dispatchEvent(new Event("input",{bubbles:true}));
    },Math.round((total-1)*fraction));
    await root.screenshot({path:path.join(review,`wfc-trace-${fraction}.png`)});
  }
  await frame.locator("#mc-view").selectOption("walk");
  const previousSeed=(await inspect(frame)).state.seed;
  await frame.locator("#mc-seed").click();
  assert.notStrictEqual((await inspect(frame)).state.seed,previousSeed);
  await frame.evaluate(()=>{
    window.openai.setWidgetState({modelContent:{demo:"macro-cave-playable"},privateContent:{
      version:1,seed:309,view:"walk",bounds:false,light:true,trace:0,x:190,face:1
    }}).catch(()=>{});
  });
  await frame.waitForFunction(()=>document.getElementById("macro-cave-playable")._inspect().state.seed===309);
  for(const width of [736,360,320]) {
    for(const theme of ["light","dark"]) {
      await page.setViewportSize({width,height:1300});
      await page.emulateMedia({colorScheme:theme});
      await page.waitForTimeout(100);
      const overflow=await frame.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1);
      assert(!overflow,`Horizontal overflow at ${width} ${theme}`);
      await root.screenshot({path:path.join(review,`layout-${width}-${theme}.png`)});
      for(const view of ["atlas","solve","walk"]) {
        await frame.locator("#mc-view").selectOption(view);
        assert(!(await frame.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1)));
      }
    }
  }
  await page.setViewportSize({width:360,height:1100});
  const right=frame.locator("#mc-right"),box=await right.boundingBox();
  const beforePointer=(await inspect(frame)).player.x;
  await page.mouse.move(box.x+box.width/2,box.y+box.height/2);
  await page.mouse.down();await page.waitForTimeout(350);await page.mouse.up();
  assert((await inspect(frame)).player.x>beforePointer+25,"Pointer hold must move the character.");
  const beforeStop=(await inspect(frame)).player.x;
  await page.waitForTimeout(220);
  assert(Math.abs((await inspect(frame)).player.x-beforeStop)<2,"Released movement must stop.");
  const final=await inspect(frame);
  assert.strictEqual(errors.length,0,errors.join("\n"));
  const touchContext=await browser.newContext({viewport:{width:360,height:1100},
    isMobile:true,hasTouch:true,deviceScaleFactor:1});
  const touchPage=await touchContext.newPage();
  await touchPage.route(/^https?:/,route=>route.abort());
  await touchPage.goto("file:///"+out+"/modular-cave-roam-browser.html");
  const touchFrame=touchPage.frames()[1];
  await touchFrame.waitForFunction(()=>document.getElementById("macro-cave-playable")?.dataset.ready==="true");
  const touchBefore=(await inspect(touchFrame)).player.x;
  const touchBox=await touchFrame.locator("#mc-right").boundingBox();
  const client=await touchContext.newCDPSession(touchPage);
  await client.send("Input.dispatchTouchEvent",{type:"touchStart",touchPoints:[{
    x:touchBox.x+touchBox.width/2,y:touchBox.y+touchBox.height/2
  }]});
  await touchPage.waitForTimeout(320);
  await client.send("Input.dispatchTouchEvent",{type:"touchEnd",touchPoints:[]});
  assert((await inspect(touchFrame)).player.x>touchBefore+25,"Native touch hold must move.");
  await touchFrame.locator("#mc-jump").tap();
  await touchPage.waitForTimeout(100);
  assert(!(await inspect(touchFrame)).player.grounded,"Touch jump must initiate flight.");
  await touchFrame.locator("#macro-cave-playable").screenshot({path:path.join(review,"touch-360.png")});
  await touchContext.close();
  const result={ok:true,identity:"Current source hashes in manifest.json; offline browser only.",
    solverSeeds:solverResults,defaultSeed:309,defaultSequence:initial.moduleSequence,
    socketErrors:initial.socketErrors,maxSeamError:Math.max(...initial.seamErrors),
    checked:["same-seed determinism","socket rules","six actual art modules","keyboard movement","jump and landing",
      "cross-module movement","16 module camera probes","pointer hold and release","native touch hold and jump","seed change","state restore",
      "three views at 736/360/320 light and dark","four trace checkpoints"],points,errors,finalState:final.state};
  fs.writeFileSync(path.join(__dirname,"verification.json"),JSON.stringify(result,null,2));
  await browser.close();
  console.log(JSON.stringify({ok:true,defaultSeed:309,solverSeeds:32,layouts:18,errors,
    maxSeamError:result.maxSeamError,review}));
})().catch(async error=>{
  fs.writeFileSync(path.join(review,`failure-${Date.now()}.json`),JSON.stringify({message:error.message,stack:error.stack},null,2));
  if(browser)await browser.close();
  console.error(error);process.exit(1);
});
