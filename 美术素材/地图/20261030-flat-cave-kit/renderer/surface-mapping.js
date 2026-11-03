(function(root){
  "use strict";
  const api=root.FlatCave,SCALE=.4;
  function strip(image,kind) {
    const c=api.canvas(image.width,image.height),ctx=c.getContext("2d");ctx.drawImage(image,0,0);
    const pixels=ctx.getImageData(0,0,c.width,c.height);
    for(let i=3;i<pixels.data.length;i+=4)pixels.data[i]=pixels.data[i]<224?0:255;
    // End the overlay along an actual dark rock joint, without blending two rock faces.
    api.rockBoundary(pixels.data,c.width,c.height,kind);
    ctx.putImageData(pixels,0,0);return c;
  }
  function prepareSurface(images){
    return {floor:strip(images.floor,"floor"),ceiling:strip(images.ceiling,"ceiling")};
  }
  function reflected(x,size) {
    const t=((x%(2*size))+2*size)%(2*size);return t<size?t:2*size-t;
  }
  function surfaceRuns(edges) {
    const runs=[];let run=null,previous=null;
    for(const edge of edges) {
      const direction=Math.sign(edge.b.x-edge.a.x),floor=edge.ny<0;
      if(Math.abs(edge.ny)<.28||!direction){run=null;previous=null;continue;}
      if(!run||run.direction!==direction||run.floor!==floor||Math.hypot(previous.x-edge.a.x,previous.y-edge.a.y)>.01) {
        run={direction,floor,points:[edge.a]};runs.push(run);
      }
      run.points.push(edge.b);previous=edge.b;
    }
    return runs.map(run=>{
      if(run.direction<0)run.points.reverse();
      run.lengths=[0];
      for(let i=1;i<run.points.length;i++)run.lengths.push(run.lengths[i-1]+Math.hypot(
        run.points[i].x-run.points[i-1].x,run.points[i].y-run.points[i-1].y));
      run.length=run.lengths.at(-1);run.phase=run.points[0].x/SCALE;return run;
    });
  }
  function pointAt(run,s) {
    s=Math.max(0,Math.min(run.length,s));
    let lo=0,hi=run.points.length-1;
    while(lo+1<hi){const mid=(lo+hi)>>1;if(run.lengths[mid]<s)lo=mid;else hi=mid;}
    const p=run.points[lo],q=run.points[hi],length=run.lengths[hi]-run.lengths[lo];
    const t=length>0?(s-run.lengths[lo])/length:0;
    return {x:p.x+(q.x-p.x)*t,y:p.y+(q.y-p.y)*t};
  }
  function frameAt(run,s) {
    const p=pointAt(run,s),a=pointAt(run,s-5),b=pointAt(run,s+5),len=Math.hypot(b.x-a.x,b.y-a.y)||1;
    return {...p,tx:(b.x-a.x)/len,ty:(b.y-a.y)/len,nx:-(b.y-a.y)/len,ny:(b.x-a.x)/len};
  }
  const offset=(p,d)=>({x:p.x+p.nx*d,y:p.y+p.ny*d});
  function triangle(ctx,image,uv,p) {
    const x1=uv[1].x-uv[0].x,y1=uv[1].y-uv[0].y,x2=uv[2].x-uv[0].x,y2=uv[2].y-uv[0].y;
    const det=x1*y2-x2*y1;if(Math.abs(det)<.00001)return;
    const dx1=p[1].x-p[0].x,dy1=p[1].y-p[0].y,dx2=p[2].x-p[0].x,dy2=p[2].y-p[0].y;
    const a=(dx1*y2-dx2*y1)/det,c=(dx2*x1-dx1*x2)/det;
    const b=(dy1*y2-dy2*y1)/det,d=(dy2*x1-dy1*x2)/det;
    const transform=ctx.getTransform(),pixel=.65/(Math.hypot(transform.a,transform.b)||1);
    const sign=Math.sign(dx1*dy2-dy1*dx2);
    const clip=p.map((v,i)=>{
      const prev=p[(i+2)%3],next=p[(i+1)%3],l0=Math.hypot(v.x-prev.x,v.y-prev.y)||1,l1=Math.hypot(next.x-v.x,next.y-v.y)||1;
      const n0={x:sign*(v.y-prev.y)/l0,y:-sign*(v.x-prev.x)/l0},n1={x:sign*(next.y-v.y)/l1,y:-sign*(next.x-v.x)/l1};
      const amount=Math.min(pixel*8,pixel/Math.max(.01,1+n0.x*n1.x+n0.y*n1.y));
      return {x:v.x+(n0.x+n1.x)*amount,y:v.y+(n0.y+n1.y)*amount};
    });
    ctx.save();ctx.beginPath();clip.forEach((v,i)=>i?ctx.lineTo(v.x,v.y):ctx.moveTo(v.x,v.y));ctx.closePath();ctx.clip();
    ctx.transform(a,b,c,d,p[0].x-a*uv[0].x-c*uv[0].y,p[0].y-b*uv[0].x-d*uv[0].y);
    ctx.drawImage(image,0,0);ctx.restore();
  }
  function band(ctx,image,run,sy,ey,anchor,box) {
    const step=12;
    for(let cursor=0;cursor<run.length;){
      const s=cursor,raw=run.phase+s/SCALE;
      const boundary=(Math.floor((raw+.00001)/image.width)+1)*image.width;
      const end=Math.min(run.length,s+step,s+(boundary-raw)*SCALE);
      cursor=end;
      const a=frameAt(run,s),b=frameAt(run,end);
      if(Math.max(a.x,b.x)<box.x-130||Math.min(a.x,b.x)>box.x+box.w+130||
        Math.max(a.y,b.y)<box.y-150||Math.min(a.y,b.y)>box.y+box.h+150)continue;
      const ua=reflected(run.phase+s/SCALE,image.width),ub=reflected(run.phase+end/SCALE,image.width);
      if(Math.abs(ub-ua)<.01)continue;
      const d0=(sy-anchor)*SCALE,d1=(ey-anchor)*SCALE;
      const pa=offset(a,d0),pb=offset(b,d0),pc=offset(b,d1),pd=offset(a,d1);
      // Keep rock faces opaque on steep slopes; angular fading reintroduces ghosted outlines.
      ctx.globalAlpha=1;
      triangle(ctx,image,[{x:ua,y:sy},{x:ub,y:sy},{x:ub,y:ey}],[pa,pb,pc]);
      triangle(ctx,image,[{x:ua,y:sy},{x:ub,y:ey},{x:ua,y:ey}],[pa,pc,pd]);
    }
  }
  function surfaceBands(ctx,art,visual,box){
    const runs=visual.arcSurfaceRuns||(visual.arcSurfaceRuns=surfaceRuns(visual.edges));
    ctx.save();ctx.clip(visual.solid,"evenodd");ctx.lineWidth=8;ctx.strokeStyle="#292b26";ctx.stroke(visual.outline);ctx.restore();
    for(const inside of [true,false]){
      ctx.save();if(inside)ctx.clip(visual.solid,"evenodd");
      for(const run of runs){
        const image=run.floor?art.surfaces.floor:art.surfaces.ceiling,anchor=run.floor?580:220;
        const sy=inside?(run.floor?anchor:0):(run.floor?350:anchor);
        const ey=inside?(run.floor?910:anchor):(run.floor?anchor:520);
        band(ctx,image,run,sy,ey,anchor,box);
      }
      ctx.restore();
    }
  }
  Object.assign(api,{prepareSurface,surfaceBands,surfaceRuns,surfaceFrameAt:frameAt,surfaceScale:SCALE});
})(globalThis);
