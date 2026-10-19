using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 既有航程SharedConfig中的导航区；导航资料只参与会话身份，不加入地图生成依赖。
    /// 两个额外节点用于比较距离和风险，保持预览身份，不生成第二主题地图。
    /// </summary>
    [Serializable]
    public sealed class NavigationPreset
    {
        public float Speed = 8;
        public float DescentSeconds = 3;
        public List<NavigationNodePreset> Nodes = new List<NavigationNodePreset>
        {
            new NavigationNodePreset(),
            new NavigationNodePreset { Id = "rift-preview", Name = "裂谷星（示意）", X = 90, Y = 10,
                Danger = 2, Risk = "落差与塌方（mock资料）", PreviewOnly = true },
            new NavigationNodePreset { Id = "deep-preview", Name = "暗脉星（示意）", X = 45, Y = 70,
                Danger = 3, Risk = "厚壁与巢穴（mock资料）", PreviewOnly = true }
        };

        public NavigationDefinition Freeze(IReadOnlyList<PlanetDefinition> planets)
        {
            var definition = new NavigationDefinition(Speed, DescentSeconds, Nodes?.Select(n => n?.Freeze()));
            if (definition.Nodes.Any(n => !n.PreviewOnly && !planets.Any(p => p.Id == n.Id && p.Enabled)) ||
                planets.Any(p => p.Enabled && definition.Find(p.Id) == null))
                throw new ArgumentException("正式导航节点须对应启用星球，每个启用星球必须有星图坐标。");
            return definition;
        }
    }
}
