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
    public sealed class ObjectSession : SessionWorld
    {
        private readonly DIContainer container = new DIContainer();
        private readonly HashSet<ObjectInstance> sceneObjects = new HashSet<ObjectInstance>();
        private readonly Dictionary<string, ObjectInstance> sceneOwners = new Dictionary<string, ObjectInstance>();
        private SessionSnapshot initial;
        public ObjectWorldSaveJson SaveCodec { get; private set; }
        private readonly Func<bool> authority;
        private ObjectInstance owner;
        private bool disposed;
        public override GameCatalog Catalog { get; }
        public override LevelLayout Layout { get; }
        public ObjectSessionResources Resources { get; }
        public Transform Parent { get; }
        public SessionEntityIndex Index { get; private set; } = new SessionEntityIndex();
        public ObjectSessionContext Context { get; }
        public ObjectSessionContext EntityContext { get; private set; }
        public ObjectMutationBatch Mutations { get; }
        public CampSimulationBehaviour Camp { get; private set; }
        public EconomyBehaviour Economy { get; private set; }
        public ObjectWorkOrders Work { get; }
        public ObjectConstruction Construction { get; }
        public override SessionFeedback Feedback { get; } = new SessionFeedback();
        public override bool Paused => Camp.Read().Paused;
        public override int Speed => Camp.Read().Speed;
        public override double Elapsed => Camp.Read().Elapsed;

        public ObjectSession(GameCatalog catalog, LevelLayout layout, ObjectSessionResources resources,
            Func<bool> isAuthority, Transform parent = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            authority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            Parent = parent;
            container.Initialize();
            container.Register(this);
            Context = ObjectSessionContext.CreateAuthority(container, () => !disposed && authority());
            EntityContext = ObjectSessionContext.CreateAuthority(container, () => !disposed && authority());
            Mutations = new ObjectMutationBatch(Context);
            Work = new ObjectWorkOrders(this);
            Construction = new ObjectConstruction(this);
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
            Camp.Prepare();
            Economy.Prepare();
            SaveCodec = new ObjectWorldSaveJson(Catalog, Layout,
                Resources.Definitions.ToDictionary(ObjectSessionResources.Rule, d => d.Guid.ToString()),
                placements.ToDictionary(p => p.PlacementKey, p => ObjectSessionResources.Rule(p.Definition)));
            Mutations.Run(() =>
            {
                foreach (ObjectPlacement placement in placements)
                    Create(placement.Definition, placement.X, placement.PlacementKey, true,
                        placement.Variant, placement.ActorName, placement.Loader);
                return true;
            });
            initial = CaptureWorld();
        }

        public override void Activate()
        {
            Context.Activate();
            EntityContext.Activate();
            owner.Activate();
            foreach (IEntityBehaviour entity in Index.FreezeOrder())
            {
                entity.Object.Activate();
                entity.Object.gameObject.SetActive(true);
            }
        }

        internal IEntityBehaviour Create(ObjectDefinition definition, float x, string placement, bool complete,
            int variant, string actorName, ObjectDefinitionLoader loader)
        {
            Mutations.RequireWriting();
            ObjectInstance instance = null;
            try
            {
                if (loader != null)
                {
                    if (loader.ResolveDefinition() != definition) throw new InvalidOperationException("Placement definition changed after freezing.");
                    instance = loader.BindSession(EntityContext);
                    sceneObjects.Add(instance);
                    sceneOwners.Add(placement, instance);
                }
                else instance = Resources.Create(definition, EntityContext, Parent).Owner;
                ObjectInstance created = instance;
                int id = Camp.Allocate();
                IEntityBehaviour entity = instance.GetAllBehaviors().OfType<IEntityBehaviour>().Single();
                if (entity is ActorBehaviour actor) actor.Prepare(id, x, placement, false, actorName);
                else if (entity is BuildingBehaviour building) building.Prepare(id, x, placement, complete);
                else if (entity is WorksiteBehaviour site) site.Prepare(id, x, placement, variant, 0);
                else throw new InvalidOperationException("Unknown object family.");
                if (EntityContext.IsActive) instance.Activate();
                Index.Add(entity);
                Mutations.OnRollback(() => { Index.Remove(entity); Release(created); });
                Mutations.AfterCommit(() => { if (EntityContext.IsActive) created.gameObject.SetActive(true); });
                return entity;
            }
            catch
            {
                if (instance != null) Release(instance);
                throw;
            }
        }

        public int IssueOrders(IReadOnlyList<int> ids, int targetId, float x) =>
            Mutations.Run(() => Work.Issue(ids, targetId, x));

        public int PlaceBuilding(string ruleKey, float x, IReadOnlyList<int> ids) =>
            Mutations.Run(() => Construction.Place(ruleKey, x, ids));

        public void SetTime(bool paused, int speed)
        {
            if (speed != 1 && speed != 2) throw new ArgumentOutOfRangeException(nameof(speed));
            Mutations.Run(() => { Camp.Edit().Paused = paused; Camp.Edit().Speed = speed; return true; });
        }

        public override void Advance(double seconds)
        {
            if (!Context.IsActive) throw new InvalidOperationException("Prepared sessions cannot tick.");
            Mutations.Run(() =>
            {
                double delta = Camp.BeginStep(seconds);
                if (delta == 0) return false;
                Economy.Tick(delta);
                foreach (BuildingBehaviour building in Index.Buildings.ToArray()) building.Tick(delta);
                foreach (ActorBehaviour actor in Index.Actors.ToArray()) actor.Tick(delta);
                foreach (WorksiteBehaviour site in Index.Worksites.ToArray()) site.Tick(delta);
                return true;
            });
        }

        internal void Notify(string text, bool error = false) => Mutations.AfterCommit(() => Feedback.Notify(text, error));
        internal void Emit(VisualCue cue) => Mutations.AfterCommit(() => Feedback.Emit(cue));

        internal void Starve(ActorBehaviour actor)
        {
            ActorState state = actor.Edit();
            state.Hp = Math.Max(0, state.Hp - 1);
            state.HitFlash = 0.15;
            Emit(new VisualCue("damage", actor.X, Layout.GroundY - 19, "1"));
            if (state.Hp > 0) return;
            Work.Clear(actor);
            Camp.Edit().Lost++;
            Notify(actor.Name + "倒下了。", true);
            Emit(new VisualCue("corpse", actor.X, Layout.GroundY, ContentId: actor.RuleKey, Face: state.Face));
            Mutations.AfterCommit(() => Feedback.PlaySound("snd_worker_die1", -14));
            Index.Remove(actor, Mutations);
            Mutations.AfterCommit(() => Release(actor.Object));
        }

        private void Release(ObjectInstance instance) => ReleaseEntity(instance);

        internal void ReleaseEntity(ObjectInstance instance)
        {
            if (instance == null) return;
            instance.Release();
            if (sceneObjects.Contains(instance)) { instance.gameObject.SetActive(false); return; }
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance.gameObject);
            else UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        public override void Dispose()
        {
            if (disposed) return;
            if (Mutations.IsOpen) throw new InvalidOperationException("Cannot retire a session inside a state notification.");
            disposed = true;
            ObjectInstance[] entities = Index.FreezeOrder().Select(e => e.Object).ToArray();
            Index.Clear();
            EntityContext.Dispose();
            foreach (ObjectInstance entity in entities) Release(entity);
            Context.Dispose();
            container.OnReturnToPool();
        }

        public override bool ValidRequest(SessionRequest request) => ObjectSessionCommands.Valid(this, request);
        public override int Apply(SessionRequest request, out int entityId) => ObjectSessionCommands.Apply(this, request, out entityId);
        public override SessionSnapshot CaptureWorld() => ObjectSnapshotMapper.Capture(this);
        public override WorldViewData CaptureView() => ObjectProjection.Capture(this);
        public override SessionWorld Restore(string json) => RestoreSnapshot(SaveCodec.Parse(json));
        public override SessionWorld Restart() => RestoreSnapshot(initial);

        private SessionWorld RestoreSnapshot(SessionSnapshot snapshot)
        {
            using var candidate = new ObjectWorldRestore(this, snapshot);
            candidate.Commit();
            return this;
        }

        internal ObjectSessionContext NewEntityContext() =>
            ObjectSessionContext.CreateAuthority(container, () => !disposed && authority());

        internal ObjectInstance SceneOwner(string placement) =>
            sceneOwners.TryGetValue(placement, out ObjectInstance instance) ? instance : null;

        internal void ReplaceEntities(SessionEntityIndex index, ObjectSessionContext context)
        {
            Index = index;
            EntityContext = context;
        }
    }
}
