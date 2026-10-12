(function () {
  'use strict';
  const D=window.DN,T=D.T;
  function clear(m,x,feet){return D.blockingRects(m,x-8,feet-30,16,30).length===0;}
  function support(m,x,y){return D.pointSolid(m,x-6,y+.5)&&D.pointSolid(m,x+6,y+.5);}
  D.planLadder=function(s){
    const p=s.player,m=s.map,cursor=s.mouse.active?{x:s.mouse.x+s.camera.x,y:s.mouse.y+s.camera.y}:{x:p.x,y:p.y-8*T};
    const x=(Math.floor(cursor.x/T)+.5)*T;
    const bad=reason=>({id:'ladder-invalid',valid:false,x,top:p.y-2*T,bottom:p.y,reason});
    if(s.ladderKits<=0)return bad('梯具用完了：X 回收已有梯子，或在入口补充');
    if(m.ladders.length>=40)return bad('本图梯子已达 40 架，请先回收');
    if(!p.grounded)return bad('先在落脚点站稳，再搭梯子');
    if(Math.abs(x-p.x)>72)return bad('请走近要搭梯的位置');
    let top,bottom,down=cursor.y>p.y+10;
    if(down){
      top=Math.round(p.y/T)*T;bottom=null;
      if(!clear(m,x,top))return bad('井口被岩壁挡住');
      for(let y=top+T;y<=top+14*T;y+=T){if(!clear(m,x,y))break;if(support(m,x,y)){bottom=y;break;}}
      if(bottom===null||bottom-top<2*T)return bad('下方没有可连接的落脚点，或岩壁阻断');
      if(!D.lineClear(m,{x:p.x,y:p.y-20},{x,y:top-20}))return bad('搭梯位置被岩壁遮挡');
    }else{
      bottom=null;
      for(let y=Math.floor((p.y-20)/T)*T;y<=p.y+2*T;y+=T)if(clear(m,x,y)&&support(m,x,y)){bottom=y;break;}
      if(bottom===null)return bad('梯脚需要落在附近地面上');
      if(!D.lineClear(m,{x:p.x,y:p.y-20},{x,y:bottom-20}))return bad('梯脚被岩壁遮挡');
      const desired=D.clamp(Math.round(cursor.y/T)*T,bottom-14*T,bottom-2*T);
      top=bottom;
      for(let y=bottom-T;y>=desired;y-=T){if(!clear(m,x,y))break;top=y;}
      if(bottom-top<2*T)return bad('上方空间不足两格，先挖开岩壁');
    }
    if(m.ladders.some(l=>Math.abs(l.x-x)<18&&Math.min(l.bottom,bottom)-Math.max(l.top,top)>12))return bad('这里已经有梯子');
    return{id:`ladder-${x}-${top}-${bottom}`,valid:true,x,top,bottom,down,reason:`${down?'向下':'向上'}搭 ${Math.round((bottom-top)/T)} 格梯子`};
  };
  D.placeLadder=function(s,plan){
    const current=D.planLadder(s);if(!plan.valid||!current.valid||current.id!==plan.id)return false;
    s.map.ladders.push({id:`player-ladder-${s.metrics.laddersBuilt+1}`,x:plan.x,top:plan.top,bottom:plan.bottom,playerPlaced:true});
    s.ladderKits--;s.metrics.laddersBuilt++;D.record(s,'ladder-built','亲手搭了一架梯子。',{x:plan.x,top:plan.top,bottom:plan.bottom});D.notify(s,'梯子搭好了：W/S 攀爬，落脚后 X 可回收。');return true;
  };
  D.recoverLadder=function(s){
    if(s.panel||s.lift||!s.started)return;
    if(!s.player.grounded){D.notify(s,'先回到落脚点，不能在悬空时拆梯子。');return;}
    const p=s.player,l=s.map.ladders.find(l=>l.playerPlaced&&Math.hypot(l.x-p.x,D.clamp(p.y,l.top,l.bottom)-p.y)<75);
    if(!l){D.notify(s,'附近没有可回收的自建梯子。');return;}
    s.map.ladders.splice(s.map.ladders.indexOf(l),1);s.ladderKits++;s.metrics.laddersRecovered++;D.record(s,'ladder-recovered','回收了一架自己搭的梯子。');D.notify(s,'梯具已收回，可在别处重新搭建。');
  };
})();
