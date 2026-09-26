using System;
using DarkNights.Core.Config.Expedition;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>冻结星球预设的具体网络合同；构造只读目录时执行配置的有界校验，不能注入类型或任意策略。</summary>
    [MemoryPackable]
    public partial class PlanetWire
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public string Seed { get; set; }
        public int DockColumn { get; set; }
        public int DockRow { get; set; }
        public int LandingWidth { get; set; }
        public float ArrivalHeight { get; set; }
        public float HorizontalRange { get; set; }
        public float MaximumLift { get; set; }
        public float TransitSeconds { get; set; }
        public int StarCount { get; set; }
        public float StarSpeed { get; set; }
        public string TransitionKind { get; set; }
        public string SpaceColorHex { get; set; }
        public string SkyColorHex { get; set; }

        public static PlanetWire From(PlanetDefinition p) => new PlanetWire
        {
            Id = p.Id, DisplayName = p.DisplayName, Description = p.Description, Enabled = p.Enabled,
            Seed = p.Seed, DockColumn = p.DockColumn, DockRow = p.DockRow, LandingWidth = p.LandingWidth,
            ArrivalHeight = p.ArrivalHeight, HorizontalRange = p.HorizontalRange, MaximumLift = p.MaximumLift,
            TransitSeconds = p.TransitSeconds, StarCount = p.StarCount, StarSpeed = p.StarSpeed,
            TransitionKind = p.TransitionKind, SpaceColorHex = p.SpaceColorHex, SkyColorHex = p.SkyColorHex
        };

        public PlanetDefinition Freeze()
        {
            try
            {
                return new PlanetDefinition(Id, DisplayName, Description, Enabled, Seed, DockColumn, DockRow,
                    LandingWidth, ArrivalHeight, HorizontalRange, MaximumLift, TransitSeconds, StarCount,
                    StarSpeed, TransitionKind, SpaceColorHex, SkyColorHex);
            }
            catch (ArgumentException e) { throw new FormatException("无效星球网络配置。", e); }
        }
    }
}
