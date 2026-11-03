const design="mapmodule-lab-ui-v1",states={},products=[];
const initial=mapModuleReadState(design);
function safe(saved) {
  const state={variant:"lowpass",role:"host",scope:"fragment",bounds:false,collision:false,ecology:true,running:false,paused:false,address:"127.0.0.1"};
  if(!saved)return state;
  if(mmSchemes.some(s=>s.id===saved.variant))state.variant=saved.variant;
  if(["host","client"].includes(saved.role))state.role=saved.role;
  if(["single","fragment","map"].includes(saved.scope))state.scope=saved.scope;
  for(const key of ["bounds","collision","ecology"])if(typeof saved[key]==="boolean")state[key]=saved[key];
  return state;
}
root.querySelectorAll(".mm-product").forEach((product,index)=>{
  const layout=product.dataset.layout;
  product.append(root.querySelector("template").content.cloneNode(true));
  const state=states[layout]=safe(initial[layout]);
  const fields=key=>product.querySelector(`[data-field="${key}"]`);
  fields("variant").innerHTML=mmSchemes.map(s=>`<option value="${s.id}">${s.name}</option>`).join("");
  product.querySelector("[data-diagnostic-fold]").open=layout==="network"&&root.clientWidth>=640;
  let message="设计稿：试跑角色、版本锁定和权限操作可交互查看。";
  function render() {
    const scheme=mmSchemes.find(s=>s.id===state.variant);
    fields("variant").value=state.variant;fields("role").value=state.role;
    fields("variant").disabled=state.running;fields("role").disabled=state.running;
    product.querySelector("[data-address]").hidden=state.role!=="client";
    fields("address").disabled=state.running;
    product.querySelector("[data-scene-label]").textContent=scheme.name+" · 入口＋第二片";
    product.querySelector("[data-version]").textContent="固定版本 · "+scheme.name+"草案";
    product.querySelector("[data-case-id]").textContent=scheme.name+" / "+scheme.id;
    product.querySelector("[data-permission]").textContent=state.role==="host"?"房主请求验证":"只读／本地观察";
    product.querySelectorAll("[data-scope]").forEach(el=>el.setAttribute("aria-pressed",el.dataset.scope===state.scope));
    product.querySelectorAll("[data-toggle]").forEach(el=>el.checked=state[el.dataset.toggle]);
    product.querySelector('[data-action="start"]').disabled=state.running;
    product.querySelector('[data-action="start"]').textContent=state.role==="host"?"启动本机房主":"加入指定房主";
    product.querySelector('[data-action="pause"]').disabled=!state.running||state.role!=="host";
    product.querySelector('[data-action="pause"]').textContent=state.paused?"继续":"暂停";
    product.querySelector('[data-action="restart"]').disabled=!state.running||state.role!=="host";
    product.querySelector('[data-action="leave"]').disabled=!state.running;
    product.querySelector("[data-running-state]").textContent=state.running?(state.paused?"暂停状态示意":"运行布局示意"):"未运行";
    product.querySelector("[data-role-info]").textContent=state.role==="host"?"房主请求验证暂停与重开；运行固定当前样例和制作版本。":"客户端可调整本地观察和退出；暂停／重开由房主验证执行。";
    product.querySelector("[data-notice]").textContent=message;
    mapModuleScene(product.querySelector("svg"),{...state,second:state.variant,lab:true},"mm-lab-"+index);
    product.dataset.state=JSON.stringify(state);
  }
  function persist() {
    const kept={};
    for(const [key,value] of Object.entries(states)){const {running,paused,address,...rest}=value;kept[key]=rest;}
    mapModuleRemember(root,design,kept);
  }
  function changed(){render();persist();}
  fields("variant").addEventListener("change",()=>{state.variant=fields("variant").value;message="已选择固定样例，开始后锁定版本。";changed();});
  fields("role").addEventListener("change",()=>{state.role=fields("role").value;message=state.role==="client"?"客户端加入同版房主；控制按钮保留服务端权限限制。":"本机试玩使用既有FishNet Host与Stateful会话。";changed();});
  fields("address").addEventListener("input",()=>{state.address=fields("address").value;});
  product.querySelectorAll("[data-scope]").forEach(el=>el.addEventListener("click",()=>{state.scope=el.dataset.scope;message=state.scope==="map"?"当前样例只有两片；加载完整地图后，整图取景沿同一操作。":"只改变本端镜头，运行版本保持。";changed();}));
  product.querySelectorAll("[data-toggle]").forEach(el=>el.addEventListener("change",()=>{state[el.dataset.toggle]=el.checked;message="仅改变本端叠层显示，不修改地形或业务状态。";changed();}));
  product.querySelector('[data-action="start"]').addEventListener("click",()=>{state.running=true;state.paused=false;message="设计稿演示：样例与身份已锁定；真实连接和Ready回执尚未接入。";changed();});
  product.querySelector('[data-action="pause"]').addEventListener("click",()=>{if(state.role!=="host")return;state.paused=!state.paused;message=state.paused?"设计稿演示：房主暂停业务，网络与UI继续。":"设计稿演示：恢复同一权威会话。";changed();});
  product.querySelector('[data-action="restart"]').addEventListener("click",()=>{if(state.role!=="host")return;state.paused=false;message="设计稿演示：结束旧试跑，从同一固定版本重新开始；无真实网络操作。";changed();});
  product.querySelector('[data-action="leave"]').addEventListener("click",()=>{state.running=false;state.paused=false;message="设计稿演示：退出后释放连接、订阅和资源，再选择新样例。";changed();});
  const observer=new ResizeObserver(()=>render());observer.observe(product);
  render();products.push({product,state,render});
  if(globalThis.Tweak) {
    const settings={diagnostics:250};
    const tweak=new Tweak({container:product,onChange:()=>{product.style.setProperty("--mm-diag",settings.diagnostics+"px");render();}});
    tweak.addSlider(settings,"diagnostics",{label:"诊断栏宽度",min:210,max:290,step:4,unit:"px"});
  }
});
window.addEventListener("openai:set_globals",event=>{
  const incoming=event.detail&&event.detail.globals&&event.detail.globals.widgetState;
  if(!incoming||incoming.design!==design||!incoming.layouts)return;
  for(const item of products){Object.assign(item.state,safe(incoming.layouts[item.product.dataset.layout]));item.render();}
});
