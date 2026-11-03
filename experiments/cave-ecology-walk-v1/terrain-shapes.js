(function (root) {
  "use strict";
  const WIDTH=623, BAND=64, BLEND=112, VERSION="ecology-author-shapes-v2";
  const clone=p=>p.map(q=>q.slice());
  function sample(points,x) {
    for(let i=1;i<points.length;i++)if(x<=points[i][0]){
      const a=points[i-1],b=points[i],t=(x-a[0])/(b[0]-a[0]);
      return a[1]+(b[1]-a[1])*t;
    }
    return points.at(-1)[1];
  }
  function section(points,a,b) {
    return [[a,sample(points,a)],...points.filter(p=>p[0]>a&&p[0]<b),[b,sample(points,b)]];
  }
  function normalized(profile,native) {
    const left=profile.floor[0][1],right=profile.floor.at(-1)[1];
    const xs=new Set([0,BAND,BLEND,WIDTH-BLEND,WIDTH-BAND,WIDTH]);
    profile.floor.forEach(p=>xs.add(p[0]));
    for(let x=0;x<=WIDTH;x+=4)xs.add(Math.min(x,WIDTH));
    const order=[...xs].sort((a,b)=>a-b);
    function edgeValue(x,inner,port) {
      const distance=Math.min(x,WIDTH-x);
      if(distance<=BAND)return port;
      if(distance>=BLEND)return inner;
      const t=(distance-BAND)/(BLEND-BAND),ease=t*t*(3-2*t);
      return port+(inner-port)*ease;
    }
    const floor=order.map(x=>[x,edgeValue(x,sample(profile.floor,x),x<WIDTH/2?left:right)]);
    const roof=order.map(x=>{
      const value=native?native.roof[Math.min(622,Math.round(x))]:sample(profile.roof,x);
      return [x,edgeValue(x,value,(x<WIDTH/2?left:right)-128)];
    });
    return {floor,roof};
  }
  function cutShape(profile,gap) {
    if(!gap)return null;
    const [a,b,depth]=gap,ay=sample(profile.floor,a),by=sample(profile.floor,b);
    const base=Math.max(ay,by)+depth,bank=Math.min(19,(b-a)*.22);
    const points=[[a,ay],[a+bank*.28,ay+depth*.24],[a+bank*.64,base-9],
      [a+bank,base-2],[(a+b)/2,base],[b-bank,base-1],
      [b-bank*.62,base-8],[b-bank*.22,by+depth*.23],[b,by]];
    const floor=[...profile.floor.filter(p=>p[0]<a),...points,...profile.floor.filter(p=>p[0]>b)];
    const mask=[[a-2,Math.min(ay,by)-85],[b+2,Math.min(ay,by)-85],...points.slice().reverse()];
    return {a,b,depth,base,points,floor,mask,leftBank:points.slice(0,4),rightBank:points.slice(-4)};
  }
  function lowRoof(profile,a,b,bottom) {
    const left=a-34,right=b+36;
    const peak=Math.max(...section(profile.roof,a,b).map(p=>p[1])),offset=bottom-peak;
    const xs=[left,...profile.roof.filter(p=>p[0]>left&&p[0]<right).map(p=>p[0]),right];
    const underside=xs.map(x=>{
      const t=x<a?(x-left)/(a-left):x>b?(right-x)/(right-b):1;
      const ease=t*t*(3-2*t);
      return [x,sample(profile.roof,x)+offset*ease];
    });
    const roof=[...profile.roof.filter(p=>p[0]<left),...underside,...profile.roof.filter(p=>p[0]>right)];
    const polygon=[[left,-30],[right,-30],...underside.slice().reverse()];
    return {a,b,bottom,roof,polygon,underside};
  }
  function ledge(id,points) {
    const a=points[0],b=points.at(-1),mid=(a[0]+b[0])/2;
    return {id,points:clone(points),kind:"oneWayBackwallShelf",
      bodyPolygon:[...clone(points),[b[0]+9,b[1]+10],[mid+12,(a[1]+b[1])/2+29],
        [a[0]-13,a[1]+17]],
      backSocket:[[a[0]-25,a[1]-5],[b[0]+18,b[1]-8],[b[0]+20,b[1]+24],
        [mid-9,a[1]+40],[a[0]-27,a[1]+18]]};
  }
  function road(points) {
    const centre=clone(points),far=points.map(p=>[p[0],p[1]-8]),near=points.map(p=>[p[0],p[1]+12]);
    return {centre,far,near,lipDepth:9,farOffset:-8,nearOffset:12,projection:"sideview-depth-v2"};
  }
  function streams(profile,start=108,end=523) {
    const raw=section(profile.floor,start,end),groups=[];
    let run=[raw[0]],sign=0;
    for(let i=1;i<raw.length;i++) {
      const dy=raw[i][1]-raw[i-1][1],next=Math.abs(dy)<.025?sign:Math.sign(dy);
      if(sign&&next&&next!==sign) { if(run.length>1)groups.push(run);run=[raw[i-1]]; }
      run.push(raw[i]);if(next)sign=next;
    }
    if(run.length>1)groups.push(run);
    return groups.filter(p=>p.at(-1)[0]-p[0][0]>=55&&Math.abs(p.at(-1)[1]-p[0][1])>=.5)
      .sort((a,b)=>(b.at(-1)[0]-b[0][0])-(a.at(-1)[0]-a[0][0])).slice(0,3)
      .sort((a,b)=>a[0][0]-b[0][0]).map((points,i)=>{
        const dir=points.at(-1)[1]>points[0][1]?1:-1;
        const source=dir===1?points[0]:points.at(-1),sink=dir===1?points.at(-1):points[0];
        return {id:"river-"+i,type:"stream",points:points.map(p=>[p[0],p[1]-4]),
          dir,h:8,width:8,source:{kind:"spring",x:source[0],y:source[1]-4},
          sink:{kind:"seep",x:sink[0],y:sink[1]-4},
          pool:{x:sink[0],y:sink[1]-3,rx:15,ry:3},
          banks:{near:true,far:true,bedDepth:7},layer:"channel"};
      });
  }
  function hash(value) {
    const text=typeof value==="string"?value:JSON.stringify(value);let h=2166136261;
    for(let i=0;i<text.length;i++){h^=text.charCodeAt(i);h=Math.imul(h,16777619);}
    return (h>>>0).toString(16).padStart(8,"0");
  }
  root.EcologyTerrainShapes={VERSION,WIDTH,BAND,BLEND,normalized,cutShape,lowRoof,ledge,road,streams,
    sample,section,hash};
})(globalThis);
