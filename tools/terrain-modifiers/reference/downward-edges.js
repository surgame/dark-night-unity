/* v16.1 下缘附着岩齿。只从原始朝下边界向 +Y 增补；不递归长出二次岩齿。 */
(function(root) {
  'use strict';
  function hash(x,y,s) {
    let a=Math.imul(x,374761393)^Math.imul(y,668265263)^Math.imul(s,1442695041);
    a=Math.imul(a^(a>>>13),1274126177);
    return ((a^(a>>>16))>>>0)/4294967296;
  }
  function maskOf(rgba) {
    const mask=new Uint8Array(rgba.length/4);
    for(let p=0;p<mask.length;p++)mask[p]=rgba[p*4+3]>0?1:0;
    return mask;
  }
  function patch(x,y,s) {
    const ix=Math.floor(x),iy=Math.floor(y),tx=x-ix,ty=y-iy;
    const u=tx*tx*(3-2*tx),v=ty*ty*(3-2*ty);
    return (hash(ix,iy,s)*(1-u)+hash(ix+1,iy,s)*u)*(1-v)
      +(hash(ix,iy+1,s)*(1-u)+hash(ix+1,iy+1,s)*u)*v;
  }
  const defaults={length:4,density:45,width:11,sharpness:15,variation:70};
  const limits={length:[0,24],density:[0,100],width:[3,19],sharpness:[0,100],variation:[0,100]};
  const presets={subtle:{...defaults},rock:{length:7,density:65,width:11,sharpness:30,variation:80},
    ice:{length:15,density:100,width:9,sharpness:90,variation:85},off:{...defaults,length:0}};
  function normalize(options={}) {
    const result={};
    for(const [key,[min,max]] of Object.entries(limits)) {
      const value=options[key]??defaults[key];
      if(!Number.isFinite(value)||value<min||value>max)throw new Error('Invalid gravity parameter: '+key);
      result[key]=Math.round(value);
    }
    return result;
  }
  function extend(source,w,h,seed,options={},top=56) {
    if(source.length!==w*h)throw new Error('Invalid gravity input');
    const cfg=normalize(options),variation=cfg.variation/100,sharpness=cfg.sharpness/100,density=cfg.density/100;
    const mask=Uint8Array.from(source),added=new Uint8Array(w*h),parent=new Int32Array(w*h).fill(-1),anchors=[];
    const solid=(x,y)=>x>=0&&x<w&&y>=0&&y<h&&source[y*w+x]!==0;
    if(!cfg.length||!cfg.density)return {mask,added,parent,anchors};
    const candidates=[];
    for(let y=top+3;y<h-7;y++)for(let x=5;x<w-5;x++) {
      if(!solid(x,y)||solid(x,y+1)||!solid(x,y-3))continue;
      let above=0,below=0;
      for(let dx=-3;dx<=3;dx++)for(let dy=1;dy<=3;dy++) {
        above+=solid(x+dx,y-dy)?1:0;below+=solid(x+dx,y+dy)?1:0;
      }
      // 识别朝下岩面：上方有连续岩体，下方以空气为主；排除直立侧壁。
      if(above<17||below>7)continue;
      const grouping=patch(x/39,y/31,seed+11),random=hash(x,y,seed+17),baseChance=.10+grouping*.17;
      // 低密度保留稀疏成片分布；65% 以上逐渐解除噪声否决。
      // 100% 时几何合格的下缘全部进入候选，不再存在固定禁生区域。
      const coverage=Math.max(0,(density-.65)/.35);
      const chance=baseChance*density+(1-baseChance*density)*coverage;
      if(grouping<.31*(1-coverage)||random>chance)continue;
      // 优先保留旧候选，再补以前被随机掩码排除的空段，减少已满意部位的跳变。
      const supplemental=grouping<.31||random>baseChance;
      candidates.push({x,y,supplemental,priority:hash(x,y,seed+29)});
    }
    candidates.sort((a,b)=>Number(a.supplemental)-Number(b.supplemental)||a.priority-b.priority||a.y-b.y||a.x-b.x);
    for(const a of candidates) {
      const {x,y}=a,radius=Math.max(1,Math.round((cfg.width-1)/2*(1+(hash(x,y,seed+31)-.5)*.55*variation)));
      if(x-radius<0||x+radius>=w)continue;
      const spacing=(7+Math.floor(hash(x,y,seed+37)*13))*(1.25-.25*density);
      if(anchors.some(b=>Math.abs(b.x-x)<spacing&&Math.abs(b.y-y)<12))continue;
      let clearance=0;
      while(y+clearance+1<h&&!solid(x,y+clearance+1)&&clearance<40)clearance++;
      const wanted=Math.max(1,Math.round(cfg.length*(1-variation*.72*(1-Math.pow(hash(x,y,seed+41),1.4)))));
      const length=Math.min(wanted,Math.floor(clearance*.48));
      if(length<1)continue;
      const tip=Math.round((hash(x,y,seed+43)-.5)*radius*variation),columns=[];
      for(let dx=-radius;dx<=radius;dx++) {
        const xx=x+dx;
        let rootY=-1;
        for(let yy=y-3;yy<=y+3;yy++)if(solid(xx,yy)&&!solid(xx,yy+1)&&solid(xx,yy-2))rootY=yy;
        if(rootY<0)continue;
        const side=dx<=tip?radius+tip+1:radius-tip+1;
        const flatTip=Math.round((1-sharpness)*radius*.60);
        const taper=Math.max(0,1-Math.max(0,Math.abs(dx-tip)-flatTip)/Math.max(1,side-flatTip));
        const exponent=.35+sharpness*.90+(hash(x,y,seed+47)-.5)*.25*variation;
        let reach=Math.round(length*Math.pow(taper,exponent));
        // 成片的 1–2 像素台阶和偏心尖端，保留宽根，不做等宽梳齿。
        if(length>4&&Math.abs(dx-tip)>flatTip+1)reach=Math.floor(reach/2)*2;
        if(reach<1)continue;
        let free=0;
        while(rootY+free+1<h&&!solid(xx,rootY+free+1)&&free<reach+3)free++;
        reach=Math.min(reach,free-3);
        if(reach<1)continue;
        columns.push({x:xx,rootY,reach});
      }
      if(columns.length<Math.max(2,Math.min(5,radius+1)))continue;
      let count=0;
      for(const col of columns)for(let dy=1;dy<=col.reach;dy++) {
        const p=(col.rootY+dy)*w+col.x;
        if(mask[p])continue;
        mask[p]=1;added[p]=1;parent[p]=col.rootY*w+col.x;count++;
      }
      if(count)anchors.push({x,y,radius,length,columns,count});
    }
    return {mask,added,parent,anchors};
  }
  const api={maskOf,extend,normalize,defaults,limits,presets,revision:'v16.1-downward-edges-3-density-coverage'};
  if(typeof module!=='undefined'&&module.exports)module.exports=api;
  else root.DownwardEdges=api;
})(typeof globalThis!=='undefined'?globalThis:this);
