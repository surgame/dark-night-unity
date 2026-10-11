(function(){
  'use strict';
  const D=window.DN;
  D.nearClue=function(s){
    const origin={x:s.player.x,y:s.player.y-20},angle=D.aim(s);
    return (s.map.clues||[]).filter(q=>{
      const distance=D.dist(origin,q);
      if(distance>100||!D.lineClear(s.map,origin,q,10))return false;
      const delta=Math.atan2(q.y-origin.y,q.x-origin.x)-angle;
      return distance<45||(s.flashlight&&(Math.cos(delta)>Math.cos(Math.PI/4)||distance<76));
    }).sort((a,b)=>D.dist(origin,a)-D.dist(origin,b))[0];
  };
  D.inspectClue=function(s){
    const q=D.nearClue(s);if(!q)return false;
    if(!q.discovered){q.discovered=true;s.metrics.cluesSeen++;D.record(s,'clue-discovered',q.note,{clueId:q.id,type:q.type});}
    D.notify(s,q.note);return true;
  };
})();
