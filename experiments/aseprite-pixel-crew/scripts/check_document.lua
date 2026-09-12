-- 在隔离副本里验证真实 Aseprite 的图层、Tag、原点和编辑保存重开。
local input=assert(app.params.input)
local output=assert(app.params.output)
assert(not app.fs.isFile(output),"Probe target already exists")
local sprite=assert(app.open(input))
assert(sprite.width==64 and sprite.height==64)
assert(#sprite.frames==24 and #sprite.layers==13 and #sprite.tags==3)
local expected={idle=6,walk=8,jump=10}
for _,tag in ipairs(sprite.tags) do
  assert(tag.toFrame.frameNumber-tag.fromFrame.frameNumber+1==expected[tag.name],tag.name)
end
assert(sprite.slices[1].name=="feet_origin")
assert(sprite.slices[1].pivot.x==32 and sprite.slices[1].pivot.y==58)
local duplicate=Sprite(sprite)
local function find_layer(document,name)
  for _,layer in ipairs(document.layers) do
    if layer.name==name then return layer end
  end
  error("Missing layer "..name)
end
local cap=find_layer(duplicate,"Cap")
local cel=assert(cap:cel(1))
local changed=cel.image:clone()
local marker=app.pixelColor.rgba(255,0,255,255)
local marker_x,marker_y=cel.position.x+1,cel.position.y+1
assert(changed:getPixel(1,1)~=marker)
changed:drawPixel(1,1,marker)
cel.image=changed
find_layer(duplicate,"Cap").isVisible=false
find_layer(duplicate,"Hard hat").isVisible=true
duplicate.data="isolated edit-save-reopen probe"
duplicate:saveAs(output)
duplicate:close()
local reopened=assert(app.open(output))
assert(reopened.data=="isolated edit-save-reopen probe")
local saved=find_layer(reopened,"Cap"):cel(1)
assert(saved.image:getPixel(marker_x-saved.position.x,marker_y-saved.position.y)==marker)
assert(not find_layer(reopened,"Cap").isVisible)
assert(find_layer(reopened,"Hard hat").isVisible)
assert(find_layer(reopened,"Hard hat").data=="headwear=hard_hat")
print("ASEPRITE_DOCUMENT_CHECK passed frames=24 layers=13 tags=3 origin=32,58 edit_save_reopen=true alternative_visibility_persisted=true")
