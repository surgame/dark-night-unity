(function(root){
  "use strict";
  const M=root.TerrainBench,MODES=["baked","strip","atlas"];
  const sleep=()=>new Promise(r=>setTimeout(r,0));
  const summary=a=>{
    const s=[...a].sort((x,y)=>x-y),q=t=>s[Math.min(s.length-1,Math.floor(t*s.length))]||0;
    return{median:q(.5),p95:q(.95),max:q(1),count:a.length};
  };
  async function measure(art,notify=()=>{},rounds=3){
    const rows=MODES.map(mode=>({mode,initial:[],static:[],enqueue:[],single:[],blast:[],singleUpload:[],blastUpload:[],singleChunks:[],blastChunks:[]}));
    for(let round=0;round<rounds;round++){
      // Rotate order to avoid always rewarding the first/last method.
      for(let j=0;j<3;j++){
        const row=rows[(j+round)%3];notify(row.mode,round+1);await sleep();
        const canvas=document.createElement("canvas");canvas.width=960;canvas.height=640;
        const r=new TerrainRenderer(canvas,art,row.mode),a=M.create(),keys=Array.from({length:60},(_,k)=>k),view={x:16,y:82,w:48,h:32};
        let t=performance.now();const init=r.rebuild(a,keys);r.complete();row.initial.push(performance.now()-t);
        for(let i=0;i<5;i++){r.draw(view);r.complete();}
        for(let i=0;i<15;i++){
          t=performance.now();r.draw(view);row.enqueue.push(performance.now()-t);r.complete();row.static.push(performance.now()-t);
        }
        for(const kind of ["single","blast"]){
          const fresh=M.create();r.rebuild(fresh,keys);r.complete();
          for(const hit of M.sequence(kind)){
            t=performance.now();const d=M.damage(fresh,hit.x,hit.y,hit.r),update=r.rebuild(fresh,d.dirty);r.draw(view);r.complete();
            if(d.changed.length){row[kind].push(performance.now()-t);row[kind+"Upload"].push(update.uploadBytes);row[kind+"Chunks"].push(update.chunks);}
          }
          row[kind+"Hash"]=M.hash(fresh);
        }
        row.resources=r.draw(view);row.renderer=r.renderer;row.initGeometryMs=init.geometryMs;r.dispose();await sleep();
      }
    }
    return{date:"2026-11-01",scope:"WebGL1 offscreen 960x640; readPixels completion includes CPU submission, GPU/driver wait and readback; not FPS.",
      map:[320,192],view:[48,32],pixelDensity:16,rounds,warmupFrames:5,sampledFrames:15,rows:rows.map(r=>{
        for(const name of ["initial","static","enqueue","single","blast","singleUpload","blastUpload","singleChunks","blastChunks"])r[name]=summary(r[name]);
        return r;
      })};
  }
  function unityInput(art){
    const image=img=>{
      const c=document.createElement("canvas");c.width=img.width;c.height=img.height;c.getContext("2d").drawImage(img,0,0);
      return c.toDataURL().split(",")[1];
    };
    let a=M.create();
    const steps=[{kind:"initial",dirty:Array.from({length:60},(_,i)=>i),changed:0}];
    const operations=[...M.sequence("single").map(h=>({...h,kind:"single"})),...M.sequence("blast").map(h=>({...h,kind:"blast"}))];
    const encode=step=>({...step,hash:M.hash(a),pages:step.dirty.map(k=>({key:k,
      strip:M.chunk(a,k,"strip").map(v=>({data:Array.from(v)})),atlas:[{data:Array.from(M.chunk(a,k,"atlas")[0])}]}))});
    const frames=[encode(steps[0])];
    let previous="single";
    for(const hit of operations){
      if(hit.kind!==previous){a=M.create();frames.push({kind:"reset",hash:M.hash(a),pages:[],dirty:[],changed:0});previous=hit.kind;}
      const d=M.damage(a,hit.x,hit.y,hit.r);
      if(d.changed.length)frames.push(encode({kind:hit.kind,dirty:d.dirty,changed:d.changed.length}));
    }
    return{version:1,date:"2026-11-01",view:{x:16,y:82,w:48,h:32},width:960,height:640,
      textures:[image(art.rock),image(art.floor),image(art.ceiling),image(art.atlas)],frames};
  }
  root.TerrainMeasure={measure,unityInput,summary};
})(globalThis);
