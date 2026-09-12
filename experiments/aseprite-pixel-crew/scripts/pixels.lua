-- 在新建 Image 上执行整数像素绘制；最终由 Aseprite 创建真实图层和 Cel。
local P = {}
local Canvas = {}
Canvas.__index = Canvas

function P.canvas(palette, dx, dy)
  return setmetatable({
    image=Image(64,64,ColorMode.RGB), colors=palette.pixels,
    dx=dx or 0, dy=dy or 0
  },Canvas)
end

function Canvas:pixel(x,y,color)
  x,y=math.floor(x+self.dx),math.floor(y+self.dy)
  assert(x>=0 and y>=0 and x<64 and y<64, "pixel outside 64x64 canvas")
  self.image:drawPixel(x,y,assert(self.colors[color],color))
end

function Canvas:rect(x,y,w,h,color)
  for py=y,y+h-1 do
    for px=x,x+w-1 do self:pixel(px,py,color) end
  end
end

function Canvas:line(x0,y0,x1,y1,color)
  local dx,dy=math.abs(x1-x0),-math.abs(y1-y0)
  local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1
  local err=dx+dy
  while true do
    self:pixel(x0,y0,color)
    if x0==x1 and y0==y1 then break end
    local twice=2*err
    if twice>=dy then err=err+dy; x0=x0+sx end
    if twice<=dx then err=err+dx; y0=y0+sy end
  end
end

function Canvas:poly(points,fill,outline)
  local ymin,ymax=64,0
  for _,p in ipairs(points) do ymin=math.min(ymin,p[2]);ymax=math.max(ymax,p[2]) end
  for y=ymin,ymax do
    local crossings={}
    local scan=y+0.5
    for i,a in ipairs(points) do
      local b=points[i % #points+1]
      if (a[2]<=scan and b[2]>scan) or (b[2]<=scan and a[2]>scan) then
        crossings[#crossings+1]=a[1]+(scan-a[2])*(b[1]-a[1])/(b[2]-a[2])
      end
    end
    table.sort(crossings)
    for i=1,#crossings,2 do
      for x=math.ceil(crossings[i]-0.5),math.floor(crossings[i+1]-0.5) do
        self:pixel(x,y,fill)
      end
    end
  end
  if outline then
    for i,a in ipairs(points) do
      local b=points[i % #points+1]
      self:line(a[1],a[2],b[1],b[2],outline)
    end
  end
end

function Canvas:stroke(ax,ay,bx,by,radius,color)
  local vx,vy=bx-ax,by-ay
  local length=vx*vx+vy*vy
  for y=math.floor(math.min(ay,by)-radius),math.ceil(math.max(ay,by)+radius) do
    for x=math.floor(math.min(ax,bx)-radius),math.ceil(math.max(ax,bx)+radius) do
      local t=length==0 and 0 or ((x+0.5-ax)*vx+(y+0.5-ay)*vy)/length
      t=math.max(0,math.min(1,t))
      local dx,dy=x+0.5-(ax+t*vx),y+0.5-(ay+t*vy)
      if dx*dx+dy*dy<=radius*radius then self:pixel(x,y,color) end
    end
  end
end

function Canvas:arm(shoulder,elbow,hand,far)
  local fill=far and "skin" or "skin_light"
  self:stroke(shoulder[1],shoulder[2],elbow[1],elbow[2],3.2,"ink")
  self:stroke(elbow[1],elbow[2],hand[1],hand[2],3.0,"ink")
  self:stroke(shoulder[1],shoulder[2],elbow[1],elbow[2],2.2,fill)
  self:stroke(elbow[1],elbow[2],hand[1],hand[2],2.0,fill)
  self:rect(hand[1]-2,hand[2]-1,3,3,far and "skin_shadow" or "skin")
  self:pixel(hand[1]+1,hand[2]+1,"skin_shadow")
end

function Canvas:leg(hip,knee,foot,far)
  local ankle={foot[1]-1,foot[2]-4}
  self:stroke(hip[1],hip[2],knee[1],knee[2],3.5,"ink")
  self:stroke(knee[1],knee[2],ankle[1],ankle[2],3.1,"ink")
  local fill=far and "blue_shadow" or "blue"
  self:stroke(hip[1],hip[2],knee[1],knee[2],2.5,fill)
  self:stroke(knee[1],knee[2],ankle[1],ankle[2],2.1,fill)
  if not far then self:rect(knee[1]-1,knee[2]-2,2,2,"blue_light") end
  local x,y=foot[1],foot[2]
  self:poly({{x-4,y-4},{x+1,y-4},{x+1,y-3},{x+5,y-3},
    {x+5,y-1},{x-4,y-1}}, "boot","ink")
  self:line(x-2,y-3,x+3,y-3,far and "strap" or "boot_light")
end
return P
