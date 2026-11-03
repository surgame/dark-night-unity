(function(T) {
  "use strict";
  T.pressureProfiles=["tunnel","scatter","bursts","holes"];
  T.pressureLabels={tunnel:"连续开挖",scatter:"分散敲击",bursts:"四处交错开挖",holes:"密集打孔"};
  T.pressurePolicies=["immediate","merged"];
  T.policyLabels={immediate:"每格重建",merged:"批末合并重建"};
  T.pressureLoads=[16,64,256,1024];
  function pool(capacity) {
    const values=[],positions=new Int32Array(capacity).fill(-1);
    return {values,add(i){if(positions[i]<0){positions[i]=values.length;values.push(i);}},
      remove(i){const at=positions[i];if(at<0)return;const last=values.pop();
        if(at<values.length){values[at]=last;positions[last]=at;}positions[i]=-1;}};
  }
  T.pressureEdits=function(field,count,kind,seed=271) {
    const f=T.clone(field),stride=f.w+1,solid=pool(f.data.length),frontier=pool(f.data.length),result=[];
    const legal=i=>i%stride>0&&i%stride<f.w&&i>stride&&i<f.h*stride;
    const neighbors=i=>[i-1,i+1,i-stride,i+stride];
    for(let y=1;y<f.h;y++)for(let x=1;x<f.w;x++) {
      const i=y*stride+x;if(!f.data[i])continue;
      solid.add(i);if(neighbors(i).some(j=>!f.data[j]))frontier.add(i);
    }
    const nearest=(x,y)=>{
      let best=-1,distance=Infinity;
      for(const i of frontier.values) {
        const d=(i%stride-x)**2+(((i/stride)|0)-y)**2;
        if(d<distance){best=i;distance=d;}
      }
      return best;
    };
    const anchors=[[.18,.6],[.4,.3],[.7,.6],[.85,.4]].map(p=>nearest(p[0]*f.w,p[1]*f.h));
    for(let n=0;n<count;n++) {
      let pick=-1;
      if(kind==="holes"&&solid.values.length) {
        for(let j=0;j<32;j++) {
          pick=solid.values[T.hash(n,j,seed)%solid.values.length];
          if(neighbors(pick).every(i=>f.data[i]))break;
        }
      }else if(kind==="scatter"||kind==="tunnel"&&!result.length) {
        if(frontier.values.length)pick=frontier.values[T.hash(n,0,seed)%frontier.values.length];
      }else {
        const anchor=kind==="bursts"?anchors[n%4]:result.at(-1);
        if(anchor>=0)pick=nearest(anchor%stride,(anchor/stride)|0);
      }
      if(pick<0||!f.data[pick])throw Error("压力序列不足，未将无效空格计作破坏。");
      f.data[pick]=0;solid.remove(pick);frontier.remove(pick);
      for(const i of neighbors(pick))if(legal(i)&&f.data[i])frontier.add(i);
      if(kind==="bursts")anchors[n%4]=pick;
      result.push(pick);
    }
    return result;
  };
  T.forkAdapter=function(base) {
    const a={...base,field:T.clone(base.field),masks:base.masks.slice(),variants:base.variants.slice(),
      meshes:new Map(base.meshes),last:null};
    if(base.candidateIds)a.candidateIds=base.candidateIds.slice();
    return a;
  };
  T.pressureBatch=function(a,edits,policy) {
    const dirty=new Set(),merged=policy==="merged";
    const stats={edits:edits.length,rebuilds:0,nodes:0,propagations:0,backtracks:0,expansions:0,fallbacks:0};
    for(const index of edits) {
      const r=T.breakCell(a,index,{deferGeometry:merged});
      if(!r)throw Error("压力输入包含重复或无效破坏。");
      for(const key of r.chunks)dirty.add(key);
      stats.rebuilds+=r.chunks.length;stats.nodes+=r.nodes;stats.propagations+=r.propagations;
      stats.backtracks+=r.backtracks;stats.expansions+=r.expansions;stats.fallbacks+=Number(r.fallback);
    }
    if(merged) {
      for(const key of dirty)a.meshes.set(key,T.mesh(a,key));
      stats.rebuilds=dirty.size;
    }
    return {...stats,chunks:[...dirty]};
  };
  T.pressureCheck=function(a) {
    const ports=T.checkPorts(a.masks,a.variants,a.w,a.h),
      geometry=T.signature(a.masks),expected=T.signature(T.masks(a.field));
    if(ports||geometry!==expected)throw Error("压力更新后几何／纹理端口失效。");
    return {geometry,variant:T.signature(a.variants),ports};
  };
})(globalThis.TerrainFour);
