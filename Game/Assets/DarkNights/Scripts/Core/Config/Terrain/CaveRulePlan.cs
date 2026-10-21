using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>单候选只读制作元数据；RoomId取数组下标，形状与用途分离，声明路程不代表角色合法路径。无需传输或存档。</summary>
    public sealed class CaveRulePlan
    {
        public IReadOnlyList<TerrainRoom> Rooms { get; }
        public IReadOnlyList<CavePassage> Connections { get; }
        public IReadOnlyList<int> BranchLevels { get; }
        public IReadOnlyList<double> Distances { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public CaveRulePlan(TerrainRoom[] rooms, CavePassage[] connections, int[] levels, double[] distances, string[] diagnostics)
        {
            Rooms = Array.AsReadOnly((TerrainRoom[])rooms.Clone());
            Connections = Array.AsReadOnly((CavePassage[])connections.Clone());
            BranchLevels = Array.AsReadOnly((int[])levels.Clone());
            Distances = Array.AsReadOnly((double[])distances.Clone());
            Diagnostics = Array.AsReadOnly((string[])diagnostics.Clone());
        }
    }
}
