(function(root) {
  "use strict";
  const api=root.MacroCave,CELL=16,HP=20,REACH=64;
  const key=(x,y)=>`${Math.floor(x/CELL)}:${Math.floor(y/CELL)}`;
  const coordinates=id=>id.split(":").map(Number).map(n=>n*CELL);

  function create(seed,placements,tiles,coverage) {
    const rng=api.random(seed^0x41A73);
    const world={seed,placements,tiles,clear:new Set(),rock:new Map(),ore:new Map(),
      sites:[],clock:0,next:0,action:null,revision:0,harvested:0,message:""};
    placements.forEach((placed,index)=>{
      const tile=tiles[placed.id],slots=api.moduleContent[tile.panel].mineralSlots;
      const selected=slots[Math.floor(rng()*slots.length)];
      const local=tile.mirror?api.width-selected:selected;
      const x=Math.floor((placed.x+(index===0?208:local))/CELL)*CELL;
      const floor=api.surface(placements,tiles,x+32);
      const y=Math.floor((floor-27)/CELL)*CELL,rolled=Math.floor(rng()*4);
      const variant=index===0?0:rolled;
      const site={x,y,variant,id:index};
      world.sites.push(site);
      for(let cy=0;cy<4;cy++)for(let cx=0;cx<4;cx++) {
        if(!coverage[variant][cy*4+cx])continue;
        const id=key(x+cx*CELL,y+cy*CELL);
        world.ore.set(id,{hp:HP,site:index});
      }
    });
    return world;
  }

  function initialSolid(world,x,y) {
    return y>=api.rockSurface(world.placements,world.tiles,x)||
      y<api.rockSurface(world.placements,world.tiles,x,"roof");
  }
  const solid=(world,x,y)=>initialSolid(world,x,y)&&!world.clear.has(key(x,y));

  function ground(world,x) {
    let y=api.rockSurface(world.placements,world.tiles,x);
    for(let i=0;i<64&&!solid(world,x,y+.1);i++)y=(Math.floor(y/CELL)+1)*CELL;
    return y;
  }
  function roof(world,x) {
    let y=api.rockSurface(world.placements,world.tiles,x,"roof");
    for(let i=0;i<64&&!solid(world,x,y-.1);i++)y=Math.floor((y-.1)/CELL)*CELL;
    return y;
  }
  function at(world,x,y) {
    const id=key(x,y);
    if(solid(world,x,y))return {id,type:"rock",hp:world.rock.get(id)??HP};
    const ore=world.ore.get(id);
    if(ore&&ore.hp>0)return {id,type:"ore",hp:ore.hp};
    return null;
  }
  function ray(world,origin,target) {
    const distance=Math.hypot(target.x-origin.x,target.y-origin.y);
    if(distance>REACH)return null;
    for(let d=1;d<=Math.max(1,distance);d+=1) {
      const t=Math.min(1,d/Math.max(1,distance));
      const x=origin.x+(target.x-origin.x)*t,y=origin.y+(target.y-origin.y)*t;
      const hit=at(world,x,y);
      if(hit)return {...hit,x,y};
    }
    const hit=at(world,target.x,target.y);
    return hit?{...hit,x:target.x,y:target.y}:null;
  }
  const hand=player=>({x:player.x+player.face*13,y:player.y-26});

  function aim(world,player) {
    const origin=hand(player),available=[];
    for(const [id,value] of world.ore) {
      if(value.hp<=0)continue;
      const [x,y]=coordinates(id),target={x:x+8,y:y+8};
      const distance=Math.hypot(target.x-origin.x,target.y-origin.y);
      if(distance>REACH||Math.sign(target.x-origin.x)!==player.face)continue;
      const hit=ray(world,origin,target);
      if(hit&&hit.type==="ore")available.push({target,distance});
    }
    available.sort((a,b)=>a.distance-b.distance);
    if(available.length)return available[0].target;
    const x=player.x+player.face*25;
    return {x,y:ground(world,x)+8};
  }

  function request(world,player,point) {
    if(world.action||world.clock<world.next)return false;
    const origin=hand(player),target=point||aim(world,player);
    if(Math.hypot(target.x-origin.x,target.y-origin.y)>REACH) {
      world.message="目标超出样板矿镐距离";return false;
    }
    const hit=ray(world,origin,target);
    if(!hit){world.message="附近没有可采目标";return false;}
    world.action={...hit,impact:world.clock+.288};
    world.next=world.clock+.48;
    world.message=hit.type==="rock"?"挥镐：围岩":"挥镐：已露矿格";
    return true;
  }

  function tick(world,dt,player) {
    world.clock+=dt;
    const action=world.action;
    if(!action||world.clock<action.impact)return false;
    world.action=null;
    const current=player?ray(world,hand(player),action):at(world,action.x,action.y);
    if(!current||current.id!==action.id||current.type!==action.type||current.hp!==action.hp) {
      world.message="冻结目标已变化，本次落镐取消";return true;
    }
    const hp=Math.max(0,action.hp-10);
    if(action.type==="rock") {
      if(!world.rock.has(action.id)&&world.rock.size>=256) {
        world.message="本样板已达到256格耐久记录上限";return true;
      }
      if(hp===0) {
        if(world.clear.size>=256){world.message="本样板已达到256格编辑上限";return true;}
        world.clear.add(action.id);world.rock.delete(action.id);
        world.message="围岩已移除；后壁保持，后方矿层可继续揭露";
      } else {
        world.rock.set(action.id,hp);world.message="围岩耐久 "+hp;
      }
    } else {
      world.ore.get(action.id).hp=hp;
      if(hp===0){world.harvested++;world.message="矿格已采空；后壁保持";}
      else world.message="矿格耐久 "+hp;
    }
    world.revision++;
    return true;
  }

  function snapshot(world) {
    return {clear:[...world.clear],rock:[...world.rock],
      ore:[...world.ore].filter(([,value])=>value.hp!==HP).map(([id,value])=>[id,value.hp])};
  }
  function restore(world,value) {
    if(!value||!Array.isArray(value.clear))return;
    const valid=id=>typeof id==="string"&&/^-?\d+:-?\d+$/.test(id);
    world.clear=new Set(value.clear.filter(valid).slice(0,256));
    for(const [id,hp] of (value.rock||[]).slice(0,256))if(valid(id)&&hp===10)world.rock.set(id,hp);
    for(const [id,hp] of (value.ore||[]).slice(0,512)) {
      if(world.ore.has(id)&&(hp===0||hp===10))world.ore.get(id).hp=hp;
    }
    world.harvested=[...world.ore.values()].filter(v=>v.hp===0).length;
  }
  Object.assign(api,{createTerrain:create,terrainSolid:solid,terrainGround:ground,terrainRoof:roof,
    miningAim:aim,miningRequest:request,miningTick:tick,terrainSnapshot:snapshot,
    restoreTerrain:restore,terrainKey:key,terrainCoordinates:coordinates,terrainRay:ray});
})(globalThis);
