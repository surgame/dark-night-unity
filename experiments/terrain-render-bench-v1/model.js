(function(root) {
  "use strict";
  const W=320,H=192,CH=32,PX=16;
  const corners=[[0,0],[1,0],[1,1],[0,1]], mids=[[.5,0],[1,.5],[.5,1],[0,.5]];
  const topology=[
    [],[[0,"t","l"]],[[1,"r","t"]],[[0,1,"r","l"]],
    [[2,"b","r"]],[[0,"t","l"],[2,"b","r"]],[[1,2,"b","t"]],
    [[0,1,2,"b","l"]],[[3,"l","b"]],[[0,"t","b",3]],
    [[1,"r","t"],[3,"l","b"]],[[0,1,"r","b",3]],
    [["l","r",2,3]],[[0,"t","r",2,3]],[["t",1,2,3,"l"]],[[0,1,2,3]]
  ];
  const index={t:0,r:1,b:2,l:3};
  const polygon=topology.map(group=>group.map(poly=>poly.map(p=>typeof p==="number"?corners[p]:mids[index[p]])));
  function create() {
    const a=new Uint8Array((W+1)*(H+1));a.fill(1);
    for(let y=1;y<H;y++)for(let x=1;x<W;x++){
      const ceiling=87+3*Math.sin(x/12)+1.7*Math.sin(x/4.7);
      const floor=104+2.8*Math.sin(x/13)+1.5*Math.sin(x/5.3);
      const chamber=((x-33)/11)**2+((y-98)/11)**2<1;
      const pocket=((x-55)/5)**2+((y-99)/7)**2<1;
      const other=((x-150)/30)**2+((y-66)/15)**2<1||((x-245)/25)**2+((y-130)/18)**2<1;
      if((y>ceiling&&y<floor)||chamber||pocket||other)a[y*(W+1)+x]=0;
    }
    return a;
  }
  function mask(a,x,y){
    return a[y*(W+1)+x]|a[y*(W+1)+x+1]<<1|a[(y+1)*(W+1)+x+1]<<2|a[(y+1)*(W+1)+x]<<3;
  }
  function key(x,y){return Math.floor(y/CH)*10+Math.floor(x/CH);}
  function bounds(k){return{x:k%10*CH,y:Math.floor(k/10)*CH,w:CH,h:CH};}
  function damage(a,x,y,radius){
    const changed=[],dirty=new Set(),tiles=new Set();
    for(let v=Math.max(1,Math.floor(y-radius));v<=Math.min(H-1,Math.ceil(y+radius));v++)
      for(let u=Math.max(1,Math.floor(x-radius));u<=Math.min(W-1,Math.ceil(x+radius));u++){
        if((u-x)**2+(v-y)**2>radius*radius||!a[v*(W+1)+u])continue;
        a[v*(W+1)+u]=0;changed.push(v*(W+1)+u);
        for(const dx of [-1,0])for(const dy of [-1,0]){
          const tx=u+dx,ty=v+dy;dirty.add(key(tx,ty));tiles.add(ty*W+tx);
        }
      }
    return{changed,dirty:[...dirty].sort((a,b)=>a-b),tiles:[...tiles]};
  }
  function sequence(kind){
    if(kind==="single")return Array.from({length:24},(_,i)=>({x:25+i%12,y:108+Math.floor(i/12),r:.75}));
    return Array.from({length:12},(_,i)=>({x:23+(i%6)*3,y:108+Math.floor(i/6)*3,r:2.2}));
  }
  function edges(m,x,y){
    const result=[];
    for(const poly of polygon[m])for(let i=0;i<poly.length;i++){
      const p=poly[i],q=poly[(i+1)%poly.length];
      if(p[0]===q[0]&&(p[0]===0||p[0]===1)||p[1]===q[1]&&(p[1]===0||p[1]===1))continue;
      const dx=q[0]-p[0],dy=q[1]-p[1],len=Math.hypot(dx,dy);
      result.push({a:[p[0]+x,p[1]+y],b:[q[0]+x,q[1]+y],nx:-dy/len,ny:dx/len,len});
    }
    return result;
  }
  const vertex=(out,x,y,u,v)=>out.push(x,y,u,v);
  function quad(out,p,u){
    for(const i of [0,1,2,0,2,3])vertex(out,p[i][0],p[i][1],u[i][0],u[i][1]);
  }
  function atlasUV(m,x,y){
    const id=m*16+(y%4)*4+x%4,tx=id%16*18+1,ty=Math.floor(id/16)*18+1;
    return [[tx,ty],[tx+16,ty],[tx+16,ty+16],[tx,ty+16]].map(p=>[p[0]/288,p[1]/288]);
  }
  function edgeQuad(e){
    const d=.18;
    return[e.a,e.b,[e.b[0]+e.nx*d,e.b[1]+e.ny*d],[e.a[0]+e.nx*d,e.a[1]+e.ny*d]];
  }
  function clippedStrip(out,e,x,y){
    const u=e.a[0]/4;
    let p=edgeQuad(e).map((v,i)=>[...v,i===1||i===2?u+e.len/4:u,i<2?0:1]);
    for(const [axis,value,sign] of [[0,x,1],[0,x+1,-1],[1,y,1],[1,y+1,-1]]){
      const next=[];
      for(let i=0;i<p.length;i++){
        const a=p[i],b=p[(i+1)%p.length],ia=(a[axis]-value)*sign>=-1e-9,ib=(b[axis]-value)*sign>=-1e-9;
        if(ia)next.push(a);
        if(ia!==ib){const t=(value-a[axis])/(b[axis]-a[axis]);next.push(a.map((n,j)=>n+(b[j]-n)*t));}
      }
      p=next;
    }
    for(let i=1;i<p.length-1;i++)for(const j of [0,i,i+1])out.push(...p[j]);
  }
  function chunk(a,k,mode){
    const box=bounds(k),groups=[[],[],[]];
    for(let y=box.y;y<box.y+CH;y++)for(let x=box.x;x<box.x+CH;x++){
      const m=mask(a,x,y);if(!m)continue;
      if(mode==="atlas"){
        quad(groups[0],[[x,y],[x+1,y],[x+1,y+1],[x,y+1]],atlasUV(m,x,y));continue;
      }
      if(m===15){
        let end=x+1;while(end<box.x+CH&&mask(a,end,y)===15)end++;
        quad(groups[0],[[x,y],[end,y],[end,y+1],[x,y+1]],[[x/4,y/4],[end/4,y/4],[end/4,(y+1)/4],[x/4,(y+1)/4]]);
        x=end-1;continue;
      }
      for(const p of polygon[m])for(let i=1;i<p.length-1;i++)
        for(const j of [0,i,i+1])vertex(groups[0],x+p[j][0],y+p[j][1],(x+p[j][0])/4,(y+p[j][1])/4);
      for(const e of edges(m,x,y)){
        const group=e.ny>=0?1:2;clippedStrip(groups[group],e,x,y);
      }
    }
    return groups.map(v=>new Float32Array(v));
  }
  function hash(a){let h=2166136261;for(const n of a){h^=n;h=Math.imul(h,16777619);}return(h>>>0).toString(16);}
  root.TerrainBench={W,H,CH,PX,polygon,create,mask,key,bounds,damage,sequence,edges,edgeQuad,quad,chunk,hash,atlasUV};
})(globalThis);
