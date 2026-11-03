(function (root) {
  "use strict";
  const M=root.MacroCave={},C=root.CaveWhitebox;
  M.width=184;M.height=86;
  M.mix=(a,b,t)=>a+(b-a)*t;
  M.clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
  M.random=function (seed) {
    let s=seed>>>0;
    return ()=>{s=(s+0x6D2B79F5)>>>0;let t=s;
      t=Math.imul(t^(t>>>15),t|1);t^=t+Math.imul(t^(t>>>7),t|61);
      return ((t^(t>>>14))>>>0)/4294967296;};
  };
  M.curve=function (points,sharp=[]) {
    const slopes=points.slice(1).map((p,i)=>(p[1]-points[i][1])/(p[0]-points[i][0]));
    const tangent=points.map((p,i)=>{
      if (!i||i===points.length-1) return 0;
      const a=slopes[i-1],b=slopes[i];
      return a*b<=0?0:2*a*b/(a+b);
    });
    return x=>{
      if (x<=points[0][0]) return points[0][1];
      let i=1;while(i<points.length-1&&x>points[i][0]) i++;
      const a=points[i-1],b=points[i],h=b[0]-a[0],t=M.clamp((x-a[0])/h,0,1);
      const start=sharp.includes(i-1)?slopes[i-1]:tangent[i-1];
      const end=sharp.includes(i)?slopes[i-1]:tangent[i];
      return (2*t*t*t-3*t*t+1)*a[1]+(t*t*t-2*t*t+t)*h*start+
        (-2*t*t*t+3*t*t)*b[1]+(t*t*t-t*t)*h*end;
    };
  };
  M.withCaps=function (interior) {
    return x=>{
      if (x<5||x>175) return [43,43];
      if (x<8||x>172) {
        const edge=x<8?8:172,p=interior(edge),middle=(p[0]+p[1])/2;
        const u=(x-edge)/3,h=(p[1]-p[0])/2*Math.sqrt(Math.max(0,1-u*u));
        return [middle-h,middle+h];
      }
      return interior(x);
    };
  };
  M.author=function (seed) {
    const rng=M.random(seed),top=[],bottom=[];
    M.slots.forEach((slot,i)=>{
      const a=M.port(i,1),b=M.port(i+1,1);
      const variation=(rng()-.5)*1.1;
      [slot.top,slot.bottom].forEach((points,edge)=>{
        const knots=points.map(p=>[slot.start+p[0]*slot.width,
          M.mix(a[edge],b[edge],p[0])+p[1]+
          (edge===0?variation*Math.sin(Math.PI*p[0])**2:0)]);
        M.flatten(knots,i,edge);
        knots.forEach((p,j)=>{if(!i||j) (edge===0?top:bottom).push(p);});
      });
    });
    const roof=M.curve(top),floor=M.curve(bottom);
    return M.withCaps(x=>[roof(x),floor(x)]);
  };
  function mappedVertex(p,values) {
    if (p[0]!==.5&&p[1]!==.5) return p;
    let a,b;
    if (p[1]===0) {a=0;b=1;}
    else if (p[0]===1) {a=1;b=2;}
    else if (p[1]===1) {a=3;b=2;}
    else {a=0;b=3;}
    const t=values[a]/(values[a]-values[b]);
    return p[1]===0||p[1]===1?[t,p[1]]:[p[0],t];
  }
  M.gridCatalog=Array.from({length:256},(_,id)=>{
    const values=Array.from({length:4},(_,i)=>[-3,-1,1,3][Math.floor(id/4**i)%4]);
    const mask=values.reduce((m,v,i)=>m|(v<0?1<<i:0),0);
    return {id,values,mask,polygons:C.templates[mask].polygons.map(poly=>
      poly.map(p=>mappedVertex(p,values)))};
  });
  M.grid=function (profile,expanded) {
    const world={width:M.width,height:M.height,
      data:new Uint8Array((M.width+1)*(M.height+1)),values:[],polygons:new Map()};
    for (let y=0;y<=M.height;y++) for (let x=0;x<=M.width;x++) {
      const p=profile(x),v=Math.min(y-p[0],p[1]-y),index=y*(M.width+1)+x;
      world.data[index]=v>0?0:1;
      world.values[index]=(v>0?1:-1)*(Math.abs(v)>1.15?3:1);
    }
    let occupied=0,edgeCount=0;const ids=new Set();
    for (let y=0;y<M.height;y++) for (let x=0;x<M.width;x++) {
      const mask=C.maskAt(world,x,y);if(mask) occupied++;
      if (!mask||mask===15) continue;
      edgeCount++;
      if (expanded) {
        const i=y*(M.width+1)+x,n=M.width+1;
        const values=[world.values[i],world.values[i+1],world.values[i+n+1],world.values[i+n]];
        const digit=v=>[-3,-1,1,3].indexOf(v);
        const id=values.reduce((sum,v,k)=>sum+digit(v)*4**k,0);ids.add(id);
        world.polygons.set(y*M.width+x,M.gridCatalog[id].polygons);
      }
    }
    world.stats={occupied,edgeCount,usedConfigurations:ids.size};
    return world;
  };
  const originalSolid=C.solid;
  function pointInside(polygon,x,y) {
    let hit=false;
    for(let i=0,j=polygon.length-1;i<polygon.length;j=i++) {
      const a=polygon[i],b=polygon[j];
      if ((a[1]>y)!==(b[1]>y)&&x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]) hit=!hit;
    }
    return hit;
  }
  M.solid=function (world,x,y) {
    if(x<0||y<0||x>=M.width||y>=M.height) return true;
    if(world.method==="dual") return originalSolid(world.grid,x,y);
    if(world.method==="expanded") {
      const ix=Math.floor(x),iy=Math.floor(y),mask=C.maskAt(world.grid,ix,iy);
      if(!mask||mask===15) return mask===15;
      return world.grid.polygons.get(iy*M.width+ix).some(p=>pointInside(p,x-ix,y-iy));
    }
    const p=world.profile(x);return y<=p[0]||y>=p[1];
  };
  C.solid=M.solid;
  M.floor=function(world,x) {
    const p=world.profile(x),floor=C.floorAt(world,x,p[0]+.15,p[1]-p[0]+2);
    return floor===null?p[1]:floor;
  };
  M.makeWorld=function(method,seed) {
    const started=performance.now(),author=M.author(seed);
    let profile=author,blocks=[],transitions=[],solution=null;
    if (method==="blocks"||method==="bridges"||method==="wfc") {
      const made=M.assemble(method,seed);
      profile=made.profile;blocks=made.blocks;transitions=made.transitions;solution=made.solution;
    }
    const outline=M.sample(profile,.125);
    const baked=x=>{
      if(x<5||x>175) return [43,43];
      const at=(x-5)*8,i=Math.min(outline.top.length-2,Math.floor(at)),t=at-i;
      return [M.mix(outline.top[i][1],outline.top[i+1][1],t),
        M.mix(outline.bottom[i][1],outline.bottom[i+1][1],t)];
    };
    const world={width:M.width,height:M.height,method,seed,profile:baked,
      author,blocks,transitions,solution,outline};
    if(method==="dual"||method==="expanded") world.grid=M.grid(author,method==="expanded");
    world.spawn={x:18,y:M.floor(world,18)-1.02};
    world.buildMs=performance.now()-started;
    return world;
  };
  M.sample=function(profile,step=.25) {
    const top=[],bottom=[];
    for(let x=5;x<175;x+=step) {const p=profile(x);top.push([x,p[0]]);bottom.push([x,p[1]]);}
    const last=profile(175);top.push([175,last[0]]);bottom.push([175,last[1]]);
    return {top,bottom};
  };
})(globalThis);
