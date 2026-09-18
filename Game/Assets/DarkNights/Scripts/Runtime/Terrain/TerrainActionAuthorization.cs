using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>服务端为一次中心格请求签发的短期授权；生成它必须读取 Ready、租约、装备、冷却和距离等权威状态。</summary>
    public sealed class TerrainActionAuthorization
    {
        public int ConnectionGeneration { get; }
        public WorldIdentity World { get; }
        public TerrainEditAction Action { get; }
        public Func<CellCoord, bool> AllowTarget { get; }
        public Action<IReadOnlyList<string>> OnCommitted { get; }

        public TerrainActionAuthorization(int connectionGeneration, WorldIdentity world,
            TerrainEditAction action, Func<CellCoord, bool> allowTarget,
            Action<IReadOnlyList<string>> onCommitted = null)
        {
            if (connectionGeneration < 0 || allowTarget == null) throw new ArgumentException("地形授权不完整。");
            ConnectionGeneration = connectionGeneration; World = world; Action = action; AllowTarget = allowTarget; OnCommitted = onCommitted;
        }
    }
}
