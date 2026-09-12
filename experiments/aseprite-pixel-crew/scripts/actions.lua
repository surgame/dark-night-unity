-- 有限的逐帧像素姿势；关节和头部位移均为整数，不做模糊旋转或补间采样。
local A = {}

local function base()
  return {
    body_y=0, head_y=0, pack_y=0, closed=false, effort=false,
    rear_hip={28,45},rear_knee={25,51},rear_foot={23,58},
    front_hip={34,45},front_knee={35,51},front_foot={37,58},
    rear_shoulder={36,38},rear_elbow={39,41},rear_hand={40,45},
    front_shoulder={22,38},front_elbow={19,42},front_hand={20,47}
  }
end

function A.idle(index)
  local p=base()
  local breath={0,-1,-1,0,0,0}
  p.body_y=breath[index];p.head_y=breath[index];p.pack_y=breath[index]
  p.closed=index==4
  if index==3 then p.front_hand={21,46} end
  return p
end

function A.walk(index)
  local p=base()
  local foot_x={40,37,34,31,27,25,29,35}
  local foot_y={58,58,58,58,57,53,51,54}
  local knee_x={36,35,32,29,27,28,31,34}
  local knee_y={51,51,51,51,50,48,47,49}
  local rear=(index+3)%8+1
  local body={0,1,0,-1,0,1,0,-1}
  local swing={-4,-2,1,3,4,2,-1,-3}
  p.body_y=body[index];p.head_y=p.body_y;p.pack_y=p.body_y
  p.front_knee={knee_x[index],knee_y[index]}
  p.front_foot={foot_x[index],foot_y[index]}
  p.rear_knee={knee_x[rear]-4,knee_y[rear]}
  p.rear_foot={foot_x[rear]-4,foot_y[rear]}
  p.front_elbow={20+math.floor(swing[index]/2),42}
  p.front_hand={20+swing[index],46-math.floor(math.abs(swing[index])/2)}
  p.rear_elbow={38-math.floor(swing[index]/2),41}
  p.rear_hand={39-swing[index],45}
  return p
end

function A.jump(index)
  local p=base()
  local lift={0,3,-2,-7,-9,-8,-5,1,3,0}
  p.body_y=lift[index];p.head_y=p.body_y;p.pack_y=p.body_y
  p.closed=index==2 or index==9
  p.effort=index>=3 and index<=7
  if index==2 or index==8 or index==9 then
    p.front_knee={38,51};p.front_foot={39,58}
    p.rear_knee={24,52};p.rear_foot={23,58}
    p.front_elbow={17,43};p.front_hand={18,47}
    p.rear_hand={42,44}
  elseif index>=3 and index<=7 then
    local height=({[3]=3,[4]=8,[5]=11,[6]=10,[7]=6})[index]
    p.front_knee={38,45-height};p.front_foot={40,56-height}
    p.rear_knee={26,48-height};p.rear_foot={23,57-height}
    p.front_elbow={16,33};p.front_hand={18,27}
    p.rear_elbow={43,33};p.rear_hand={44,28}
  end
  return p
end

A.clips={
  {name="idle",count=6,durations={180,140,180,100,100,180},loop=true,color="86a88a"},
  {name="walk",count=8,durations={100,100,100,100,100,100,100,100},loop=true,color="78a6be"},
  {name="jump",count=10,durations={100,80,80,90,100,100,90,70,100,120},loop=false,color="d6a064"}
}
return A
