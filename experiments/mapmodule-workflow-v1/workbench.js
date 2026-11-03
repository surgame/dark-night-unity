const design="mapmodule-workbench-ui-v1",states={},products=[];
const initial=mapModuleReadState(design);
function safe(saved) {
  const state={theme:"entrance",variant:"lowpass",second:"lowpass",task:"assembly",scope:"fragment",ecology:true,bounds:true,collision:false,dirty:false,phase:"source",version:0};
  if(!saved)return state;
  for(const key of ["variant","second"])if(mmSchemes.some(s=>s.id===saved[key]))state[key]=saved[key];
  for(const [key,allowed] of Object.entries({theme:["entrance","fragment","map"],task:["module","assembly","run"],scope:["single","fragment","map"],phase:["source","checked","conflict","baked"]}))if(allowed.includes(saved[key]))state[key]=saved[key];
  for(const key of ["ecology","bounds","collision","dirty"])if(typeof saved[key]==="boolean")state[key]=saved[key];
  state.version=Number.isInteger(saved.version)?Math.max(0,Math.min(saved.version,99)):0;
  if(state.variant!==state.second&&state.phase!=="source")state.phase="conflict";
  if(state.dirty&&state.phase==="baked")state.phase="source";
  return state;
}
root.querySelectorAll(".mm-product").forEach((product,index)=>{
  const layout=product.dataset.layout;
  product.append(root.querySelector("template").content.cloneNode(true));
  const state=states[layout]=safe(initial[layout]);
  const fields=key=>product.querySelector(`[data-field="${key}"]`);
  for(const key of ["variant","second"])fields(key).innerHTML=mmSchemes.map(s=>`<option value="${s.id}">${s.name}</option>`).join("");
  product.querySelector("[data-library-title]").textContent=layout==="study"?"入口研究样例":"入口模块库";
  product.querySelector("[data-library]").innerHTML=mmSchemes.map(s=>`<button type="button" class="cursor-interaction" data-case="${s.id}" aria-pressed="false"><svg class="mm-thumb" viewBox="0 0 6.4 3.2" aria-hidden="true"><path class="mm-rock" d="${s.floorSolid}"/><path class="mm-rock" d="${s.ceilingSolid}"/></svg><span>${s.name}<small>${layout==="study"?"入口＋第二片":"入口 · 2×1"}</small></span></button>`).join("");
  if(root.clientWidth<640)product.querySelectorAll("[data-fold]").forEach(el=>el.open=false);
  let message="设计稿：可切换样例、尝试错配第二片，查看检查与试跑状态。";
  function render() {
    const scheme=mmSchemes.find(s=>s.id===state.variant);
    fields("theme").value=state.theme;fields("variant").value=state.variant;fields("second").value=state.second;
    fields("variant").disabled=state.theme!=="entrance";
    fields("second").disabled=state.theme!=="entrance";
    product.querySelectorAll("[data-task]").forEach(el=>el.setAttribute("aria-pressed",el.dataset.task===state.task));
    product.querySelectorAll("[data-scope]").forEach(el=>el.setAttribute("aria-pressed",el.dataset.scope===state.scope));
    product.querySelectorAll("[data-case]").forEach(el=>{el.setAttribute("aria-pressed",el.dataset.case===state.variant);el.disabled=state.theme!=="entrance";});
    product.querySelectorAll("[data-toggle]").forEach(el=>el.checked=state[el.dataset.toggle]);
    product.querySelector("[data-selection-name]").textContent="入口 · "+scheme.name;
    product.querySelector("[data-scene-label]").textContent=state.theme==="entrance"?`入口片段 · ${scheme.name}`:state.theme==="map"?"完整地图研究":"其他片段研究";
    product.querySelector("[data-port]").textContent=`地面 ${scheme.port.floor.toFixed(2)} / 顶 ${scheme.port.ceiling.toFixed(2)}`;
    product.querySelector("[data-check-state]").textContent={source:"未检查",checked:"配对示意匹配",conflict:"接口不匹配",baked:"已检查示意"}[state.phase];
    product.querySelector("[data-check-state]").classList.toggle("mm-error",state.phase==="conflict");
    product.querySelector("[data-bake-state]").textContent=state.phase==="baked"?`草稿版本 ${state.version}`:"未准备／已失效";
    product.querySelector("[data-source-state]").textContent=state.dirty?"作者源已修改":state.phase==="baked"?"当前源与演示版本一致":"作者源已保存";
    product.querySelector('[data-action="check"]').disabled=state.theme!=="entrance"||state.scope==="map";
    product.querySelector('[data-action="bake"]').disabled=state.phase!=="checked"||state.dirty||state.theme!=="entrance"||state.scope==="map";
    product.querySelector('[data-action="run"]').disabled=state.phase!=="baked"||state.dirty||state.theme!=="entrance"||state.scope==="map";
    product.querySelector("[data-notice]").textContent=message;
    const canvas=product.querySelector("[data-canvas]");
    if(state.theme!=="entrance"||state.scope==="map")mapModuleEmpty(canvas,"尚无整图或其他片段样例","新增原生作者Scene后，沿同一流程检查和试跑。");
    else {
      if(!canvas.querySelector("svg"))canvas.innerHTML='<svg class="mm-scene" role="img" aria-label="当前作者场景草图"></svg><span class="mm-overlay-note">作者场景 · 正侧视草案</span>';
      mapModuleScene(canvas.querySelector("svg"),state,"mm-workbench-"+index);
    }
    product.dataset.state=JSON.stringify(state);
  }
  function changed(){render();mapModuleRemember(root,design,states);}
  function choose(value){state.variant=value;state.second=value;state.theme="entrance";state.phase="source";state.dirty=false;message="已切换样例；对比镜头与主角尺度保持。";changed();}
  product.querySelectorAll("[data-case]").forEach(el=>el.addEventListener("click",()=>choose(el.dataset.case)));
  fields("variant").addEventListener("change",()=>choose(fields("variant").value));
  fields("second").addEventListener("change",()=>{state.second=fields("second").value;state.phase="source";state.dirty=true;message="拼装源已修改，旧检查与试跑版本失效。";changed();});
  fields("theme").addEventListener("change",()=>{state.theme=fields("theme").value;state.phase="source";message="主题只选择作者来源，不调用旧空间生成器。";changed();});
  product.querySelectorAll("[data-task]").forEach(el=>el.addEventListener("click",()=>{
    state.task=el.dataset.task;if(state.task==="module")state.scope="single";else state.scope="fragment";
    message=state.task==="run"?"检查当前作者源，准备固定版本后交给试验场。":state.task==="module"?"模块内部归Prefab；拼装位置归作者Scene。":"连接、出生点与镜头归当前作者Scene。";changed();
  }));
  product.querySelectorAll("[data-scope]").forEach(el=>el.addEventListener("click",()=>{state.scope=el.dataset.scope;changed();}));
  product.querySelectorAll("[data-toggle]").forEach(el=>el.addEventListener("change",()=>{state[el.dataset.toggle]=el.checked;message="仅切换显示，作者源与制作版本保持。";changed();}));
  product.querySelector('[data-action="camera"]').addEventListener("click",()=>{state.scope=state.scope==="single"?"fragment":"single";message="在同尺度方案之间保留当前观察范围。";changed();});
  product.querySelector('[data-action="save"]').addEventListener("click",()=>{state.dirty=false;message="设计稿演示：保存作者源；不会自动检查或烘焙。";changed();});
  product.querySelector('[data-action="check"]').addEventListener("click",()=>{
    const next=mmSchemes.find(s=>s.id===state.second);
    const match=Math.abs(schemePort().floor-next.port.floor)<.001&&Math.abs(schemePort().ceiling-next.port.ceiling)<.001;
    state.phase=match?"checked":"conflict";
    message=match?"示例端口配对匹配；原生形状、资源和实际绘制仍待Unity接入。":`接口不匹配：入口地面${schemePort().floor.toFixed(2)}，后段地面${next.port.floor.toFixed(2)}；检查开口范围。`;
    changed();
  });
  function schemePort(){return mmSchemes.find(s=>s.id===state.variant).port;}
  product.querySelector('[data-action="bake"]').addEventListener("click",()=>{state.phase="baked";state.version++;message="设计稿演示：生成固定试跑版本；作者源再次修改即失效。";changed();});
  product.querySelector('[data-action="run"]').addEventListener("click",()=>{message=`设计稿演示：将“${mmSchemes.find(s=>s.id===state.variant).name}”版本${state.version}交给地图试验场，以本机Host启动。`;changed();});
  const observer=new ResizeObserver(()=>render());observer.observe(product);
  render();products.push({product,state,render});
  if(globalThis.Tweak) {
    const settings={library:174};
    const tweak=new Tweak({container:product,onChange:()=>{product.style.setProperty("--mm-lib",settings.library+"px");render();}});
    tweak.addSlider(settings,"library",{label:"目录宽度",min:150,max:220,step:2,unit:"px"});
  }
});
window.addEventListener("openai:set_globals",event=>{
  const incoming=event.detail&&event.detail.globals&&event.detail.globals.widgetState;
  if(!incoming||incoming.design!==design||!incoming.layouts)return;
  for(const item of products){Object.assign(item.state,safe(incoming.layouts[item.product.dataset.layout]));item.render();}
});
