(function(root){
  "use strict";
  const api=root.FlatCave;
  function strip(image,kind) {
    const c=api.canvas(image.width,image.height),ctx=c.getContext("2d");ctx.drawImage(image,0,0);
    // Alpha-test removes generated translucent halo in the renderer; original PNG stays intact.
    const pixels=ctx.getImageData(0,0,c.width,c.height);
    for(let i=3;i<pixels.data.length;i+=4)pixels.data[i]=pixels.data[i]<224?0:255;
    ctx.putImageData(pixels,0,0);
    ctx.globalCompositeOperation="destination-in";
    const fade=kind==="floor"?ctx.createLinearGradient(0,940,0,1024):ctx.createLinearGradient(0,0,0,60);
    if(kind==="floor"){fade.addColorStop(0,"#000");fade.addColorStop(1,"transparent");}
    else {fade.addColorStop(0,"transparent");fade.addColorStop(1,"#000");}
    ctx.fillStyle=fade;ctx.fillRect(0,0,c.width,c.height);
    return c;
  }
  function prepareSurface(images){
    return {floor:strip(images.floor,"floor"),ceiling:strip(images.ceiling,"ceiling")};
  }
  function reflected(x,size) {
    const t=((x%(2*size))+2*size)%(2*size);return t<size?t:2*size-t-.001;
  }
  function surfaceRuns(edges) {
    const runs=[];let run=null,previous=null;
    for(const edge of edges) {
      const direction=Math.sign(edge.b.x-edge.a.x),floor=edge.ny<0;
      if(Math.abs(edge.ny)<.28||!direction){run=null;previous=null;continue;}
      if(!run||run.direction!==direction||run.floor!==floor||Math.hypot(previous.x-edge.a.x,previous.y-edge.a.y)>.01) {
        run={direction,floor,points:[edge.a],normals:[]};runs.push(run);
      }
      run.points.push(edge.b);run.normals.push(Math.abs(edge.ny));previous=edge.b;
    }
    return runs.map(run=>{
      if(run.direction<0){run.points.reverse();run.normals.reverse();}
      return run;
    });
  }
  function surfaceBands(ctx,art,visual,box){
    const runs=visual.surfaceRuns||(visual.surfaceRuns=surfaceRuns(visual.edges));
    ctx.save();ctx.clip(visual.solid,"evenodd");ctx.lineWidth=8;ctx.strokeStyle="#292b26";ctx.stroke(visual.outline);ctx.restore();
    for(const pass of ["inside","outside"]){
      ctx.save();if(pass==="inside")ctx.clip(visual.solid,"evenodd");
      for(const run of runs){
        const points=run.points,a=points[0],b=points.at(-1);
        if(b.x<box.x-2||a.x>box.x+box.w+2)continue;
        const floor=run.floor,image=floor?art.surfaces.floor:art.surfaces.ceiling;
        const scale=.4,anchor=floor?580:220;
        const sy=pass==="inside"?(floor?anchor:0):(floor?350:anchor);
        const sh=pass==="inside"?(floor?image.height-anchor:anchor):(floor?anchor-350:300);
        const start=Math.max(a.x,box.x-2),end=Math.min(b.x,box.x+box.w+2);
        let segment=0;
        for(let x=Math.floor(start);x<Math.ceil(end);x++){
          const mid=x+.5;
          while(segment<points.length-2&&points[segment+1].x<mid)segment++;
          const p=points[segment],q=points[segment+1];
          const t=Math.max(0,Math.min(1,(mid-p.x)/(q.x-p.x))),y=p.y+(q.y-p.y)*t;
          if(y<box.y-220||y>box.y+box.h+220)continue;
          ctx.globalAlpha=Math.min(1,Math.max(0,(run.normals[segment]-.28)/.22));
          const sourceX=Math.min(image.width-3,Math.floor(reflected(mid/scale,image.width)));
          const vscale=floor?scale*(1+.08*Math.sin(mid/233)):scale;
          ctx.drawImage(image,sourceX,sy,2.5,sh,x,y+(sy-anchor)*vscale,1,sh*vscale);
        }
      }
      ctx.restore();
    }
  }
  Object.assign(api,{prepareSurface,surfaceBands});
})(globalThis);
