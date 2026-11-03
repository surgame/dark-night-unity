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
