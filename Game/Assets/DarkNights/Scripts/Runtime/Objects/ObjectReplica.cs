using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.ViewData;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Types;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 客户端只读对象集合，先完整验证和准备候选对象，再按放置键接管原场景实例。
    /// 没有经济或 Tick；坏帧、装配或激活失败保留旧对象及上下文，成功才退休旧 epoch。
    /// </summary>
    public sealed class ObjectReplica : IDisposable
    {
        private readonly ObjectSessionResources resources;
        private readonly Dictionary<string, ObjectPlacement> placements;
        private Dictionary<int, ObjectInstance> objects = new Dictionary<int, ObjectInstance>();
        private readonly HashSet<ObjectInstance> sceneObjects = new HashSet<ObjectInstance>();
        private readonly DIContainer container = new DIContainer();
        private readonly Transform parent;
        private ObjectSessionContext context;
        private int epoch;
        private bool applying;
        public int Count => objects.Count;

        public ObjectReplica(ObjectSessionResources resources, IReadOnlyList<ObjectPlacement> placements, Transform parent)
        {
            this.resources = resources;
            this.placements = placements.ToDictionary(p => p.PlacementKey);
            this.parent = parent;
            container.Initialize();
            context = NewContext();
        }

        private ObjectSessionContext NewContext()
        {
            var created = ObjectSessionContext.CreateReplica(container);
            created.Activate();
            return created;
        }

        public ObjectView View(int id) => objects.TryGetValue(id, out ObjectInstance instance) ? instance.ObjectView : null;

        public void Validate(WorldViewData world)
        {
            var kinds = world.Actors.Select(a => (a.Id, a.Kind)).Concat(world.Buildings.Select(b => (b.Id, b.Kind)))
                .Concat(world.Worksites.Select(w => (w.Id, w.Kind))).ToDictionary(p => p.Id, p => p.Kind);
            if (world.Identities.Count != kinds.Count) throw new InvalidOperationException("Complete identity baseline is required.");
            var placementKeys = new HashSet<string>();
            foreach (EntityIdentityData identity in world.Identities)
            {
                var definition = resources.Find(kinds[identity.Id]);
                if (definition.Guid.ToString() != identity.DefinitionGuid ||
                    (identity.PlacementKey.Length != 0 && (!placementKeys.Add(identity.PlacementKey) ||
                    !placements.TryGetValue(identity.PlacementKey, out var source) ||
                    (source.Definition != definition && (source.Definition.Type != ObjectType.Unit || definition.Type != ObjectType.Unit)))))
                    throw new InvalidOperationException("Unknown or conflicting object identity in projection.");
            }
        }

        public void Apply(WorldViewData world, int worldEpoch = 1)
        {
            if (applying || worldEpoch <= 0) throw new InvalidOperationException("Invalid or reentrant replica application.");
            Validate(world);
            IReadOnlyList<ReplicaEntityState> rows = ReplicaEntityState.From(world);
            bool replacing = epoch != worldEpoch;
            ObjectSessionContext nextContext = replacing ? NewContext() : context;
            var previous = objects;
            var next = new Dictionary<int, ObjectInstance>();
            var owned = new List<ObjectInstance>();
            var rollback = new List<Action>();
            var changes = new List<SessionStateChange>();
            bool committed = false;
            applying = true;
            try
            {
                foreach (ReplicaEntityState row in rows)
                {
                    var definition = resources.Find(row.RuleKey);
                    int id = row.Identity.Id;
                    if (!replacing && previous.TryGetValue(id, out ObjectInstance existing) && existing.Definition == definition)
                    {
                        ObjectAssemblyValidation.Validate(definition, existing.ObjectView);
                        next.Add(id, existing);
                        continue;
                    }
                    ObjectInstance candidate = resources.Create(definition, nextContext, parent).Owner;
                    owned.Add(candidate);
                    using (SessionStateChange initial = row.Prepare(candidate, nextContext))
                        SessionStateChange.CommitAll(new[] { initial });
                    candidate.Activate();
                    candidate.gameObject.SetActive(false);
                    next.Add(id, candidate);
                    if (row.Identity.PlacementKey.Length != 0 && placements[row.Identity.PlacementKey].Loader != null &&
                        placements[row.Identity.PlacementKey].Definition == definition)
                    {
                        var original = placements[row.Identity.PlacementKey].Loader.ObjectInstance;
                        if (original == null) throw new InvalidOperationException("Scene Loader has no bound ObjectInstance.");
                        ObjectAssemblyValidation.Validate(definition, original.ObjectView);
                    }
                }
                foreach (ReplicaEntityState row in rows)
                {
                    ObjectInstance target = next[row.Identity.Id];
                    if (owned.Contains(target) && row.Identity.PlacementKey.Length != 0 &&
                        placements[row.Identity.PlacementKey].Loader != null && placements[row.Identity.PlacementKey].Definition == target.Definition)
                    {
                        ObjectPlacement placement = placements[row.Identity.PlacementKey];
                        ObjectInstance original = placement.Loader.ObjectInstance;
                        var oldDefinition = original.Definition ?? placement.Definition;
                        var oldContext = original.SessionContext;
                        bool active = original.IsActive, visible = original.gameObject.activeSelf;
                        Action<ObjectInstance, ObjectSessionContext> restore = ReplicaEntityState.FreezeRestore(original);
                        rollback.Add(() =>
                        {
                            original.Initialize("replica-rollback", oldDefinition, session: oldContext, activate: false);
                            if (oldContext != null) restore(original, oldContext);
                            if (active) original.Activate();
                            original.gameObject.SetActive(visible);
                        });
                        original.Initialize("replica-" + row.Identity.Id, target.Definition, session: nextContext, activate: false);
                        using (SessionStateChange initial = row.Prepare(original, nextContext))
                            SessionStateChange.CommitAll(new[] { initial });
                        original.Activate();
                        next[row.Identity.Id] = target = original;
                        sceneObjects.Add(original);
                    }
                    changes.Add(row.Prepare(target, nextContext));
                }
                objects = next;
                SessionStateChange.CommitAll(changes);
                committed = true;
                ObjectSessionContext old = context;
                context = nextContext;
                epoch = worldEpoch;
                if (replacing) old.Dispose();
                var retained = new HashSet<ObjectInstance>(next.Values);
                foreach (ObjectInstance instance in previous.Values)
                    if (!retained.Contains(instance)) Release(instance);
                foreach (ObjectInstance instance in next.Values) instance.gameObject.SetActive(true);
            }
            finally
            {
                try
                {
                    foreach (SessionStateChange change in changes) change.Dispose();
                    if (!committed)
                    {
                        objects = previous;
                        for (int i = rollback.Count - 1; i >= 0; i--)
                        {
                            try { rollback[i](); }
                            catch (Exception error) { Debug.LogException(error); }
                        }
                        if (replacing) nextContext.Dispose();
                    }
                    var adopted = committed ? new HashSet<ObjectInstance>(next.Values) : new HashSet<ObjectInstance>();
                    foreach (ObjectInstance instance in owned)
                        if (!adopted.Contains(instance)) Release(instance);
                }
                finally { applying = false; }
            }
        }

        public void Clear()
        {
            if (applying) throw new InvalidOperationException("Cannot retire a replica inside its state notification.");
            var previous = objects.Values.ToArray();
            objects.Clear();
            context.Dispose();
            foreach (ObjectInstance instance in previous) Release(instance);
            context = NewContext();
            epoch = 0;
        }

        private void Release(ObjectInstance instance)
        {
            if (instance == null) return;
            instance.Release();
            if (sceneObjects.Contains(instance)) { instance.gameObject.SetActive(false); return; }
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance.gameObject);
            else UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        public void Dispose()
        {
            Clear();
            context.Dispose();
            container.OnReturnToPool();
        }
    }
}
