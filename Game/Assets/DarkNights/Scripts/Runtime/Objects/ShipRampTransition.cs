using System;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 飞船坡道与地形的局部运动适配函数；只在开门后的出口交界处理一次跨界，角色仍持有唯一世界坐标。
    /// 不拥有状态或通用运动框架。更换坡道、登船入口或船体几何时检查本函数和 ShipCabinMotion 的入口判定。
    /// </summary>
    internal static class ShipRampTransition
    {
        /// <summary>
        /// 返回是否接管了本次出舱尝试，leftCabin 表示已越过出口；受真实地形阻挡时由原船内运动继续处理。
        /// 移除此适配时，只需撤掉 ShipCabinMotion 的调用，原步行出舱路径仍在调用点下方。
        /// </summary>
        internal static bool TryLeave(ActorBehaviour owner, float shipX, float target, bool open, double delta,
            ref bool grounded, out bool leftCabin)
        {
            leftCabin = false;
            float toe = shipX + ShipGeometry.RampToe;
            if (!open || target >= toe) return false;
            var world = owner.World;
            var actor = owner.Edit();

            // 交界处运动适配：先应用船内起跳，再做水平跨界；不能要求跳跃者落回坡道才允许离船。
            var rules = world.Catalog.Balance.HeroControl;
            if (grounded && actor.JumpPending && rules != null)
            {
                actor.VerticalSpeed = rules.JumpSpeed;
                actor.SupportPlatform = -1;
                actor.JumpPending = false;
                grounded = false;
            }

            float previousX = actor.X;
            actor.X = toe;
            if (world.Terrain == null) actor.X = Math.Clamp(target, 16, world.Layout.WorldWidth - 16);
            else Terrain.TerrainHeroMotion.MoveHorizontal(world.Terrain.Map, actor, target);
            if (actor.X >= toe - .001f)
            {
                actor.X = toe;
                return true;
            }

            // 交界处运动适配：归属在出口改变，但高度和竖直速度不重置；本函数同帧接续地形运动。
            actor.Boarded = false;
            actor.SupportPlatform = -1;
            actor.Walking = Math.Abs(actor.X - previousX) > .001f;
            if (actor.Walking) actor.Face = Math.Sign(actor.X - previousX);
            actor.JumpPending = actor.DropPending = false;
            HeroEquipment.Cancel(actor);
            // 交界处运动适配：同一帧只补一次地形纵向运动，控制主链无需了解飞船出口细节。
            var motion = owner.Object.GetBehaviour<HeroMotionBehaviour>() ??
                throw new InvalidOperationException("Boarded actor is missing hero motion capability.");
            motion.Tick(delta, false, false, false);
            HeroEquipment.Tick(owner, delta);
            leftCabin = true;
            return true;
        }
    }
}
