using System;
using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>航程阶段与冻结目录的有界网络投影；驾驶者仍随飞船合同发送，完整验证后才能发布给本地界面。</summary>
    [MemoryPackable]
    public partial class JourneyWire
    {
        public bool Enabled { get; set; }
        public string JourneyId { get; set; }
        public int Revision { get; set; }
        public int Phase { get; set; }
        public string PlanetId { get; set; }
        public string Seed { get; set; }
        public string MapId { get; set; }
        public string ContentFingerprint { get; set; }
        public double PhaseElapsed { get; set; }
        public string Error { get; set; }
        public PlanetWire[] Planets { get; set; }

        public static JourneyWire From(JourneyViewData v) => v == null ? null : new JourneyWire
        {
            Enabled = v.Enabled, JourneyId = v.JourneyId, Revision = v.Revision, Phase = (int)v.Phase,
            PlanetId = v.PlanetId, Seed = v.Seed, MapId = v.MapId, ContentFingerprint = v.ContentFingerprint,
            PhaseElapsed = v.PhaseElapsed, Error = v.Error, Planets = v.Planets.Select(PlanetWire.From).ToArray()
        };

        public JourneyViewData Freeze()
        {
            if (Planets == null || Planets.Length == 0 || Planets.Length > 32 || Planets.Any(p => p == null) ||
                JourneyId == null || PlanetId == null || Seed == null || MapId == null || ContentFingerprint == null || Error == null)
                throw new FormatException("航程网络目录或字段缺失。");
            var value = new JourneyViewData(Enabled, JourneyId, Revision, (JourneyPhase)Phase, PlanetId, Seed,
                MapId, ContentFingerprint, PhaseElapsed, Error, Planets.Select(p => p.Freeze()).ToArray());
            string error = JourneyValidator.Validate(value);
            if (error.Length != 0) throw new FormatException(error);
            return value;
        }
    }
}
