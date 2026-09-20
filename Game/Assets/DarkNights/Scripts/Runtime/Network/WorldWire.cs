using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// WorldViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class WorldWire
    {
        public ExpeditionViewWire Expedition { get; set; }
        public CampWire Camp { get; set; }
        public ActorWire[] Actors { get; set; }
        public BuildingWire[] Buildings { get; set; }
        public WorksiteWire[] Worksites { get; set; }
        public ProjectileWire[] Projectiles { get; set; }
        public EntityIdentityWire[] Identities { get; set; }

        public static WorldWire From(WorldViewData value) => new WorldWire
        {
            Expedition = ExpeditionViewWire.From(value.Expedition),
            Camp = CampWire.From(value.Camp),
            Actors = value.Actors.Select(ActorWire.From).ToArray(),
            Buildings = value.Buildings.Select(BuildingWire.From).ToArray(),
            Worksites = value.Worksites.Select(WorksiteWire.From).ToArray(),
            Projectiles = value.Projectiles.Select(ProjectileWire.From).ToArray(),
            Identities = value.Identities.Select(EntityIdentityWire.From).ToArray(),
        };

        public WorldViewData Freeze() => new WorldViewData(
            Camp?.Freeze(),
            Actors?.Select(item => item?.Freeze()).ToArray(),
            Buildings?.Select(item => item?.Freeze()).ToArray(),
            Worksites?.Select(item => item?.Freeze()).ToArray(),
            Projectiles?.Select(item => item?.Freeze()).ToArray(),
            Identities?.Select(item => item?.Freeze()).ToArray(), Expedition?.Freeze());
    }
}
