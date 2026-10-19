using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>固定飞船航程协调；冻结配置与地图候选仅作资源准备，业务唯一归Journey／船体所属YYGC状态。</summary>
    [RequireConfig(typeof(ExpeditionFlowConfig))]
    public sealed partial class ExpeditionFlowBehaviour : PooledBehaviour
    {
        [Inject] private ExpeditionFlowConfig config;
        private ObjectSession world;
        private Core.Config.Terrain.PlayableTerrain candidate;
        private string candidateTicket;
        public IReadOnlyList<PlanetDefinition> Planets { get; private set; } = Array.Empty<PlanetDefinition>();
        public string ContentFingerprint { get; private set; } = "";
        public NavigationDefinition Navigation { get; private set; }
        public bool Enabled { get; private set; }
        public double PreparationTimeoutSeconds { get; private set; }
        public double ArrivalTimeoutSeconds { get; private set; }
        public JourneyPhase Phase => world?.Journey?.Read()?.Phase ?? JourneyPhase.Orbit;
        public bool IsSpace => Enabled && Phase is JourneyPhase.Orbit or JourneyPhase.Preparing or JourneyPhase.Transit;
        public bool CanSave => !Enabled || Phase is JourneyPhase.Orbit or JourneyPhase.Landed;
        public PlanetDefinition ActivePlanet => Planets.FirstOrDefault(p => p.Id == world?.Journey?.Read()?.PlanetId);

        public bool Accepts(JourneyViewData value)
        {
            if (value == null) return !Enabled;
            return Enabled && value.Enabled && value.ContentFingerprint == ContentFingerprint &&
                value.Navigation != null && Navigation.Matches(value.Navigation.Definition) &&
                value.Planets.Count == Planets.Count && value.Planets.Zip(Planets, (a, b) =>
                    a != null && a.Id == b.Id && a.Enabled == b.Enabled && a.Seed == b.Seed &&
                    a.DockColumn == b.DockColumn && a.DockRow == b.DockRow && a.ArrivalHeight == b.ArrivalHeight &&
                    a.DisplayName == b.DisplayName && a.Description == b.Description && a.LandingWidth == b.LandingWidth &&
                    a.HorizontalRange == b.HorizontalRange && a.MaximumLift == b.MaximumLift && a.TransitSeconds == b.TransitSeconds &&
                    a.StarCount == b.StarCount && a.StarSpeed == b.StarSpeed && a.TransitionKind == b.TransitionKind &&
                    a.SpaceColorHex == b.SpaceColorHex && a.SkyColorHex == b.SkyColorHex).All(ok => ok);
        }

        public void InitializeSession(ObjectSession session)
        {
            ResetPending(); world = session ?? throw new ArgumentNullException(nameof(session));
            if (config == null) throw new InvalidOperationException("航程能力缺少配置。");
            Planets = Array.AsReadOnly(config.FreezePlanets()); Navigation = config.Navigation.Freeze(Planets);
            ContentFingerprint = config.Fingerprint();
            PreparationTimeoutSeconds = config.PreparationTimeoutSeconds; ArrivalTimeoutSeconds = config.ArrivalTimeoutSeconds;
            Enabled = config.Enabled && world.IsExpedition;
        }

        internal bool AtNavigation(ActorBehaviour hero)
        {
            var s = world.Expedition.Ship?.Read(); var a = hero?.Read();
            return s != null && a != null && hero.Hp > 0 && a.ControllerSlot >= 0 && a.Boarded &&
                ShipGeometry.AtPilot(a.X - s.X, a.Height - s.Height);
        }

        public int SelectDestination(ActorBehaviour hero, string planetId, int expectedRevision)
        {
            if (!Enabled || Phase is not (JourneyPhase.Orbit or JourneyPhase.Transit) || !AtNavigation(hero) ||
                world.Journey.Read().Revision != expectedRevision) return 0;
            var node = Navigation.Find(planetId);
            if (node == null || node.PreviewOnly || !Planets.Any(p => p.Id == planetId && p.Enabled)) return 0;
            var journey = world.Journey.Edit();
            journey.JourneyId = Guid.NewGuid().ToString("N"); journey.MapId = Guid.NewGuid().ToString("N");
            journey.PlanetId = planetId; journey.Seed = ActivePlanet.Seed;
            world.Journey.SetPhase(JourneyPhase.Preparing);
            world.Mutations.AfterCommit(ResetPending);
            world.Notify("全队航线已确认：" + node.Name + "，正在装载审核地图。");
            return 1;
        }

        internal int Depart(ActorBehaviour hero)
        {
            if (!Enabled || Phase != JourneyPhase.Landed || !AtNavigation(hero) ||
                world.Expedition.Ship.Read().ShipDoorClock != 0) return 0;
            world.Journey.SetPhase(JourneyPhase.Gathering);
            world.Expedition.Ship.Edit().ShipPhase = 1;
            world.Notify("准备撤离：请全员上船；人员未齐或有人离船时停止倒计时。"); return 1;
        }

        public int Cancel(ActorBehaviour hero)
        {
            if (!Enabled || !AtNavigation(hero)) return 0;
            if (Phase == JourneyPhase.Ascent && world.Journey.Read().Error.Length != 0)
            {
                var j = world.Journey.Edit(); j.Error = ""; j.Revision = checked(j.Revision + 1);
                world.Mutations.AfterCommit(ResetPending); return 1;
            }
            if (Phase == JourneyPhase.Gathering)
            {
                var ship = world.Expedition.Ship.Edit(); ship.ShipPhase = 0; ship.ShipDoorClock = 0;
                world.Journey.SetPhase(JourneyPhase.Landed); world.Notify("已取消撤离，坡道保持开放。"); return 1;
            }
            if (Phase is not (JourneyPhase.Preparing or JourneyPhase.Transit)) return 0;
            Abort("已取消目标，飞船在当前位置安全待命。"); return 1;
        }

        internal void Tick(double delta)
        {
            if (!Enabled) return;
            if (Phase is not (JourneyPhase.Orbit or JourneyPhase.Landed)) world.Journey.Edit().PhaseElapsed += delta;
            var j = world.Journey.Edit();
            if (Phase == JourneyPhase.Ascent)
                j.StarY = Math.Clamp(Navigation.Find(j.PlanetId).Y + Navigation.Speed *
                    (float)Math.Min(j.PhaseElapsed, Math.Max(1, Navigation.DescentSeconds)), -4096, 4096);
            if (Phase == JourneyPhase.Transit)
            {
                var n = Navigation.Find(j.PlanetId); double distance = Navigation.Distance(j.StarX, j.StarY, n.Id);
                double amount = Math.Min(distance, Navigation.Speed * delta);
                if (distance > 0) { j.StarX += (float)((n.X - j.StarX) * amount / distance); j.StarY += (float)((n.Y - j.StarY) * amount / distance); }
            }
            if (Phase == JourneyPhase.Preparing && j.PhaseElapsed > PreparationTimeoutSeconds) Abort("地图准备超时，保留当前船位和收入。");
        }

        public void Pump()
        {
            if (!Enabled || world == null || !world.Context.IsActive || world.Paused) return;
            if (Phase == JourneyPhase.Ascent && world.Journey.Read().Error.Length != 0) return;
            if (Phase == JourneyPhase.Ascent && candidate == null)
            { candidate = PlanetTerrainGenerator.Space(Guid.NewGuid().ToString("N")); candidateTicket = world.Journey.Read().RunId; }
            if (Phase != JourneyPhase.Preparing) return;
            try
            {
                var j = world.Journey.Read();
                if (config.ReviewedMap == null) throw new InvalidOperationException("缺少审核地图，拒绝回退到运行时抽图。");
                config.ReviewedMap.VerifySource(config);
                var prepared = config.ReviewedMap.Freeze(j.MapId);
                string ticket = j.JourneyId;
                world.Mutations.Run(() => { world.Journey.Edit().Seed = prepared.Seed; world.Journey.SetPhase(JourneyPhase.Transit); return true; });
                candidate = prepared; candidateTicket = ticket;
            }
            catch (Exception error) { world.Mutations.Run(() => { Abort("地图准备失败：" + error.Message); return true; }); }
        }

        public Core.Config.Terrain.PlayableTerrain TakeArrivalCandidate()
        {
            if (!Enabled || world.Paused || candidate == null) return null;
            var j = world.Journey.Read();
            if (Phase == JourneyPhase.Ascent && candidateTicket == j.RunId && j.PhaseElapsed >= Math.Max(1, Navigation.DescentSeconds)) return candidate;
            if (Phase == JourneyPhase.Transit && candidateTicket == j.JourneyId &&
                Navigation.Distance(j.StarX, j.StarY, j.PlanetId) <= .01) return candidate;
            return null;
        }

        public void ArrivalCommitted()
        {
            if (candidate == null) throw new InvalidOperationException("缺少航程候选。");
            if (Phase == JourneyPhase.Ascent)
            {
                new ShipCargoService(world).Settle();
                world.Ship.Arrive(ExpeditionTerrainGenerator.ShipX, 0, 0);
                ClearTarget(); world.Journey.SetPhase(JourneyPhase.Orbit);
                world.Notify("已脱离矿区，货物自动交接；可补给并选择下一目的地。");
            }
            else
            {
                if (Phase != JourneyPhase.Transit || candidate.WorldId != world.Journey.Read().MapId)
                    throw new InvalidOperationException("到达候选身份失效。");
                world.Ship.Arrive(ActivePlanet.DockX, ActivePlanet.DockHeight, ActivePlanet.ArrivalHeight);
                world.Journey.Edit().RunId = Guid.NewGuid().ToString("N");
                world.Journey.SetPhase(JourneyPhase.ArrivalSync);
                world.Notify("已到达星球上空，等待全队地图就绪后自动降落。");
            }
            world.Mutations.AfterCommit(ResetPending);
        }

        public void ReleaseDescent()
        {
            if (!Enabled || Phase != JourneyPhase.ArrivalSync) return;
            world.Journey.SetPhase(JourneyPhase.Descent); world.Notify("降落环境已就绪，飞船自动降落。");
        }

        public void ArrivalFailed(string reason)
        {
            if (Phase is JourneyPhase.Preparing or JourneyPhase.Transit) Abort(reason);
            else
            {
                world.Journey.Edit().Error = Bounded(reason, 512);
                world.Mutations.AfterCommit(ResetPending); world.Notify(Bounded(reason, 256), true);
            }
        }

        internal void Landed()
        {
            if (!Enabled || Phase != JourneyPhase.Descent) return;
            world.Journey.SetPhase(JourneyPhase.Landed); world.Expedition.BeginGround();
        }

        internal void PrepareOrbit()
        {
            if (!Enabled) return;
            var ship = world.Expedition.Ship.Edit(); ship.ShipPhase = 3; ship.ShipDoorClock = 0; ship.PilotId = 0;
            ship.ShipVelocityX = ship.ShipVelocityY = 0;
            foreach (var actor in world.Index.Actors.Where(a => !a.Enemy)) world.Ship.Cabin.Place(actor);
        }

        private void ClearTarget()
        {
            var j = world.Journey.Edit(); j.JourneyId = j.PlanetId = j.Seed = j.MapId = "";
        }
        private void Abort(string reason)
        {
            ClearTarget(); world.Journey.SetPhase(JourneyPhase.Orbit, Bounded(reason, 512));
            world.Mutations.AfterCommit(ResetPending); world.Notify(Bounded(reason, 256), true);
        }
        public void ResetPending() { candidate = null; candidateTicket = null; }
        private static string Bounded(string value, int limit) => value == null ? "" : value.Length <= limit ? value : value.Substring(0, limit);
        protected override void OnSpawn() => ClearSession();
        public override void OnDespawn() { ClearSession(); base.OnDespawn(); }
        private void ClearSession()
        {
            ResetPending(); world = null; Enabled = false; Planets = Array.Empty<PlanetDefinition>(); Navigation = null;
            ContentFingerprint = ""; PreparationTimeoutSeconds = ArrivalTimeoutSeconds = 0;
        }
    }
}
