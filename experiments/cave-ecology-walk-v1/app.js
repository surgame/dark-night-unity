(async function () {
  "use strict";
  const root=document.getElementById("ecology-walk"),get=id=>root.querySelector("#ew-"+id);
  const controls={left:false,right:false,jump:false,interact:false},keys=new Set();
  let seed=271,world,engine,renderer,inspector,ready=false,active=false,auto=false,visited=new Set(),clock=0,last=0,lastUI=0,saveStamp="";
  let autoCooldown=0,autoJump=0,visible=true,restoring=false,lastEvent=0,lastSentState="";
  const buildId=window.ECOLOGY_BUILD_ID;
  const savedContent=value=>value?.privateContent?.version===2&&value.privateContent.buildId===buildId?value.privateContent:null;
  const initial=savedContent(window.openai?.widgetState);
  const writer="ew-"+Date.now().toString(36)+"-"+Math.random().toString(36).slice(2,7);
  async function image(src) {
    const img=new Image();img.src=src;await img.decode();return img;
  }
  try {
    const source=window.ECOLOGY_ASSETS;
    const names=Object.keys(source.plants);
    const [hero,back,rockMaterial,bridgeDesign,...rest]=await Promise.all([
      source.hero,source.back,source.rockMaterial,source.bridgeDesign,...source.panels,
      ...names.map(n=>source.plants[n])].map(image));
    const panels=rest.slice(0,source.panels.length),plants=Object.fromEntries(names.map((name,i)=>[name,rest[i+source.panels.length]]));
    const assets={hero,back,rockMaterial,bridgeDesign,panels,plants};
    renderer=new window.EcologyRenderer(get("canvas"),assets);
    inspector=new window.EcologyAssemblyInspector({
      canvas:get("inspector-canvas"),select:get("seam-select"),
      renderer:()=>renderer,world:()=>world
    });
    function install(nextSeed,restore) {
      try {
        const candidateSeed=Number(nextSeed)>>>0,candidateWorld=window.EcologyWorld.create(candidateSeed);
        const candidateEngine=new window.EcologyEngine(candidateWorld);
        if(restore&&(!candidateEngine.restoreState(restore.engine)||restore.buildId!==buildId))
          throw new Error("试走记录与当前模块库不匹配。");
        const candidateRenderer=new window.EcologyRenderer(get("canvas"),assets);
        candidateRenderer.setWorld(candidateWorld);
        candidateRenderer.motion=restore?restore.motion!==false:renderer.motion;
        candidateRenderer.debug=renderer.debug;
        candidateRenderer.overview=!!restore?.overview;
        candidateRenderer.focus=candidateWorld.rooms.some(r=>r.id===restore?.focus)?restore.focus:null;
        seed=candidateSeed;world=candidateWorld;engine=candidateEngine;renderer=candidateRenderer;
        visited=new Set((restore?.visited||[]).filter(id=>world.rooms.some(r=>r.id===id)));
        lastEvent=engine.events.at(-1)?.id||0;auto=false;active=false;autoCooldown=0;autoJump=0;saveStamp="";
        get("motion").checked=renderer.motion;get("status").hidden=true;get("status").textContent="";
        inspector.refresh();setInspection(!!restore?.inspection,restore?.seamIndex||0,false);
      } catch(error) {
        if(!world)throw error;
        get("status").hidden=false;
        get("status").textContent="本次组合失败，上一局已保留："+error.message;
        root.dataset.lastGenerationError=error.code||error.message;
        return false;
      }
      get("room-select").replaceChildren(new Option("跟随角色",""),...world.rooms.map(r=>new Option(r.name,r.id)));
      get("room-select").value=renderer.focus||"";
      get("seed").textContent="种子 "+seed+" · "+world.catalog.length+" 选 "+world.rooms.length;
      get("catalog").replaceChildren(...window.EcologyWorld.catalog.map(item=>{
        const node=document.createElement("span"),chosen=world.rooms.some(r=>r.name===item.name);
        node.textContent=item.name;node.className=chosen?"ew-selected":"ew-unused";return node;
      }));
      get("start").textContent="开始试走";get("auto").setAttribute("aria-pressed","false");
      get("map").setAttribute("aria-pressed",String(renderer.overview));ui(engine.snapshot(),true);
      delete root.dataset.lastGenerationError;
      return true;
    }
    function save() {
      if(!ready||restoring)return;
      const snap=engine.snapshot(),state={version:2,buildId,writer,seed,engine:engine.exportState(),visited:[...visited],overview:renderer.overview,focus:renderer.focus,motion:renderer.motion,
        inspection:inspector.enabled,seamIndex:inspector.index};
      if(JSON.stringify(state).length>14000){state.engine.logs=[];state.engine.events=[];}
      lastSentState=JSON.stringify(state);
      window.openai?.setWidgetState?.({
        modelContent:{seed,modules:world.rooms.map(r=>r.name),visited:visited.size,primaryCollected:snap.flags.primaryCollected,completed:snap.completed},
        privateContent:state
      })?.catch(()=>{});
    }
    function clearInput(){keys.clear();Object.keys(controls).forEach(k=>controls[k]=false);}
    function setInspection(enabled,index,persist=true) {
      inspector.setEnabled(enabled,index);
      get("inspect").setAttribute("aria-pressed",String(inspector.enabled));
      get("inspector-controls").hidden=!inspector.enabled;
      const inspection=inspector.inspect();
      get("inspector-summary").textContent="查看本图 "+world.connections.length+" 处接边；规则允许 "
        +inspection.allowedSourcePairs+" / "+inspection.totalSourcePairs+" 个有向源配对。";
      if(inspector.enabled) {
        active=false;auto=false;clearInput();renderer.focus=null;renderer.overview=false;
        get("room-select").value="";get("auto").setAttribute("aria-pressed","false");
        get("map").setAttribute("aria-pressed","false");get("start").textContent="回到角色";
      }
      if(persist)save();
    }
    function start() {
      setInspection(false,inspector.index,false);
      active=true;renderer.overview=false;renderer.focus=null;get("room-select").value="";
      get("start").textContent="暂停试走";get("map").setAttribute("aria-pressed","false");
    }
    function rescue() {
      Object.assign(engine.hero,{x:engine.checkpoint.x,y:engine.checkpoint.y,vx:0,vy:0,grounded:false});
      engine.addLog("回到最近干台；已采矿、木梁与货物保持。");setInspection(false,inspector.index,false);renderer.focus=null;renderer.overview=false;save();
    }
    function ui(state,force) {
      if(!force&&clock-lastUI<.13)return;lastUI=clock;
      const room=world.rooms.find(r=>state.hero.x>=r.x&&state.hero.x<=r.x+623)||world.rooms.reduce((a,b)=>Math.abs(a.x-state.hero.x)<Math.abs(b.x-state.hero.x)?a:b);
      visited.add(room.id);
      const looking=renderer.focus?world.rooms.find(r=>r.id===renderer.focus):room;
      get("room").textContent=(looking?.name||"洞间短廊")+(renderer.focus?" · 镜头查看":"");
      get("load").textContent=state.hero.carry?"携矿 · 跳跃降低":state.flags.hasBeam?"已携木梁":"空手";
      get("progress").textContent=visited.size+" / "+world.rooms.length;
      get("design").textContent=looking?.intent||"";
      get("log").textContent=state.logs.at(-1)?.text||"";
      const message=state.completed?"主矿已交付。可以继续探索，或重新组合地图。":
        !state.flags.primaryCollected?"目标：深入右端矿室，取回主矿。":
        state.hero.carry?"返程：回营地交付。低顶先投槽，再空手穿过。":"主矿已采下：到货槽另一端取回，再回营地。";
      get("objective").textContent=message;
      get("prompt").textContent=inspector.enabled?"接边检查 · "+inspector.label():state.completed?"主矿交付完成 · 动态生态继续运行":renderer.focus?"查看局部；按“回到角色”开始试走":
        !active?"按“开始试走”，A / D 移动，空格跳跃，E 交互。":state.prompt||"A / D 移动 · 空格跳跃 · E 交互";
      const stamp=[visited.size,state.events.at(-1)?.id||0,state.completed].join("-");
      if(stamp!==saveStamp){saveStamp=stamp;save();}
    }
    function automated(state,dt) {
      const h=state.hero,back=state.flags.primaryCollected,input={left:back,right:!back,jump:false,interact:false};
      autoCooldown-=dt;autoJump-=dt;
      if(state.completed){auto=false;active=false;get("auto").setAttribute("aria-pressed","false");get("start").textContent="继续探索";return {};}
      const target=state.target;
      const useful=target&&(target.type==="supply"||target.type==="bridge"||target.type==="beam"||
        target.type==="ore"&&target.primary||target.type==="looseOre"&&target.primary||
        target.type==="chute"&&h.carry&&back&&target.target?.x<target.x||target.type==="stash"&&h.carry);
      if(useful&&autoCooldown<=0) {
        if(target.type!=="bridge"||state.flags.hasBeam) {
          input.interact=true;autoCooldown=.3;input.left=false;input.right=false;
        }
      }
      if(h.grounded&&autoJump<=0) {
        const direction=back?-1:1,lookX=h.x+direction*22;
        const ground=state.surfaces.filter(s=>s.enabled!==false).map(s=>engine.surfaceY(s,lookX)).filter(y=>y!==null&&y>h.y-8).sort((a,b)=>a-b)[0];
        if(ground===undefined||ground>h.y+18){input.jump=true;autoJump=.8;}
      }
      return input;
    }
    function step(now) {
      const dt=last?Math.min(.05,(now-last)/1000):1/60;last=now;
      if(visible&&!document.hidden) {
        clock+=dt;let state=engine.snapshot();
        if(active&&!renderer.focus&&!inspector.enabled) {
          const input=auto?automated(state,dt):{
            left:controls.left||keys.has("KeyA")||keys.has("ArrowLeft"),
            right:controls.right||keys.has("KeyD")||keys.has("ArrowRight"),
            jump:controls.jump||keys.has("Space")||keys.has("KeyW")||keys.has("ArrowUp"),
            interact:controls.interact||keys.has("KeyE")
          };
          state=engine.step(dt,input);
        }
        renderer.draw(state,clock,dt);inspector.draw(state,clock);ui(state,false);
        if((state.events.at(-1)?.id||0)!==lastEvent){lastEvent=state.events.at(-1)?.id||0;save();}
      }
      requestAnimationFrame(step);
    }
    get("start").addEventListener("click",()=>{
      if(renderer.focus||renderer.overview||inspector.enabled){start();return;}
      if(active){active=false;auto=false;clearInput();get("start").textContent="继续试走";get("auto").setAttribute("aria-pressed","false");}
      else start();
    });
    get("auto").addEventListener("click",()=>{
      auto=!auto;if(auto)start();get("auto").setAttribute("aria-pressed",String(auto));save();
    });
    get("new").addEventListener("click",()=>{clearInput();install((seed+1009)>>>0);save();});
    get("map").addEventListener("click",()=>{
      setInspection(false,inspector.index,false);
      renderer.overview=!renderer.overview;renderer.focus=null;get("room-select").value="";
      get("start").textContent=renderer.overview?"回到角色":active?"暂停试走":"开始试走";
      get("map").setAttribute("aria-pressed",String(renderer.overview));save();
    });
    get("rescue").addEventListener("click",rescue);
    get("room-select").addEventListener("change",event=>{
      setInspection(false,inspector.index,false);
      renderer.focus=event.target.value||null;renderer.overview=false;get("map").setAttribute("aria-pressed","false");
      get("start").textContent=renderer.focus?"回到角色":active?"暂停试走":"开始试走";
      clearInput();save();ui(engine.snapshot(),true);
    });
    get("motion").addEventListener("change",event=>{renderer.motion=event.target.checked;save();});
    get("debug").addEventListener("change",event=>{renderer.debug=event.target.checked;});
    get("inspect").addEventListener("click",()=>setInspection(!inspector.enabled,inspector.index));
    get("seam-select").addEventListener("change",event=>setInspection(true,Number(event.target.value)));
    get("canvas").addEventListener("pointerdown",()=>{
      if(renderer.overview){renderer.overview=false;get("map").setAttribute("aria-pressed","false");}
      start();
    });
    root.querySelectorAll("[data-hold]").forEach(button=>{
      const key=button.dataset.hold;
      button.addEventListener("pointerdown",event=>{event.preventDefault();start();auto=false;controls[key]=true;button.setPointerCapture(event.pointerId);});
      ["pointerup","pointercancel","lostpointercapture"].forEach(name=>button.addEventListener(name,()=>{controls[key]=false;save();}));
    });
    document.addEventListener("keydown",event=>{
      if(!active||renderer.focus||inspector.enabled||event.ctrlKey||event.metaKey||["INPUT","SELECT","TEXTAREA"].includes(event.target.tagName))return;
      if(["KeyA","KeyD","KeyW","KeyE","Space","ArrowLeft","ArrowRight","ArrowUp"].includes(event.code)) {
        event.preventDefault();auto=false;get("auto").setAttribute("aria-pressed","false");keys.add(event.code);
      }
    });
    document.addEventListener("keyup",event=>{keys.delete(event.code);if(event.code==="KeyE")save();});
    window.addEventListener("blur",clearInput);
    document.addEventListener("visibilitychange",()=>{clearInput();last=0;});
    if(typeof IntersectionObserver!=="undefined")new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;last=0;}).observe(root);
    window.addEventListener("openai:set_globals",event=>{
      const value=savedContent(event.detail?.globals?.widgetState);
      if(!value||!ready||value.writer===writer||JSON.stringify(value)===lastSentState)return;
      restoring=true;try{install(value.seed,value);}finally{restoring=false;}
    });
    install(initial?.seed??271,initial);
    if(!initial) {
      const authoredRoom=world.rooms.find(room=>room.authoredArt);
      if(authoredRoom) {
        renderer.focus=authoredRoom.id;get("room-select").value=authoredRoom.id;
        renderer.size();
        const art=authoredRoom.authoredArt;
        renderer.camera.x=Math.max(0,Math.min(world.bounds.w-renderer.width,
          authoredRoom.x+art.x+art.drawWidth/2-renderer.width/2));
        renderer.camera.y=Math.max(world.bounds.y,Math.min(world.bounds.h-renderer.height,
          authoredRoom.y+art.y+art.drawHeight/2-renderer.height/2));
        get("start").textContent="回到角色";ui(engine.snapshot(),true);
      }
    }
    ready=true;root.dataset.ready="true";get("loading").hidden=true;
    root._inspect=()=>({seed,active,auto,clock,visited:[...visited],motion:renderer.motion,overview:renderer.overview,focus:renderer.focus,
      catalog:world.catalog.map(c=>({id:c.id,name:c.name,slot:c.slot})),rooms:world.rooms.map(r=>({id:r.id,name:r.name,x:r.x,y:r.y,ecology:r.ecology,plants:r.plants.length,water:r.water.length})),
      worldId:world.id,buildId,width:world.width,state:engine.snapshot(),particles:renderer.particles.length,
      assembly:inspector.inspect(),rules:world.solution?{
        version:world.solution.version,libraryIdentity:world.solution.libraryIdentity,
        stats:world.solution.stats,selected:world.solution.selected.map(value=>value.sourceId)
      }:null});
    root._authoring={get engine(){return engine;},get renderer(){return renderer;},get world(){return world;},get inspector(){return inspector;},install,start,rescue,automated,setInspection};
    requestAnimationFrame(step);
  } catch(error) {
    get("loading").textContent="预览未能加载："+error.message;root.dataset.error=error.message;
  }
})();
