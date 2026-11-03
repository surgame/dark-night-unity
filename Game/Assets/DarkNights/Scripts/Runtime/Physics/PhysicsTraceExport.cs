using System.Linq;
using DarkNights.Runtime.Save;
using Newtonsoft.Json;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 诊断冻结资料的唯一JSON投影；复用现有地图编码保存真实格形，不依赖反射枚举私有地图数组。
    /// 只返回文本，由用户操作的Editor窗口选择落盘，不写玩家槽位或启动重演。
    /// </summary>
    public static class PhysicsTraceExport
    {
        public static string Serialize(PhysicsTrace trace, string unityVersion) =>
            JsonConvert.SerializeObject(new
            {
                format = "dark-nights.physics-trace-v1", backend = Core.Physics.PhysicsWorldDefinition.Backend,
                protocol = Session.SessionAuthority.ProtocolVersion, unity = unityVersion,
                rules = trace.Rules, movement = trace.MovementFingerprint, initial_world_json = trace.InitialWorldJson,
                terrain_changes = trace.TerrainChanges.Select(change => new
                { tick = change.Tick, revision = change.Revision, terrain = TerrainSaveJson.Write(change.Terrain) }).ToArray(),
                frames = trace.Capture(), failure = trace.Failure
            }, Formatting.Indented);
    }
}
