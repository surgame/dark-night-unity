(function (C) {
  "use strict";
  const storage="dark-nights-dualgrid-cave-whitebox-v1";
  C.encodeCuts=function (world) {
    const bytes=new Uint8Array(Math.ceil(world.data.length/8));
    for (const i of world.cuts) bytes[i>>3]|=1<<(i&7);
    let text="";for (const b of bytes) text+=String.fromCharCode(b);
    return btoa(text);
  };
  C.decodeCuts=function (value) {
    if (typeof value!=="string" || value.length>7000) return [];
    try {
      const bytes=atob(value),result=[];
      for (let i=0;i<bytes.length;i++) {
        const b=bytes.charCodeAt(i);
        for (let j=0;j<8;j++) if (b>>j&1) result.push(i*8+j);
      }
      return result;
    } catch (_) { return []; }
  };
  C.readSaved=function () {
    const widget=rootState(window.openai?.widgetState);
    if (widget) return widget;
    try {return rootState(JSON.parse(localStorage.getItem(storage)));} catch (_) {return null;}
  };
  function rootState(value) {
    const state=value?.privateContent||value;
    return state?.kind==="dg-cave-v1"?state:null;
  }
  C.rootState=rootState;
  C.saveState=function (state,world) {
    const saved={kind:"dg-cave-v1",mode:state.mode,grid:state.grid,labels:state.labels,
      site:state.site,visited:[...state.visited],cuts:C.encodeCuts(world),
      player:{x:state.player.x,y:state.player.y,facing:state.player.facing},
      camera:{x:state.camera.x,y:state.camera.y,scale:state.camera.scale}};
    C.lastWrittenState=JSON.stringify(saved);
    if (window.openai?.setWidgetState) {
      window.openai.setWidgetState({
        modelContent:{view:"dualgrid-cave-exploration",mode:state.mode,site:state.site,
          visited:state.visited.size,dug:world.cuts.size,rounding:"16 analytic arc templates"},
        privateContent:saved
      }).catch(()=>{});
    } else {
      try {localStorage.setItem(storage,JSON.stringify(saved));} catch (_) {}
    }
  };
})(globalThis.CaveWhitebox);
