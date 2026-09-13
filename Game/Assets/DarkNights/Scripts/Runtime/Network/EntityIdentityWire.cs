using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>协议 6 的具体身份载荷；接收立即冻结，不能把定义或放置键当成客户端授权依据。</summary>
    [MemoryPackable]
    public partial class EntityIdentityWire
    {
        public int Id { get; set; }
        public string DefinitionGuid { get; set; }
        public string PlacementKey { get; set; }

        public static EntityIdentityWire From(EntityIdentityData value) => new EntityIdentityWire
        {
            Id = value.Id, DefinitionGuid = value.DefinitionGuid, PlacementKey = value.PlacementKey
        };

        public EntityIdentityData Freeze() => new EntityIdentityData(Id, DefinitionGuid, PlacementKey);
    }
}
