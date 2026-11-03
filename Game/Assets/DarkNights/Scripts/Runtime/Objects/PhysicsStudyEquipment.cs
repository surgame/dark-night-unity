using System;
using System.Linq;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 仅为新建合作物理研究角色装配正式矿镐，供站在箱上挖地的候选交互使用。
    /// 原地图研究和正式新局不受影响，重连／接管／加载不调用本入口，不重新配发被移除装备。
    /// </summary>
    internal static class PhysicsStudyEquipment
    {
        internal static void Prepare(ObjectSession world, ActorState state)
        {
            if (world.Layout.Physics == null || !world.Layout.MapModuleStudy) return;
            var pickaxe = world.Resources.Equipment.MiningDefinitions.SingleOrDefault(d => d.Key == "item.pickaxe")
                ?? throw new InvalidOperationException("合作物理研究缺少正式矿镐定义。");
            if (!HeroInventoryBehaviour.Give(state, pickaxe.Guid.ToString()))
                throw new InvalidOperationException("合作物理研究矿镐装配失败。");
            state.SelectedItem = 0;
        }
    }
}
