(function(root){
  "use strict";
  const api=root.ShapeStudy;
  // Ported from TerrainShapeGeometry.Build/Edge; screen Y is the inverse of local Y-up.
  function buildFlags(cells,width,height){
    const flags=new Uint8Array(cells.length);
    for(let y=2;y<height-2;y++)for(let x=2;x<width-2;x++){
      const i=y*width+x;if(!cells[i]||flags[i])continue;
      const above=!cells[i-width],below=!cells[i+width],left=!cells[i-1],right=!cells[i+1];
      if(above&&!below&&left!==right){
        if(left&&cells[i+1]&&!cells[i+1-width]){flags[i]=3;flags[i+1]=4;}
        else if(right&&cells[i-1]&&!cells[i-1-width]&&!flags[i-1]){flags[i-1]=5;flags[i]=6;}
        else flags[i]=left?1:2;
      }else if(below&&!above&&left!==right){
        if(left&&cells[i+1]&&!cells[i+1+width]&&!flags[i+1]){flags[i]=11;flags[i+1]=12;}
        else if(right&&cells[i-1]&&!cells[i-1+width]&&!flags[i-1]){flags[i-1]=9;flags[i]=10;}
        else flags[i]=left?8:7;
      }
    }
    return flags;
  }
  function edge(shape,x){
    const v=shape>=7?shape-6:shape;
    return v===1?x:v===2?1-x:v===3?x*.5:v===4?.5+x*.5:v===5?1-x*.5:v===6?.5-x*.5:1;
  }
  function slopePolygon(shape){
    if(!shape)return[[0,0],[1,0],[1,1],[0,1]];
    const a=1-edge(shape,0),b=1-edge(shape,1);
    return shape>=7?[[0,0],[1,0],[1,b],[0,a]]:[[0,a],[1,b],[1,1],[0,1]];
  }
  function boundary(polygons){
    const edges=new Map(),gcd=(a,b)=>b?gcd(b,a%b):a;
    polygons.forEach(poly=>poly.forEach((p,i)=>{
      const q=poly[(i+1)%poly.length],a=p.map(v=>Math.round(v*2)),b=q.map(v=>Math.round(v*2));
      const dx=b[0]-a[0],dy=b[1]-a[1],n=gcd(Math.abs(dx),Math.abs(dy));if(!n)return;
      for(let j=0;j<n;j++){
        const first=[a[0]+dx*j/n,a[1]+dy*j/n],last=[a[0]+dx*(j+1)/n,a[1]+dy*(j+1)/n];
        const key=[first.join(","),last.join(",")].sort().join("/");
        if(edges.has(key))edges.delete(key);
        else edges.set(key,[{x:first[0]/2,y:first[1]/2},{x:last[0]/2,y:last[1]/2}]);
      }
    }));
    return [...edges.values()];
  }
  function makeHybrid(pair){
    const width=api.W+1,height=api.H+1,cells=Uint8Array.from(pair.hybridSamples,v=>v>0?1:0);
    const flags=buildFlags(cells,width,height),polys=[];
    cells.forEach((solid,i)=>{
      if(!solid)return;
      const x=i%width-.5,y=Math.floor(i/width)-.5;
      polys.push(slopePolygon(flags[i]).map(p=>[p[0]+x,p[1]+y]));
    });
    return {width:api.W,height:api.H,plan:pair.plan,base:pair.hybridBase,tiles:pair.hybridBase.tiles,
      check:pair.hybridBase.check,geometry:{contours:[]},polys,hybridEdges:boundary(polys),
      cells,flags,cellWidth:width,source:"WFC frozen occupancy + existing 12 slope formulas"};
  }
  Object.assign(api,{makeHybrid,buildFlags,edge,slopePolygon});
})(globalThis);
