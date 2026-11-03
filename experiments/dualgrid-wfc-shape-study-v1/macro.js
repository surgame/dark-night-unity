(function(root){
  "use strict";
  const api=root.ShapeStudy,rngFor=root.CaveWB.random;
  const ports=[-.5,0,.5],kinds=[
    {name:"缓坡",floor:0,roof:0,weight:1.6},
    {name:"斜肩",floor:-.45,roof:.15,weight:1},
    {name:"浅凹",floor:.55,roof:-.45,weight:1}
  ];
  const catalog=[];
  for(let left=0;left<3;left++)for(let right=0;right<3;right++)for(let kind=0;kind<3;kind++)
    catalog.push({id:catalog.length,left,right,kind,weight:kinds[kind].weight*(left===right?1.4:1)});
  function entropy(domain){
    let sum=0,logs=0;domain.forEach(id=>{const w=catalog[id].weight;sum+=w;logs+=w*Math.log(w);});
    return Math.log(sum)-logs/sum;
  }
  function solve(seed){
    const stats={observations:0,propagations:0,attempts:0};
    for(let attempt=0;attempt<4;attempt++){
      stats.attempts=attempt+1;
      const rng=rngFor(seed^Math.imul(attempt+1,0x5bd1e995));
      const domains=Array.from({length:6},(_,i)=>catalog.filter(t=>
        (i!==0||t.left===1)&&(i!==5||t.right===1)).map(t=>t.id));
      function propagate(){
        let changed=true;
        while(changed){
          changed=false;
          for(let i=0;i<6;i++)for(const direction of [-1,1]){
            const j=i+direction;if(j<0||j>=6)continue;
            const next=domains[j].filter(id=>domains[i].some(source=>direction>0
              ?catalog[source].right===catalog[id].left:catalog[source].left===catalog[id].right));
            stats.propagations++;if(!next.length)return false;
            if(next.length!==domains[j].length){domains[j]=next;changed=true;}
          }
        }
        return true;
      }
      if(!propagate())continue;
      let failed=false;
      while(domains.some(d=>d.length>1)){
        let at=-1,best=Infinity;
        domains.forEach((d,i)=>{if(d.length>1){const h=entropy(d);if(h<best){best=h;at=i;}}});
        const d=domains[at];let roll=rng()*d.reduce((n,id)=>n+catalog[id].weight,0),chosen=d.at(-1);
        for(const id of d){roll-=catalog[id].weight;if(roll<=0){chosen=id;break;}}
        domains[at]=[chosen];stats.observations++;
        if(!propagate()){failed=true;break;}
      }
      if(!failed){
        const selected=domains.map(d=>catalog[d[0]]);
        return{selected,stats,catalogSize:catalog.length,
          mismatches:selected.slice(1).filter((t,i)=>t.left!==selected[i].right).length};
      }
    }
    throw new Error("Macro WFC contradiction after four bounded attempts.");
  }
  function makePlan(base){
    const layout=solve(base.seed),width=api.W+1,height=api.H+1;
    function offset(x,y){
      const i=Math.max(0,Math.min(5,Math.floor(x/8))),t=Math.max(0,Math.min(1,(x-i*8)/8));
      const tile=layout.selected[i],kind=kinds[tile.kind];
      const common=ports[tile.left]*(1-t)+ports[tile.right]*t;
      const blend=Math.sin(Math.PI*t)**2;
      const lower=base.floor?base.floor:[],upper=base.roof?base.roof:[];
      const sample=points=>{
        for(let j=1;j<points.length;j++)if(x<=points[j][0]){
          const a=points[j-1],b=points[j];return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);
        }
        return points.at(-1)?.[1]||14;
      };
      const roof=sample(upper),floor=sample(lower);
      const relative=base.floor?Math.max(0,Math.min(1,(y-roof)/Math.max(1,floor-roof))):.5;
      return common+blend*(kind.roof*(1-relative)+kind.floor*relative);
    }
    const signedDistance=(x,y)=>base.signedDistance(x,y-offset(x,y));
    const target=new Float32Array(width*height),allowed=new Uint8Array(target.length);
    for(let y=0;y<height;y++)for(let x=0;x<width;x++){
      const d=signedDistance(x,y),i=y*width+x;target[i]=d>=0?1:-1;allowed[i]=d>=0?4:2;
    }
    return{...base,target,allowed,signedDistance,macroLayout:layout};
  }
  Object.assign(api,{makeMacroPlan:makePlan,solveMacro:solve,macroKinds:kinds});
})(globalThis);
