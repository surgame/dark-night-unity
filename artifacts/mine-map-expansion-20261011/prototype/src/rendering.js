(function () {
  'use strict';
  const D = window.DN;
  const canvas = document.getElementById('game'), ctx = canvas.getContext('2d');
  const dark = document.createElement('canvas'), dc = dark.getContext('2d');
  D.view = { canvas, ctx, width: 960, height: 600 };
  function rect(c, color, x, y, w, h) { c.fillStyle = color; c.fillRect(Math.round(x), Math.round(y), w, h); }
  D.bakeMap = function (m) {
    const out = m.background || document.createElement('canvas');
    if(out.width!==D.width||out.height!==D.height){out.width=D.width;out.height=D.height;}
    const c = out.getContext('2d'), T = D.T;
    const gradient = c.createLinearGradient(0, 0, 0, D.height);
    gradient.addColorStop(0, '#243c43'); gradient.addColorStop(.45, '#20343b'); gradient.addColorStop(1, '#26323a');
    c.fillStyle = gradient; c.fillRect(0, 0, D.width, D.height);
    for (let y = 0; y < D.rows; y++) for (let x = 0; x < D.cols; x++) {
      const n = D.hash(x,y), px = x * T, py = y * T;
      if (m.tiles[y * D.cols + x]) {
        const t = y / D.rows;
        const variation=m.natural||m.organic?n%3:n%9;
        c.fillStyle = `rgb(${43 + variation + Math.floor(t*12)},${60 + variation - Math.floor(t*4)},${66 + variation})`;
        c.fillRect(px, py, T, T);
        if (n % 3 === 0) rect(c, '#182a3070', px + 3, py + 6 + n % 9, 11 + n % 9, 2);
        if (n % 7 === 0) rect(c, '#a4b1a124', px + 7, py + 3, 3, 3);
        const empty = (i,j) => i >= 0 && i < D.cols && j >= 0 && j < D.rows && !m.tiles[j * D.cols + i];
        if (empty(x,y-1)) {
          rect(c, y < 22 ? '#698077' : '#727f78', px, py, T, 4);
          rect(c, '#24343b', px + n % 10, py + 5, 13, 3);
          if (y < 22 && n % 2 === 0) { rect(c,'#729778',px+5,py-4,2,5); rect(c,'#5c8574',px+3,py-2,6,2); }
        }
        if (empty(x,y+1)) { rect(c,'#78908788',px,py+T-3,T,3); if (n%3===0) rect(c,'#5c7977',px+n%14+3,py+T,4,8+n%8); }
        if (empty(x-1,y)) { rect(c,'#71898066',px,py,3,T); rect(c,'#17282e',px+4,py+7,2,11); }
        if (empty(x+1,y)) rect(c,'#86928555',px+T-3,py,3,T);
        if(m.tiles[y*D.cols+x]===2){
          rect(c,'#92968266',px+1,py+1,T-2,T-2);
          c.strokeStyle='#c5c1a1';c.lineWidth=1;c.beginPath();c.moveTo(px+8,py+2);c.lineTo(px+14,py+8);c.lineTo(px+9,py+15);c.lineTo(px+17,py+22);c.stroke();
          rect(c,'#d0c9aa',px+4,py+7,2,2);
        }
      } else {
        if (n % 4 === 0) { rect(c,'#a2bdad08',px+3,py+5,15,2); rect(c,'#0f202529',px+7,py+13,12,5); }
        if (n % 23 === 0) {
          c.strokeStyle = '#92aaa514'; c.lineWidth = 2; c.beginPath(); c.moveTo(px,py); c.lineTo(px+7,py+13); c.lineTo(px+4,py+24); c.stroke();
        }
      }
    }
    // 墙面分层纹理只作外观，不改变碰撞；可走轮廓仍来自同一 tile 数据。
    (m.natural||m.organic?[]:m.rooms).forEach(r => {
      c.strokeStyle = '#14262c55'; c.lineWidth = 3;
      for (let y = r.y + 2; y < r.y + r.h - 1; y += 3) {
        c.beginPath(); c.moveTo(r.x*T+10,y*T); c.lineTo((r.x+r.w)*T-13,y*T-8); c.stroke();
      }
      const floor = (r.y+r.h)*T;
      for (let i=0;i<r.w;i+=3) {
        const x=(r.x+i+.5)*T;
        if(!D.tileSolid(m,x,floor+1))continue;
        rect(c,'#607575',x,floor-5,8,5); rect(c,'#879188',x+2,floor-7,3,3);
      }
    });
    if(m.natural){
      // 小段弯曲岩纹沿真实洞顶起伏，不按房间边框画整齐水平线。
      c.strokeStyle='#11272b40';c.lineWidth=2;
      for(let x=27;x<125;x+=7){
        const y=(m.ceilProfile[x]+2)*T;c.beginPath();c.moveTo(x*T,y);
        c.quadraticCurveTo((x+2)*T,y-7,(x+4)*T,(m.ceilProfile[x+4]+2)*T);c.stroke();
      }
    }
    // 可推断的真实线索：隐藏矿室方向的冷色晶脉。
    (m.id==='baseline'?[[71,31],[70,33],[68,36],[67,38],[66,39]]:[]).forEach(([x,y],i) => {
      rect(c,'#6eb9b0',x*T+10,y*T+5,3,13); rect(c,'#89d2c5',x*T+13,y*T+9,7,3);
      rect(c,'#587d79',x*T+8,y*T+15,4,4);
    });
    m.background = out;
    m.backgroundDirty=false;
  };
  function ship(c) {
    const x = 5 * D.T, y = 16 * D.T;
    rect(c,'#263942',x,y-61,130,51); rect(c,'#58717a',x+8,y-65,115,8);
    rect(c,'#869b9d',x+23,y-78,75,13); rect(c,'#4b626e',x+31,y-83,58,6);
    rect(c,'#142630',x+24,y-50,45,26); rect(c,'#a3cbd0',x+29,y-47,35,16);
    rect(c,'#597581',x+92,y-47,20,36); rect(c,'#e6c88a',x+101,y-40,5,10);
    rect(c,'#94a496',x-12,y-14,157,5); rect(c,'#26343a',x+13,y-9,22,9); rect(c,'#26343a',x+104,y-9,22,9);
    rect(c,'#8b9b8c',x+131,y-9,26,4); rect(c,'#829b92',x+153,y-5,22,4);
    c.fillStyle = '#bbcab9'; c.font = '10px Consolas'; c.fillText('DN-01',x+25,y-26);
    const lx=11*D.T, ly=12.5*D.T;
    rect(c,'#71968c',lx-2,ly-5,4,26); rect(c,'#f1d595',lx-5,ly-6,10,6);
  }
  function ore(c, o, selected) {
    const color = o.kind==='gold'?'#ddb879':'#8ab8bc', bright=o.kind==='gold'?'#f2dba3':'#bfddda';
    if (!o.amount) { rect(c,'#56696c',o.x-13,o.y+3,26,5); return; }
    const size=8+Math.min(7,o.amount), x=o.x, y=o.y;
    c.fillStyle=color; c.beginPath(); c.moveTo(x-size,y+6);c.lineTo(x-size+3,y-8);c.lineTo(x-4,y-12);c.lineTo(x,y-3);c.lineTo(x+6,y-14);c.lineTo(x+size,y-6);c.lineTo(x+size,y+7);c.closePath();c.fill();
    rect(c,bright,x-8,y-7,4,9);rect(c,bright,x+5,y-10,3,8);rect(c,'#435556',x-3,y+1,8,6);
    if (selected) { c.strokeStyle='#e2d9b5'; c.lineWidth=1; c.strokeRect(x-size-4,y-19,size*2+8,31); }
    if (o.progress>0) { rect(c,'#16272d',x-16,y+13,32,3); rect(c,color,x-16,y+13,32*o.progress,3); }
  }
  function gate(c, g, selected) {
    if (g.open) return;
    rect(c,g.id==='hatch'?'#40545a':'#607471',g.x,g.y,g.w,g.h);
    if (g.id==='hatch') {
      rect(c,'#8aa197',g.x,g.y, g.w,4); rect(c,'#323b3a',g.x+12,g.y+8,g.w-24,7); return;
    }
    c.strokeStyle=g.id==='secret'?'#8ed0c0':'#d5b98b'; c.lineWidth=2; c.beginPath();
    c.moveTo(g.x+8,g.y); c.lineTo(g.x+15,g.y+g.h*.28); c.lineTo(g.x+6,g.y+g.h*.48);c.lineTo(g.x+17,g.y+g.h*.72);c.lineTo(g.x+10,g.y+g.h);c.stroke();
    for(let y=g.y+8;y<g.y+g.h;y+=23) rect(c,'#354b4a',g.x+2,y,8,3);
    if(selected){c.strokeStyle='#e8c27f';c.strokeRect(g.x-2,g.y-2,g.w+4,g.h+4);}
  }
  function rails(c,s) {
    const active=s.metrics.gateOpened.includes('shortcut');
    c.strokeStyle=active?'#98b5a0':'#4b5c5d';c.lineWidth=2;
    c.beginPath();c.moveTo(20.5*D.T,16*D.T);c.lineTo(20.5*D.T,75*D.T);c.lineTo(123*D.T,75*D.T-2);c.lineTo(123*D.T,67*D.T);c.stroke();
    for(let x=21;x<123;x+=2) rect(c,active?'#81948a':'#445553',x*D.T,75*D.T-2,4,5);
    for(let y=18;y<74;y+=2) { rect(c,'#647676',19*D.T,y*D.T,3,D.T);rect(c,'#647676',22*D.T,y*D.T,3,D.T); }
    const cart=s.lift?{x:s.player.x,y:s.player.y}:{x:123*D.T,y:67*D.T};
    rect(c,active?'#789995':'#485e63',cart.x-20,cart.y-5,40,7);
    rect(c,'#34474f',cart.x-17,cart.y+2,8,5);rect(c,'#34474f',cart.x+9,cart.y+2,8,5);
    rect(c,active?'#b4d5b7':'#657f76',cart.x-22,cart.y-28,3,24);rect(c,active?'#b4d5b7':'#657f76',cart.x+19,cart.y-28,3,24);
  }
  function passages(c,s){
    s.map.platforms.forEach(r=>{rect(c,'#83a09a',r.x,r.y,r.w,4);for(let x=r.x+4;x<r.x+r.w;x+=12)rect(c,'#405958',x,r.y,2,4);});
    s.map.ladders.forEach(l=>{const color=l.playerPlaced?'#d3bd83':'#adab83';rect(c,color,l.x-12,l.top-20,2,l.bottom-l.top+20);rect(c,color,l.x+10,l.top-20,2,l.bottom-l.top+20);for(let y=l.top-18;y<l.bottom;y+=14)rect(c,l.playerPlaced?'#b1c1a3':'#7a9690',l.x-12,y,24,3);});
    s.map.nests.forEach(n=>{if(!n.opened)return;rect(c,'#251f1e',n.x-11,n.y-10,22,20);rect(c,'#b67557',n.x-5,n.y-4,3,3);rect(c,'#b67557',n.x+4,n.y-4,3,3);});
    if(s.target&&(s.target.type==='wall'||s.target.type==='bedrock')){
      const o=s.target.object,x=o.x-12,y=o.y-12;
      c.strokeStyle=s.target.type==='wall'?'#e4c990':'#839394';c.lineWidth=1;c.strokeRect(x-1,y-1,26,26);
      if(s.target.type==='wall'&&o.progress>0){rect(c,'#203239',x,y-6,24,3);rect(c,'#d3bd8d',x,y-6,24*Math.min(1,o.progress),3);}
    }
  }
  function traces(c,s){
    (s.map.clues||[]).forEach(q=>{
      if(q.type==='glint'||q.type==='vein'){
        for(let i=0;i<4;i++){const dx=(q.dx||0)*i*5,dy=(q.dy||0)*i*5;rect(c,i%2?'#bdcab0':'#80beb3',q.x+dx-1,q.y+dy-1,2,3);}
      }else if(q.type==='wind'){
        for(let i=0;i<5;i++){const t=(s.time*.3+i*.2)%1;rect(c,'#b2c5bd66',q.x+(q.dx||0)*t*26+Math.sin(i*2.4)*7,q.y+(q.dy||-1)*t*26,2,2);}
      }else if(q.type==='echo'){
        c.strokeStyle='#a4b9a055';c.lineWidth=1;
        for(let i=0;i<2;i++){c.beginPath();c.arc(q.x,q.y,5+(s.time*8+i*7)%16,-.7,.7);c.stroke();}
      }else{for(let i=0;i<4;i++)rect(c,'#a69d80',q.x+i*4-6,q.y+Math.sin(i)*2,3,2);}
      if(q.discovered){c.strokeStyle='#b7c7ad66';c.strokeRect(q.x-10,q.y-10,20,20);}
    });
  }
  function worm(c,w,s) {
    const awake=w.mode!=='sleep', wiggle=awake?Math.sin(s.time*18)*2:Math.sin(s.time*2);
    const color=awake?'#b78162':'#5f7368';
    for(let i=0;i<5;i++){const x=w.x-22+i*9,y=w.y-8-Math.sin(i+wiggle)*3;rect(c,color,x,y,11,8);rect(c,awake?'#d7a178':'#809586',x+2,y,6,2);}
    rect(c,awake?'#f0b575':'#bbbc8c',w.x+18,w.y-13,4,4);
    if(w.mode==='warning'){c.fillStyle='#e4b58b';c.font='11px "Microsoft YaHei"';c.fillText('敲击……',w.x-24,w.y-31);c.strokeStyle='#d79b6977';c.beginPath();c.arc(w.x,w.y-8,25+Math.sin(s.time*10)*6,Math.PI,Math.PI*2);c.stroke();}
    if(w.mode==='rush'){c.strokeStyle='#d4a078';c.beginPath();c.moveTo(w.x,w.y-4);c.lineTo(w.x-(w.target>w.x?35:-35),w.y-4);c.stroke();}
  }
  function player(c,s) {
    const p=s.player, step=p.grounded?Math.sin(s.time*15)*Math.min(4,Math.abs(p.vx)/42):2;
    if(p.invulnerable>0&&Math.floor(s.time*12)%2===0)c.globalAlpha=.5;
    const x=Math.round(p.x),y=Math.round(p.y);
    rect(c,'#202d35',x-7,y-13,14,11);rect(c,'#7c9fa0',x-7,y-23,14,12);
    rect(c,'#617f84',x-11,y-22,5,13);rect(c,'#b7ad84',x-6,y-22,3,9);
    rect(c,'#acb5a2',x-6,y-31,13,9);rect(c,'#d3c09b',x-5,y-27,11,7);
    rect(c,'#9dbeac',x-8,y-32,16,5);rect(c,'#e2cc8c',x+2*p.facing-2,y-31,4,3);
    rect(c,'#33454e',x-7,y-5+Math.max(0,step),6,5-Math.max(0,step));rect(c,'#33454e',x+2,y-5+Math.max(0,-step),6,5-Math.max(0,-step));
    const angle=D.aim(s),hand=D.lampOrigin(s,angle),fx=hand.x,fy=hand.y;
    c.save();c.translate(fx,fy);c.rotate(angle);rect(c,'#d8c38d',-3,-3,10,5);rect(c,s.flashlight?'#f6e0ac':'#64767e',6,-3,3,5);c.restore();
    if(s.tool===1){const swing=s.mouse.down&&s.target?Math.sin(s.time*(s.fast?21:13))*.7:0;c.save();c.translate(x+9*p.facing,y-17);c.rotate((p.facing>0?-.6:2.5)+swing);rect(c,'#b6a380',0,-1,16,3);rect(c,'#b2c5c5',11,-6,5,12);c.restore();}
    if(s.tool===3){rect(c,'#b97b66',x+6*p.facing,y-17,6,10);rect(c,'#d1b57e',x+6*p.facing,y-19,4,3);}
    if(s.tool===4){rect(c,'#c6b388',x+8*p.facing-5,y-23,2,18);rect(c,'#c6b388',x+8*p.facing+3,y-23,2,18);for(let i=0;i<3;i++)rect(c,'#8da79b',x+8*p.facing-5,y-21+i*6,10,2);}
    if(s.jetpack){rect(c,'#728a89',x-11*p.facing-3,y-23,6,15);if(p.fuel<1){rect(c,'#253941',x-12,y-41,24,3);rect(c,'#c2bc82',x-12,y-41,p.fuel*24,3);}}
    c.globalAlpha=1;
  }
  D.lampOrigin=function(s,angle){const p=s.player,ray=D.ray(s.map,p.x,p.y-20,angle,14);return{x:ray.x,y:ray.y};};
  function light(c,s,source,start,end,range,power) {
    const x=source.x-s.camera.x,y=source.y-s.camera.y;
    if(x+range<0||x-range>D.view.width||y+range<0||y-range>D.view.height)return;
    const n=Math.max(32,Math.ceil((end-start)*25)),points=[];
    for(let i=0;i<=n;i++) {const a=start+(end-start)*i/n;const v=D.ray(s.map,source.x,source.y,a,range);points.push({x:v.x-s.camera.x,y:v.y-s.camera.y});}
    c.save();c.beginPath();c.moveTo(x,y);points.forEach(p=>c.lineTo(p.x,p.y));c.closePath();c.clip();
    const g=c.createRadialGradient(x,y,0,x,y,range);g.addColorStop(0,`rgba(0,0,0,${power})`);g.addColorStop(.5,`rgba(0,0,0,${power*.9})`);g.addColorStop(.88,`rgba(0,0,0,${power*.43})`);g.addColorStop(1,'rgba(0,0,0,0)');
    c.fillStyle=g;c.fillRect(x-range,y-range,range*2,range*2);c.restore();
  }
  function lighting(s) {
    const W=D.view.width,H=D.view.height;
    if(dark.width!==W||dark.height!==H){dark.width=W;dark.height=H;}
    dc.clearRect(0,0,W,H);dc.globalCompositeOperation='source-over';dc.fillStyle=s.zone===0?'rgba(3,10,17,.90)':'rgba(3,8,16,.96)';dc.fillRect(0,0,W,H);
    dc.globalCompositeOperation='destination-out';
    s.map.lights.forEach(l=>light(dc,s,l,0,Math.PI*2,l.radius,l.power));
    const a=D.aim(s),origin=D.lampOrigin(s,a);
    light(dc,s,{x:s.player.x,y:s.player.y-16},0,Math.PI*2,s.flashlight?76:41,s.flashlight?.64:.4);
    if(s.flashlight){
      light(dc,s,origin,a-Math.PI/4,a+Math.PI/4,336,.8);
      light(dc,s,origin,a-Math.PI/5,a+Math.PI/5,329,.7);
      light(dc,s,origin,a-Math.PI/6,a+Math.PI/6,318,.4);
    }
    ctx.drawImage(dark,0,0);
  }
  function ladderPreview(s){
    if(s.tool!==4||!s.started||s.panel||!s.target)return;
    const l=s.target.object,x=l.x-s.camera.x,top=l.top-s.camera.y,bottom=l.bottom-s.camera.y;
    ctx.save();ctx.globalAlpha=l.valid?.8:.55;ctx.strokeStyle=l.valid?'#a8e2c7':'#d9a28f';ctx.lineWidth=2;ctx.setLineDash([5,4]);
    ctx.strokeRect(x-12,top-20,24,bottom-top+20);ctx.setLineDash([]);
    for(let y=top-12;y<bottom;y+=14){ctx.beginPath();ctx.moveTo(x-10,y);ctx.lineTo(x+10,y);ctx.stroke();}
    if(l.valid){rect(ctx,'#142930',x-21,bottom+7,42,4);rect(ctx,'#a8e2c7',x-21,bottom+7,42*Math.min(1,s.buildProgress),4);}
    ctx.restore();
  }
  D.render=function(s,dt) {
    if(s.map.backgroundDirty)D.bakeMap(s.map);
    const p=s.player,W=D.view.width,H=D.view.height;
    const targetX=D.clamp(p.x-W*.40,0,D.width-W),targetY=D.clamp(p.y-H*.62,0,D.height-H);
    const smooth=s.started?1-Math.exp(-8*dt):1;
    s.camera.x+=(targetX-s.camera.x)*smooth;s.camera.y+=(targetY-s.camera.y)*smooth;
    s.camera.x=Math.round(s.camera.x);s.camera.y=Math.round(s.camera.y);
    ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,W,H);ctx.save();
    const shake=s.shake>0?Math.sin(s.time*83)*3*s.shake:0;
    ctx.translate(-s.camera.x+shake,-s.camera.y);ctx.drawImage(s.map.background,0,0);
    if(s.map.id==='baseline')rails(ctx,s);
    passages(ctx,s);ship(ctx);traces(ctx,s);
    s.map.signs.forEach(sign=>{rect(ctx,'#7a8c82',sign.x-2,sign.y-33,4,33);rect(ctx,'#334b4d',sign.x-39,sign.y-43,78,18);ctx.fillStyle='#b8c3a4';ctx.font='9px "Microsoft YaHei"';ctx.textAlign='center';ctx.fillText(sign.text,sign.x,sign.y-31);ctx.textAlign='left';});
    s.map.ores.forEach(o=>{
      if(!D.oreExposed(s.map,o))return;
      if(o.buried){ctx.save();ctx.beginPath();ctx.rect(Math.floor(o.x/D.T)*D.T,Math.floor(o.y/D.T)*D.T,D.T,D.T);ctx.clip();}
      ore(ctx,o,s.target&&s.target.object===o);
      if(o.buried)ctx.restore();
    });
    s.map.gates.forEach(g=>gate(ctx,g,s.target&&s.target.object===g));
    s.map.worms.forEach(w=>worm(ctx,w,s));
    s.bags.forEach(b=>{rect(ctx,'#c7a476',b.x-10,b.y-15,20,14);rect(ctx,'#917963',b.x-5,b.y-19,10,5);});
    s.particles.forEach(p=>{ctx.globalAlpha=Math.min(1,p.life*3);rect(ctx,p.color,p.x,p.y,3,3);});ctx.globalAlpha=1;
    player(ctx,s);ctx.restore();lighting(s);ladderPreview(s);
    if(s.mouse.active&&!s.panel){const x=s.mouse.x,y=s.mouse.y;ctx.strokeStyle=s.target?'#edd4a0':'#9aafb177';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(x-7,y);ctx.lineTo(x-3,y);ctx.moveTo(x+3,y);ctx.lineTo(x+7,y);ctx.moveTo(x,y-7);ctx.lineTo(x,y-3);ctx.moveTo(x,y+3);ctx.lineTo(x,y+7);ctx.stroke();}
    if(s.started&&!s.panel){ctx.fillStyle='#9cb3bd';ctx.font='10px Consolas';ctx.fillText(s.jetpack?'JET ASSIST READY':'MOUSE → LIGHT / F',22,H-90);}
  };
  D.drawBlueprint=function(s) {
    const canvas=document.getElementById('blueprint'),c=canvas.getContext('2d'),T=7,ox=37,oy=22;
    c.clearRect(0,0,canvas.width,canvas.height);rect(c,'#0b171e',0,0,canvas.width,canvas.height);
    for(let y=0;y<D.rows;y++)for(let x=0;x<D.cols;x++)if(!s.map.tiles[y*D.cols+x])rect(c,y<24?'#4e7168':y<39?'#78704c':'#724d4c',ox+x*T,oy+y*T,T,T);
    if(s.map.id==='baseline'){c.save();c.setLineDash([6,5]);c.lineWidth=3;c.strokeStyle='#9bc3c2';c.beginPath();c.moveTo(ox+123*T,oy+66*T);c.lineTo(ox+123*T,oy+74*T);c.lineTo(ox+20.5*T,oy+74*T);c.lineTo(ox+20.5*T,oy+15*T);c.stroke();c.restore();}
    else{
      for(let y=0;y<D.rows;y++)for(let x=0;x<D.cols;x++)if(s.map.tiles[y*D.cols+x]===2)rect(c,'#adad87',ox+x*T,oy+y*T,T,T);
      c.lineWidth=2;s.map.ladders.forEach(l=>{c.strokeStyle=l.playerPlaced?'#f0d292':'#a3c9c3';c.beginPath();c.moveTo(ox+l.x/D.T*T,oy+l.top/D.T*T);c.lineTo(ox+l.x/D.T*T,oy+l.bottom/D.T*T);c.stroke();});
    }
    s.map.gates.forEach(g=>{if(g.id==='hatch')return;c.fillStyle=g.open?'#8bc4a2':'#edc68a';c.fillRect(ox+g.x/D.T*T,oy+g.y/D.T*T,Math.max(3,g.w/D.T*T),g.h/D.T*T);});
    const spoilers=document.getElementById('hiddenMapToggle').checked;
    s.map.ores.forEach(o=>{if(!spoilers&&!D.oreExposed(s.map,o))return;c.fillStyle=o.kind==='gold'?'#f0d292':'#b1dbd9';c.beginPath();c.arc(ox+o.x/D.T*T,oy+o.y/D.T*T,3,0,Math.PI*2);c.fill();});
    s.map.nests.forEach(n=>{if(!spoilers&&!n.opened)return;c.fillStyle='#ec987e';c.font='14px Consolas';c.fillText('N',ox+n.x/D.T*T-4,oy+n.y/D.T*T);});
    (s.map.clues||[]).forEach(q=>{c.fillStyle=q.discovered?'#f0d292':'#9fbcac';c.beginPath();c.arc(ox+q.x/D.T*T,oy+q.y/D.T*T,2.5,0,Math.PI*2);c.fill();});
    const labels=s.map.rooms.map(r=>[r.name,r.x+1,r.y+2]);
    c.font='13px "Microsoft YaHei"';c.fillStyle='#e0e7dc';labels.forEach(([text,x,y])=>c.fillText(text,ox+x*T,oy+y*T));
    c.font='12px "Microsoft YaHei"';c.fillStyle='#aad2cb';if(s.map.id==='baseline'){c.fillText('S ↓ 晶脉线索',ox+62*T,oy+34*T);c.fillText('回程升降井',ox+12*T,oy+51*T);c.fillText('爆破 → 矿车回程',ox+86*T,oy+73*T);}else c.fillText(`${s.map.stats.junctions} 个岔口 / ${s.map.stats.deadEnds} 个尽头 / ${s.map.stats.loops} 个回环`,ox,oy+73*T);
    s.map.worms.forEach(w=>{c.fillStyle='#e5a384';c.font='13px "Microsoft YaHei"';c.fillText('≈',ox+w.anchor/D.T*T,oy+w.floor/D.T*T-9);});
    c.fillStyle='#f2f3df';c.beginPath();c.arc(ox+s.player.x/D.T*T,oy+s.player.y/D.T*T-4,5,0,Math.PI*2);c.fill();c.fillStyle='#f1e7bd';c.font='12px "Microsoft YaHei"';c.fillText('你的位置',ox+s.player.x/D.T*T+9,oy+s.player.y/D.T*T-2);
    c.fillStyle='#8ca6b4';c.font='12px Consolas';c.fillText('≈ 岩鸣虫   ◆ 已露矿簇   浅色方块：可挖岩层   N：已开巢穴 / 剧透',ox,canvas.height-20);
  };
  D.drawLayoutThumbnail=function(canvas,m){
    const c=canvas.getContext('2d'),scale=Math.min(canvas.width/D.cols,canvas.height/D.rows);
    rect(c,'#0c1a21',0,0,canvas.width,canvas.height);
    for(let y=0;y<D.rows;y++)for(let x=0;x<D.cols;x++)if(!m.tiles[y*D.cols+x])rect(c,y<24?'#699289':y<45?'#9d8e63':'#a87770',x*scale,y*scale,Math.ceil(scale),Math.ceil(scale));
    c.fillStyle='#efddb0';c.beginPath();c.arc(8.5*scale,16*scale,3,0,Math.PI*2);c.fill();
  };
})();
