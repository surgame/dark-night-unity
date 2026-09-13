using System;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 从制作场景一次冻结的放置描述，关联可直接接管的原 Loader；重开不会读取移动后的 Transform。
    /// 放置键、定义 GUID 与运行 EntityId 各自独立；动态对象使用空放置键。
    /// </summary>
    public sealed class ObjectPlacement
    {
        public string PlacementKey { get; }
        public ObjectDefinition Definition { get; }
        public float X { get; }
        public int Variant { get; }
        public string ActorName { get; }
        public ObjectDefinitionLoader Loader { get; }

        public ObjectPlacement(string placementKey, ObjectDefinition definition, float x,
            int variant = 0, string actorName = "", ObjectDefinitionLoader loader = null)
        {
            if (string.IsNullOrWhiteSpace(placementKey)) throw new ArgumentException("Placement key is required.");
            PlacementKey = placementKey;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            X = x;
            Variant = variant;
            ActorName = actorName ?? throw new ArgumentNullException(nameof(actorName));
            Loader = loader;
        }
    }
}
