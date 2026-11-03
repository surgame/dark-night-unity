(function(T) {
  "use strict";
  const stride=9;
  function vertex(out,x,y,u,v,kind,style,px,py,mask) {out.push(x,y,u,v,kind,style,px,py,mask);}
  function triangle(out,a,b,c,kind,style,px,py,mask) {
    for(const p of [a,b,c])vertex(out,p[0],p[1],p[2]||0,p[3]||0,kind,style,px,py,mask);
  }
  function quad(out,a,b,c,d,kind,style,px,py,mask) {
    triangle(out,a,b,c,kind,style,px,py,mask);triangle(out,a,c,d,kind,style,px,py,mask);
  }
  function clipRect(points,x,y) {
    let result=points;
    for(const [axis,value,sign] of [[0,x,1],[0,x+1,-1],[1,y,1],[1,y+1,-1]]) {
      const input=result;result=[];
      for(let i=0;i<input.length;i++) {
        const a=input[i],b=input[(i+1)%input.length],da=(a[axis]-value)*sign,db=(b[axis]-value)*sign;
        if(da>=-1e-8)result.push(a);
        if((da>=0)!==(db>=0)) {
          const t=da/(da-db);result.push(a.map((n,j)=>n+(b[j]-n)*t));
        }
      }
    }
    return result;
  }
  const pointKey=p=>Math.round(p[0]*100000)+","+Math.round(p[1]*100000);
  function contourRuns(segments) {
    const starts=new Map(),used=new Uint8Array(segments.length),runs=[];
    segments.forEach((s,i)=>{const k=pointKey(s.points[0]);if(!starts.has(k))starts.set(k,[]);starts.get(k).push(i);});
    for(let i=0;i<segments.length;i++) {
      if(used[i])continue;
      const run=[];let next=i;
      while(next!==undefined&&!used[next]) {
        used[next]=1;const segment=segments[next];run.push(segment);
        next=starts.get(pointKey(segment.points.at(-1)))?.find(j=>!used[j]);
      }
      runs.push(run);
    }
    return runs;
  }
  function neighborPoint(point,before,adapter) {
    const x=Math.floor(point[0]),y=Math.floor(point[1]),key=pointKey(point);
    for(let py=y-1;py<=y;py++)for(let px=x-1;px<=x;px++) {
      if(px<0||py<0||px>=adapter.w||py>=adapter.h)continue;
      for(const line of T.templates[adapter.masks[py*adapter.w+px]].segments) {
        const at=before?line.length-1:0,p=[line[at][0]+px,line[at][1]+py];
        if(pointKey(p)===key) {
          const next=line[before?at-1:1];return [next[0]+px,next[1]+py];
        }
      }
    }
    return point;
  }
  function warpedBands(out,segments,adapter) {
    for(const run of contourRuns(segments)) {
      const flattened=[],owners=[];
      for(const segment of run) {
        for(let j=0;j<segment.points.length-1;j++) {
          flattened.push(segment.points[j]);owners.push(segment);
        }
      }
      flattened.push(run.at(-1).points.at(-1));
      const normals=flattened.map((p,i)=>{
        const a=i===0?neighborPoint(p,true,adapter):flattened[i-1],
          b=i===flattened.length-1?neighborPoint(p,false,adapter):flattened[i+1];
        const dx=b[0]-a[0],dy=b[1]-a[1],len=Math.hypot(dx,dy)||1;
        return [-dy/len,dx/len];
      });
      // Each chunk/run owns a stable phase; a cut cannot shift a distant chunk's texture.
      let length=T.hash(Math.floor(flattened[0][0]),Math.floor(flattened[0][1]))%128/128;
      for(let i=0;i<flattened.length-1;i++) {
        const a=flattened[i],b=flattened[i+1],n=normals[i],m=normals[i+1],owner=owners[i];
        const end=length+Math.hypot(b[0]-a[0],b[1]-a[1])*.8;
        const points=[[a[0],a[1],length,0],[b[0],b[1],end,0],
          [b[0]+m[0]*.18,b[1]+m[1]*.18,end,1],[a[0]+n[0]*.18,a[1]+n[1]*.18,length,1]];
        const clipped=clipRect(points,owner.x,owner.y);
        const kind=(n[1]+m[1])>.6?1:(n[1]+m[1])<-.6?2:3;
        for(let j=1;j+1<clipped.length;j++)
          triangle(out,clipped[0],clipped[j],clipped[j+1],kind,0,owner.x,owner.y,owner.mask);
        length=end;
      }
    }
  }
  T.mesh = function(adapter,key) {
    const {w,h,masks,variants,method}=adapter,r=T.chunkRect(key,w,h),out=[],segments=[];
    let tiles=0;
    for(let y=r.y;y<r.y+r.h;y++)for(let x=r.x;x<r.x+r.w;x++) {
      const i=y*w+x,mask=masks[i];if(!mask)continue;tiles++;
      if(method!=="edge") {
        quad(out,[x,y],[x+1,y],[x+1,y+1],[x,y+1],-1,variants[i],x,y,mask);
        continue;
      }
      if(mask===15) {
        let end=x+1;
        while(end<r.x+r.w&&masks[y*w+end]===15)end++;
        quad(out,[x,y],[end,y],[end,y+1],[x,y+1],0,0,x,y,15);
        tiles+=end-x-1;x=end-1;continue;
      }
      for(const p of T.templates[mask].polygons) {
        const points=p.map(q=>[q[0]+x,q[1]+y]);
        for(let j=1;j+1<points.length;j++)triangle(out,points[0],points[j],points[j+1],0,0,x,y,mask);
      }
      for(const line of T.templates[mask].segments)
        segments.push({x,y,mask,points:line.map(p=>[p[0]+x,p[1]+y])});
    }
    if(method==="edge")warpedBands(out,segments,adapter);
    return {data:new Float32Array(out),vertices:out.length/stride,triangles:out.length/(stride*3),tiles,
      contourSegments:segments.length,rect:r};
  };
  T.stride=stride;
})(globalThis.TerrainFour);
