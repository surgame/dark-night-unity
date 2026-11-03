(function(M,C) {
  "use strict";
  const root=document.getElementById("macro-cave-compare");if(!root) return;
  const $=id=>root.querySelector("#mc-"+id),canvas=$("scene");
  const storageKey="dark-nights-macro-cave-comparison-v1",kind="macro-cave-v1";
  const methods=["dual","expanded","contour","blocks","bridges","wfc"];
  const names=["16型圆角","256多值","连续轮廓","大块直拼","大块＋过渡","大块WFC"];
  const focuses={
    all:{x:92,y:43,span:190},entry:{x:31,y:25,span:55},
    hall:{x:61,y:26,span:53},elbow:{x:90,y:40,span:48},
    basin:{x:122,y:51,span:53},seam:{x:90,y:40,span:22}
  };
  const renderer=M.createRenderer(root,canvas),keys={left:false,right:false};
  const state={method:"bridges",seed:271,focus:"elbow",seams:true,reference:false,
    walk:false,follow:false,camera:{x:90,y:40,scale:1},player:null,baseline:null};
  let world=null,worlds={},dirty=true,disposed=false,last=0,accumulator=0,lastWritten="";
  const cleanup=[];
  function on(target,event,handler,options) {
    target.addEventListener(event,handler,options);
    cleanup.push(()=>target.removeEventListener(event,handler,options));
  }
  function compatible(value) {
    const data=value?.privateContent||value;
    return data&&data.kind===kind?data:null;
  }
  function load() {
    const host=compatible(window.openai?.widgetState);if(host) return host;
    try{return compatible(JSON.parse(localStorage.getItem(storageKey)));}catch{return null;}
  }
  function getWorld(method) {return worlds[method]||(worlds[method]=M.makeWorld(method,state.seed));}
  function placePlayer(x=18) {
    x=M.clamp(x,10,170);
    state.player=C.makePlayer(world,{x,y:M.floor(world,x)-1.02,facing:state.player?.facing||1});
  }
  function focus(id) {
    state.focus=id;root.dataset.focus=id;root.dataset.walk=String(state.walk);
    renderer.resize();
    const target=focuses[id]||focuses.elbow,box=renderer.dimensions();
    state.camera={x:target.x,y:target.y,
      scale:Math.min(box.width/target.span,box.height/(target.span*.58))};
    state.follow=false;dirty=true;
  }
  function install(method,keepPlayer=true) {
    const next=getWorld(method),x=keepPlayer&&state.player?state.player.x:18;
    world=next;state.method=method;state.baseline=getWorld("dual");
    placePlayer(x);renderer.setWorld(world);state.follow=false;dirty=true;update();
  }
  function detail() {
    if(state.method==="dual") return "二值四角 · 16型 · 固定中点／半格圆弧。";
    if(state.method==="expanded") return "四角各4值 · 256配置 · 本图使用"+
      world.grid.stats.usedConfigurations+"种边界配置；与16型共用空实角点。";
    if(state.method==="contour") return "同一作者引导，跨格连续塑形；显示与试走消费同一折线轮廓。";
    if(state.method==="blocks") return "8块直接连接 · 相邻端口匹配 · 每块完整岩台／洞顶轮廓。";
    if(state.method==="bridges") return "8块＋7段短过渡 · 阴影带为连接段 · 可连接不同宽度的端口。";
    const s=world.solution.stats;
    return "链式WFC · "+world.solution.library+"项参数候选／8槽 · "+
      s.observations+"次选块，传播删除"+s.removed+"项；端口逐对校验。";
  }
  function update() {
    root.dataset.walk=String(state.walk);
    root.dataset.focus=state.focus;
    root.querySelectorAll("[data-method]").forEach(b=>
      b.setAttribute("aria-pressed",String(b.dataset.method===state.method)));
    $("focus").value=state.focus;$("seams").checked=state.seams;$("reference").checked=state.reference;
    $("walk").setAttribute("aria-pressed",String(state.walk));
    $("walk").textContent=state.walk?"停止试走":"试走";
    $("seed").textContent="种子 "+state.seed;
    $("caption").textContent=names[methods.indexOf(state.method)]+" · "+(state.walk?"局部试走":"同镜头对比");
    $("detail").textContent=detail();
    if($("library-open").open) renderer.drawLibrary(world);
  }
  function snapshot() {
    return {kind,method:state.method,seed:state.seed,focus:state.focus,seams:state.seams,
      reference:state.reference,walk:state.walk,
      camera:{x:state.camera.x,y:state.camera.y,span:renderer.dimensions().width/state.camera.scale},
      player:{x:state.player.x,y:state.player.y,facing:state.player.facing}};
  }
  function save() {
    const data=snapshot();lastWritten=JSON.stringify(data);
    try{localStorage.setItem(storageKey,lastWritten);}catch{}
    window.openai?.setWidgetState?.({modelContent:{view:"macro-cave-comparison",
      method:state.method,seed:state.seed,focus:state.focus,walk:state.walk},
      privateContent:data})?.catch(()=>{});
  }
  function apply(data) {
    if(!data) return;
    const seed=Number.isInteger(data.seed)&&data.seed>0?data.seed>>>0:271;
    if(seed!==state.seed) {state.seed=seed;worlds={};}
    state.seams=data.seams!==false;state.reference=data.reference===true;
    state.walk=data.walk===true;
    install(methods.includes(data.method)?data.method:"bridges",false);
    focus(focuses[data.focus]?data.focus:"elbow");
    const cam=data.camera;
    if(cam&&[cam.x,cam.y,cam.span].every(Number.isFinite)) state.camera={
      x:M.clamp(cam.x,0,M.width),y:M.clamp(cam.y,0,M.height),
      scale:renderer.dimensions().width/M.clamp(cam.span,8,300)};
    const p=data.player;
    if(p&&[p.x,p.y].every(Number.isFinite)) state.player=C.makePlayer(world,p);
    keys.left=keys.right=false;update();dirty=true;
  }
  function attempt(action) {
    try {action();$("error").hidden=true;save();}
    catch(e) {$("error").textContent=String(e.message);$("error").hidden=false;}
  }
  root.querySelectorAll("[data-method]").forEach(button=>on(button,"click",()=>
    attempt(()=>install(button.dataset.method))));
  on($("focus"),"change",()=>attempt(()=>{
    state.walk=false;keys.left=keys.right=false;focus($("focus").value);update();
  }));
  for(const id of ["seams","reference"]) on($(id),"change",()=>{
    state[id]=$(id).checked;dirty=true;save();
  });
  on($("walk"),"click",()=>{
    state.walk=!state.walk;keys.left=keys.right=false;state.follow=state.walk;
    if(state.walk) state.camera={x:state.player.x+6,y:state.player.y-5,
      scale:Math.min(renderer.dimensions().width/38,renderer.dimensions().height/24)};
    dirty=true;update();save();
  });
  on($("entry"),"click",()=>{
    placePlayer();state.follow=state.walk;
    if(state.walk) {state.camera.x=24;state.camera.y=world.spawn.y-5;}
    else focus("entry");update();dirty=true;save();
  });
  on($("next"),"click",()=>attempt(()=>{
    const nextSeed=(state.seed+101)>>>0,next=M.makeWorld(state.method,nextSeed);
    state.seed=nextSeed;worlds={[state.method]:next};install(state.method,false);
  }));
  function editable(target) {return target.closest?.("input,select,textarea,[contenteditable=true]");}
  on(window,"keydown",event=>{
    if(!state.walk||editable(event.target)||event.ctrlKey||event.metaKey||event.altKey) return;
    if(event.code==="Space"&&event.target.closest?.("button")) return;
    if(["ArrowLeft","KeyA","ArrowRight","KeyD","Space"].includes(event.code)) {
      event.preventDefault();state.follow=true;
      if(["ArrowLeft","KeyA"].includes(event.code)) keys.left=true;
      if(["ArrowRight","KeyD"].includes(event.code)) keys.right=true;
      if(event.code==="Space"&&!event.repeat) state.player.jumpBuffer=.12;
    }
  });
  on(window,"keyup",event=>{
    if(["ArrowLeft","KeyA"].includes(event.code)) keys.left=false;
    if(["ArrowRight","KeyD"].includes(event.code)) keys.right=false;
    if(state.walk&&["ArrowLeft","KeyA","ArrowRight","KeyD","Space"].includes(event.code)) save();
  });
  on(window,"blur",()=>{keys.left=keys.right=false;});
  root.querySelectorAll("[data-hold]").forEach(button=>{
    on(button,"pointerdown",e=>{
      e.preventDefault();button.setPointerCapture(e.pointerId);keys[button.dataset.hold]=true;state.follow=true;
    });
    const release=()=>{keys[button.dataset.hold]=false;save();};
    on(button,"pointerup",release);on(button,"pointercancel",release);
  });
  on($("jump"),"click",()=>{state.player.jumpBuffer=.12;state.follow=true;save();});
  on(root,"click",event=>{
    if(state.walk&&event.detail>0) event.target.closest?.("button")?.blur();
  });
  let drag=null;
  on(canvas,"contextmenu",event=>event.preventDefault());
  on(canvas,"pointerdown",event=>{
    if(drag) return;canvas.setPointerCapture(event.pointerId);
    drag={id:event.pointerId,x:event.clientX,y:event.clientY};state.follow=false;
  });
  on(canvas,"pointermove",event=>{
    if(!drag||event.pointerId!==drag.id) return;
    state.camera.x-=(event.clientX-drag.x)/state.camera.scale;
    state.camera.y-=(event.clientY-drag.y)/state.camera.scale;
    drag.x=event.clientX;drag.y=event.clientY;dirty=true;
  });
  const releaseDrag=event=>{if(drag&&event.pointerId===drag.id) {drag=null;save();}};
  on(canvas,"pointerup",releaseDrag);on(canvas,"pointercancel",releaseDrag);
  on(canvas,"wheel",event=>{
    event.preventDefault();const box=canvas.getBoundingClientRect();
    const x=event.clientX-box.left,y=event.clientY-box.top,before=renderer.point(state.camera,x,y);
    state.camera.scale=M.clamp(state.camera.scale*Math.exp(-event.deltaY*.0015),2,65);
    const after=renderer.point(state.camera,x,y);state.camera.x+=before.x-after.x;state.camera.y+=before.y-after.y;
    state.follow=false;dirty=true;save();
  },{passive:false});
  on($("library-open"),"toggle",()=>{if($("library-open").open) renderer.drawLibrary(world);});
  const resize=new ResizeObserver(()=>{
    const old=renderer.dimensions(),span=old.width/state.camera.scale;
    renderer.resize();state.camera.scale=renderer.dimensions().width/span;
    if($("library-open").open) renderer.drawLibrary(world);dirty=true;
  });
  resize.observe(canvas);
  const refreshTheme=()=>{renderer.colors();dirty=true;
    if($("library-open").open) renderer.drawLibrary(world);};
  const theme=new MutationObserver(refreshTheme);
  theme.observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
  on(matchMedia("(prefers-color-scheme: dark)"),"change",refreshTheme);
  on(window,"openai:set_globals",event=>{
    const data=compatible(event.detail?.globals?.widgetState);
    if(data&&JSON.stringify(data)!==lastWritten) apply(data);
  });
  install(state.method,false);focus("elbow");apply(load());
  root._inspect=()=>({ready:true,method:state.method,seed:state.seed,focus:state.focus,
    camera:{...state.camera},walk:state.walk,seams:state.seams,reference:state.reference,
    player:{x:state.player.x,y:state.player.y,grounded:state.player.grounded},
    insideRock:C.playerCollides(world,state.player.x,state.player.y),
    size:[M.width,M.height],grid:world.grid?.stats||null,
    blocks:world.blocks.map(b=>({slot:b.slot,id:b.id,variant:b.variant,left:b.left,right:b.right,start:b.start,end:b.end})),
    bridges:world.transitions.length,wfc:world.solution?{...world.solution.stats,library:world.solution.library}:null});
  root._world=()=>world;
  function frame(time) {
    if(!root.isConnected) {
      disposed=true;resize.disconnect();theme.disconnect();cleanup.forEach(fn=>fn());return;
    }
    const dt=last?Math.min(.05,(time-last)/1000):0;last=time;
    if(state.walk&&!document.hidden) {
      accumulator+=dt;
      while(accumulator>=1/120) {C.stepPlayer(world,state.player,keys,1/120);accumulator-=1/120;}
      if(state.follow) {
        const t=1-Math.exp(-dt*9);
        state.camera.x=M.mix(state.camera.x,state.player.x+state.player.facing*5,t);
        state.camera.y=M.mix(state.camera.y,state.player.y-5,t);
      }
      dirty=true;
    } else accumulator=0;
    if(dirty) {renderer.render(state);dirty=false;}
    if(!disposed) requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})(globalThis.MacroCave,globalThis.CaveWhitebox);
