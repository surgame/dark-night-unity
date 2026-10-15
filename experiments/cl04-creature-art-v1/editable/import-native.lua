-- 只允许构建器完成哈希保护后调用；不修改原生PNG。
if app.params["verified_native_import"] ~= "1" then
  error("Use authoring/build.py with --aseprite after output hash verification")
end
local sprite = Sprite(32, 32, ColorMode.RGB)
sprite.layers[1].name = "Pixels"
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/dormant.png]] }
sprite:newCel(sprite.layers[1], 1, png, Point(0, 0))
sprite.frames[1].duration = 0.16
local tag = sprite:newTag(1, 1)
tag.name = "dormant"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/warning.png]] }
sprite:newCel(sprite.layers[1], 2, png, Point(0, 0))
sprite.frames[2].duration = 0.16
local tag = sprite:newTag(2, 2)
tag.name = "warning"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/reveal.png]] }
sprite:newCel(sprite.layers[1], 3, png, Point(0, 0))
sprite.frames[3].duration = 0.16
local tag = sprite:newTag(3, 3)
tag.name = "reveal"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/crawl_a.png]] }
sprite:newCel(sprite.layers[1], 4, png, Point(0, 0))
sprite.frames[4].duration = 0.16
local tag = sprite:newTag(4, 4)
tag.name = "crawl_a"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/crawl_b.png]] }
sprite:newCel(sprite.layers[1], 5, png, Point(0, 0))
sprite.frames[5].duration = 0.16
local tag = sprite:newTag(5, 5)
tag.name = "crawl_b"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/lunge.png]] }
sprite:newCel(sprite.layers[1], 6, png, Point(0, 0))
sprite.frames[6].duration = 0.16
local tag = sprite:newTag(6, 6)
tag.name = "lunge"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/recoil.png]] }
sprite:newCel(sprite.layers[1], 7, png, Point(0, 0))
sprite.frames[7].duration = 0.16
local tag = sprite:newTag(7, 7)
tag.name = "recoil"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ore-mimic/fallen.png]] }
sprite:newCel(sprite.layers[1], 8, png, Point(0, 0))
sprite.frames[8].duration = 0.16
local tag = sprite:newTag(8, 8)
tag.name = "fallen"
sprite:saveAs([[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/editable/ore-mimic.aseprite]])
sprite:close()
local sprite = Sprite(32, 24, ColorMode.RGB)
sprite.layers[1].name = "Pixels"
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/idle_a.png]] }
sprite:newCel(sprite.layers[1], 1, png, Point(0, 0))
sprite.frames[1].duration = 0.16
local tag = sprite:newTag(1, 1)
tag.name = "idle_a"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/idle_b.png]] }
sprite:newCel(sprite.layers[1], 2, png, Point(0, 0))
sprite.frames[2].duration = 0.16
local tag = sprite:newTag(2, 2)
tag.name = "idle_b"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/alert.png]] }
sprite:newCel(sprite.layers[1], 3, png, Point(0, 0))
sprite.frames[3].duration = 0.16
local tag = sprite:newTag(3, 3)
tag.name = "alert"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/crawl_a.png]] }
sprite:newCel(sprite.layers[1], 4, png, Point(0, 0))
sprite.frames[4].duration = 0.16
local tag = sprite:newTag(4, 4)
tag.name = "crawl_a"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/crawl_b.png]] }
sprite:newCel(sprite.layers[1], 5, png, Point(0, 0))
sprite.frames[5].duration = 0.16
local tag = sprite:newTag(5, 5)
tag.name = "crawl_b"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/lunge.png]] }
sprite:newCel(sprite.layers[1], 6, png, Point(0, 0))
sprite.frames[6].duration = 0.16
local tag = sprite:newTag(6, 6)
tag.name = "lunge"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/recoil.png]] }
sprite:newCel(sprite.layers[1], 7, png, Point(0, 0))
sprite.frames[7].duration = 0.16
local tag = sprite:newTag(7, 7)
tag.name = "recoil"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/ambush-grub/fallen.png]] }
sprite:newCel(sprite.layers[1], 8, png, Point(0, 0))
sprite.frames[8].duration = 0.16
local tag = sprite:newTag(8, 8)
tag.name = "fallen"
sprite:saveAs([[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/editable/ambush-grub.aseprite]])
sprite:close()
local sprite = Sprite(24, 24, ColorMode.RGB)
sprite.layers[1].name = "Pixels"
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/egg-clutch/intact.png]] }
sprite:newCel(sprite.layers[1], 1, png, Point(0, 0))
sprite.frames[1].duration = 0.16
local tag = sprite:newTag(1, 1)
tag.name = "intact"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/egg-clutch/pulse.png]] }
sprite:newCel(sprite.layers[1], 2, png, Point(0, 0))
sprite.frames[2].duration = 0.16
local tag = sprite:newTag(2, 2)
tag.name = "pulse"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/egg-clutch/cracked.png]] }
sprite:newCel(sprite.layers[1], 3, png, Point(0, 0))
sprite.frames[3].duration = 0.16
local tag = sprite:newTag(3, 3)
tag.name = "cracked"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/egg-clutch/empty.png]] }
sprite:newCel(sprite.layers[1], 4, png, Point(0, 0))
sprite.frames[4].duration = 0.16
local tag = sprite:newTag(4, 4)
tag.name = "empty"
sprite:saveAs([[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/editable/egg-clutch.aseprite]])
sprite:close()
local sprite = Sprite(32, 24, ColorMode.RGB)
sprite.layers[1].name = "Pixels"
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/burrow/empty.png]] }
sprite:newCel(sprite.layers[1], 1, png, Point(0, 0))
sprite.frames[1].duration = 0.16
local tag = sprite:newTag(1, 1)
tag.name = "empty"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/burrow/occupied.png]] }
sprite:newCel(sprite.layers[1], 2, png, Point(0, 0))
sprite.frames[2].duration = 0.16
local tag = sprite:newTag(2, 2)
tag.name = "occupied"
sprite:saveAs([[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/editable/burrow.aseprite]])
sprite:close()
local sprite = Sprite(16, 16, ColorMode.RGB)
sprite.layers[1].name = "Pixels"
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/clues/scratch.png]] }
sprite:newCel(sprite.layers[1], 1, png, Point(0, 0))
sprite.frames[1].duration = 0.16
local tag = sprite:newTag(1, 1)
tag.name = "scratch"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/clues/slime.png]] }
sprite:newCel(sprite.layers[1], 2, png, Point(0, 0))
sprite.frames[2].duration = 0.16
local tag = sprite:newTag(2, 2)
tag.name = "slime"
sprite:newEmptyFrame()
local png = Image{ fromFile = [[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/native/clues/molt.png]] }
sprite:newCel(sprite.layers[1], 3, png, Point(0, 0))
sprite.frames[3].duration = 0.16
local tag = sprite:newTag(3, 3)
tag.name = "molt"
sprite:saveAs([[D:/Developer/MiniGames/Dark Nights/unity-projects/experiments/cl04-creature-art-v1/editable/clues.aseprite]])
sprite:close()
