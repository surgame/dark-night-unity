import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

// 输出 native apply_patch 输入；本工具只读 references，不直接写资源、基线或工作区。
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const source = path.join(root, 'references/h5-v0.4/src');
const repo = path.resolve(root, '../..');
const box = { window: {} }; vm.createContext(box);
const files = ['map.js','map-registry.js','natural-terrain.js','natural-content.js',
  'cave-shape.js','cave-terrain.js','cave-content.js', 'maps/06-rift.js', 'maps/07-karst.js',
  'maps/08-folded.js','maps/09-shaft-loop.js','maps/10-vein-maze.js'];
for (const file of files) vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), box);
const D = box.window.DN;
const room = (Id,Name,X,Floor,extra={}) => ({Id,Name,X,Floor,Radius:4,Height:5,Base:false,
  SurfaceOre:false,BuriedOre:true,...extra});
const edge = (Id,From,To,Points,SealX=-1) => ({Id,From,To,Points:Points.flat(),SealX});
const shaft = (Id,Lower,Upper,X,Top,Bottom,Side=-1) => ({Id,Lower,Upper,X,Top,Bottom,Side});

const additions = [
  {
    base:'folded',id:'folded-expanded',title:'08 折返洞网 · 扩展',seams:[117,60,15,18],
    rooms:[
      room('relay','碎岩接点',144,78),room('glint','东段矿庭',170,82,{Radius:5}),
      room('lower','第二井底',184,96),room('fork','东段三岔',200,98),
      room('look','回声观察口',225,102),room('rim','远端岩庭',254,108,{Radius:5,SurfaceOre:true}),
      room('turn','第二折返',238,122),room('court','左折矿庭',196,128,{Radius:5}),
      room('deep-relay','深层整顿点',214,138),room('deep','最终金矿庭',254,148,{Radius:5,Height:6}),
      room('high','碎光高洞',207,84,{SurfaceOre:true}),room('blind','矿屑盲支',159,98),
      room('neighbor','隔墙岩腔',241,86),room('wind','回风下洞',177,140),
      room('west','西段折岩',140,126),room('return','回程岩庭',151,108),
      room('blind-east','远端尽头',257,128,{Radius:3}),room('shell','碎壳侧室',173,124)
    ],
    paths:[
      edge('entry','base/core','relay',[[122,68],[134,74],[144,78]]),
      edge('east-court','relay','glint',[[144,78],[157,81],[170,82]]),
      edge('east-fork','lower','fork',[[184,96],[191,99],[200,98]]),
      edge('lookout','fork','look',[[200,98],[212,101],[225,102]]),
      edge('far-rim','look','rim',[[225,102],[239,107],[254,108]]),
      edge('second-turn','rim','turn',[[254,108],[246,118],[238,122]]),
      edge('left-court','turn','court',[[238,122],[217,129],[196,128]]),
      edge('deep-relay-path','court','deep-relay',[[196,128],[206,138],[214,138]]),
      edge('deep-court','deep-relay','deep',[[214,138],[235,146],[254,148]]),
      edge('high-branch','fork','high',[[200,98],[207,84]]),
      edge('blind-branch','lower','blind',[[184,96],[174,99],[159,98]]),
      edge('neighbor-seal','look','neighbor',[[225,102],[241,86]],233),
      edge('wind-bend','deep-relay','wind',[[214,138],[195,141],[177,140]]),
      edge('west-return','wind','west',[[177,140],[157,133],[140,126]]),
      edge('return-rise','west','return',[[140,126],[151,108]]),
      edge('return-seal','return','glint',[[151,108],[158,98],[170,82]],158),
      edge('far-blind','turn','blind-east',[[238,122],[250,128],[257,128]]),
      edge('shell-blind','court','shell',[[196,128],[182,124],[173,124]])
    ],shafts:[shaft('middle-east','lower','glint',178,82,96)]
  },
  {
    base:'shaft-loop',id:'shaft-loop-expanded',title:'09 环绕竖井 · 扩展',seams:[74,64,58,14],
    rooms:[
      room('entry','第二岩柱入口',132,82),room('mouth','岩柱分流口',158,84),
      room('left','左侧井底',181,98),room('left-court','绕柱矿庭',196,112),
      room('right','对岸弯洞',219,98),room('right-court','右岸岩庭',243,112),
      room('merge','下层汇合',222,126,{Radius:5}),room('rim','远岸侧庭',254,126,{Radius:5}),
      room('deep-relay','深部回声口',199,140),room('deep','深层金矿庭',254,148,{Radius:5,Height:6}),
      room('hidden','第二岩柱暗腔',207,84),room('blind','碎屑盲洞',150,112),
      room('wind','回风侧廊',160,132),room('west','回程接点',142,106),
      room('upper','高处透光洞',241,86,{SurfaceOre:true}),room('blind-deep','深层尽头',181,150,{Radius:3})
    ],
    paths:[
      edge('entry-route','base/core','entry',[[79,70],[102,75],[123,82],[132,82]]),
      edge('mouth-path','entry','mouth',[[132,82],[145,85],[158,84]]),
      edge('left-court-path','left','left-court',[[181,98],[188,108],[196,112]]),
      edge('right-view','mouth','right',[[158,84],[184,90],[206,94],[219,98]]),
      edge('right-court-path','right','right-court',[[219,98],[232,110],[243,112]]),
      edge('left-merge','left-court','merge',[[196,112],[209,123],[222,126]]),
      edge('right-merge','right-court','merge',[[243,112],[232,122],[222,126]]),
      edge('far-court','merge','rim',[[222,126],[237,128],[254,126]]),
      edge('deep-descent','merge','deep-relay',[[222,126],[211,139],[199,140]]),
      edge('deep-court','deep-relay','deep',[[199,140],[223,144],[254,148]]),
      edge('hidden-seal','right','hidden',[[219,98],[207,84]],214),
      edge('blind-left','left-court','blind',[[196,112],[174,114],[150,112]]),
      edge('wind-return','deep-relay','wind',[[199,140],[179,136],[160,132]]),
      edge('west-return','wind','west',[[160,132],[142,106]]),
      edge('return-mouth','west','mouth',[[142,106],[158,84]],150),
      edge('upper-branch','right','upper',[[219,98],[233,88],[241,86]]),
      edge('deep-blind','deep-relay','blind-deep',[[199,140],[189,150],[181,150]])
    ],shafts:[shaft('second-left-shaft','left','mouth',174,84,98)]
  },
  {
    base:'vein-maze',id:'vein-maze-expanded',title:'10 断脉迷宫 · 扩展',seams:[102,64,30,14],
    rooms:[
      room('entry','新中层入口',132,82),room('first','断脉岔口',154,86),
      room('lower','新井底',174,100),room('second','第二迷宫单元',192,104),
      room('third','岩柱定位庭',216,118,{Radius:5}),room('rim','远端矿庭',254,122,{Radius:5}),
      room('turn','深层折角',234,136),room('court','回声整顿庭',196,140,{Radius:5}),
      room('deep','最后金矿庭',254,148,{Radius:5,Height:6}),
      room('blind-one','矿屑尽头',148,100),room('hidden-one','近邻封岩洞',191,84),
      room('high','上方斜洞',220,102,{SurfaceOre:true}),room('blind-two','旧痕盲洞',169,120),
      room('neighbor','深层邻洞',245,104),room('west','回程下洞',157,138),
      room('return','回程中洞',140,116),room('short','湿痕接点',158,104),
      room('blind-deep','最后侧缝',172,151,{Radius:3})
    ],
    paths:[
      edge('entry-route','base/core','entry',[[107,70],[120,78],[132,82]]),
      edge('first-path','entry','first',[[132,82],[144,87],[154,86]]),
      edge('second-path','lower','second',[[174,100],[182,105],[192,104]]),
      edge('third-path','second','third',[[192,104],[204,117],[216,118]]),
      edge('rim-path','third','rim',[[216,118],[236,123],[254,122]]),
      edge('turn-path','rim','turn',[[254,122],[243,136],[234,136]]),
      edge('court-path','turn','court',[[234,136],[216,142],[196,140]]),
      edge('deep-path','court','deep',[[196,140],[226,145],[254,148]]),
      edge('first-blind','lower','blind-one',[[174,100],[161,102],[148,100]]),
      edge('hidden-first','first','hidden-one',[[154,86],[173,87],[191,84]],183),
      edge('high-branch','third','high',[[216,118],[220,112],[227,102],[220,102]]),
      edge('second-blind','second','blind-two',[[192,104],[180,118],[169,120]]),
      edge('neighbor-seal','rim','neighbor',[[254,122],[245,104]],249),
      edge('west-path','court','west',[[196,140],[177,142],[157,138]]),
      edge('return-path','west','return',[[157,138],[140,116]]),
      edge('short-path','return','short',[[140,116],[150,105],[158,104]]),
      edge('shortcut','short','lower',[[158,104],[174,100]],165),
      edge('deep-blind','west','blind-deep',[[157,138],[166,151],[172,151]])
    ],shafts:[shaft('new-middle-shaft','lower','first',164,86,100)]
  },
  {
    base:'rift',id:'rift-expanded',title:'06 裂谷竖井 · 扩展',seams:[122,60,10,18],
    rooms:[
      room('relay','碎岩过渡厅',146,78),room('observe','第二裂谷观察口',172,82,{Radius:5}),
      room('court','裂谷中庭',198,98,{Radius:5,Height:7}),room('wind','冷风转角',225,102),
      room('rim','远端洞庭',254,116,{Radius:5}),room('bend','下层折岩',229,132),
      room('deep-relay','深部整顿庭',199,140,{Radius:5}),room('deep','深层金矿庭',254,148,{Radius:5,Height:6}),
      room('high','漏光侧洞',193,68,{SurfaceOre:true}),room('neighbor','封岩邻洞',238,88),
      room('blind','旧矿屑尽头',159,102),room('return','侧向回廊',170,124),
      room('blind-deep','深层盲支',180,151,{Radius:3})
    ],
    paths:[
      edge('entry','base/cavern-4','relay',[[128,68],[137,75],[146,78]]),
      edge('observe-path','relay','observe',[[146,78],[158,83],[172,82]]),
      edge('court-path','observe','court',[[172,82],[185,97],[198,98]]),
      edge('wind-path','court','wind',[[198,98],[211,104],[225,102]]),
      edge('rim-path','wind','rim',[[225,102],[241,115],[254,116]]),
      edge('bend-path','rim','bend',[[254,116],[243,128],[229,132]]),
      edge('deep-relay-path','bend','deep-relay',[[229,132],[214,141],[199,140]]),
      edge('deep-path','deep-relay','deep',[[199,140],[226,146],[254,148]]),
      edge('neighbor-seal','wind','neighbor',[[225,102],[238,88]],232),
      edge('blind-path','court','blind',[[198,98],[181,103],[159,102]]),
      edge('side-return','deep-relay','return',[[199,140],[183,132],[170,124]]),
      edge('local-loop','return','court',[[170,124],[183,111],[198,98]]),
      edge('blind-deep-path','return','blind-deep',[[170,124],[184,143],[180,151]])
    ],shafts:[shaft('upper-rift','observe','high',184,68,82,1)]
  },
  {
    base:'karst',id:'karst-expanded',title:'07 溶洞回环 · 扩展',seams:[120,58,12,20],
    rooms:[
      room('relay','新岩腔入口',147,78),room('west','偏左矿庭',174,84,{Radius:5}),
      room('pillar','右侧岩柱',198,100),room('court','下层岩腔',225,110,{Radius:5}),
      room('rim','远端接点',254,120,{Radius:5}),room('bend','下层环转角',226,134),
      room('deep-relay','深部环口',196,142,{Radius:5}),room('deep','最后矿庭',254,148,{Radius:5,Height:6}),
      room('upper','新上层曲道',202,86,{SurfaceOre:true}),room('high','高洞岩台',232,84),
      room('neighbor','薄岩邻厅',242,100),room('blind','旧矿屑尽头',165,104),
      room('return','回环西口',159,136),room('seal','挖墙回程口',143,114),
      room('blind-deep','深层侧洞',180,151,{Radius:3}),room('wind','中段风庭',177,119)
    ],
    paths:[
      edge('entry','base/cavern-4','relay',[[128,66],[137,76],[147,78]]),
      edge('west-path','relay','west',[[147,78],[162,85],[174,84]]),
      edge('pillar-path','west','pillar',[[174,84],[184,97],[198,100]]),
      edge('court-path','pillar','court',[[198,100],[211,109],[225,110]]),
      edge('rim-path','court','rim',[[225,110],[241,121],[254,120]]),
      edge('bend-path','rim','bend',[[254,120],[239,133],[226,134]]),
      edge('deep-relay-path','bend','deep-relay',[[226,134],[211,143],[196,142]]),
      edge('deep-path','deep-relay','deep',[[196,142],[225,147],[254,148]]),
      edge('upper-court','upper','high',[[202,86],[219,85],[232,84]]),
      edge('upper-return','high','court',[[232,84],[243,100],[225,110]]),
      edge('neighbor-seal','court','neighbor',[[225,110],[242,100]],236),
      edge('blind-path','pillar','blind',[[198,100],[183,105],[165,104]]),
      edge('west-return','deep-relay','return',[[196,142],[175,139],[159,136]]),
      edge('seal-return','return','seal',[[159,136],[143,114]]),
      edge('return-relay','seal','relay',[[143,114],[156,96],[147,78]],149),
      edge('deep-blind','return','blind-deep',[[159,136],[171,151],[180,151]]),
      edge('wind-branch','pillar','wind',[[198,100],[186,118],[177,119]])
    ],shafts:[shaft('new-loop-shaft','pillar','upper',204,86,100,-1)]
  }
];

