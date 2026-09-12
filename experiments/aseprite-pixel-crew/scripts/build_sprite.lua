-- 一次性初始化原生 Aseprite 文档。后续导出只读取人工维护的 .aseprite。
local folder=assert(app.params.source_root,"--script-param source_root is required")
local palette=dofile(app.fs.joinPath(folder,"palette.lua"))
local pixels=dofile(app.fs.joinPath(folder,"pixels.lua"))
local character=dofile(app.fs.joinPath(folder,"character.lua"))
local actions=dofile(app.fs.joinPath(folder,"actions.lua"))
local output=assert(app.params.output,"--script-param output is required")
assert(not app.fs.isFile(output),"Refusing to overwrite an existing .aseprite")
local pilot=app.params.pilot=="true"
local sprite=Sprite(64,64,ColorMode.RGB)
sprite:setPalette(palette.palette)
sprite.data="Pixel Crew 2D trial; forward=right; feet pivot=(32,58); gameplay events=none"
local initial=sprite.layers[1]
local order={"Back arm","Backpack","Canvas bag","Back leg","Front leg",
  "Torso","Work vest","Front arm","Head","Face","Cap","Hard hat","Hair"}
local alternatives={
  ["Backpack"]="pack=backpack",["Canvas bag"]="pack=canvas_bag",
  ["Torso"]="outfit=overalls",["Work vest"]="outfit=work_vest",
  ["Cap"]="headwear=cap",["Hard hat"]="headwear=hard_hat",["Hair"]="headwear=hair"
}
local hidden={["Canvas bag"]=true,["Work vest"]=true,["Hard hat"]=true,["Hair"]=true}
local layers={}
for _,name in ipairs(order) do
  local layer=sprite:newLayer()
  layer.name=name
  layer.data=alternatives[name] or "base"
  layer.isVisible=not hidden[name]
  layers[name]=layer
end
sprite:deleteLayer(initial)
local slice=sprite:newSlice(Rectangle(0,0,64,64))
slice.name="feet_origin"
slice.pivot=Point(32,58)

local function cel(name,frame,draw,dx,dy)
  local canvas=pixels.canvas(palette,dx,dy)
  draw(canvas)
  sprite:newCel(layers[name],frame,canvas.image,Point(0,0))
end

local function shifted(point,dy)
  return {point[1],point[2]+dy}
end

local function draw_frame(frame,p)
  cel("Back arm",frame,function(c)
    c:arm(p.rear_shoulder,p.rear_elbow,p.rear_hand,true)
  end,0,p.body_y)
  cel("Backpack",frame,character.pack,0,p.pack_y)
  cel("Canvas bag",frame,character.canvas_bag,0,p.pack_y)
  cel("Back leg",frame,function(c)
    c:leg(shifted(p.rear_hip,p.body_y),p.rear_knee,p.rear_foot,true)
  end)
  cel("Front leg",frame,function(c)
    c:leg(shifted(p.front_hip,p.body_y),p.front_knee,p.front_foot,false)
  end)
  cel("Torso",frame,character.torso,0,p.body_y)
  cel("Work vest",frame,character.vest,0,p.body_y)
  cel("Front arm",frame,function(c)
    c:arm(p.front_shoulder,p.front_elbow,p.front_hand,false)
  end,0,p.body_y)
  cel("Head",frame,character.head,0,p.head_y)
  cel("Face",frame,function(c) character.face(c,p.closed,p.effort) end,0,p.head_y)
  cel("Cap",frame,character.hat,0,p.head_y)
  cel("Hard hat",frame,character.hard_hat,0,p.head_y)
  cel("Hair",frame,character.hair,0,p.head_y)
end

local frame_number=0
local ranges={}
for _,clip in ipairs(actions.clips) do
  if not pilot or clip.name=="idle" then
    local from=frame_number+1
    local count=pilot and 1 or clip.count
    for i=1,count do
      frame_number=frame_number+1
      if frame_number>1 then sprite:newEmptyFrame(frame_number) end
      sprite.frames[frame_number].duration=clip.durations[i]/1000
      draw_frame(frame_number,actions[clip.name](i))
    end
    ranges[#ranges+1]={clip=clip,first=from,last=frame_number}
  end
end
-- Adding frames at an existing tag's end expands that tag in Aseprite.
-- Create all tags only after the complete timeline exists.
for _,range in ipairs(ranges) do
    local clip=range.clip
    local tag=sprite:newTag(range.first,range.last)
    tag.name=clip.name
    tag.data=clip.loop and "loop=true" or "loop=false"
    tag.color=Color{r=tonumber(clip.color:sub(1,2),16),
      g=tonumber(clip.color:sub(3,4),16),b=tonumber(clip.color:sub(5,6),16)}
end
app.activeFrame=sprite.frames[1]
sprite:saveAs(output)
print("ASEPRITE_BUILD passed frames="..#sprite.frames.." layers="..#sprite.layers.." file="..output)
