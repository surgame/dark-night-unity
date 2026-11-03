(function(root){
  "use strict";
  const api=root.FlatCave;
  function seam(width,height,cost) {
    let previous=new Float64Array(height),next=new Float64Array(height);
    const parent=new Int16Array(width*height);
    for(let y=0;y<height;y++)previous[y]=cost(0,y);
    for(let x=1;x<width;x++){
      for(let y=0;y<height;y++){
        let best=Infinity,from=y;
        for(let dy=-2;dy<=2;dy++){
          const py=y+dy;if(py<0||py>=height)continue;
          const value=previous[py]+Math.abs(dy)*.025;
          if(value<best){best=value;from=py;}
        }
        next[y]=best+cost(x,y);parent[x*height+y]=from;
      }
      [previous,next]=[next,previous];
    }
    let y=0;for(let i=1;i<height;i++)if(previous[i]<previous[y])y=i;
    const result=new Int16Array(width);
    for(let x=width-1;x>=0;x--){result[x]=y;y=parent[x*height+y];}
    return result;
  }
  function rockBoundary(data,width,height,kind) {
    const from=kind==="floor"?700:45,to=kind==="floor"?910:170,step=3;
    const columns=Math.ceil(width/step),rows=Math.floor((to-from)/step)+1;
    const path=seam(columns,rows,(x,y)=>{
      const at=((from+y*step)*width+Math.min(width-1,x*step))*4;
      const luminance=(data[at]*.2126+data[at+1]*.7152+data[at+2]*.0722)/255;
      return luminance+Math.abs(y-rows*.45)/rows*.025;
    });
    const boundary=new Int16Array(width);
    for(let x=0;x<width;x++){
      const p=x/step,i=Math.floor(p),j=Math.min(columns-1,i+1);
      boundary[x]=Math.round(from+(path[i]*(1-p+i)+path[j]*(p-i))*step);
    }
    for(let x=0;x<width;x++)for(let y=0;y<height;y++)
      if(kind==="floor"?y>boundary[x]:y<boundary[x])data[(y*width+x)*4+3]=0;
    return boundary;
  }
  function quilted(image,size) {
    const overlap=96,c=api.canvas(size+overlap,size+overlap),ctx=c.getContext("2d");
    ctx.drawImage(image,0,0,c.width,c.height);
    const source=ctx.getImageData(0,0,c.width,c.height),sw=c.width;
    const colorCost=(a,b)=>{
      let difference=0;for(let k=0;k<3;k++)difference+=Math.abs(source.data[a+k]-source.data[b+k])/765;
      const inkA=(source.data[a]+source.data[a+1]+source.data[a+2])/765;
      const inkB=(source.data[b]+source.data[b+1]+source.data[b+2])/765;
      return difference*.6+Math.min(inkA,inkB)*.6;
    };
    const vertical=seam(c.height,overlap,(y,x)=>colorCost((y*sw+x)*4,(y*sw+x+size)*4));
    const h=api.canvas(size,size+overlap),hc=h.getContext("2d"),horizontal=hc.createImageData(h.width,h.height);
    for(let y=0;y<h.height;y++)for(let x=0;x<size;x++){
      const sx=x<overlap&&x<=vertical[y]?x+size:x;
      horizontal.data.set(source.data.subarray((y*sw+sx)*4,(y*sw+sx)*4+4),(y*size+x)*4);
    }
    // A mismatched join becomes a narrow ink joint, never a soft second rock face.
    for(let y=0;y<h.height;y++)for(let dx=-1;dx<=1;dx++){
      const x=vertical[y]+dx;if(x<0||x>=size)continue;
      const at=(y*size+x)*4;
      for(let k=0;k<3;k++)horizontal.data[at+k]=Math.min(horizontal.data[at+k],42);
    }
    const lower=seam(size,overlap,(x,y)=>{
      const a=(y*size+x)*4,b=((y+size)*size+x)*4;let diff=0;
      for(let k=0;k<3;k++)diff+=Math.abs(horizontal.data[a+k]-horizontal.data[b+k])/765;
      const inkA=(horizontal.data[a]+horizontal.data[a+1]+horizontal.data[a+2])/765;
      const inkB=(horizontal.data[b]+horizontal.data[b+1]+horizontal.data[b+2])/765;
      return diff*.6+Math.min(inkA,inkB)*.6;
    });
    const result=api.canvas(size,size),rc=result.getContext("2d"),pixels=rc.createImageData(size,size);
    for(let y=0;y<size;y++)for(let x=0;x<size;x++){
      const sy=y<overlap&&y<=lower[x]?y+size:y;
      pixels.data.set(horizontal.data.subarray((sy*size+x)*4,(sy*size+x)*4+4),(y*size+x)*4);
    }
    for(let x=0;x<size;x++)for(let dy=-1;dy<=1;dy++){
      const y=lower[x]+dy;if(y<0||y>=size)continue;
      const a=(lower[x]*size+x)*4,b=((lower[x]+size)*size+x)*4;
      let difference=0;for(let k=0;k<3;k++)difference+=Math.abs(horizontal.data[a+k]-horizontal.data[b+k])/765;
      for(let k=0;k<3;k++)pixels.data[(y*size+x)*4+k]=Math.min(pixels.data[(y*size+x)*4+k],42);
    }
    rc.putImageData(pixels,0,0);return result;
  }
  function periodic(image,size) {
    const c=api.canvas(size*2,size*2),ctx=c.getContext("2d");
    for(let y=0;y<2;y++)for(let x=0;x<2;x++){
      ctx.save();ctx.translate(x?size*2:0,y?size*2:0);ctx.scale(x?-1:1,y?-1:1);
      ctx.drawImage(image,0,0,size,size);ctx.restore();
    }
    return c;
  }
  Object.assign(api,{rockBoundary,periodicOpaque:periodic});
})(globalThis);
