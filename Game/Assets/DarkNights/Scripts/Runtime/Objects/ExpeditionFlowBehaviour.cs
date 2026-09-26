using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>配置驱动的航程能力；只持有冻结配置及后台候选句柄，航程、船体和乘员始终写回各自唯一 YYGC 状态。</summary>
    [RequireConfig(typeof(ExpeditionFlowConfig))]
    public sealed partial class ExpeditionFlowBehaviour : PooledBehaviour
    {
        [Inject] private ExpeditionFlowConfig config;
        private ObjectSession world;
        private Task<PlayableTerrain> generation;
        private CancellationTokenSource generationCancellation;
        private PlayableTerrain candidate;
        private string taskJourney;
        public IReadOnlyList<PlanetDefinition> Planets { get; private set; } = Array.Empty<PlanetDefinition>();
        public string ContentFingerprint { get; private set; } = "";
        public bool Enabled { get; private set; }
        public double PreparationTimeoutSeconds { get; private set; }
        public double ArrivalTimeoutSeconds { get; private set; }
        public JourneyPhase Phase => world?.Journey?.Read()?.Phase ?? JourneyPhase.Orbit;
        public bool IsSpace => Enabled && Phase is JourneyPhase.Orbit or JourneyPhase.Preparing or JourneyPhase.Transit;
        public bool CanSave => !Enabled || Phase is JourneyPhase.Orbit or JourneyPhase.Descent or JourneyPhase.Landed;
        public PlanetDefinition ActivePlanet => Planets.FirstOrDefault(p => p.Id == world?.Journey?.Read()?.PlanetId);

        public bool Accepts(JourneyViewData value)
        {
            if (value == null) return !Enabled;
            if (!Enabled || !value.Enabled || value.ContentFingerprint != ContentFingerprint || value.Planets.Count != Planets.Count)
                return false;
            for (int i = 0; i < Planets.Count; i++)
            {
                var a = Planets[i]; var b = value.Planets[i];
                if (b == null || a.Id != b.Id || a.DisplayName != b.DisplayName || a.Description != b.Description ||
                    a.Enabled != b.Enabled || a.Seed != b.Seed || a.DockColumn != b.DockColumn || a.DockRow != b.DockRow ||
                    a.LandingWidth != b.LandingWidth || a.ArrivalHeight != b.ArrivalHeight || a.HorizontalRange != b.HorizontalRange ||
                    a.MaximumLift != b.MaximumLift || a.TransitSeconds != b.TransitSeconds || a.StarCount != b.StarCount ||
                    a.StarSpeed != b.StarSpeed || a.TransitionKind != b.TransitionKind || a.SpaceColorHex != b.SpaceColorHex ||
                    a.SkyColorHex != b.SkyColorHex) return false;
            }
            return true;
        }

        public void InitializeSession(ObjectSession session)
        {
            ResetPending();
            world = session ?? throw new ArgumentNullException(nameof(session));
            if (config == null) throw new InvalidOperationException("航程能力缺少 ExpeditionFlowConfig。");
            Planets = Array.AsReadOnly(config.FreezePlanets());
            ContentFingerprint = config.Fingerprint();
            PreparationTimeoutSeconds = config.PreparationTimeoutSeconds;
            ArrivalTimeoutSeconds = config.ArrivalTimeoutSeconds;
            Enabled = config.Enabled && world.IsExpedition;
        }

        internal void PrepareOrbit()
        {
            if (!Enabled) return;
            var ship = world.Expedition.Ship.Edit();
            ship.ShipPhase = 3; ship.ShipDoorClock = 0; ship.PilotId = 0;
            ship.ShipVelocityX = ship.ShipVelocityY = 0;
            foreach (var actor in world.Index.Actors.Where(a => !a.Enemy)) world.Ship.Cabin.Place(actor);
            world.Notify("太空待命：可在船内走动，靠近右侧驾驶台选择目的地。");
        }

        public int SelectDestination(ActorBehaviour hero, string planetId, int expectedRevision)
        {
            if (!Enabled || Phase != JourneyPhase.Orbit || hero == null || hero.Hp <= 0 ||
                world.Journey.Read().Revision != expectedRevision) return 0;
            var planet = Planets.FirstOrDefault(p => p.Id == planetId && p.Enabled);
            var ship = world.Expedition.Ship;
            if (planet == null || ship == null) return 0;
            var a = hero.Read(); var s = ship.Read();
            if (a.ControllerSlot < 0 || !a.Boarded || s.PilotId != 0 ||
                !ShipGeometry.AtPilot(a.X - s.X, a.Height - s.Height) ||
                world.Index.Actors.Any(v => !v.Enemy && v.Hp > 0 && !v.Read().Boarded)) return 0;
            ship.Edit().PilotId = hero.Id;
            a = hero.Edit(); a.ControlLease = checked(a.ControlLease + 1); HeroControlBehaviour.ResetInput(a);
            var journey = world.Journey.Edit();
            journey.JourneyId = Guid.NewGuid().ToString("N"); journey.MapId = Guid.NewGuid().ToString("N");
            journey.PlanetId = planet.Id;
            journey.Seed = string.IsNullOrWhiteSpace(planet.Seed)
                ? planet.Id + "-" + world.Camp.RandomInt(1, 1000000) : planet.Seed;
            world.Journey.SetPhase(JourneyPhase.Preparing);
            world.Notify("正在准备 " + planet.DisplayName + "；你已取得本次航程驾驶席。");
            return 1;
        }

        public int Cancel(ActorBehaviour hero)
        {
            if (!Enabled || Phase != JourneyPhase.Preparing || hero == null ||
                world.Expedition.Ship.Read().PilotId != hero.Id) return 0;
            Abort("已取消航程。"); return 1;
        }

        internal void Tick(double delta)
        {
            if (!Enabled) return;
            if (Phase is JourneyPhase.Preparing or JourneyPhase.Transit or JourneyPhase.ArrivalSync)
                world.Journey.Edit().PhaseElapsed += delta;
            if ((Phase is JourneyPhase.Preparing or JourneyPhase.Transit) && !PilotAvailable())
            { Abort("驾驶者已离线，航程已取消；可重新选择目的地。"); return; }
            if (Phase == JourneyPhase.Preparing && world.Journey.Read().PhaseElapsed > PreparationTimeoutSeconds)
                Abort("地图准备超时，请重新选择目的地。");
        }

        public void Pump()
        {
            if (!Enabled || world == null || !world.Context.IsActive) return;
            if ((Phase is JourneyPhase.Preparing or JourneyPhase.Transit) && !PilotAvailable())
            { world.Mutations.Run(() => { Abort("驾驶者已离线，航程已取消。"); return true; }); return; }
            if (world.Paused) return;
            if (Phase != JourneyPhase.Preparing) return;
            var journey = world.Journey.Read();
            if (generation == null)
            {
                var planet = ActivePlanet;
                string seed = journey.Seed, mapId = journey.MapId;
                taskJourney = journey.JourneyId;
                generationCancellation = new CancellationTokenSource();
                var token = generationCancellation.Token;
                generation = Task.Run(() => GenerateCandidate(planet, seed, mapId, token), token);
                return;
            }
            if (!generation.IsCompleted) return;
            if (taskJourney != journey.JourneyId) { ResetPending(); return; }
            if (generation.IsFaulted || generation.IsCanceled)
            {
                string detail = generation.Exception?.GetBaseException().Message ?? "任务已取消";
                world.Mutations.Run(() => { Abort("地图准备失败：" + detail); return true; }); return;
            }
            PlayableTerrain ready = generation.Result;
            world.Mutations.Run(() =>
            { world.Journey.Edit().Seed = ready.Seed; world.Journey.SetPhase(JourneyPhase.Transit); return true; });
            candidate = ready; generation = null;
        }

        public PlayableTerrain TakeArrivalCandidate()
        {
            if (!Enabled || world.Paused || Phase != JourneyPhase.Transit || candidate == null ||
                world.Journey.Read().PhaseElapsed < ActivePlanet.TransitSeconds) return null;
            return candidate;
        }

        public void ArrivalCommitted()
        {
            if (Phase != JourneyPhase.Transit || candidate?.WorldId != world.Journey.Read().MapId)
                throw new InvalidOperationException("到达提交与航程候选不匹配。");
            var planet = ActivePlanet;
            world.Ship.Arrive(planet.DockX, planet.DockHeight, planet.ArrivalHeight);
            world.Journey.SetPhase(JourneyPhase.ArrivalSync);
            world.Mutations.AfterCommit(ResetPending);
            world.Notify("已到达 " + planet.DisplayName + " 上空，正在同步降落环境。");
        }

        public void ReleaseDescent()
        {
            if (!Enabled || Phase != JourneyPhase.ArrivalSync) return;
            world.Journey.SetPhase(JourneyPhase.Descent);
            var pilot = world.Index.Find<ActorBehaviour>(world.Expedition.Ship.Read().PilotId);
            if (pilot != null)
            {
                var a = pilot.Edit(); a.ControlLease = checked(a.ControlLease + 1); HeroControlBehaviour.ResetInput(a);
            }
            world.Notify("可以驾驶降落：A/D 平移，空格上升，S 下降；低速接近降落区后确认着陆。");
        }

        public void ArrivalFailed(string reason)
        {
            reason = Bounded(reason, 512);
            if (Phase is JourneyPhase.Preparing or JourneyPhase.Transit) Abort(reason);
            else if (Phase == JourneyPhase.ArrivalSync)
            { world.Journey.Edit().Error = reason; world.Notify(Bounded(reason, 256), true); }
        }

        internal void Landed()
        {
            if (!Enabled || Phase != JourneyPhase.Descent) return;
            world.Journey.SetPhase(JourneyPhase.Landed);
            world.Expedition.BeginGround();
        }

        private bool PilotAvailable()
        {
            var pilot = world.Index.Find<ActorBehaviour>(world.Expedition.Ship.Read().PilotId);
            return pilot != null && pilot.Hp > 0 && pilot.Read().Boarded && pilot.Read().ControllerSlot >= 0;
        }

        private void Abort(string reason)
        {
            reason = Bounded(reason, 512);
            var ship = world.Expedition.Ship.Edit();
            var pilot = world.Index.Find<ActorBehaviour>(ship.PilotId);
            if (pilot != null)
            {
                var a = pilot.Edit(); a.ControlLease = checked(a.ControlLease + 1); HeroControlBehaviour.ResetInput(a);
            }
            ship.PilotId = 0; ship.ShipVelocityX = ship.ShipVelocityY = 0;
            var journey = world.Journey.Edit();
            journey.JourneyId = journey.PlanetId = journey.Seed = journey.MapId = "";
            world.Journey.SetPhase(JourneyPhase.Orbit, reason);
            world.Mutations.AfterCommit(ResetPending);
            world.Notify(Bounded(reason, 256), true);
        }

        public void ResetPending()
        {
            generationCancellation?.Cancel(); generationCancellation?.Dispose(); generationCancellation = null;
            if (generation != null)
                _ = generation.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            generation = null; candidate = null; taskJourney = null;
        }

        private static string Bounded(string value, int limit) => value == null ? "" : value.Length <= limit ? value : value.Substring(0, limit);

        private static PlayableTerrain GenerateCandidate(PlanetDefinition planet, string seed, string mapId, CancellationToken token)
        {
            int attempts = planet.Seed.Length == 0 ? 3 : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                string actualSeed = attempt == 0 ? seed : seed + "-retry" + attempt;
                try { return PlanetTerrainGenerator.Generate(planet, actualSeed, mapId, () => token.IsCancellationRequested); }
                catch (InvalidOperationException) when (attempt + 1 < attempts) { }
            }
            throw new InvalidOperationException("星球候选生成重试已耗尽。");
        }

        protected override void OnSpawn()
        {
            ResetPending(); world = null; Enabled = false; Planets = Array.Empty<PlanetDefinition>();
            ContentFingerprint = ""; PreparationTimeoutSeconds = ArrivalTimeoutSeconds = 0;
        }

        public override void OnDespawn()
        {
            ResetPending(); world = null; Enabled = false;
            base.OnDespawn();
        }
    }
}
