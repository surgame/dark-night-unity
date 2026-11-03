(function(root){
  "use strict";
  const lib=root.CaveWB,api=root.ShapeStudy;
  const directions=[[0,-1],[1,0],[0,1],[-1,0]];
  const entropy=d=>{
    if(d.options.length<2)return 0;
    let sum=0,logs=0;
    for(const id of d.options){const w=d.weights[id];sum+=w;logs+=w*Math.log(w);}
    return Math.log(sum)-logs/sum;
  };
  const snapshot=domains=>domains.map(d=>d.options.slice());
  function propagate(domains,plan,queue,stats,changes){
    let cursor=0;
    while(cursor<queue.length){
      const at=queue[cursor++],source=domains[at];source.queued=false;
      const x=at%plan.width,y=Math.floor(at/plan.width);
      for(let direction=0;direction<4;direction++){
        const nx=x+directions[direction][0],ny=y+directions[direction][1];
        if(nx<0||ny<0||nx>=plan.width||ny>=plan.height)continue;
        const index=ny*plan.width+nx,neighbor=domains[index];
        let edges=0;for(const id of source.options)edges|=1<<lib.catalog[id].edges[direction];
        const next=neighbor.options.filter(id=>edges&(1<<lib.catalog[id].edges[(direction+2)%4]));
        stats.propagations++;if(!next.length)return false;
        if(next.length===neighbor.options.length)continue;
        changes?.push({source:at,at:index,before:neighbor.options.slice(),after:next.slice(),direction});
        stats.removed+=neighbor.options.length-next.length;
        neighbor.options=next;neighbor.entropy=entropy(neighbor);
        if(!neighbor.queued){neighbor.queued=true;queue.push(index);}
      }
    }
    return true;
  }
  async function solveTraced(plan,options={}){
    const stats={observations:0,propagations:0,removed:0,attempts:0};
    for(let attempt=0;attempt<4;attempt++){
      stats.attempts=attempt+1;
      const rng=lib.random(plan.seed^Math.imul(attempt+1,0x9E3779B1)),domains=lib.makeDomains(plan);
      if(domains.some(d=>!d.options.length))continue;
      const initial=snapshot(domains);
      domains.forEach(d=>{d.entropy=entropy(d);d.queued=true;});
      if(!propagate(domains,plan,domains.map((_,i)=>i),stats))continue;
      const timeline=options.timeline?{start:snapshot(domains),events:[]}:null;
      let event=null,failed=false;
      while(true){
        let best=-1,minimum=Infinity;
        domains.forEach((d,i)=>{
          if(d.options.length<2)return;
          const value=d.entropy+((i*.61803398875)%1)*.000001;
          if(value<minimum){minimum=value;best=i;}
        });
        if(best<0){
          const map={width:plan.width,height:plan.height,seed:plan.seed,plan,
            tiles:Uint16Array.from(domains.map(d=>d.options[0])),stats};
          map.check=lib.verify(map);map.geometry=lib.extractContours(map);
          if(map.check.mismatches||map.check.cornerConflicts||map.geometry.nonManifold)
            throw new Error("Trace solve violated shared boundaries.");
          if(!event)throw new Error("No boundary observation was available to explain.");
          return {map,initial,event,stats,timeline};
        }
        const domain=domains[best],before=event?null:snapshot(domains),candidates=domain.options.slice();
        let roll=rng()*candidates.reduce((n,id)=>n+domain.weights[id],0),chosen=candidates.at(-1);
        for(const id of candidates){roll-=domain.weights[id];if(roll<=0){chosen=id;break;}}
        const selectedEntropy=domain.entropy;
        domain.options=[chosen];domain.entropy=0;domain.queued=true;stats.observations++;
        const observed=event?null:snapshot(domains),changes=[];
        if(!propagate(domains,plan,[best],stats,changes)){failed=true;break;}
        timeline?.events.push({at:best,chosen,changes});
        const mask=lib.catalog[chosen].mask;
        if(!event&&mask>0&&mask<15&&changes.length){
          event={at:best,chosen,candidates,entropy:selectedEntropy,priority:minimum,
            before,observed,after:snapshot(domains),changes,number:stats.observations,
            entropies:before.map((options,i)=>entropy({...domains[i],options}))};
        }
      }
      if(!failed)break;
    }
    throw new Error("Trace WFC contradiction after four attempts.");
  }
  const ports=[-.5,0,.5],catalog=[];
  for(let left=0;left<3;left++)for(let right=0;right<3;right++)for(let kind=0;kind<3;kind++)
    catalog.push({id:catalog.length,left,right,kind,
      weight:api.macroKinds[kind].weight*(left===right?1.4:1)});
  function macroEntropy(d){
    let sum=0,logs=0;
    for(const id of d){const w=catalog[id].weight;sum+=w;logs+=w*Math.log(w);}
    return Math.log(sum)-logs/sum;
  }
  function traceMacro(seed){
    const stats={observations:0,propagations:0,attempts:0};
    for(let attempt=0;attempt<4;attempt++){
      stats.attempts=attempt+1;
      const rng=lib.random(seed^Math.imul(attempt+1,0x5bd1e995));
      const domains=Array.from({length:6},(_,i)=>catalog.filter(t=>
        (i!==0||t.left===1)&&(i!==5||t.right===1)).map(t=>t.id));
      const initial=domains.map(d=>d.slice());
      function propagate(changes){
        let changed=true;
        while(changed){
          changed=false;
          for(let i=0;i<6;i++)for(const direction of [-1,1]){
            const j=i+direction;if(j<0||j>=6)continue;
            const next=domains[j].filter(id=>domains[i].some(source=>direction>0
              ?catalog[source].right===catalog[id].left:catalog[source].left===catalog[id].right));
            stats.propagations++;if(!next.length)return false;
            if(next.length!==domains[j].length){
              changes?.push({source:i,at:j,before:domains[j].slice(),after:next.slice()});
              domains[j]=next;changed=true;
            }
          }
        }
        return true;
      }
      if(!propagate())continue;
      const events=[];let failed=false;
      while(domains.some(d=>d.length>1)){
        let at=-1,best=Infinity;
        domains.forEach((d,i)=>{if(d.length>1){const h=macroEntropy(d);if(h<best){best=h;at=i;}}});
        const before=domains.map(d=>d.slice()),d=domains[at];
        let roll=rng()*d.reduce((n,id)=>n+catalog[id].weight,0),chosen=d.at(-1);
        for(const id of d){roll-=catalog[id].weight;if(roll<=0){chosen=id;break;}}
        domains[at]=[chosen];stats.observations++;
        const observed=domains.map(d=>d.slice()),changes=[];
        if(!propagate(changes)){failed=true;break;}
        events.push({at,chosen,entropy:best,before,observed,after:domains.map(d=>d.slice()),changes});
      }
      if(!failed)return{initial,events,selected:domains.map(d=>catalog[d[0]]),stats,catalog,ports};
    }
    throw new Error("Macro trace contradiction.");
  }
  async function buildSteps(seed=271,options={}){
    const pair=await api.generate(seed,"basin"),trace=await solveTraced(pair.plan,options),macro=traceMacro(seed);
    if(trace.map.tiles.some((v,i)=>v!==pair.wfc.tiles[i]))throw new Error("Traced WFC diverged from original.");
    if(macro.selected.some((v,i)=>v.id!==pair.hybridPlan.macroLayout.selected[i].id))
      throw new Error("Traced macro WFC diverged from original.");
    const focus=trace.event.at,dualCandidates=[api.toMask(lib.catalog[pair.dual.tiles[focus]].mask)];
    const before={dual:pair.dual,wfc:pair.wfc,hybrid:pair.hybrid};
    const base=pair.hybridBase;
    api.dig(pair,true);
    const dug={dual:pair.dual,wfc:pair.wfc,hybrid:pair.hybrid};
    const digPoint={...pair.digPoint};
    api.dig(pair,false);
    const slopeAt=Array.from(pair.hybrid.flags).findIndex((f,i)=>f>0&&i%49>12&&i%49<25&&Math.floor(i/49)>12);
    return {pair,trace,macro,focus,dualCandidates,before,dug,digPoint,base,slopeAt};
  }
  Object.assign(api,{buildSteps,solveTraced,traceMacro});
})(globalThis);
