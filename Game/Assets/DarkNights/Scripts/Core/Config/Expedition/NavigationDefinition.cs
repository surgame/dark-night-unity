using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Config.Expedition
{
    /// <summary>
    /// 会话星图和航速的不可变目录；与地形生成输入分离，所有端从同一冻结目录派生ETA。
    /// 节点最多32个，预览节点可见但不授予正式降落资格。
    /// </summary>
    public sealed class NavigationDefinition
    {
        public float Speed { get; }
        public float DescentSeconds { get; }
        public IReadOnlyList<NavigationNodeDefinition> Nodes { get; }

        public NavigationDefinition(float speed, float descentSeconds, IEnumerable<NavigationNodeDefinition> nodes)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 1 || speed > 512 ||
                float.IsNaN(descentSeconds) || float.IsInfinity(descentSeconds) || descentSeconds < 0 || descentSeconds > 30)
                throw new ArgumentException("导航速度或降落预计时间无效。");
            var entries = nodes?.ToArray();
            if (entries == null || entries.Length < 1 || entries.Length > 32 || entries.Any(n => n == null) ||
                entries.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new ArgumentException("导航节点缺失、重复或超过32个。");
            Speed = speed; DescentSeconds = descentSeconds; Nodes = Array.AsReadOnly(entries);
        }

        public NavigationNodeDefinition Find(string id) => Nodes.FirstOrDefault(n => n.Id == id);

        public double Distance(float x, float y, string id)
        {
            NavigationNodeDefinition.ValidateCoordinate(x); NavigationNodeDefinition.ValidateCoordinate(y);
            var node = Find(id) ?? throw new ArgumentException("导航目标不存在。", nameof(id));
            double dx = node.X - x, dy = node.Y - y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double ArrivalSeconds(float x, float y, string id) => Distance(x, y, id) / Speed + DescentSeconds;

        public bool Matches(NavigationDefinition other)
        {
            if (other == null || Speed != other.Speed || DescentSeconds != other.DescentSeconds || Nodes.Count != other.Nodes.Count)
                return false;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var a = Nodes[i]; var b = other.Nodes[i];
                if (a.Id != b.Id || a.Name != b.Name || a.X != b.X || a.Y != b.Y ||
                    a.Danger != b.Danger || a.Risk != b.Risk || a.PreviewOnly != b.PreviewOnly) return false;
            }
            return true;
        }
    }
}
