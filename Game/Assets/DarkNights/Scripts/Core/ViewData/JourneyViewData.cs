using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.ViewData
{
    /// <summary>航程与会话冻结星球目录的只读投影；不保存第二份飞船、驾驶者或地图状态，客户端只据此显示和提交请求。</summary>
    public sealed class JourneyViewData
    {
        public bool Enabled { get; }
        public string JourneyId { get; }
        public int Revision { get; }
        public JourneyPhase Phase { get; }
        public string PlanetId { get; }
        public string Seed { get; }
        public string MapId { get; }
        public string ContentFingerprint { get; }
        public double PhaseElapsed { get; }
        public string Error { get; }
        public IReadOnlyList<PlanetDefinition> Planets { get; }
        public PlanetDefinition ActivePlanet => Planets.FirstOrDefault(p => p.Id == PlanetId);

        public JourneyViewData(bool enabled, string journeyId, int revision, JourneyPhase phase,
            string planetId, string seed, string mapId, string contentFingerprint, double phaseElapsed,
            string error, IReadOnlyList<PlanetDefinition> planets)
        {
            Enabled = enabled; JourneyId = journeyId ?? ""; Revision = revision; Phase = phase;
            PlanetId = planetId ?? ""; Seed = seed ?? ""; MapId = mapId ?? "";
            ContentFingerprint = contentFingerprint ?? ""; PhaseElapsed = phaseElapsed; Error = error ?? "";
            Planets = Array.AsReadOnly(planets == null ? Array.Empty<PlanetDefinition>() : planets.ToArray());
        }
    }
}
