using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 完工建筑可装配的训练或塔攻击能力，生命与计时由 BuildingState 持有。
    /// 施工期间不调用；每个建筑最多一个完工活动，固定由建筑阶段推进。
    /// </summary>
    public interface IBuildingActivity : IArchetypeCapability
    {
        void Tick(double delta);
    }
}
