(function(P,C) {
  "use strict";
  function vertex(p,values) {
    if(p[0]!==.5&&p[1]!==.5) return p;
    let a,b;
    if(p[1]===0) {a=0;b=1;} else if(p[0]===1) {a=1;b=2;}
    else if(p[1]===1) {a=3;b=2;} else {a=0;b=3;}
    const t=values[a]/(values[a]-values[b]);
    return p[1]===0||p[1]===1?[t,p[1]]:[p[0],t];
  }
  const side=(a,b)=>a[0]===b[0]&&(a[0]===0||a[0]===1)||
    a[1]===b[1]&&(a[1]===0||a[1]===1);
  P.meshRegion=function(data,stride,x0,y0,nx,ny,step,ox=0,oy=0) {
    const triangles=[],lines=[];
    function sample(x,y) {const i=(y+y0)*stride+x+x0;return [data[i],data[i+1],data[i+stride+1],data[i+stride]];}
    const mask=values=>values.reduce((bits,v,i)=>bits|(v>0?1<<i:0),0);
    const add=(target,p,x,y)=>target.push(ox+(x+p[0])*step,oy+(y+p[1])*step);
    for(let y=0;y<ny;y++) for(let x=0;x<nx;x++) {
      const values=sample(x,y),bits=mask(values);if(!bits) continue;
      if(bits===15) {
        let run=1;while(x+run<nx&&mask(sample(x+run,y))===15) run++;
        triangles.push(ox+x*step,oy+y*step,ox+(x+run)*step,oy+y*step,
          ox+(x+run)*step,oy+(y+1)*step,ox+x*step,oy+y*step,
          ox+(x+run)*step,oy+(y+1)*step,ox+x*step,oy+(y+1)*step);
        x+=run-1;continue;
      }
      for(const original of C.templates[bits].polygons) {
        const points=original.map(p=>vertex(p,values));
        for(let i=1;i<points.length-1;i++) {
          add(triangles,points[0],x,y);add(triangles,points[i],x,y);add(triangles,points[i+1],x,y);
        }
        for(let i=0;i<points.length;i++) if(!side(original[i],original[(i+1)%points.length])) {
          add(lines,points[i],x,y);add(lines,points[(i+1)%points.length],x,y);
        }
      }
    }
    return {triangles:new Float32Array(triangles),lines:new Float32Array(lines),
      bounds:{x0:ox,y0:oy,x1:ox+nx*step,y1:oy+ny*step}};
  };
  P.moduleMesh=function(def) {
    const n=def.size/2,stride=n+1,field=new Float32Array(stride*stride);
    for(let y=0;y<=n;y++) for(let x=0;x<=n;x++)
      field[y*stride+x]=P.moduleField(def,x*2,y*2);
    return P.meshRegion(field,stride,0,0,n,n,2);
  };
  P.bridgeMesh=function(def) {
    const triangles=[],lines=[],count=Math.max(2,Math.ceil(def.length/2));
    const point=(x,y)=>def.vertical?[y,x]:[x,y];
    const from=P.width(def.from),to=P.width(def.to);
    function half(t) {
      const u=t*t*t*(10-15*t+6*t*t);
      return from+(to-from)*u+(def.variant-1)*.35*Math.sin(Math.PI*t)**2;
    }
    function add(target,p) {target.push(...point(...p));}
    for(let i=0;i<count;i++) {
      const a=i/count,b=(i+1)/count,pa=[def.length*a,-half(a)],qa=[def.length*a,half(a)];
      const pb=[def.length*b,-half(b)],qb=[def.length*b,half(b)];
      for(const p of [pa,pb,qb,pa,qb,qa]) add(triangles,p);
      for(const p of [pa,pb,qa,qb]) add(lines,p);
    }
    const r=Math.max(from,to)+.35;
    return {triangles:new Float32Array(triangles),lines:new Float32Array(lines),
      bounds:def.vertical?{x0:-r,y0:0,x1:r,y1:def.length}:{x0:0,y0:-r,x1:def.length,y1:r}};
  };
  P.prepare=function(g) {
    const started=performance.now(),sources=new Map(),instances=[];
    if(g.field) {
      const f=g.field;
      for(let y=0;y<f.height-1;y+=32) for(let x=0;x<f.width-1;x+=32) {
        const mesh=P.meshRegion(f.data,f.width,x,y,Math.min(32,f.width-1-x),
          Math.min(32,f.height-1-y),2,x*2,y*2);
        if(!mesh.triangles.length) continue;
        const key="u:"+x+":"+y;sources.set(key,mesh);instances.push({key,x:0,y:0});
      }
    } else for(const def of g.choices.concat(g.connectors)) {
      if(!sources.has(def.key)) sources.set(def.key,def.kind==="bridge"?P.bridgeMesh(def):P.moduleMesh(def));
      instances.push({key:def.key,x:def.x,y:def.y});
    }
    return {g,sources,instances,geometryMs:performance.now()-started,chunks:null};
  };
  P.bake=function(prepared) {
    const started=performance.now(),{g,sources,instances}=prepared,groups=new Map();
    if(g.field) prepared.chunks=[...sources.values()];
    else {
      for(const instance of instances) {
        const source=sources.get(instance.key),b=source.bounds;
        const cx=(b.x0+b.x1)/2+instance.x,cy=(b.y0+b.y1)/2+instance.y;
        const key=Math.floor(cx/P.chunk)+":"+Math.floor(cy/P.chunk);
        if(!groups.has(key)) groups.set(key,{triangles:[],lines:[],
          bounds:{x0:Infinity,y0:Infinity,x1:-Infinity,y1:-Infinity}});
        const chunk=groups.get(key);
        for(const field of ["triangles","lines"]) {
          const vertices=source[field],out=chunk[field];
          for(let i=0;i<vertices.length;i+=2) out.push(vertices[i]+instance.x,vertices[i+1]+instance.y);
        }
        chunk.bounds.x0=Math.min(chunk.bounds.x0,b.x0+instance.x);
        chunk.bounds.y0=Math.min(chunk.bounds.y0,b.y0+instance.y);
        chunk.bounds.x1=Math.max(chunk.bounds.x1,b.x1+instance.x);
        chunk.bounds.y1=Math.max(chunk.bounds.y1,b.y1+instance.y);
      }
      prepared.chunks=[...groups.values()].map(c=>({...c,
        triangles:new Float32Array(c.triangles),lines:new Float32Array(c.lines)}));
    }
    prepared.chunkMs=performance.now()-started;return prepared;
  };
  P.resources=function(p) {
    const source=[...p.sources.values()],bytes=m=>m.triangles.byteLength+m.lines.byteLength;
    const sourceBytes=source.reduce((s,m)=>s+bytes(m),0),chunkBytes=p.chunks.reduce((s,m)=>s+bytes(m),0);
    const freeKeys=[...p.sources.keys()].filter(k=>!k.startsWith("p:")&&!k.startsWith("u:"));
    const profile=p.g.plan.profile,method=p.g.method;
    return {presetUsed:p.g.plan.pinTypes,presetLibrary:32,freeUsed:freeKeys.length,
      freeLibrary:method==="contour"?0:method==="bridges"?(profile==="standard"?60:72):
        profile==="standard"?48:243,
      instances:p.instances.length,sourceVertices:sourceBytes/8,chunkVertices:chunkBytes/8,
      sourceBytes:sourceBytes+(p.g.field?0:p.instances.length*8),chunkBytes,
      chunks:p.chunks.length,fieldBytes:p.g.field?
        p.g.field.data.byteLength+p.g.field.frozen.byteLength:0};
  };
})(globalThis.CavePerf,globalThis.CaveWhitebox);
