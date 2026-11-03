(async function(){
  "use strict";
  const host=document.getElementById("terrain-flow"),get=id=>host.querySelector("#fl-"+id),api=window.ShapeStudy;
  const names={
    dual:["空实数据就位","原位匹配16型","本格直接查表","向全图铺设拼接","完整地图冻结","原位置开挖"],
    wfc:["空实约束就位","原位展开候选","真实熵选择","约束向邻格传播","全图收敛冻结","原位置开挖"],
    hybrid:["宏观槽位就位","原位展开大段候选","选择一段并传播","其余段依次确定","投影小格与坡形","原位置开挖"]
  };
  const valid=value=>value?.version===3&&Object.hasOwn(names,value.method)&&Number.isInteger(value.step)&&value.step>=0&&value.step<6;
  const saved=window.openai?.widgetState?.privateContent;
  let state=valid(saved)?{...saved}:{version:3,method:"dual",step:0};
  let data,renderer,position=state.step,raf=0,finishMotion=null,playing=false,playToken=0,ready=false,lastSent="";
  const reduced=matchMedia("(prefers-reduced-motion: reduce)");
  host.querySelectorAll("button,select").forEach(n=>n.disabled=true);
  function save(){
    lastSent=JSON.stringify(state);
    window.openai?.setWidgetState?.({modelContent:{view:"continuous-map",method:state.method,
      step:state.step+1,stage:names[state.method][state.step],seed:271},privateContent:{...state}})?.catch(()=>{});
  }
  function detail(){
    const m=state.method,s=state.step,e=data.trace.event;
    if(s===5)return data.dug[m].changed.length+"格结果变化；镜头与地图位置不变，WFC不重新求解。";
    if(m==="dual")return[
      "空实输入落在同一张地图上；虚线是固定的洞形参照。",
      "每个小格在原位置读取四角，匹配16种拓扑中的一种。",
      "着色格直接查表得到mask "+api.details(data.before.dual,data.focus).mask+"，无需随机选择。",
      "其余格在原位置铺满；相邻格共用中点边口。",
      "同一片岩壁保留，叠加池岸、植被位置；原人物为3格比例参照。"
    ][s];
    if(m==="wfc")return[
      "空实布局与16型相同，边缘角点允许不同幅值。",
      "候选轮廓叠在各自的小格中；收敛时这些轮廓会收缩。",
      "实际第"+e.number+"次选择：本格"+e.candidates.length+"候选选1；前序选择也真实执行。",
      "实际传播依次影响"+new Set(e.changes.map(c=>c.at)).size+"个邻格；着色范围保留在原处。",
      "继续回放其余真实选择，共"+data.trace.stats.observations+"次观察后冻结完整地图。"
    ][s];
    return[
      "同一洞形按宽8格划为6段；分段只影响制作期选形。",
      "每段的可用轮廓叠在原位置；数字表示剩余候选数。",
      "大段选定后，邻段按左右端口删去不兼容候选。",
      "按真实顺序完成6段选择；已确定轮廓留在原位置。",
      "宏观结果冻结，再在同一坐标上落成16型基础和现有12种坡形。"
    ][s];
  }
  function paint(){if(data&&renderer)renderer.render(data,state.method,position);}
  function ui(){
    host.querySelectorAll("[data-method]").forEach(b=>b.setAttribute("aria-pressed",String(b.dataset.method===state.method)));
    get("stage").replaceChildren(...names[state.method].map((name,i)=>{
      const option=document.createElement("option");option.value=i;option.textContent=(i+1)+" / 6 · "+name;return option;
    }));
    get("stage").value=state.step;get("stage").disabled=false;
    get("prev").disabled=state.step===0;get("next").disabled=state.step===5;
    get("play").disabled=false;get("play").textContent=playing?"暂停":state.step===5?"重播流程":"播放流程";
    get("phase").textContent=(state.step===5?"运行时变化":"制作过程")+" · 固定镜头／同一地图坐标 · 种子271";
    get("detail").textContent=detail();
    get("canvas").setAttribute("aria-label",names[state.method][state.step]+"。"+detail());
  }
  function cancelMotion(){
    cancelAnimationFrame(raf);raf=0;
    if(finishMotion){const finish=finishMotion;finishMotion=null;finish(false);}
  }
  function stop(){
    playing=false;playToken++;cancelMotion();position=state.step;
    if(ready){paint();ui();}
  }
  function move(step,persist=true){
    cancelMotion();state.step=step;ui();
    const from=position,started=performance.now(),duration=reduced.matches?0:Math.min(1500,Math.abs(step-from)*850);
    return new Promise(resolve=>{
      finishMotion=resolve;
      function tick(now){
        const t=duration?Math.min(1,(now-started)/duration):1;
        const eased=t*t*(3-2*t);position=from+(step-from)*eased;paint();
        if(t<1)raf=requestAnimationFrame(tick);
        else{raf=0;finishMotion=null;if(persist)save();resolve(true);}
      }
      raf=requestAnimationFrame(tick);
    });
  }
  async function play(){
    if(playing){stop();return;}
    cancelMotion();
    if(state.step===5){state.step=0;position=0;paint();}
    playing=true;const token=++playToken;ui();
    for(let next=state.step+1;next<=5;next++){
      if(token!==playToken)return;
      if(!await move(next)||token!==playToken)return;
      await new Promise(resolve=>setTimeout(resolve,900));
    }
    if(token===playToken){playing=false;ui();}
  }
  try{
    const hero=new Image();hero.src=window.SHAPE_STUDY_HERO;await hero.decode();
    data=await api.buildFlow(271);renderer=new api.FlowRenderer(get("canvas"),null,host,hero);
    host.querySelectorAll("[data-method]").forEach(button=>{
      button.disabled=false;button.addEventListener("click",()=>{stop();state.method=button.dataset.method;paint();ui();save();});
    });
    get("prev").addEventListener("click",()=>{stop();move(Math.max(0,state.step-1));});
    get("next").addEventListener("click",()=>{stop();move(Math.min(5,state.step+1));});
    get("stage").addEventListener("change",event=>{const step=Number(event.target.value);stop();move(step);});
    get("play").addEventListener("click",play);
    new ResizeObserver(paint).observe(get("canvas"));
    new MutationObserver(paint).observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
    matchMedia("(prefers-color-scheme: dark)").addEventListener("change",paint);
    document.addEventListener("visibilitychange",()=>{if(document.hidden)stop();});
    window.addEventListener("openai:set_globals",event=>{
      const value=event.detail?.globals?.widgetState?.privateContent;
      if(valid(value)&&JSON.stringify(value)!==lastSent){stop();state={...value};position=state.step;ui();paint();}
    });
    ready=true;ui();paint();host.dataset.ready="true";
    host._inspect=()=>({state:{...state},playing,animating:raf!==0,position,frame:renderer.lastFrame,
      historyLength:data.trace.timeline.events.length,observations:data.trace.stats.observations,
      finalTileIdentity:data.trace.map.tiles.every((v,i)=>v===data.before.wfc.tiles[i]),
      macroSelected:data.macro.selected.map(t=>t.id),digChanges:Object.fromEntries(["dual","wfc","hybrid"].map(k=>[k,data.dug[k].changed.length]))});
    host._authoring={get data(){return data;},get renderer(){return renderer;},move,stop};
  }catch(error){host.dataset.error=error.message;get("error").hidden=false;get("error").textContent=error.message;}
})();
