using System;
using System.Linq;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 固定模块研究的初始角色装配，状态仍归已有ActorBehaviour；只在所属会话初始化事务内执行一次。
    /// 出生高度来自已检查Bake，不创建新角色模型，不改正式玩家移动数值或发放采掘道具。
    /// </summary>
    internal static class MapModuleSessionSetup
    {
        internal static void Prepare(ObjectSession world)
        {
            if (!world.Layout.MapModuleStudy) throw new InvalidOperationException("不是模块研究会话。");
            var actor = world.Index.Actors.Single();
            var state = actor.Edit();
            state.ManualControl = true;
            state.Height = world.Layout.MapModuleSpawnHeight;
            state.VerticalSpeed = 0; state.SupportPlatform = 0; state.Boarded = false;
            PhysicsStudyEquipment.Prepare(world, state);
        }
    }
}
