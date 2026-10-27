using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>既有航程可靠投影内的有界星图合同；不增加独立RPC，冻结前核对每个节点与数值。</summary>
    [MemoryPackable]
    public partial class NavigationWire
    {
        public float Speed { get; set; }
        public float DescentSeconds { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string RunId { get; set; }
        public string SettledRunId { get; set; }
        public int LastIncome { get; set; }
        public NavigationNodeWire[] Nodes { get; set; }
        public string SurveyBits { get; set; }
        public bool CoordinateFlight { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }

        public static NavigationWire From(JourneyNavigationData data) => data == null ? null : new NavigationWire
        {
            Speed = data.Definition.Speed, DescentSeconds = data.Definition.DescentSeconds,
            X = data.X, Y = data.Y, RunId = data.RunId, SettledRunId = data.SettledRunId, LastIncome = data.LastIncome,
            SurveyBits = data.Survey.Encoded, CoordinateFlight = data.CoordinateFlight, TargetX = data.TargetX, TargetY = data.TargetY,
            Nodes = data.Definition.Nodes.Select(n => new NavigationNodeWire
            { Id = n.Id, Name = n.Name, X = n.X, Y = n.Y, Danger = n.Danger, Risk = n.Risk, PreviewOnly = n.PreviewOnly }).ToArray()
        };

        public JourneyNavigationData Freeze()
        {
            if (Nodes == null || Nodes.Length < 1 || Nodes.Length > 32 || Nodes.Any(n => n == null))
                throw new FormatException("导航网络节点缺失或超限。");
            try
            {
                return new JourneyNavigationData(new NavigationDefinition(Speed, DescentSeconds,
                    Nodes.Select(n => new NavigationNodeDefinition(n.Id, n.Name, n.X, n.Y, n.Danger, n.Risk, n.PreviewOnly))),
                    X, Y, RunId, SettledRunId, LastIncome, new NavigationSurveyData(SurveyBits), CoordinateFlight, TargetX, TargetY);
            }
            catch (ArgumentException e) { throw new FormatException("导航网络字段无效。", e); }
        }
    }
}
