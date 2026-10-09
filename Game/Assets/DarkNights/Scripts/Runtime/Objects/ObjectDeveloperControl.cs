#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using DarkNights.Runtime.Session;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>本局开发者授权及操作适配；授权只对启用时的 epoch 有效，库存和物体仍归原业务状态所有者。</summary>
    internal sealed class ObjectDeveloperControl
    {
        private readonly ObjectSession world;
        private int enabledEpoch = -1;
        internal ObjectDeveloperControl(ObjectSession world) { this.world = world; }
        internal bool EnabledFor(int epoch) => enabledEpoch == epoch;
        internal int Apply(SessionRequest request, out int id)
        {
            id = 0;
            if (request.Operation == SessionOperation.DebugSetEnabled)
            { enabledEpoch = request.Value == 1 ? request.Epoch : -1; return 1; }
            if (enabledEpoch != request.Epoch || !world.IsExpedition || world.Paused) return 0;
            var definition = ObjectDefinitionDatabase.Instance?.Definitions.FirstOrDefault(value =>
                value != null && value.Guid.ToString() == request.Kind);
            if (definition == null) return 0;
            int affected = 0;
            world.Mutations.Run(() =>
            {
                if (request.Operation == SessionOperation.DebugRemoveObject)
                    affected = RemoveObject(request.TargetId, definition);
                else
                {
                    var actor = world.Index.Find<ActorBehaviour>(request.ActorIds[0]);
                    if (actor == null || actor.Hp <= 0 || actor.Enemy || actor.Read().OwnerSlot < 0 ||
                        actor.Read().InventoryRevision != request.Value && request.Operation != SessionOperation.DebugSpawnProjectile) return false;
                    if (request.Operation == SessionOperation.DebugGiveEquipment)
                        affected = HeroInventoryTransactions.Give(world, actor, definition, (int)request.X) ? 1 : 0;
                    else if (request.Operation == SessionOperation.DebugRemoveEquipment)
                        affected = HeroInventoryTransactions.Remove(world, actor, definition) ? 1 : 0;
                    else if (request.Operation == SessionOperation.DebugSpawnProjectile)
                    {
                        if (!IsProjectile(definition) || actor.Read().Boarded) return false;
                        for (int index = 0; index < request.Value; index++)
                            if (world.Projectiles.LaunchHandheld(actor.Edit(), false, 0)) affected++;
                    }
                }
                return affected > 0;
            });
            id = request.Operation == SessionOperation.DebugRemoveObject ? request.TargetId : request.ActorIds[0];
            return affected;
        }
        private static bool IsProjectile(ObjectDefinition definition) => definition ==
            ObjectDefinitionDatabase.Instance.GetDefinitionByKey("effect.ballistic");
        private int RemoveObject(int id, ObjectDefinition definition)
        {
            if (IsProjectile(definition))
            {
                var state = world.Projectiles.Read();
                int index = Array.FindIndex(state.Ballistics, value => value.Kind != 0 && value.ViewId == id);
                if (index < 0) return 0;
                world.Projectiles.Edit().Ballistics[index] = default; return 1;
            }
            var entity = world.Index.Find(id);
            if (entity == null || entity.Object.Definition != definition || entity.Object == world.Expedition.Ship?.Object) return 0;
            if (entity is ActorBehaviour actor && (actor.Read().ManualControl || actor.Read().OwnerSlot >= 0)) return 0;
            if (entity is BuildingBehaviour || entity is WorksiteBehaviour || entity is MineralDepositBehaviour) return 0;
            world.Work.Release(id); world.Lifecycle.Retire(entity); return 1;
        }
    }
}
#endif
