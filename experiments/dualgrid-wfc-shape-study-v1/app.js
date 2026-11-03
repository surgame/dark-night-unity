(async function(){
  "use strict";
  const host=document.getElementById("dg-wfc-study"),get=id=>host.querySelector("#ds-"+id),api=window.ShapeStudy;
  const valid=value=>value?.version===1&&["basin","shoulder","network"].includes(value.scene)&&
    ["dual","wfc","hybrid"].includes(value.method)&&Number.isFinite(value.seed);
  const saved=window.openai?.widgetState?.privateContent;
  let state={version:1,seed:271,scene:"basin",method:"hybrid",camera:innerWidth<480?"close":"whole",
    grid:false,overlay:false,atlas:false,dug:false,selected:null,...(valid(saved)?saved:{})};
  let pair=null,renderer=null,generation=0,ready=false,lastSent="";
  const image=new Image();image.src=window.SHAPE_STUDY_HERO;
  function save(){
    if(!ready)return;
    lastSent=JSON.stringify(state);
    window.openai?.setWidgetState?.({modelContent:{seed:state.seed,scene:state.scene,
      method:state.method,dug:state.dug},privateContent:{...state}})?.catch(()=>{});
  }
  function draw(){
    if(!pair||!renderer)return;
    host.querySelectorAll("[data-method]").forEach(b=>b.setAttribute("aria-pressed",String(b.dataset.method===state.method)));
    get("scene").value=state.scene;get("camera").value=state.camera;get("grid").checked=state.grid;
    get("overlay").checked=state.overlay;get("atlas").checked=state.atlas;
    get("dig").setAttribute("aria-pressed",String(state.dug));get("dig").textContent=state.dug?"撤销开挖":"局部挖一格";
    get("atlas-region").hidden=!state.atlas;get("seed").textContent="种子 "+state.seed+
      (state.method==="hybrid"?" · WFC初始选形":" · A/B同一角点占据");
    renderer.draw(pair,state);
    const map=pair[state.method],m=api.metrics(map);
    get("summary").textContent=state.method==="dual"?"固定16拓扑 · 边口在1/2 · "+m.boundaryConfigurations+"种边界配置用于本图":
      state.method==="wfc"?"256角点配置 · 边口在1/4、1/2、3/4 · 本图选用"+m.boundaryConfigurations+"种边界配置":
      "WFC组合6段轮廓 → Dual Grid基础＋现有12坡形 · 本图"+m.slopeCells+"格使用坡形";
    if(state.dug)get("summary").textContent+=" · "+m.changed+"格结果变化";
    get("legend").textContent=state.method==="wfc"?"着色轮廓：包含非中点边口，需额外定义形状与美术接口。":
      state.method==="hybrid"?"小块地形和已有坡形表达边界；WFC只用于制作初始候选，开挖不重求解。":
      "16型包含透明与全实心；坡形和美术变体另计。";
    if(state.selected!==null){
      const d=api.details(map,state.selected);
      get("selection").textContent="格("+d.x+","+d.y+") · mask "+d.mask+" · NW/NE/SW/SE："+d.values.join("/")+
        " · 边口："+(d.positions.length?d.positions.map(v=>v===.5?"1/2":v===.25?"1/4":"3/4").join("、"):"无");
    }else get("selection").textContent="点选边缘格查看四角与接口。";
    if(state.atlas){
      get("atlas-label").textContent=state.method==="dual"?"16种四角拓扑（位序NW/NE/SW/SE）":
        state.method==="wfc"?"固定同一种上沿拓扑：16种角点幅值配置":"16种基础拓扑＋12种现有坡形（独立定义）";
      renderer.drawAtlas(state);
    }
  }
  async function generate(next,persist=false){
    const id=++generation;get("next").disabled=true;get("error").hidden=true;
    get("summary").textContent="正在求解同一洞形…";
    try{
      const candidate=await api.generate(next.seed,next.scene,{pause:()=>new Promise(r=>setTimeout(r,0)),
        cancelled:()=>id!==generation});
      if(id!==generation)return false;
      if(next.dug)api.dig(candidate,true);
      pair=candidate;state={...next};delete host.dataset.error;draw();
      if(persist)save();return true;
    }catch(error){
      if(id!==generation)return false;
      get("error").hidden=false;get("error").textContent="生成失败，保留上一有效白模："+error.message;
      host.dataset.error=error.message;if(pair)draw();return false;
    }finally{if(id===generation)get("next").disabled=false;}
  }
  try{
    await image.decode();renderer=new api.Renderer(get("canvas"),get("atlas-canvas"),host,image);
    if(!await generate(state))throw new Error("Initial candidate failed.");
    host.querySelectorAll("[data-method]").forEach(button=>button.addEventListener("click",()=>{
      state.method=button.dataset.method;draw();save();
    }));
    get("next").addEventListener("click",()=>generate({...state,seed:(state.seed+1009)>>>0,dug:false,selected:null},true));
    get("scene").addEventListener("change",event=>generate({...state,scene:event.target.value,dug:false,selected:null},true));
    get("camera").addEventListener("change",event=>{state.camera=event.target.value;draw();save();});
    for(const key of ["grid","atlas","overlay"])get(key).addEventListener("change",event=>{state[key]=event.target.checked;draw();save();});
    get("dig").addEventListener("click",()=>{state.dug=!state.dug;api.dig(pair,state.dug);draw();save();});
    get("canvas").addEventListener("click",event=>{state.selected=renderer.pick(event.clientX,event.clientY);draw();save();});
    new ResizeObserver(draw).observe(get("canvas"));
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change",draw);
    new MutationObserver(draw).observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
    window.addEventListener("openai:set_globals",event=>{
      const value=event.detail?.globals?.widgetState?.privateContent;
      if(valid(value)&&JSON.stringify(value)!==lastSent)generate({...state,...value});
    });
    ready=true;host.dataset.ready="true";
    host._inspect=()=>({state:{...state},dual:api.metrics(pair.dual),wfc:api.metrics(pair.wfc),hybrid:api.metrics(pair.hybrid),
      stats:pair.wfc.stats,hybridStats:pair.hybridSource.stats,
      macroLayout:pair.hybridPlan.macroLayout.selected.map(t=>({left:t.left,right:t.right,kind:api.macroKinds[t.kind].name})),
      macroMismatches:pair.hybridPlan.macroLayout.mismatches,
      hybridOccupancyChanges:pair.baseSamples.dual.reduce((n,v,i)=>n+((v>0)!==(pair.baseSamples.hybrid[i]>0)?1:0),0),
      sameOccupancy:pair.baseSamples.dual.every((v,i)=>(v>0)===(pair.baseSamples.wfc[i]>0)),
      geometryIdentity:pair.plan.seed+"/"+pair.plan.scene,selected:state.selected===null?null:api.details(pair[state.method],state.selected)});
    host._authoring={get pair(){return pair;},get renderer(){return renderer;},generate};
  }catch(error){host.dataset.error=error.message;get("error").hidden=false;get("error").textContent=error.message;}
})();
