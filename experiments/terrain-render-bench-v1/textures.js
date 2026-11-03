(function(root){
  "use strict";
  const M=root.TerrainBench;
  function canvas(w,h){const c=document.createElement("canvas");c.width=w;c.height=h;return c;}
  function triangle(ctx,image,uv,p){
    const [a,b,c]=uv,[v,w,z]=p,x1=b[0]-a[0],y1=b[1]-a[1],x2=c[0]-a[0],y2=c[1]-a[1],det=x1*y2-x2*y1;
    if(Math.abs(det)<1e-8)return;
    const ax=((w[0]-v[0])*y2-(z[0]-v[0])*y1)/det,cx=((z[0]-v[0])*x1-(w[0]-v[0])*x2)/det;
    const ay=((w[1]-v[1])*y2-(z[1]-v[1])*y1)/det,cy=((z[1]-v[1])*x1-(w[1]-v[1])*x2)/det;
    ctx.save();ctx.beginPath();ctx.moveTo(...v);ctx.lineTo(...w);ctx.lineTo(...z);ctx.closePath();ctx.clip();
    ctx.transform(ax,ay,cx,cy,v[0]-ax*a[0]-cx*a[1],v[1]-ay*a[0]-cy*a[1]);
    ctx.drawImage(image,0,0);ctx.restore();
  }
  function cell(ctx,art,m,x,y,px=16){
    ctx.save();ctx.translate(x*px,y*px);
    const path=new Path2D();
    for(const p of M.polygon[m]){p.forEach((v,i)=>i?path.lineTo(v[0]*px,v[1]*px):path.moveTo(v[0]*px,v[1]*px));path.closePath();}
    ctx.clip(path);ctx.translate(-x*px,-y*px);ctx.fillStyle=ctx.createPattern(art.rock,"repeat");
    ctx.save();ctx.scale(px*4/art.rock.width,px*4/art.rock.height);ctx.fillRect(x*art.rock.width/4,y*art.rock.height/4,art.rock.width/4,art.rock.height/4);ctx.restore();
    for(const e of M.edges(m,x,y)){
      const img=e.ny>=0?art.floor:art.ceiling,p=M.edgeQuad(e).map(v=>v.map(n=>n*px));
      let start=e.a[0]/4;start-=Math.floor(start);
      const u0=start*img.width,u1=u0+e.len/4*img.width;
      // A doubled strip preserves source phase at the texture wrap.
      const uv=[[u0,0],[u1,0],[u1,img.height],[u0,img.height]];
      triangle(ctx,art.double[e.ny>=0?0:1],[uv[0],uv[1],uv[2]],[p[0],p[1],p[2]]);
      triangle(ctx,art.double[e.ny>=0?0:1],[uv[0],uv[2],uv[3]],[p[0],p[2],p[3]]);
    }
    ctx.restore();
  }
  async function load(sources){
    const art={};
    await Promise.all(Object.entries(sources).map(async([key,src])=>{
      const img=new Image();img.src=src;await img.decode();art[key]=img;
    }));
    art.double=[art.floor,art.ceiling].map(img=>{const c=canvas(img.width*2,img.height),ctx=c.getContext("2d");ctx.drawImage(img,0,0);ctx.drawImage(img,img.width,0);return c;});
    const atlas=canvas(288,288),ctx=atlas.getContext("2d");ctx.imageSmoothingEnabled=false;
    for(let m=0;m<16;m++)for(let v=0;v<4;v++)for(let u=0;u<4;u++){
      const tile=canvas(16,16),tc=tile.getContext("2d");tc.translate(-u*16,-v*16);cell(tc,art,m,u,v);
      const id=m*16+v*4+u,x=id%16*18+1,y=Math.floor(id/16)*18+1;
      ctx.drawImage(tile,x,y);ctx.drawImage(tile,0,0,16,1,x,y-1,16,1);ctx.drawImage(tile,0,15,16,1,x,y+16,16,1);
      ctx.drawImage(tile,0,0,1,16,x-1,y,1,16);ctx.drawImage(tile,15,0,1,16,x+16,y,1,16);
    }
    art.atlas=atlas;return art;
  }
  function bake(a,k,art,target){
    const c=target||canvas(512,512),ctx=c.getContext("2d"),box=M.bounds(k);ctx.clearRect(0,0,512,512);ctx.imageSmoothingEnabled=false;
    for(let y=box.y;y<box.y+32;y++)for(let x=box.x;x<box.x+32;x++){
      const m=M.mask(a,x,y);if(!m)continue;
      const id=m*16+(y%4)*4+x%4;
      ctx.drawImage(art.atlas,id%16*18+1,Math.floor(id/16)*18+1,16,16,(x-box.x)*16,(y-box.y)*16,16,16);
    }
    return c;
  }
  root.TerrainArt={load,bake,canvas,cell};
})(globalThis);
