(function(T) {
  "use strict";
  // Finite Wang catalogue: blank, ends, straights and elbows; no T or four-way crossing.
  const styles=[0,1,2,4,8,3,5,6,9,10,12], all=(1<<styles.length)-1;
  const dx=[0,1,0,-1],dy=[-1,0,1,0],bits=[1,2,4,8],opposite=[4,8,1,2];
  const compat=Array.from({length:4},()=>new Uint16Array(styles.length));
  for(let d=0;d<4;d++)for(let a=0;a<styles.length;a++)for(let b=0;b<styles.length;b++)
    if(!!(styles[a]&bits[d])===!!(styles[b]&opposite[d]))compat[d][a]|=1<<b;
  const count = x => {let n=0;for(;x;x&=x-1)n++;return n;};
  const each = (mask,fn) => {for(let j=0;j<styles.length;j++)if(mask&(1<<j))fn(j);};
  T.styles=styles;
  T.catalog=Array.from({length:16},(_,mask)=>styles.map((style,index)=>({
    id:mask*styles.length+index,mask,style,
    sockets:[mask&3,(mask>>1&1)|(mask>>2&1)<<1,(mask>>3&1)|(mask>>2&1)<<1,(mask&1)|(mask>>3&1)<<1]
  }))).flat();
  function solveGraph(nodes,masks,w,h,previous,seed,stats) {
    const n=nodes.length, lookup=new Map(nodes.map((key,i)=>[key,i]));
    const domains=new Uint16Array(n),neighbors=new Int32Array(n*4).fill(-1);
    for(let i=0;i<n;i++) {
      const key=nodes[i],x=key%w,y=(key/w)|0,active=masks[key]!==0&&masks[key]!==15;
      let allowed=active?all:1;
      for(let d=0;d<4;d++) {
        const nx=x+dx[d],ny=y+dy[d],k=ny*w+nx;
        const legal=nx>=0&&ny>=0&&nx<w&&ny<h;
        const other=legal?(lookup.get(k)??-1):-1;
        neighbors[i*4+d]=other;
        if(other>=0) {
          const a=T.catalog[masks[key]*styles.length].sockets[d],
            b=T.catalog[masks[k]*styles.length].sockets[(d+2)%4];
          if(a!==b){stats.geometryConflicts=(stats.geometryConflicts??0)+1;return null;}
        }
        if(other<0) {
          const outward=legal&&masks[k]!==0&&masks[k]!==15 ? !!(previous[k]&opposite[d]):false;
          each(allowed,j=>{if(!!(styles[j]&bits[d])!==outward)allowed&=~(1<<j);});
        }
      }
      domains[i]=allowed;
      if(!allowed)return null;
    }
    const trail=[],queue=[], queued=new Uint8Array(n);
    function enqueue(i) {if(!queued[i]){queued[i]=1;queue.push(i);}}
    function assign(i,value) {if(domains[i]!==value){trail.push([i,domains[i]]);domains[i]=value;enqueue(i);}}
    function propagate() {
      for(let head=0;head<queue.length;head++) {
        const i=queue[head];queued[i]=0;stats.propagations++;
        if(stats.propagations>180000){stats.budgetExceeded=true;return false;}
        for(let d=0;d<4;d++) {
          const k=neighbors[i*4+d];if(k<0)continue;
          let support=0;each(domains[i],j=>support|=compat[d][j]);
          const next=domains[k]&support;
          if(!next)return false;
          assign(k,next);
        }
      }
      queue.length=0;return true;
    }
    for(let i=0;i<n;i++)enqueue(i);
    if(!propagate())return null;
    const frames=[];
    function choose(i) {
      let candidates=[];
      each(domains[i],j=>candidates.push(j));
      candidates.sort((a,b)=>{
        const old=previous[nodes[i]],pa=styles[a]===old?-2:0,pb=styles[b]===old?-2:0;
        return pa-pb+(T.hash(nodes[i],a,seed)%1000-T.hash(nodes[i],b,seed)%1000)/1000;
      });
      // Keep existing variants when possible; new frontier receives actual stochastic choices.
      if(oldEmpty(i))candidates.sort((a,b)=>T.hash(nodes[i],a,seed)-T.hash(nodes[i],b,seed));
      return candidates;
    }
    function oldEmpty(i) {return previous[nodes[i]]===255;}
    let failed=false;
    for(;;) {
      let best=-1,min=100,rank=Infinity;
      if(!failed) {
        for(let i=0;i<n;i++) {
          const c=count(domains[i]),r=T.hash(nodes[i],0,seed);
          if(c>1&&(c<min||c===min&&r<rank)){best=i;min=c;rank=r;}
        }
        if(best<0) {
          const result=new Uint8Array(n);
          for(let i=0;i<n;i++)each(domains[i],j=>result[i]=styles[j]);
          return result;
        }
        frames.push({index:best,choices:choose(best),mark:trail.length});
      }
      let frame=frames.at(-1);
      while(frame&&!frame.choices.length) {
        while(trail.length>frame.mark){const [i,v]=trail.pop();domains[i]=v;}
        frames.pop();frame=frames.at(-1);stats.backtracks++;
      }
      if(!frame||stats.backtracks>80)return null;
      while(trail.length>frame.mark){const [i,v]=trail.pop();domains[i]=v;}
      queue.length=0;queued.fill(0);
      assign(frame.index,1<<frame.choices.shift());stats.observations++;
      failed=!propagate();
      if(failed)stats.contradictions++;
      if(stats.budgetExceeded)return null;
    }
  }
  T.solve = function(masks,w,h,previous,changed,mode,seed) {
    const stats={nodes:0,propagations:0,observations:0,backtracks:0,contradictions:0,
      expansions:0,fallback:false,budgetExceeded:false};
    const output=previous.slice(),cx=changed===null?0:changed%w,cy=changed===null?0:(changed/w)|0;
    const radii=changed===null?[Infinity]:[4,8,Infinity];
    let lastNodes=[];
    for(const radius of radii) {
      const nodes=[];
      for(let y=Math.max(0,cy-radius);y<=Math.min(h-1,cy+radius);y++)
        for(let x=Math.max(0,cx-radius);x<=Math.min(w-1,cx+radius);x++) {
          const key=y*w+x;
          if(mode==="whole"||masks[key]!==0&&masks[key]!==15)nodes.push(key);
        }
      // Infinity cannot be used as a loop origin; whole-map bounds are explicit.
      lastNodes=nodes;stats.nodes+=nodes.length;
      const answer=solveGraph(nodes,masks,w,h,previous,seed,stats);
      if(answer) {
        nodes.forEach((key,i)=>output[key]=answer[i]);
        return {values:output,stats,nodes:lastNodes};
      }
      stats.expansions++;
    }
    // The guaranteed blank catalogue member is an explicit, global appearance fallback.
    output.fill(0);stats.fallback=true;return {values:output,stats,nodes:lastNodes};
  };
  T.checkPorts = function(masks,values,w,h) {
    let errors=0;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++) {
      const i=y*w+x,v=values[i];
      if((masks[i]===0||masks[i]===15)&&v!==0)errors++;
      if(x+1<w&&!!(v&2)!==!!(values[i+1]&8))errors++;
      if(y+1<h&&!!(v&4)!==!!(values[i+w]&1))errors++;
      if((x===0&&v&8)||(x===w-1&&v&2)||(y===0&&v&1)||(y===h-1&&v&4))errors++;
    }
    return errors;
  };
  T.solveGraph=solveGraph;
})(globalThis.TerrainFour);
