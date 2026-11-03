(async function(){
  "use strict";
  const root=document.getElementById("flat-cave-kit"),canvas=root.querySelector("#flat-scene"),ctx=canvas.getContext("2d");
  const $=id=>root.querySelector("#flat-"+id),maps=new Map(),images={},U=FlatCave.U;
  const state={version:3,view:"compare",style:"new",stage:2,light:false,seed:271,room:3,cx:29,cy:20,span:12};
  function restore(saved) {
    if(!saved||saved.version!==3)return;
    if(["compare","mining","local","overview","sources"].includes(saved.view))state.view=saved.view;
    if(["new","previous"].includes(saved.style))state.style=saved.style;
    if([0,1,2].includes(+saved.stage))state.stage=+saved.stage;
    if([271,902,1643].includes(+saved.seed))state.seed=+saved.seed;
    state.light=!!saved.light;
    for(const key of ["room","cx","cy","span"])if(Number.isFinite(saved[key]))state[key]=saved[key];
    state.room=Math.max(0,Math.floor(state.room));state.span=Math.max(5,Math.min(85,state.span));
  }
  restore(window.openai?.widgetState?.privateContent);
  await Promise.all(Object.entries(FLAT_SOURCES).map(async([key,url])=>{
    const img=new Image();img.src=url;await img.decode();images[key]=img;
  }));
  const art=FlatCave.prepare(images);
  let map,width=736,height=460,busy=false,revision=0;
  async function ensure(seed) {
    if(!maps.has(seed)){
      const plan=CaveWB.makePlan(seed,80,48);
      const m=await CaveWB.solve(plan,{pause:()=>new Promise(resolve=>setTimeout(resolve,0))});
      FlatCave.build(m);maps.set(seed,m);
    }
    return maps.get(seed);
  }
  function focus() {
    const p=map.plan.nodes[state.room%map.plan.nodes.length];
    state.cx=p.x;state.cy=p.y+.3;state.span=width<480?7:12;
  }
  function save() {
    window.openai?.setWidgetState({modelContent:{style:state.style,view:state.view,stage:state.stage,light:state.light,seed:state.seed},
      privateContent:{...state}})?.catch(()=>{});
  }
  function theme(name){return getComputedStyle(root).getPropertyValue("--"+name).trim();}
  function fit(image) {
    ctx.clearRect(0,0,width,height);const s=Math.min(width/image.width,height/image.height);
    ctx.drawImage(image,(width-image.width*s)/2,(height-image.height*s)/2,image.width*s,image.height*s);
  }
  function gallery(){
    ctx.clearRect(0,0,width,height);
    const cw=width/3,ch=height*.3,names=["rock","floor","ceiling","backdrop","ore","hero"],labels=["岩面","地面顶面与厚岩壁","不规则悬顶","蓝色远景","嵌岩矿斑","原角色"];
    for(let i=0;i<6;i++){
      const image=images[names[i]],s=Math.min((cw-10)/image.width,(ch-30)/image.height);
      const x=i%3*cw,y=Math.floor(i/3)*ch;
      ctx.drawImage(image,x+(cw-image.width*s)/2,y+4,image.width*s,image.height*s);
      ctx.fillStyle=theme("foreground");ctx.font="12px sans-serif";ctx.textAlign="center";ctx.fillText(labels[i],x+cw/2,y+ch-6);
    }
    const ids=[0,3,12,15,48,51,60,63,192,195,204,207,240,243,252,255];
    const sw=width/8,sh=(height-ch*2)/2;
    ids.forEach((id,i)=>{
      const tile=FlatCave.tileCanvas(art,id,128),s=Math.min(sw-4,sh-16);
      ctx.drawImage(tile,(i%8)*sw+(sw-s)/2,ch*2+Math.floor(i/8)*sh,s,s);
      ctx.fillStyle=theme("foreground");ctx.fillText(String(i),(i%8)*sw+sw/2,ch*2+Math.floor(i/8)*sh+s+12);
    });
  }
  function draw(){
    if(!map)return;
    $("view").value=state.view;$("stage").value=state.stage;$("seed").value=state.seed;$("light").checked=state.light;
    const mining=state.view==="mining",wfc=["local","overview"].includes(state.view);
    $("stage-label").hidden=!mining;$("light-label").hidden=!mining;$("seed-label").hidden=!wfc;$("next").hidden=state.view!=="local";
    $("style-label").hidden=state.view!=="compare";$("style").value=state.style;
    if(state.view==="compare") {
      if(state.style==="previous")fit(images.previous);
      else {
        const selected=maps.get(271),c=FlatCave.canvas(1536,864),node=selected.plan.nodes[3];
        FlatCave.draw(c.getContext("2d"),art,selected,{x:node.x*U-768,y:node.y*U-400,w:1536,h:864},1536,864,{hero:true,room:3});
        fit(c);
      }
      $("status").textContent="种子 271 · 同一局部构图 · 新版地面顶面 / 厚岩壁 / 悬顶装配 · 待视觉评审";
    } else if(mining){
      const image=FlatCave.mining.render(art,state.stage,state.light).canvas;
      ctx.clearRect(0,0,width,height);
      const displayHeight=width*9/16;
      ctx.drawImage(image,320,260,1080,607.5,0,(height-displayHeight)/2,width,displayHeight);
      $("status").textContent="局部作者样板 · "+["初始少量露矿","移除 4 格围岩","累计移除 15 格围岩"][state.stage]+" · 照明为离线示意";
    } else if(state.view==="sources"){
      gallery();$("status").textContent="5 类美术源 · 16 类拓扑 / 256 种角点组合 · 形状色图采用零相位，仅供目录查看";
    } else {
      let box;
      if(state.view==="overview"){
        const span=Math.max(map.width,map.height*width/height)*U;
        box={x:(map.width*U-span)/2,y:(map.height*U-span*height/width)/2,w:span,h:span*height/width};
      } else box={x:(state.cx-state.span/2)*U,y:state.cy*U-state.span*U*height/width/2,w:state.span*U,h:state.span*U*height/width};
      FlatCave.draw(ctx,art,map,box,width,height,{hero:state.view==="local",room:state.room%map.plan.nodes.length});
      $("status").textContent="320 × 192 格 · 接边冲突 "+map.check.mismatches+(state.view==="local"?" · 拖动 / 滚轮缩放":" · 同一套源图与共享轮廓");
    }
    root.dataset.ready="true";
  }
  async function select(seed,refocus=true){
    const request=++revision;busy=true;
    try{const selected=await ensure(seed);if(request!==revision)return;map=selected;state.seed=seed;if(refocus)focus();draw();}
    finally{if(request===revision)busy=false;}
  }
  $("view").onchange=()=>{state.view=$("view").value;draw();save();};
  $("style").onchange=()=>{state.style=$("style").value;draw();save();};
  $("stage").onchange=()=>{state.stage=+$("stage").value;draw();save();};
  $("light").onchange=()=>{state.light=$("light").checked;draw();save();};
  $("seed").onchange=async()=>{await select(+$("seed").value);save();};
  $("next").onclick=()=>{state.room=(state.room+1)%map.plan.nodes.length;focus();draw();save();};
  let drag=null;
  canvas.onpointerdown=e=>{if(state.view!=="local")return;drag={x:e.clientX,y:e.clientY,cx:state.cx,cy:state.cy};canvas.setPointerCapture(e.pointerId);};
  canvas.onpointermove=e=>{if(!drag)return;state.cx=drag.cx-(e.clientX-drag.x)*state.span/width;state.cy=drag.cy-(e.clientY-drag.y)*state.span/width;draw();};
  canvas.onpointerup=()=>{drag=null;save();};canvas.onpointercancel=()=>{drag=null;};
  canvas.addEventListener("wheel",e=>{if(state.view!=="local")return;e.preventDefault();state.span=Math.max(5,Math.min(85,state.span*Math.exp(e.deltaY*.001)));draw();save();},{passive:false});
  function resize(){
    width=canvas.clientWidth;height=width*10/16;const dpr=Math.min(devicePixelRatio||1,2);
    canvas.width=Math.round(width*dpr);canvas.height=Math.round(height*dpr);ctx.setTransform(dpr,0,0,dpr,0,0);draw();
  }
  window.addEventListener("openai:set_globals",async e=>{const value=e.detail?.globals?.widgetState?.privateContent;if(value){restore(value);await select(state.seed,false);}});
  new ResizeObserver(resize).observe(canvas);
  matchMedia("(prefers-color-scheme: dark)").addEventListener("change",draw);
  await ensure(271);await select(state.seed,!window.openai?.widgetState?.privateContent);resize();
  root._inspect=()=>({state:{...state},busy,width,height,seed:map.seed,check:map.check.mismatches,stageCells:FlatCave.mining.cellsFor(state.stage).length});
  root._art={images,art,maps,select,ensure,get map(){return map;},draw};
})().catch(error=>{const root=document.getElementById("flat-cave-kit");root.dataset.error=error.message;root.querySelector("#flat-status").textContent=error.message;console.error(error);});
