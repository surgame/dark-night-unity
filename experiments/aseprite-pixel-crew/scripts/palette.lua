-- 明确的像素色板；0 号为透明，所有可见像素不使用半透明或抖色。
local swatches = {
  {"ink",         "241f24"},
  {"edge",        "403039"},
  {"hair",        "4b3633"},
  {"hair_light",  "705045"},
  {"skin_shadow", "b97951"},
  {"skin",        "e5aa73"},
  {"skin_light",  "f7ca91"},
  {"skin_glint",  "ffe2ae"},
  {"red_dark",    "762e35"},
  {"red",         "b4423c"},
  {"red_light",   "de6952"},
  {"red_glint",   "ee9871"},
  {"cream",       "f3ddb0"},
  {"blue_shadow", "263743"},
  {"blue",        "3d5667"},
  {"blue_light",  "6f8893"},
  {"blue_glint",  "a0afa9"},
  {"strap",       "694b3c"},
  {"strap_light", "a57b51"},
  {"pack_dark",   "353d33"},
  {"pack",        "5b624b"},
  {"pack_light",  "8b9071"},
  {"boot",        "40312c"},
  {"boot_light",  "72513a"}
}
local result = {pixels={}, palette=Palette(#swatches+1), swatches=swatches}
result.palette:setColor(0, Color{r=0,g=0,b=0,a=0})
for i, swatch in ipairs(swatches) do
  local h = swatch[2]
  local r,g,b = tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16)
  local color = Color{r=r,g=g,b=b,a=255}
  result.palette:setColor(i,color)
  result.pixels[swatch[1]] = app.pixelColor.rgba(r,g,b,255)
end
return result
