using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 场景标记导出的不可变初始摆放条目，保留内容 ID、规则横坐标、名称和外观变体；实体 ID 由世界按顺序分配。
    /// </summary>
    public sealed class PlacementDefinition
    {
        public string Kind { get; }
        public float X { get; }
        public int Variant { get; }
        public string Name { get; }

        public PlacementDefinition(string kind, float x, int variant = 0, string name = "")
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            X = x;
            Variant = variant;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
