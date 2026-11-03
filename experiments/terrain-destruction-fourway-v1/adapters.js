(function(T) {
  "use strict";
  T.methods=["dual","whole","hybrid","edge"];
  T.names={dual:"Dual Grid",whole:"整块候选 WFC",hybrid:"Dual Grid＋WFC",edge:"边缘变形网格"};
  T.descriptions={
    dual:"四角直接查 16 型；局部换片，8×8 格合批",
    whole:"16 形状×11 纹理端口候选；在局部矩形里求解整块记录",
    hybrid:"四角定形；只在局部边缘格上求解同一组 11 种端口",
    edge:"提取共边曲线；重建分段厚边网格，岩体内部合并整行"
  };
  T.adapter = function(field,method) {
    const t=performance.now(),a={method,w:field.w,h:field.h,field:T.clone(field),
      masks:T.masks(field),variants:new Uint8Array(field.w*field.h),meshes:new Map(),last:null};
    const initial={select:0,geometry:0,nodes:0,propagations:0,observations:0,backtracks:0,expansions:0,fallback:false};
    if(method==="whole"||method==="hybrid") {
      for(let i=0;i<a.variants.length;i++)if(a.masks[i]!==0&&a.masks[i]!==15)a.variants[i]=255;
      const answer=T.solve(a.masks,a.w,a.h,a.variants,null,method,field.seed);
      a.variants=answer.values;Object.assign(initial,answer.stats);
    }
    if(method==="whole") {
      a.candidateIds=Uint16Array.from(a.masks,(mask,i)=>mask*T.styles.length+T.styles.indexOf(a.variants[i]));
      for(let i=0;i<a.masks.length;i++)a.masks[i]=T.catalog[a.candidateIds[i]].mask;
    }
    initial.select=performance.now()-t;
    const meshStart=performance.now();
    const count=Math.ceil(a.w/T.chunkSize)*Math.ceil(a.h/T.chunkSize);
    for(let key=0;key<count;key++)a.meshes.set(key,T.mesh(a,key));
    initial.geometry=performance.now()-meshStart;a.initial=initial;
    return a;
  };
  T.breakCell = function(a,index,options={}) {
    if(index<0||index>=a.field.data.length||!a.field.data[index])return null;
    const start=performance.now(),patches=T.affected(a.field,index),old=a.variants;
    const isWfc=a.method==="whole"||a.method==="hybrid";
    a.field.data[index]=0;
    for(const key of patches) {
      a.masks[key]=T.maskAt(a.field,key%a.w,(key/a.w)|0);
      if(isWfc) {
        if(a.masks[key]===0||a.masks[key]===15)a.variants[key]=0;
        else if(old[key]===0)a.variants[key]=255;
      }
    }
    let stats={nodes:0,propagations:0,observations:0,backtracks:0,contradictions:0,
      expansions:0,fallback:false},nodes=[];
    if(isWfc) {
      const answer=T.solve(a.masks,a.w,a.h,a.variants,patches[0]??0,a.method,
        T.hash(index,0,a.field.seed));
      a.variants=answer.values;stats=answer.stats;nodes=answer.nodes;
      if(a.method==="whole")for(const i of stats.fallback?a.masks.keys():nodes) {
        const candidate=T.catalog[a.masks[i]*T.styles.length+T.styles.indexOf(a.variants[i])];
        a.candidateIds[i]=candidate.id;a.masks[i]=candidate.mask;
      }
    }
    const changed=new Set(patches);
    const candidates=stats.fallback?a.variants.keys():nodes;
    for(const i of candidates)if(old[i]!==a.variants[i])changed.add(i);
    const chunks=new Set();
    for(const key of changed)chunks.add(T.chunkKey(key%a.w,(key/a.w)|0,a.w));
    // Neighbor chunks share end tangents. Invalidate a one-patch halo for warped borders.
    if(a.method==="edge")for(const key of patches) {
      const x=key%a.w,y=(key/a.w)|0;
      for(let oy=-1;oy<=1;oy++)for(let ox=-1;ox<=1;ox++) {
        if(x+ox>=0&&x+ox<a.w&&y+oy>=0&&y+oy<a.h)
          chunks.add(T.chunkKey(x+ox,y+oy,a.w));
      }
    }
    const select=performance.now()-start,meshStart=performance.now();
    if(!options.deferGeometry)for(const key of chunks)a.meshes.set(key,T.mesh(a,key));
    const result={index,patches,changed:[...changed],chunks:[...chunks],solveKeys:nodes,
      select,geometry:performance.now()-meshStart,...stats};
    a.last=result;return result;
  };
  T.payload = function(a) {
    let vertices=0,bytes=0,triangles=0,draws=0;
    for(const m of a.meshes.values()){vertices+=m.vertices;bytes+=m.data.byteLength;
      triangles+=m.triangles;if(m.vertices)draws++;}
    return {vertices,triangles,bytes,draws,tiles:a.masks.reduce((n,m)=>n+(m!==0),0)};
  };
  T.workload = function(field,count=48,kind="scatter") {
    const f=T.clone(field),result=[];
    for(let n=0;n<count;n++) {
      const candidates=[];
      for(let y=1;y<f.h;y++)for(let x=1;x<f.w;x++) {
        const index=y*(f.w+1)+x;if(!f.data[index])continue;
        const neighbors=[index-1,index+1,index-f.w-1,index+f.w+1];
        if(neighbors.some(i=>!f.data[i]))candidates.push(index);
      }
      if(!candidates.length)break;
      let pick;
      if(kind==="tunnel"&&result.length) {
        const last=result.at(-1),lx=last%(f.w+1),ly=(last/(f.w+1))|0;
        candidates.sort((a,b)=>{
          const dist=i=>Math.abs(i%(f.w+1)-lx)+Math.abs(((i/(f.w+1))|0)-ly);
          return dist(a)-dist(b)||T.hash(a,n,271)-T.hash(b,n,271);
        });pick=candidates[0];
      }else pick=candidates[T.hash(n,field.seed)%candidates.length];
      f.data[pick]=0;result.push(pick);
    }
    return result;
  };
})(globalThis.TerrainFour);
