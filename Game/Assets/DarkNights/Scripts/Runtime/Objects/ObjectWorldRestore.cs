using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Save;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 未激活的恢复候选对象及提交边界；准备不触碰现有世界，失败只释放自己的实例。
    /// 场景对象在提交点按放置键精确重绑，并保存回滚初始化器；全部成功才切换索引及退休旧 epoch。
    /// </summary>
    internal sealed class ObjectWorldRestore : IDisposable
    {
        private readonly ObjectSession session;
        private readonly SessionSnapshot snapshot;
        private readonly ObjectSessionContext context;
        private readonly List<IEntityBehaviour> staged = new List<IEntityBehaviour>();
        private readonly List<ObjectInstance> owned = new List<ObjectInstance>();
        private readonly HashSet<ObjectInstance> retained = new HashSet<ObjectInstance>();
        private bool committed;

        internal ObjectWorldRestore(ObjectSession session, SessionSnapshot snapshot)
        {
            this.session = session;
            this.snapshot = snapshot;
            context = session.NewEntityContext();
            try
            {
                var order = snapshot.Buildings.Select(b => (b.Id, b.Kind))
                    .Concat(snapshot.Actors.Select(a => (a.Id, a.Kind)))
                    .Concat(snapshot.Worksites.Select(w => (w.Id, w.Kind)));
                foreach (var record in order)
                {
                    var definition = session.Resources.Find(record.Kind);
                    ObjectInstance instance = session.Resources.Create(definition, context, session.Parent).Owner;
                    owned.Add(instance);
                    ObjectSnapshotMapper.Initialize(instance, record.Id, snapshot);
                    var entity = instance.GetAllBehaviors().OfType<IEntityBehaviour>().Single();
                    staged.Add(entity);
                    ObjectInstance original = session.SceneOwner(entity.PlacementKey, definition);
                    if (original != null) ObjectAssemblyValidation.Validate(definition, original.ObjectView);
                }
            }
            catch { Dispose(); throw; }
        }

        internal void Commit()
        {
            if (committed) throw new InvalidOperationException("Restore candidate was already committed.");
            SessionEntityIndex previous = session.Index;
            ObjectSessionContext previousContext = session.EntityContext;
            var oldEntities = previous.FreezeOrder();
            var oldOwners = oldEntities.Select(e => e.Object).ToArray();
            var oldByOwner = oldEntities.ToDictionary(e => e.Object);
            var replacement = new SessionEntityIndex();
            session.Mutations.Run(() =>
            {
                context.Activate();
                session.Mutations.OnRollback(() => session.ReplaceEntities(previous, previousContext));
                foreach (IEntityBehaviour prepared in staged)
                {
                    int id = prepared.Id;
                    ObjectInstance next = session.SceneOwner(prepared.PlacementKey, prepared.Object.Definition);
                    if (next != null)
                    {
                        var oldDefinition = next.Definition;
                        bool wasVisible = next.gameObject.activeSelf;
                        oldByOwner.TryGetValue(next, out IEntityBehaviour old);
                        int oldId = old?.Id ?? 0;
                        Action<ObjectInstance> reset = old == null ? null : ObjectSnapshotMapper.FreezeInitializer(old);
                        ObjectInstance original = next;
                        session.Mutations.OnRollback(() =>
                        {
                            original.Initialize("restore-rollback", oldDefinition, session: previousContext, activate: false);
                            if (reset != null)
                            {
                                reset(original);
                                original.Activate();
                                previous.Replace(oldId, original.GetAllBehaviors().OfType<IEntityBehaviour>().Single());
                            }
                            else original.Retire();
                            original.gameObject.SetActive(wasVisible);
                        });
                        next.Initialize("restored-" + id, prepared.Object.Definition, session: context, activate: false);
                        ObjectSnapshotMapper.FreezeInitializer(prepared)(next);
                    }
                    else
                    {
                        next = prepared.Object;
                        retained.Add(next);
                    }
                    next.Activate();
                    replacement.Add(next.GetAllBehaviors().OfType<IEntityBehaviour>().Single());
                }
                session.Camp.Edit().CopyFrom(ObjectSnapshotMapper.Camp(snapshot));
                session.Economy.Edit().CopyFrom(ObjectSnapshotMapper.Economy(snapshot));
                session.Waves.Edit().CopyFrom(ObjectSnapshotMapper.Wave(snapshot));
                session.Projectiles.Edit().CopyFrom(ObjectSnapshotMapper.Projectiles(snapshot, session.Layout.GroundY));
                session.ReplaceEntities(replacement, context);
                ExpeditionMapping.Restore(session, snapshot.Expedition);
                return true;
            });
            committed = true;
            previousContext.Dispose();
            var adopted = new HashSet<ObjectInstance>(replacement.FreezeOrder().Select(e => e.Object));
            foreach (ObjectInstance old in oldOwners)
                if (!adopted.Contains(old)) session.ReleaseEntity(old);
            foreach (IEntityBehaviour entity in replacement.FreezeOrder()) entity.Object.gameObject.SetActive(true);
            session.Feedback.Reset();
        }

        public void Dispose()
        {
            if (!committed) context.Dispose();
            foreach (ObjectInstance instance in owned)
                if (!committed || !retained.Contains(instance)) session.ReleaseEntity(instance);
            owned.Clear();
        }
    }
}
