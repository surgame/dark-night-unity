(function () {
  "use strict";
  const P = globalThis.CaveBlockDesign = {};
  P.kind = "cave-macroblock-design-v1";
  P.modules = [
    {id:"M01",name:"洞口缓入",w:24,h:16,form:"entry",stage:"首做",ports:2,goal:"从洞檐收进地下，保留来侧观察和集合位置。",slots:"地标、观察位；不附带商店或交货。",relation:"进入／折返",source:"自然入口与地下探索"},
    {id:"M02",name:"浅层试采台",w:24,h:12,form:"shelf",stage:"首做",ports:2,goal:"较宽站位接触路边小矿，建立首次收益和返程地标。",slots:"路边小矿、少量壁内延伸。",relation:"试采／折返",source:"路边收益与浅层试采"},
    {id:"M03",name:"长檐窄路",w:24,h:12,form:"low",stage:"首做",ports:2,goal:"长岩檐压缩视野，穿过后由下一洞室展开。",slots:"小凹窝、独立草根锚点。",relation:"穿行",source:"原窄洞的空间节奏"},
    {id:"M04",name:"错层岩台厅",w:40,h:24,form:"hall",stage:"首做",ports:3,goal:"大空腔、错位岩台与上窝形成可记忆的大地标。",slots:"观察台、可选高窝、局部露矿。",relation:"深入＋可选侧支",source:"错层岩台厅"},
    {id:"M05",name:"天然岔洞",w:32,h:20,form:"fork",stage:"首做",ports:3,goal:"来侧即可观察主路和侧支，两条路各有明确用途。",slots:"方向地标、支路颈口。",relation:"主路／支路选择",source:"天然岔路与关键发现"},
    {id:"M06",name:"层状转坡",w:32,h:16,form:"slope",stage:"第二批",ports:2,goal:"沿长岩层自然变高，组合直段、缓坡和岩肩。",slots:"层状轮廓、坡旁小凹处。",relation:"变高穿行；尺寸待校准",source:"层状天然洞穴"},
    {id:"M07",name:"浅沟跳口",w:24,h:16,form:"gap",stage:"第二批",ports:2,goal:"先看到短断口、两岸站位与浅沟，再作动作选择。",slots:"起落位、失误落区。",relation:"预期跳跃；未验证",source:"浅口试跳"},
    {id:"M08",name:"露头矿窝",w:24,h:16,form:"pocket",stage:"首做",ports:1,goal:"短支线有明确尽头，露头关联岩壁后的厚矿体。",slots:"露矿、壁内矿体、转身位。",relation:"单口折返",source:"壁内盲盒矿脉"},
    {id:"M09",name:"壁后富矿厅",w:40,h:24,form:"mine",stage:"首做",ports:2,goal:"厚围岩中的矿脉潜力形成继续挖还是继续走的选择。",slots:"露头、独立厚矿层、暂歇位。",relation:"深入／停留采集",source:"富矿与沿路收益递进"},
    {id:"M10",name:"侧巢矿厅",w:32,h:20,form:"nest",stage:"第二批",ports:1,goal:"矿体与侧巢平行，前兆、出虫和退避都有独立空间。",slots:"关联矿体、侧巢、前兆区。",relation:"单口侧支；启用依规则",source:"CL-04联合矿巢"},
    {id:"M11",name:"塌方咽喉",w:32,h:20,form:"collapse",stage:"第二批",ports:2,goal:"在收束段表现支撑与变化区，记录可能封堵的返程。",slots:"支撑点、落石与堆积范围。",relation:"初始／变化后分别记录",source:"CL-02人工塌方"},
    {id:"M12",name:"深部折返厅",w:40,h:24,form:"end",stage:"首做",ports:1,goal:"端厅是深入的诱因和折返点，保留停留观察空间。",slots:"深部矿体、折返地标。",relation:"单口折返",source:"深部矿碗与原路返回"},
    {id:"M13",name:"高处悬台",w:32,h:28,form:"high",stage:"扩展",ports:1,goal:"可选高处带来携带工具的探索收益。",slots:"梯具预留、上部观察位。",relation:"梯具候选；不自动生成梯子",source:"玩家携带梯具高处"},
    {id:"M14",name:"菌根凹厅",w:32,h:20,form:"fungus",stage:"扩展",ports:2,goal:"用根幕、菌丛和凹窝构成另一种空间地标。",slots:"草菌根部与动画包络。",relation:"穿行／观察",source:"动态生态探索"},
    {id:"M15",name:"泉瀑干台",w:40,h:24,form:"water",stage:"扩展",ports:2,goal:"高处来水、瀑身和水盆独立，较高干台承担行进。",slots:"水源、流段、落点、排水。",relation:"干台穿行；水机制未定",source:"泉瀑与干湿空间"},
    {id:"M16",name:"天然竖裂",w:24,h:32,form:"rift",stage:"扩展",ports:2,goal:"少量可选大落差，来侧观察位和深裂分开表达。",slots:"观察台、落差风险区。",relation:"特殊风险；不默认插主干",source:"天然竖裂候选"}
  ];
  P.byId = id => P.modules.find(m => m.id === id) || P.modules[0];
  P.connectors = [
    ["C01","同高短接","同剖面直接相接，或插4H／8H短段"],
    ["C02","缓坡接段","用8H～12H消化已允许的小高度差"],
    ["C03","宽窄渐变","S↔N／S↔W，匹配完整轮廓"],
    ["C04","岩肩折接","作者转折段；不任意旋转房间"],
    ["C05","支路颈口","连接岔口与可选侧支"],
    ["C06","围岩封口","关闭未使用开口"]
  ];
  P.placements = [
    {id:"M01",x:0,y:20},{id:"M02",x:30,y:24},{id:"M03",x:60,y:24},
    {id:"M05",x:90,y:20},{id:"M04",x:130,y:18},{id:"M11",x:178,y:20},
    {id:"M09",x:218,y:18},{id:"M12",x:266,y:18},
    {id:"M08",x:90,y:-12},{id:"M10",x:138,y:58}
  ];
  P.edges = [
    ["M01","M02"],["M02","M03"],["M03","M05"],["M05","M04"],
    ["M04","M11"],["M11","M09"],["M09","M12"],["M05","M08"],["M04","M10"]
  ];
  P.shape = function (m) {
    const w=m.w,h=m.h,base=h*.72;
    const xs=[0,1,w*.14,w*.25,w*.38,w*.51,w*.64,w*.76,w*.88,w-1,w];
    const ceil=[0,0,-.2,-1,-2.2,-2.5,-1.5,-1.8,-.8,0,0].map(v=>base-2.5+v);
    const floor=[0,0,-.15,-.2,.15,.4,.15,-.2,0,0,0].map(v=>base+v);
    if (["hall","mine","end","water","high"].includes(m.form)) {
      [2,3,4,5,6,7,8].forEach(i=>ceil[i]-=[1.5,4,5,4.5,5.2,3,1][i-2]);
    }
    if (m.form==="entry") [2,3,4].forEach(i=>ceil[i]-=2.5);
    if (m.form==="low") [3,4,5,6,7].forEach(i=>ceil[i]=base-[2,1.7,1.5,1.75,2.1][i-3]);
    if (m.form==="slope") [2,3,4,5,6,7,8,9,10].forEach(i=>{
      const dy=-(i-2)*.25; floor[i]+=dy;ceil[i]+=dy;
    });
    if (["hall","high"].includes(m.form)) [3,4,5,6,7,8].forEach(i=>floor[i]+=[0,1,1,-1,-2,-1][i-3]);
    if (m.form==="gap") [4,5,6].forEach(i=>floor[i]+=[2,3,1.5][i-4]);
    if (m.form==="rift") [4,5,6].forEach(i=>floor[i]+=[7,8,6][i-4]);
    if (m.form==="end") [3,4,5,6,7].forEach(i=>floor[i]+=[1,2,3,2,1][i-3]);
    if (m.form==="nest" || m.form==="pocket") [3,4,5,6,7].forEach(i=>ceil[i]-=1.5);
    if (m.form==="collapse") [4,5,6].forEach(i=>ceil[i]=base-[2,1.8,2.1][i-4]);
    if (m.ports===1) {
      ceil[9]=base-1.7;floor[9]=base-.3;
      ceil[10]=base-.8;floor[10]=base-.8;
    }
    let floorPoints=xs.map((x,i)=>[x,floor[i]]);
    if(m.form==="hall")floorPoints=[
      [0,base],[1,base],[7,base],[9,base+.7],[14,base+.7],
      [14.7,base-1],[20,base-1],[20.7,base-2.8],[27,base-2.8],
      [28,base-1.4],[33,base-1.4],[36,base],[w-1,base],[w,base]
    ];
    if(m.form==="gap")floorPoints=[
      [0,base],[1,base],[7,base-.15],[9,base],[9.2,base+2.7],
      [13.5,base+2.7],[14,base],[19,base-.15],[w-1,base],[w,base]
    ];
    if(m.form==="low")floorPoints=[
      [0,base],[1,base],[5,base],[8,base+.15],[15,base+.15],[19,base],[w-1,base],[w,base]
    ];
    if(m.form==="slope")floorPoints=[
      [0,base],[1,base],[6,base],[11,base-1],[15,base-1],
      [20,base-2],[25,base-2],[w-1,base-2],[w,base-2]
    ];
    return {roof:xs.map((x,i)=>[x,ceil[i]]),floor:floorPoints};
  };
  P.branch = function(m) {
    if(m.ports!==3)return null;
    const w=m.w,base=m.h*.72;
    return {
      roof:[[w*.37,base-4],[w*.54,base-6.5],[w*.74,base-7],[w-1,base-6],[w,base-6]],
      floor:[[w*.37,base-2.5],[w*.54,base-5],[w*.74,base-5.5],[w-1,base-4.5],[w,base-4.5]]
    };
  };
  P.contents = function (m) {
    const s=P.shape(m),g=m.h*.72;
    const result=[];
    if (["shelf","pocket","mine","nest","end"].includes(m.form)) {
      result.push({type:"ore",x:m.w*.7,y:g+1.4,r:1.2});
      result.push({type:"ore",x:m.w*.78,y:g+3.3,r:2});
    }
    if(m.form==="nest")result.push({type:"nest",x:m.w*.86,y:g-.5,r:1.3});
    if(m.form==="collapse")result.push({type:"event",x:m.w*.38,y:g-3,w:m.w*.27,h:5});
    if(m.form==="water")result.push({type:"water",x:m.w*.54,y:g-8,w:2,h:8});
    if(m.form==="fungus")result.push({type:"plant",x:m.w*.62,y:g,r:1.5});
    if(m.form==="high")result.push({type:"tool",x:m.w*.6,y:g-5,w:1,h:5});
    return result;
  };
}());
