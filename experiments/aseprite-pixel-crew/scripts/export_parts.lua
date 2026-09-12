-- Read the maintained document, then export each layer and native reference composites.
-- Changes to visibility stay in memory; this script never saves the source document.
local input=assert(app.params.input)
local output=assert(app.params.output)
local sprite=assert(app.open(input))
local columns=8
local width,height=64*columns,64*math.ceil(#sprite.frames/columns)
local layer_folder=app.fs.joinPath(output,"layers")
local combo_folder=app.fs.joinPath(output,"native-combinations")
app.fs.makeAllDirectories(layer_folder)
app.fs.makeAllDirectories(combo_folder)

local function quote(value)
  return '"'..value:gsub('[%z\1-\31\\"]',function(c)
    if c=='"' then return '\\"' end
    if c=='\\' then return '\\\\' end
    return string.format('\\u%04x',string.byte(c))
  end)..'"'
end

local function render(path)
  assert(not app.fs.isFile(path),"Refusing to overwrite "..path)
  local image=Image(width,height,ColorMode.RGB)
  for frame=1,#sprite.frames do
    local x=((frame-1)%columns)*64
    local y=math.floor((frame-1)/columns)*64
    image:drawSprite(sprite,frame,Point(x,y))
  end
  image:saveAs(path)
end

local specs={}
for i,layer in ipairs(sprite.layers) do
  assert(not layer.isGroup,"This experiment expects flat paint layers")
  local part,choice=layer.data:match("^(%w+)=(%w+[_%w]*)$")
  specs[i]={layer=layer,part=part or "base",choice=choice or "base",visible=layer.isVisible}
  layer.isVisible=false
end
local entries={}
for i,spec in ipairs(specs) do
  local filename=string.format("layer-%02d.png",i)
  spec.layer.isVisible=true
  render(app.fs.joinPath(layer_folder,filename))
  spec.layer.isVisible=false
  entries[#entries+1]='{"name":'..quote(spec.layer.name)..',"part":'..quote(spec.part)..
    ',"choice":'..quote(spec.choice)..',"default_visible":'..tostring(spec.visible)..
    ',"file":'..quote("layers/"..filename)..'}'
end

local combinations={}
for _,headwear in ipairs({"cap","hard_hat","hair"}) do
  for _,outfit in ipairs({"overalls","work_vest"}) do
    for _,pack in ipairs({"backpack","canvas_bag","none"}) do
      local selected={headwear=headwear,outfit=outfit,pack=pack}
      for _,spec in ipairs(specs) do
        spec.layer.isVisible=spec.part=="base" or selected[spec.part]==spec.choice
      end
      local id=headwear.."-"..outfit.."-"..pack
      render(app.fs.joinPath(combo_folder,id..".png"))
      combinations[#combinations+1]='{"id":'..quote(id)..',"headwear":'..quote(headwear)..
        ',"outfit":'..quote(outfit)..',"pack":'..quote(pack)..
        ',"file":'..quote("native-combinations/"..id..".png")..'}'
    end
  end
end
local manifest=assert(io.open(app.fs.joinPath(output,"parts.json"),"w"))
manifest:write('{"layers":['..table.concat(entries,",")..'],"combinations":['..
  table.concat(combinations,",")..'],"frames":'..#sprite.frames..'}')
manifest:close()
print("ASEPRITE_PARTS passed layers="..#specs.." combinations="..#combinations.." frames="..#sprite.frames)
