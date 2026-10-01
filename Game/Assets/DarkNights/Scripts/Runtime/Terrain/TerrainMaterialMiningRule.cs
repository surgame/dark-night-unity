using System;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>材质采集的作者规则；稳定 Key 绑定原生材质，耐久只取 AnyRuleD 目录，产出与工具效率由游戏负责。</summary>
    [Serializable]
    public sealed class TerrainMaterialMiningRule
    {
        public string MaterialKey = "slate";
        public string ResourceId = "";
        public int Yield;
        public int PickaxeEfficiencyPercent = 100;
    }
}