function encode(values){
  const runs=[];let value=values[0],count=0;
  for(const next of values){if(next!==value){runs.push(value+':'+count);value=next;count=0;}count++;}
  runs.push(value+':'+count);return runs.join(',');
}
const patch=['*** Begin Patch'];
for(const a of additions){
  const m=D.createMap(a.base),baseFile=files.find(f=>f.startsWith('maps/')&&fs.readFileSync(path.join(source,f),'utf8').includes(a.base));
  const hash=crypto.createHash('sha256');for(const f of files.filter(f=>!f.startsWith('maps/')||f===baseFile))hash.update(f).update(fs.readFileSync(path.join(source,f)));
  const baseRooms=m.rooms.map(r=>({Id:'base/'+r.id,Name:r.name,X:Math.round(r.anchorX||r.x+r.w/2),Floor:r.floor,
    Radius:Math.min(12,Math.floor(r.w/2)),Height:Math.min(14,Math.max(3,r.h-4)),Base:true,SurfaceOre:false,BuriedOre:false}));
  // 入口的元数据中心应在原生完整净空内；不修改捕获的栅格或矿簇。
  baseRooms.find(r=>r.Id==='base/camp').X=14;
  const definition={SchemaVersion:1,Id:a.id,Title:a.title,SourceHash:hash.digest('hex'),BaseRuns:encode(m.tiles),
    BaseWidth:132,BaseHeight:78,Width:264,Height:156,OffsetX:40,OffsetY:32,Seams:a.seams,
    Rooms:[...baseRooms,...a.rooms],Paths:a.paths,Shafts:a.shafts,
    BaseOres:m.ores.map(o=>({Id:o.id,Kind:o.kind,X:Math.floor(o.x/24),Y:Math.floor(o.y/24),Capacity:o.initial,Buried:o.buried}))};
  const file=path.join(repo,'Game/Assets/DarkNights/Res/Terrain/MineMaps',a.id+'.minemap').replaceAll('\\','/');
  patch.push('*** Add File: '+file,...JSON.stringify(definition,null,2).split('\n').map(line=>'+'+line));
}
patch.push('*** End Patch');process.stdout.write(patch.join('\n'));
