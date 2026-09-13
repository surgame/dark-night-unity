using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 一次冻结的实体身份关系，分别描述本局 EntityId、定义 GUID 和可选场景放置键。
    /// 不携带可写状态或引擎引用；存档、网络与表现可共享此不可变合同。
    /// </summary>
    public sealed class EntityIdentityData
    {
        public int Id { get; }
        public string DefinitionGuid { get; }
        public string PlacementKey { get; }

        public EntityIdentityData(int id, string definitionGuid, string placementKey)
        {
            if (id <= 0 || !Guid.TryParse(definitionGuid, out Guid guid) || guid == Guid.Empty ||
                placementKey == null || placementKey.Length > 80)
                throw new ArgumentException("Invalid object identity.");
            Id = id;
            DefinitionGuid = definitionGuid;
            PlacementKey = placementKey;
        }
    }
}
