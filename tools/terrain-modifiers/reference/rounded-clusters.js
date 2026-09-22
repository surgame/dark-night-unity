/* 原生像素的圆钝岩簇轮廓。多尺度圆瓣与墙体距离场融合，再交给原材质着色。 */
(function(root) {
  'use strict';
  const defaults={depth:8,size:20,petal:3,density:90,variation:65};
  const limits={depth:[0,12],size:[8,30],petal:[2,6],density:[0,100],variation:[0,100]};
  const presets={balanced:{...defaults},subtle:{depth:3,size:13,petal:2,density:75,variation:60},
    bubbly:{depth:8,size:23,petal:4,density:100,variation:80},off:{...defaults,depth:0}};
  function normalize(input={}) {
    const out={};
    for(const [key,[lo,hi]] of Object.entries(limits)) {
      const v=input[key]??defaults[key];if(!Number.isFinite(v)||v<lo||v>hi)throw Error('Invalid cluster parameter: '+key);
      out[key]=Math.round(v);
    }
    return out;
  }
  function hash(x,y,s) {
    let v=Math.imul(x,374761393)^Math.imul(y,668265263)^Math.imul(s,1442695041);
    v=Math.imul(v^(v>>>13),1274126177);return ((v^(v>>>16))>>>0)/4294967296;
  }
  function distance(mask,w,h,target) {
    const out=new Float32Array(w*h),inf=w+h,diagonal=Math.SQRT2;
    for(let p=0;p<out.length;p++)out[p]=mask[p]===target?0:inf;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++) {
      const p=y*w+x;out[p]=Math.min(out[p],x?out[p-1]+1:inf,y?out[p-w]+1:inf,
        x&&y?out[p-w-1]+diagonal:inf,x+1<w&&y?out[p-w+1]+diagonal:inf);
    }
    for(let y=h-1;y>=0;y--)for(let x=w-1;x>=0;x--) {
      const p=y*w+x;out[p]=Math.min(out[p],x+1<w?out[p+1]+1:inf,y+1<h?out[p+w]+1:inf,
        x+1<w&&y+1<h?out[p+w+1]+diagonal:inf,x&&y+1<h?out[p+w-1]+diagonal:inf);
    }
    return out;
  }
  function smoothMin(a,b,k) {
    const t=Math.max(0,Math.min(1,.5+.5*(b-a)/k));
    return b*(1-t)+a*t-k*t*(1-t);
  }
  function build(source,w,h,seed,input={},top=56) {
    const cfg=normalize(input);if(source.length!==w*h)throw Error('Invalid cluster source');
    const mask=Uint8Array.from(source),added=new Uint8Array(w*h),parent=new Int32Array(w*h).fill(-1);
    const influence=new Float32Array(w*h),owner=new Int32Array(w*h).fill(-1),clusters=[],lobes=[];
    if(!cfg.depth||!cfg.density)return {mask,added,parent,influence,owner,clusters,lobes};
    const inside=distance(source,w,h,0),outside=distance(source,w,h,1),field=new Float32Array(w*h);
    for(let p=0;p<field.length;p++)field[p]=source[p]?-(inside[p]-.5):outside[p]-.5;
    const solid=(x,y)=>x>=0&&x<w&&y>=0&&y<h&&source[y*w+x]!==0,candidates=[];
    const variation=cfg.variation/100,density=cfg.density/100;
    for(let y=top+3;y<h-4;y++)for(let x=3;x<w-3;x++) {
      if(!solid(x,y)||solid(x,y+1)||!solid(x,y-2))continue;
      let above=0,below=0;
      for(let dx=-2;dx<=2;dx++)for(let dy=1;dy<=3;dy++){above+=solid(x+dx,y-dy)?1:0;below+=solid(x+dx,y+dy)?1:0;}
      if(above<10||below>6)continue;
      if(hash(x,y,seed+17)>density)continue;
      candidates.push({x,y,priority:hash(x,y,seed+29)});
    }
    candidates.sort((a,b)=>a.priority-b.priority||a.y-b.y||a.x-b.x);
    for(const a of candidates) {
      const {x,y}=a;
      const radius=cfg.size*.5*(1+(hash(x,y,seed+31)-.5)*.65*variation);
      const spacing=radius*(1.45+(1-density)*1.2);
      if(clusters.some(c=>Math.abs(x-c.x)<Math.max(spacing,c.radius*1.15)&&Math.abs(y-c.y)<8))continue;
      let clearance=0;while(y+clearance+1<h&&!solid(x,y+clearance+1)&&clearance<40)clearance++;
      const depth=Math.min(cfg.depth*(1-.4*variation*hash(x,y,seed+41)),Math.floor(clearance*.42));
      if(depth<1)continue;
      const group={x,y,radius,depth,lobes:[],roots:[]},index=clusters.length;
      // 主体宽圆肩部跨入旧墙体；下方小瓣互相搭接，避免细长吊坠和统一半圆。
      group.lobes.push({x,y:y-depth*.35,rx:radius*.90,ry:Math.max(2,depth*.65),group:index});
      const count=Math.max(3,Math.min(7,Math.round(radius*1.65/(cfg.petal*1.7))));
      for(let i=0;i<count;i++) {
        const u=count===1?0:(i/(count-1)*2-1),jitter=(hash(x+i,y,seed+53)-.5)*variation;
        const rx=Math.max(cfg.petal,depth*.32)*(1+jitter*.60);
        const ry=rx*(.84+hash(x+i,y,seed+61)*.23);
        const bottom=y+depth*(.98-.20*Math.abs(u))-(hash(x+i,y,seed+67)*variation*.8);
        group.lobes.push({x:x+u*radius*.74+jitter*1.2,y:bottom-ry,rx,ry,group:index});
      }
      const start=Math.max(1,Math.floor(x-radius-cfg.petal)),end=Math.min(w-2,Math.ceil(x+radius+cfg.petal));
      for(let xx=start;xx<=end;xx++) {
        let rootY=-1,best=Infinity;
        for(let yy=Math.max(top,y-4);yy<=Math.min(h-4,y+4);yy++)if(solid(xx,yy)&&!solid(xx,yy+1)&&solid(xx,yy-2)&&Math.abs(yy-y)<best){rootY=yy;best=Math.abs(yy-y);}
        if(rootY<0)continue;
        let free=0;while(rootY+free+1<h&&!solid(xx,rootY+free+1)&&free<cfg.depth+6)free++;
        const bottom=Math.min(rootY+cfg.depth,rootY+free-3,h-1);
        if(bottom<=rootY)continue;
        group.roots.push({x:xx,y:rootY,bottom});
        let reach=0;
        for(let yy=Math.max(top,rootY-10);yy<=bottom;yy++) {
          const p=yy*w+xx;
          let d=field[p],closest=Infinity,bestLobe=-1;
          for(let li=0;li<group.lobes.length;li++) {
            const l=group.lobes[li],q=Math.hypot((xx-l.x)/l.rx,(yy-l.y)/l.ry);
            const sd=(q-1)*Math.min(l.rx,l.ry);
            d=smoothMin(d,sd,.65);
            if(sd<closest){closest=sd;bestLobe=li;}
          }
          field[p]=d;
          const rootFade=Math.max(0,Math.min(1,(yy-(rootY-8))/5));
          const lateral=Math.max(0,Math.min(1,(radius+2-Math.abs(xx-x))/3));
          influence[p]=Math.max(influence[p],rootFade*lateral);
          if(closest<2&&bestLobe>=0)owner[p]=lobes.length+bestLobe;
          if(yy>rootY&&d<=0)reach=yy-rootY;
        }
        // 同列连续附着；每个新增像素都能追溯到生成前的墙体下缘。
        for(let dy=1;dy<=reach;dy++) {
          const p=(rootY+dy)*w+xx;if(source[p])continue;
          mask[p]=1;added[p]=1;parent[p]=rootY*w+xx;
        }
      }
      clusters.push(group);lobes.push(...group.lobes);
    }
    return {mask,added,parent,influence,owner,clusters,lobes};
  }
  const api={build,normalize,defaults,limits,presets,revision:'v16.1-rounded-clusters-1'};
  if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.RoundedClusters=api;
})(typeof globalThis!=='undefined'?globalThis:this);
