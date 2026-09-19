using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using DarkNights.Core.Save;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 单局 YYGC 对象的组合与调度上下文，只保存能力和对象引用，不拥有另一份运行世界。
    /// State 分别归会话及个体 Behaviour；所有玩法入口在主线程短事务内执行。
    /// </summary>
    public sealed class ObjectSession : IDisposable
    {
        private readonly DIContainer container = new DIContainer();
        private readonly HashSet<ObjectInstance> sceneObjects = new HashSet<ObjectInstance>();
        private readonly Dictionary<string, ObjectInstance> sceneOwners = new Dictionary<string, ObjectInstance>();
        private readonly Dictionary<string, ObjectDefinition> sceneDefinitions = new Dictionary<string, ObjectDefinition>();
        private SessionSnapshot initial;
        public ObjectWorldSaveJson SaveCodec { get; private set; }
        private readonly Func<bool> authority;
        private ObjectInstance owner;
        private bool disposed;
        public Terrain.SessionTerrain Terrain { get; set; }
        public GameCatalog Catalog { get; }
        public LevelLayout Layout { get; }
        public ObjectSessionResources Resources { get; }
        public Transform Parent { get; }
        public SessionEntityIndex Index { get; private set; } = new SessionEntityIndex();
        public ObjectSessionContext Context { get; }
        public ObjectSessionContext EntityContext { get; private set; }
        public ObjectMutationBatch Mutations { get; }
        public CampSimulationBehaviour Camp { get; private set; }
        public EconomyBehaviour Economy { get; private set; }
        public WaveBehaviour Waves { get; private set; }
        public ProjectileBehaviour Projectiles { get; private set; }
        public ObjectWorkOrders Work { get; }
        public ObjectConstruction Construction { get; }
        public ObjectEntityLifecycle Lifecycle { get; }
        public ObjectCombat Combat { get; }
        public ObjectCampCommands Commands { get; }
        public SessionFeedback Feedback { get; } = new SessionFeedback();
        public bool Paused => Camp.Read().Paused;
        public int Speed => Camp.Read().Speed;
        public double Elapsed => Camp.Read().Elapsed;
        public float DebugHeroSpeedMultiplier { get; }

        public ObjectSession(GameCatalog catalog, LevelLayout layout, ObjectSessionResources resources,
            Func<bool> isAuthority, Transform parent = null, float debugHeroSpeedMultiplier = 1)
        {
            if (debugHeroSpeedMultiplier < 1 || debugHeroSpeedMultiplier > 16 || float.IsNaN(debugHeroSpeedMultiplier))
                throw new ArgumentOutOfRangeException(nameof(debugHeroSpeedMultiplier));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Layout.Validate(Catalog);
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            authority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            Parent = parent;
            DebugHeroSpeedMultiplier = debugHeroSpeedMultiplier;
            container.Initialize();
            container.Register(this);
            Context = ObjectSessionContext.CreateAuthority(container, () => !disposed && authority());
            EntityContext = ObjectSessionContext.CreateAuthority(container, () => !disposed && authority());
            Mutations = new ObjectMutationBatch(Context);
            Work = new ObjectWorkOrders(this);
            Construction = new ObjectConstruction(this);
            Lifecycle = new ObjectEntityLifecycle(this);
            Combat = new ObjectCombat(this);
            Commands = new ObjectCampCommands(this);
        }

        public void Prepare(ObjectInstance sessionOwner, IReadOnlyList<ObjectPlacement> placements)
        {
            if (owner != null || sessionOwner == null || sessionOwner.SessionContext != Context)
                throw new InvalidOperationException("Session owner must be assembled exactly once with the explicit context.");
            if (placements == null || placements.Select(p => p.PlacementKey).Distinct().Count() != placements.Count)
                throw new InvalidOperationException("Scene placement keys must be unique.");
            owner = sessionOwner;
            Camp = owner.GetBehaviour<CampSimulationBehaviour>() ?? throw new InvalidOperationException("Missing camp simulation capability.");
            Economy = owner.GetBehaviour<EconomyBehaviour>() ?? throw new InvalidOperationException("Missing economy capability.");
            Waves = owner.GetBehaviour<WaveBehaviour>() ?? throw new InvalidOperationException("Missing wave capability.");
            Projectiles = owner.GetBehaviour<ProjectileBehaviour>() ?? throw new InvalidOperationException("Missing projectile capability.");
            Camp.Prepare();
            Economy.Prepare();
            Waves.Prepare();
            Projectiles.Prepare();
            SaveCodec = new ObjectWorldSaveJson(Catalog, Layout,
                Resources.Definitions.ToDictionary(ObjectSessionResources.Rule, d => d.Guid.ToString()),
                placements.ToDictionary(p => p.PlacementKey, p => ObjectSessionResources.Rule(p.Definition)), Projectiles.Settings);
            Mutations.Run(() =>
            {
                foreach (ObjectPlacement placement in placements)
                    Create(placement.Definition, placement.X, placement.PlacementKey, true,
                        placement.Variant, placement.ActorName, placement.Loader);
                return true;
            });
            initial = CaptureWorld();
        }

        public void Activate()
        {
            Context.Activate();
            EntityContext.Activate();
            Terrain?.Activate();
            owner.Activate();
            foreach (IEntityBehaviour entity in Index.FreezeOrder())
            {
                entity.Object.Activate();
                entity.Object.gameObject.SetActive(true);
            }
            Loaded(true);
        }

        public void Loaded(bool restarted)
        {
            if (!restarted) return;
            Feedback.ShowBanner("灰松谷 · 第一天", "安排生产，训练守卫。守住三次夜袭。");
            Feedback.Notify("先安排一名工人耕作，再采集木材。东侧已有两名守卫。");
        }

        internal IEntityBehaviour Create(ObjectDefinition definition, float x, string placement, bool complete,
            int variant, string actorName, ObjectDefinitionLoader loader, bool enemy = false, int farmId = 0)
        {
            Mutations.RequireWriting();
            ObjectInstance instance = null;
            bool registered = false;
            try
            {
                if (loader != null)
                {
                    if (loader.ResolveDefinition() != definition) throw new InvalidOperationException("Placement definition changed after freezing.");
                    instance = loader.BindSession(EntityContext);
                    sceneObjects.Add(instance);
                    sceneOwners.Add(placement, instance);
                    sceneDefinitions.Add(placement, definition);
                }
                else instance = Resources.Create(definition, EntityContext, Parent).Owner;
                ObjectInstance created = instance;
                int id = Camp.Allocate();
                IEntityBehaviour entity = instance.GetAllBehaviors().OfType<IEntityBehaviour>().Single();
                if (entity is ActorBehaviour actor) actor.Prepare(id, x, placement, enemy, actorName);
                else if (entity is BuildingBehaviour building) building.Prepare(id, x, placement, complete);
                else if (entity is WorksiteBehaviour site) site.Prepare(id, x, placement, variant, farmId);
                else throw new InvalidOperationException("Unknown object family.");
                if (EntityContext.IsActive) instance.Activate();
                Index.Add(entity);
                Mutations.OnRollback(() => { Index.Remove(entity); Release(created); });
                registered = true;
                Mutations.AfterCommit(() => { if (EntityContext.IsActive) created.gameObject.SetActive(true); });
                if (entity is BuildingBehaviour farm && complete && farm.RuleKey == "farm")
                    farm.Edit().FarmSiteId = Lifecycle.SpawnSite("food", x, farmId: id).Id;
                return entity;
            }
            catch
            {
                if (instance != null && !registered) Release(instance);
                throw;
            }
        }

        public int IssueOrders(IReadOnlyList<int> ids, int targetId, float x) =>
            Mutations.Run(() => Work.Issue(ids, targetId, x));

        public int PlaceBuilding(string ruleKey, float x, IReadOnlyList<int> ids) =>
            Mutations.Run(() => Construction.Place(ruleKey, x, ids));

        public int TrainActors(string ruleKey, IReadOnlyList<int> ids) => Mutations.Run(() => Commands.Train(ruleKey, ids));
        public int Recruit() => Mutations.Run(Commands.Recruit);
        public bool Repair(int id) => Mutations.Run(() => Commands.Repair(id));
        public bool StartNight() => Mutations.Run(Waves.StartNight);

        public void SetTime(bool paused, int speed)
        {
            if (speed != 1 && speed != 2) throw new ArgumentOutOfRangeException(nameof(speed));
            Mutations.Run(() => { Camp.Edit().Paused = paused; Camp.Edit().Speed = speed; return true; });
        }

        public void Advance(double seconds)
        {
            if (!Context.IsActive) throw new InvalidOperationException("Prepared sessions cannot tick.");
            if (Camp.Read().Mode != SessionMode.Playing || Paused) return;
            Mutations.Run(() =>
            {
                double delta = Camp.BeginStep(seconds);
                Economy.Tick(delta);
                foreach (BuildingBehaviour building in Index.Buildings.ToArray()) building.Tick(delta);
                foreach (ActorBehaviour actor in Index.Actors.ToArray())
                {
                    if (Camp.Read().Mode != SessionMode.Playing) return true;
                    actor.Tick(delta);
                }
                foreach (WorksiteBehaviour site in Index.Worksites.ToArray()) site.Tick(delta);
                Projectiles.Tick(delta);
                if (Camp.Read().Mode == SessionMode.Playing) Waves.Tick(delta);
                return true;
            });
        }

        internal void Notify(string text, bool error = false) => Mutations.AfterCommit(() => Feedback.Notify(text, error));
        internal void Emit(VisualCue cue) => Mutations.AfterCommit(() => Feedback.Emit(cue));

        private void Release(ObjectInstance instance) => ReleaseEntity(instance);

        internal void ReleaseEntity(ObjectInstance instance)
        {
            if (instance == null) return;
            instance.Release();
            if (sceneObjects.Contains(instance)) { instance.gameObject.SetActive(false); return; }
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance.gameObject);
            else UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        public void Dispose()
        {
            if (disposed) return;
            if (Mutations.IsOpen) throw new InvalidOperationException("Cannot retire a session inside a state notification.");
            disposed = true;
            ObjectInstance[] entities = Index.FreezeOrder().Select(e => e.Object).ToArray();
            Index.Clear();
            EntityContext.Dispose();
            foreach (ObjectInstance entity in entities) Release(entity);
            Terrain?.Dispose();
            Context.Dispose();
            container.OnReturnToPool();
        }

        public bool ValidRequest(SessionRequest request) => ObjectSessionCommands.Valid(this, request);
        public int Apply(SessionRequest request, out int entityId) => ObjectSessionCommands.Apply(this, request, out entityId);
        public SessionSnapshot CaptureWorld() => ObjectSnapshotMapper.Capture(this);
        public WorldViewData CaptureView() => ObjectProjection.Capture(this);
        public void Restore(string json) => RestoreSnapshot(SaveCodec.Parse(json));
        public void Restart() => RestoreSnapshot(initial);

        private void RestoreSnapshot(SessionSnapshot snapshot)
        {
            if ((Terrain == null) != (snapshot.Terrain == null)) throw new FormatException("存档地图类型不匹配。");
            DarkNights.Runtime.Terrain.TerrainMapAuthority map = Terrain?.Prepare(snapshot.Terrain);
            try
            {
                using var candidate = new ObjectWorldRestore(this, snapshot);
                candidate.Commit();
                if (map != null) { Terrain.Replace(map, snapshot.Terrain.Seed); map = null; }
            }
            finally { map?.Dispose(); }
        }

        internal ObjectSessionContext NewEntityContext() =>
            ObjectSessionContext.CreateAuthority(container, () => !disposed && authority());

        internal ObjectInstance SceneOwner(string placement, ObjectDefinition definition) =>
            sceneDefinitions.TryGetValue(placement, out ObjectDefinition original) && original == definition ? sceneOwners[placement] : null;

        internal void ReplaceEntities(SessionEntityIndex index, ObjectSessionContext context)
        {
            Index = index;
            EntityContext = context;
        }
    }
}
