(async function(){
  "use strict";
  const host=document.getElementById("static-cave-study"),get=id=>host.querySelector("#sc-"+id),api=window.StaticCave;
  const valid=v=>v?.version===1&&["base","whole","hybrid"].includes(v.mode)&&[0,1].includes(v.form)&&Number.isFinite(v.seed);
  const saved=window.openai?.widgetState?.privateContent;
  let state=valid(saved)?{...saved}:{version:1,mode:"hybrid",form:0,seed:271,layers:false,conflict:false};
  let data,renderer,ready=false,lastSent="",raf=0,visible=true;
  const motion=matchMedia("(prefers-reduced-motion: reduce)");
  const save=()=>{
    lastSent=JSON.stringify(state);
    window.openai?.setWidgetState?.({modelContent:{view:"static-terrain-comparison",mode:state.mode,form:state.form,
      seed:state.seed,conflict:state.conflict,identical:data.identical},privateContent:{...state}})?.catch(()=>{});
  };
  function draw(time=0){if(data&&renderer)renderer.draw(data,state,time);}
  function ui(){
    host.querySelectorAll("[data-mode]").forEach(b=>b.setAttribute("aria-pressed",String(b.dataset.mode===state.mode)));
    get("layers").checked=!!state.layers;get("conflict").setAttribute("aria-pressed",String(!!state.conflict));
    get("conflict").textContent=state.conflict?"恢复合法规则":"试一次规则冲突";
    get("status").textContent=state.mode==="base"?"基础16型 · 当前洞形已确定；无需WFC":
      state.mode==="whole"?"176种整块组合记录 · 几何与生态一起编码":
      "16种基础形＋11种生态原型 · 几何与生态分层编码";
    get("detail").textContent=state.mode==="base"?"水为共用独立层；这里不选择额外生态组合。":
      state.mode==="whole"?"176是可自动派生的组合记录，非176张手绘图；本次输出与分层方式相同。":
      "同一生态规则、同一种子，输出与整块方式相同；区别在候选如何组织。";
    if(state.layers)get("detail").textContent+=" 本例生态沿地面20槽匹配。";
    get("error").hidden=!state.conflict;
    if(state.conflict)get("error").textContent="干岩D与湿岸W直接相邻，规则无解；两种WFC编码均拒绝，当前保留上一有效图。";
  }
  function generate(next,persist=true){
    try{
      const candidate=api.generate(next.form,next.seed,false);
      if(next.conflict){
        let rejected=0;
        for(const mode of ["whole","hybrid"])try{api.solve(candidate.scene,mode,next.seed,true);}catch{rejected++;}
        if(rejected!==2)throw new Error("Conflict example unexpectedly solved.");
      }
      data=candidate;state={...next};delete host.dataset.error;ui();draw();
      if(persist)save();return true;
    }catch(error){
      host.dataset.error=error.message;get("error").hidden=false;get("error").textContent=error.message;
      if(data)draw();return false;
    }
  }
  function frame(now){if(!visible||motion.matches){raf=0;return;}draw(now/1000);raf=requestAnimationFrame(frame);}
  function animate(){
    cancelAnimationFrame(raf);raf=0;
    if(visible&&!motion.matches)raf=requestAnimationFrame(frame);else draw();
  }
  try{
    const hero=new Image();hero.src=window.STATIC_CAVE_HERO;await hero.decode();
    renderer=new api.Renderer(get("canvas"),null,host,hero);
    if(!generate(state,false))throw new Error("Initial comparison failed.");
    host.querySelectorAll("[data-mode]").forEach(button=>button.addEventListener("click",()=>{
      state.mode=button.dataset.mode;ui();draw();save();
    }));
    get("shuffle").addEventListener("click",()=>generate({...state,seed:(state.seed+1009)>>>0,conflict:false}));
    get("form").addEventListener("click",()=>generate({...state,form:1-state.form,conflict:false}));
    get("conflict").addEventListener("click",()=>generate({...state,conflict:!state.conflict}));
    get("layers").addEventListener("change",event=>{state.layers=event.target.checked;ui();draw();save();});
    new ResizeObserver(()=>draw()).observe(get("canvas"));
    new MutationObserver(()=>draw()).observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
    matchMedia("(prefers-color-scheme: dark)").addEventListener("change",()=>draw());
    motion.addEventListener("change",animate);
    document.addEventListener("visibilitychange",()=>{visible=!document.hidden;animate();});
    window.addEventListener("openai:set_globals",event=>{
      const value=event.detail?.globals?.widgetState?.privateContent;
      if(valid(value)&&JSON.stringify(value)!==lastSent)generate({...state,...value},false);
    });
    ready=true;host.dataset.ready="true";animate();
    host._inspect=()=>({state:{...state},ready,identical:data.identical,
      geometry:data.scene.geometry,wholeGeometry:data.whole.geometry,hybridGeometry:data.hybrid.geometry,
      wholeEco:data.whole.eco,hybridEco:data.hybrid.eco,wholeStats:data.whole.stats,hybridStats:data.hybrid.stats,
      viewport:{...renderer.viewport},catalog:{base:16,eco:11,whole:api.fullCatalog.length},conflictRejected:!!state.conflict});
    host._authoring={get data(){return data;},draw,generate};
  }catch(error){host.dataset.error=error.message;get("error").hidden=false;get("error").textContent=error.message;}
})();
