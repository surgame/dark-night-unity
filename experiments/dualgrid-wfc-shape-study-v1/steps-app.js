(async function(){
  "use strict";
  const host=document.getElementById("terrain-steps"),get=id=>host.querySelector("#st-"+id),api=window.ShapeStudy;
  const names={
    dual:["空实格输入","准备16型","四角直接查表","共享边拼接","完成白模","开挖后更新"],
    wfc:["空实与幅值约束","建立候选域","最小熵选择","向邻格传播","冻结完整结果","开挖后更新"],
    hybrid:["划分宏观槽位","准备大段配置","大段WFC选形","冻结后投影小格","完成组合白模","开挖后更新"]
  };
  const phases={
    dual:["地形数据","美术／轮廓资源","每格直接映射","表现拼接","冻结预制结果","运行时局部变化"],
    wfc:["生成约束","候选过滤","生成时求解","生成时求解","冻结预制结果","运行时局部变化"],
    hybrid:["制作布局","大段资源／规则","生成时求解","确定性地形投影","冻结预制结果","运行时局部变化"]
  };
  const valid=value=>value?.version===2&&Object.hasOwn(names,value.method)&&Number.isInteger(value.step)&&value.step>=0&&value.step<6;
  const saved=window.openai?.widgetState?.privateContent;
  let state=valid(saved)?{...saved}:{version:2,method:"dual",step:0},data,renderer,ready=false,lastSent="";
  function save(){
    lastSent=JSON.stringify(state);
    window.openai?.setWidgetState?.({modelContent:{method:state.method,step:state.step+1,
      stage:names[state.method][state.step],seed:271},privateContent:{...state}})?.catch(()=>{});
  }
  function detail(){
    const {method,step}=state,e=data.trace.event,m=data.macro.events[0];
    const metrics=api.metrics(data.before[method]);
    if(method==="dual")return[
      "二值数据共用同一洞形；此处方格表示底层数据。",
      "16种是拓扑数；材质、坡形、装饰与动画资源另计。",
      "位序 NW / NE / SW / SE；选择过程不消耗随机数。",
      "共享空实端点让相邻格的中点边口一致。",
      "本图使用 "+metrics.boundaryConfigurations+" 种边界配置；人物沿用原图，身高3格。",
      "1个逻辑位置改变，4个渲染格结果改变；本白模全量重建用于核对。"
    ][step];
    if(method==="wfc")return[
      "与16型使用完全相同的空实占据；只增加边缘幅值选择。",
      "这里是第 "+e.number+" 次选择前，格("+e.at%48+","+Math.floor(e.at/48)+")的真实候选。",
      "实际第 "+e.number+" 次观察：熵 "+e.entropy.toFixed(4)+"；"+e.candidates.length+" 候选选出 ID "+e.chosen+"。",
      "本次传播影响 "+new Set(e.changes.map(c=>c.at)).size+" 格，共删除 "+e.changes.reduce((n,c)=>n+c.before.length-c.after.length,0)+" 个候选。",
      "实际完成 "+data.trace.stats.observations+" 次观察；边口允许1/4、1/2、3/4。",
      "这里使用角点配置库直接更新4格；更复杂邻接规则需要另定运行时修补合同。"
    ][step];
    return[
      "6段沿用同一洞形引导；瀑布、池岸、矿位仅为内容位置标记。",
      "3种左端口 × 3种右端口 × 3种形变；不是27张成品房间。",
      "真实选择段"+(m.at+1)+"；传播影响 "+new Set(m.changes.map(c=>c.at)).size+" 个邻段。",
      "WFC选初始轮廓；16型基础与12种既有坡形承接小格表达。",
      "6段均已确定、端口一致；最终占据允许与前两种不同。",
      data.dug.hybrid.changed.length+"格材料／坡形结果变化；6段宏观选择保持原值。"
    ][step];
  }
  function draw(){
    if(!data||!renderer)return;
    host.querySelectorAll("[data-method]").forEach(b=>b.setAttribute("aria-pressed",String(b.dataset.method===state.method)));
    get("stage").replaceChildren(...names[state.method].map((name,i)=>{
      const option=document.createElement("option");option.value=i;option.textContent=(i+1)+" / 6 · "+name;return option;
    }));
    get("stage").value=state.step;get("prev").disabled=state.step===0;get("next").disabled=state.step===5;
    get("phase").textContent=phases[state.method][state.step]+" · 种子271 · 算法说明白模";
    get("detail").textContent=detail();
    get("canvas").setAttribute("aria-label",names[state.method][state.step]+"。"+detail());
    renderer.render(data,state);
  }
  try{
    const hero=new Image();hero.src=window.SHAPE_STUDY_HERO;await hero.decode();
    data=await api.buildSteps(271);renderer=new api.StepRenderer(get("canvas"),null,host,hero);
    host.querySelectorAll("[data-method]").forEach(button=>button.addEventListener("click",()=>{
      state.method=button.dataset.method;draw();save();
    }));
    get("stage").addEventListener("change",event=>{state.step=Number(event.target.value);draw();save();});
    get("prev").addEventListener("click",()=>{state.step=Math.max(0,state.step-1);draw();save();});
    get("next").addEventListener("click",()=>{state.step=Math.min(5,state.step+1);draw();save();});
    new ResizeObserver(draw).observe(get("canvas"));
    new MutationObserver(draw).observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change",draw);
    window.addEventListener("openai:set_globals",event=>{
      const value=event.detail?.globals?.widgetState?.privateContent;
      if(valid(value)&&JSON.stringify(value)!==lastSent){state={...value};draw();}
    });
    ready=true;draw();host.dataset.ready="true";
    host._inspect=()=>({state:{...state},stage:names[state.method][state.step],detail:detail(),
      observation:data.trace.event.number,focus:data.focus,
      candidateCount:data.trace.event.candidates.length,changedDomains:new Set(data.trace.event.changes.map(c=>c.at)).size,
      tileIdentity:data.trace.map.tiles.every((v,i)=>v===data.before.wfc.tiles[i]),
      macroIdentity:data.macro.selected.every((v,i)=>v.id===data.pair.hybridPlan.macroLayout.selected[i].id),
      macroSelected:data.macro.selected.map(t=>t.id),digChanges:Object.fromEntries(["dual","wfc","hybrid"].map(k=>[k,data.dug[k].changed.length])),
      solverObservations:data.trace.stats.observations,ready});
    host._authoring={get data(){return data;},draw};
  }catch(error){
    host.dataset.error=error.message;get("error").hidden=false;get("error").textContent=error.message;
  }
})();
