using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// MovementConfig的作者速度选择，显式区分沿用balance与七组已讨论的手动角色方案。
    /// 零值保留既有配置的读取含义；运行对象只消费激活时冻结的数值，选择不成为实例状态。
    /// </summary>
    public enum MovementSpeedPreset
    {
        [InspectorName("沿用 balance")]
        Balance = 0,
        [InspectorName("慢速探索 · 72 → 108")]
        SlowExploration = 1,
        [InspectorName("均衡探索 · 80 → 120")]
        BalancedExploration = 2,
        [InspectorName("利落探索 · 88 → 132")]
        AgileExploration = 3,
        [InspectorName("低步速／强冲刺 · 72 → 126")]
        StrongSprint = 4,
        [InspectorName("原游戏速度 · 112 → 176")]
        OriginalGame = 5,
        [InspectorName("Minecraft速度参照 · 106 → 137")]
        MinecraftReference = 6,
        [InspectorName("泰拉瑞亚跑鞋参照 · 189 → 377")]
        TerrariaReference = 7
    }
}
