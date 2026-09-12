-- 角色轮廓与色块在这里显式定义；头、帽、脸、躯干及前后肢体独立成层。
local C = {}

function C.pack(p)
  p:poly({{16,34},{22,32},{25,35},{24,47},{22,49},{15,48},{13,45},{13,37}},
    "pack_dark","ink")
  p:poly({{16,35},{21,34},{23,36},{22,45},{20,47},{15,45},{15,37}},"pack")
  p:rect(16,35,5,2,"pack_light")
  p:rect(14,39,2,6,"pack")
  p:line(16,42,21,42,"pack_dark")
  p:rect(18,41,2,3,"strap_light")
end

function C.torso(p)
  p:poly({{24,35},{35,35},{38,38},{38,46},{35,49},{23,49},{20,45},{21,38}},
    "blue_shadow","ink")
  p:poly({{25,36},{34,36},{36,39},{36,45},{34,47},{24,47},{23,43},{23,38}},"blue")
  p:poly({{27,37},{33,37},{34,40},{33,43},{29,44},{27,41}},"blue_light")
  p:rect(25,36,2,9,"strap")
  p:line(25,37,25,41,"strap_light")
  p:rect(34,36,2,8,"strap")
  p:pixel(34,38,"strap_light")
  p:rect(28,41,5,3,"blue")
  p:line(28,41,32,41,"blue_shadow")
  p:pixel(28,38,"cream")
  p:pixel(34,38,"cream")
  p:rect(23,46,13,2,"strap")
  p:rect(30,46,2,2,"strap_light")
  p:line(24,48,35,48,"blue_shadow")
end

function C.head(p)
  p:poly({{24,18},{38,18},{41,20},{43,23},{43,32},{41,35},
    {38,37},{28,37},{24,35},{22,32},{21,25},{22,21}},"hair","ink")
  p:poly({{28,21},{40,21},{42,23},{42,31},{40,34},{29,34},{27,32},{27,23}},
    "skin_light")
  p:rect(27,23,2,9,"skin")
  p:rect(29,31,11,2,"skin")
  p:line(24,23,24,27,"hair_light")
  p:poly({{23,26},{25,26},{26,28},{25,31},{23,30}},"skin","edge")
  p:pixel(24,28,"skin_shadow")
  p:poly({{25,29},{28,31},{30,31},{31,30},{38,30},{39,31},{42,31},
    {42,34},{39,37},{28,37},{25,35}},"hair","ink")
  p:line(27,33,28,35,"hair_light")
  p:line(30,36,38,36,"hair_light")
  p:pixel(41,33,"hair_light")
end

function C.face(p,closed,effort)
  if closed then
    p:line(31,27,33,27,"ink")
    p:line(39,27,41,27,"ink")
  else
    p:rect(31,25,2,4,"ink")
    p:rect(39,25,2,4,"ink")
    p:pixel(30,25,"skin")
  end
  p:pixel(37,30,"skin_shadow")
  p:rect(33,33,4,1,effort and "skin_light" or "skin_shadow")
  if effort then p:rect(34,34,2,1,"skin_shadow") end
end

function C.hat(p)
  p:poly({{22,21},{22,17},{24,17},{24,15},{27,15},{27,13},{37,13},
    {39,14},{41,17},{41,19},{46,19},{46,22},{42,23},{24,23},{24,22}},
    "red","ink")
  p:poly({{23,18},{25,16},{27,16},{26,20},{27,22},{23,22}},"red_dark")
  p:poly({{27,15},{29,14},{37,14},{39,16},{32,15},{29,16},{28,18},{26,18}},
    "red_light")
  p:line(28,14,36,14,"red_glint")
  p:rect(33,15,6,5,"cream")
  p:rect(32,16,1,3,"cream")
  p:rect(35,16,3,3,"red")
  p:pixel(35,16,"red_light")
  p:line(28,21,45,21,"red_dark")
  p:line(30,20,44,20,"red_light")
  p:line(28,22,43,22,"ink")
end

function C.hard_hat(p)
  p:poly({{21,21},{22,18},{23,18},{23,15},{26,12},{38,12},
    {41,15},{42,19},{46,19},{47,21},{46,23},{22,23}},"skin","ink")
  p:poly({{25,16},{27,14},{37,14},{39,16},{40,20},{25,20}},"cream")
  p:rect(29,13,3,7,"skin_glint")
  p:line(24,21,45,21,"strap_light")
  p:line(25,20,44,20,"skin_light")
  p:rect(35,16,3,3,"strap_light")
  p:pixel(36,17,"cream")
end

function C.hair(p)
  p:poly({{21,25},{20,22},{21,19},{22,19},{22,16},{25,16},{25,14},
    {30,14},{31,12},{35,13},{38,13},{38,15},{41,16},{42,20},{41,24},
    {38,24},{38,21},{36,21},{36,23},{33,24},{33,21},{29,22},
    {27,24},{26,27},{23,27}},"hair","ink")
  p:poly({{23,19},{26,17},{27,16},{33,15},{38,17},{31,17},{29,19},
    {25,20},{23,23}},"hair_light")
  p:line(28,15,31,15,"strap_light")
end

function C.vest(p)
  p:poly({{24,35},{35,35},{38,38},{38,46},{35,49},{23,49},
    {20,45},{21,38}},"red_dark","ink")
  p:poly({{25,36},{28,37},{29,42},{28,47},{23,46},{23,38}},"red_light")
  p:poly({{33,36},{36,38},{36,46},{32,47},{31,41}},"red")
  p:rect(29,37,3,9,"cream")
  p:rect(30,38,1,8,"skin_shadow")
  p:line(23,42,27,42,"cream")
  p:line(33,42,36,42,"cream")
  p:rect(24,44,3,2,"red_dark")
  p:rect(33,44,3,2,"red_dark")
  p:rect(23,47,13,2,"strap")
  p:rect(30,47,2,2,"strap_light")
end

function C.canvas_bag(p)
  p:poly({{17,36},{21,35},{24,37},{24,47},{22,50},{16,50},
    {12,47},{12,40},{14,37}},"strap_light","ink")
  p:poly({{15,38},{21,37},{22,40},{21,47},{16,48},{14,45},{14,40}},"skin")
  p:poly({{13,38},{17,35},{21,35},{24,38},{22,42},{16,42},{13,41}},"cream","strap")
  p:rect(18,39,2,8,"strap")
  p:rect(18,42,2,2,"skin_light")
  p:line(14,46,16,47,"skin_shadow")
end
return C
