(function(){
  'use strict';
  const D=window.DN,builders=new Map(),ids=new Set();
  D.layouts=[];
  D.registerMapBuilder=function(kind,create){
    if(builders.has(kind)||typeof create!=='function')throw Error('重复或无效地图生成器：'+kind);
    builders.set(kind,create);
  };
  D.registerLayout=function(definition){
    if(!definition||!/^[a-z][a-z0-9-]*$/.test(definition.id)||!definition.title||!builders.has(definition.kind))throw Error('无效地图定义：'+(definition&&definition.id));
    if(ids.has(definition.id))throw Error('重复地图 ID：'+definition.id);
    ids.add(definition.id);D.layouts.push(Object.freeze(definition));
  };
  D.createMap=function(id='baseline'){
    const definition=D.layouts.find(c=>c.id===id);
    if(!definition)throw Error('未知地图：'+id);
    return builders.get(definition.kind)(definition);
  };
  D.registerMapBuilder('baseline',definition=>({...D.createBaselineMap(),id:definition.id,title:definition.title}));
})();
