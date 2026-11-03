using System;
using System.Linq;
using DarkNights.Core.Physics;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 同一次模拟切点的完整物理闭包；配置、地图与输入确认跟随现有可靠完整投影发送。
    /// 空值明确表示格运动会话，不从缺失数组或局部物体推测可恢复状态。
    /// </summary>
    [MemoryPackable]
    public partial class PhysicsWorldWire
    {
        public string DefinitionHash { get; set; }
        public long Tick { get; set; }
        public string WorldId { get; set; }
        public ulong MapEpoch { get; set; }
        public ulong TerrainRevision { get; set; }
        public PhysicsBodyWire[] Bodies { get; set; }
        public PhysicsRopeWire[] Ropes { get; set; }

        public static PhysicsWorldWire From(PhysicsWorldSnapshot s) => s == null ? null : new PhysicsWorldWire
        {
            DefinitionHash = s.DefinitionHash, Tick = s.Tick, WorldId = s.WorldId,
            MapEpoch = s.MapEpoch, TerrainRevision = s.TerrainRevision,
            Bodies = s.Bodies.Select(PhysicsBodyWire.From).ToArray(), Ropes = s.Ropes.Select(PhysicsRopeWire.From).ToArray()
        };

        public PhysicsWorldSnapshot Freeze()
        {
            if (Bodies == null || Bodies.Length > PhysicsNumbers.MaximumBodies || Bodies.Any(b => b == null) ||
                Ropes == null || Ropes.Length > PhysicsNumbers.MaximumRopes || Ropes.Any(r => r == null))
                throw new FormatException("物理投影集合缺失或越界。");
            return new PhysicsWorldSnapshot(DefinitionHash, Tick, WorldId, MapEpoch, TerrainRevision,
                Bodies.Select(b => b.Freeze()).ToArray(), Ropes.Select(r => r.Freeze()).ToArray());
        }
    }
}
