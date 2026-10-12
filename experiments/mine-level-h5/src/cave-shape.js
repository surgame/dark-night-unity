(function(){
  'use strict';
  const D=window.DN;
  D.carveCave=function(m,x,y,w,h){
    for(let j=Math.max(1,y);j<Math.min(D.rows-1,y+h);j++)
      for(let i=Math.max(1,x);i<Math.min(D.cols-1,x+w);i++)m.tiles[j*D.cols+i]=0;
  };
  D.carveCaveRoom=function(m,r,index){
    if(r.id==='camp'){for(let x=3;x<25;x++){const h=5+Math.round(3*Math.sin((x-3)/22*Math.PI));D.carveCave(m,x,16-h,1,h);}return;}
    const radius=r.radius||4,height=r.height||5;
    for(let x=r.x-radius;x<=r.x+radius;x++){
      const t=Math.abs(x-r.x)/(radius+1),floor=r.floor+(Math.abs(x-r.x)<=2?0:Math.round(t*t));
      const h=Math.max(4,Math.round(3+height*Math.sqrt(1-t*t)+Math.sin(x*.8+index)*.45));
      D.carveCave(m,x,floor-h,1,h);
    }
  };
  D.carveCaveWalk=function(m,points,id){
    if(!Array.isArray(points)||points.length<2||points.some(p=>p.length!==2||!p.every(Number.isInteger)||p[0]<2||p[0]>D.cols-3||p[1]<8||p[1]>D.rows-5))throw Error(m.id+' '+id+'：通路须有至少两个界内整数格点');
    const samples=[];
    for(let j=1;j<points.length;j++){
      const a=points[j-1],b=points[j],dx=b[0]-a[0];
      if(!dx||Math.abs(b[1]-a[1])>Math.abs(dx)*2)throw Error(m.id+' '+id+'：步行段须横向延伸，每格落差不超过两格');
      const sign=Math.sign(dx);
      for(let k=0;k<=Math.abs(dx);k++){
        const x=a[0]+k*sign,floor=Math.round(a[1]+(b[1]-a[1])*k/Math.abs(dx));
        const ceiling=floor-Math.round(5.7+Math.sin(x*.39+m.seed)*.6);
        D.carveCave(m,x,ceiling,1,floor-ceiling);
        if(!samples.length||samples.at(-1).x!==x)samples.push({x,floor,ceiling});
      }
    }
    return samples;
  };
  D.caveFloorAt=function(m,x,wanted){
    let best=null,score=Infinity;
    for(let y=Math.max(3,wanted-12);y<Math.min(D.rows-2,wanted+15);y++){
      if(m.tiles[y*D.cols+x]&&!m.tiles[(y-1)*D.cols+x]&&!m.tiles[(y-2)*D.cols+x]&&Math.abs(y-wanted)<score){best=y;score=Math.abs(y-wanted);}
    }
    return best;
  };
})();
