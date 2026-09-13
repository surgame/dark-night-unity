using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 完整展示帧到单对象只读状态的冻结映射，提交前一次解析完所有字段。
    /// 只持有本次应用的 DTO；恢复原实例时复制其旧状态，池归还不会改变回滚数据。
    /// </summary>
    internal sealed class ReplicaEntityState
    {
        internal EntityIdentityData Identity { get; }
        internal string RuleKey { get; }
        private readonly Func<ObjectInstance, ObjectSessionContext, SessionStateChange> prepare;

        private ReplicaEntityState(EntityIdentityData identity, string ruleKey,
            Func<ObjectInstance, ObjectSessionContext, SessionStateChange> prepare)
        {
            Identity = identity;
            RuleKey = ruleKey;
            this.prepare = prepare;
        }

        internal SessionStateChange Prepare(ObjectInstance instance, ObjectSessionContext context) => prepare(instance, context);

        internal static IReadOnlyList<ReplicaEntityState> From(WorldViewData world)
        {
            var identities = world.Identities.ToDictionary(i => i.Id);
            var result = new List<ReplicaEntityState>();
            foreach (ActorViewData value in world.Actors)
            {
                EntityIdentityData identity = identities[value.Id];
                if (!Enum.TryParse(value.Activity, out ActorActivity activity) || !Enum.IsDefined(typeof(ActorActivity), activity))
                    throw new InvalidOperationException("Invalid projected actor activity.");
                var state = new ActorState
                {
                    Id = value.Id, PlacementKey = identity.PlacementKey, X = value.X, Hp = value.Hp,
                    Name = value.Name, Enemy = value.Enemy, Activity = activity, TargetId = value.TargetId,
                    Face = value.Face, ActionTime = value.ActionTime, Windup = value.Windup,
                    HitFlash = value.HitFlash, Walking = value.Walking
                };
                result.Add(new ReplicaEntityState(identity, value.Kind,
                    (instance, context) => instance.GetBehaviour<ActorBehaviour>().PrepareSessionState(context, state)));
            }
            foreach (BuildingViewData value in world.Buildings)
            {
                EntityIdentityData identity = identities[value.Id];
                var state = new BuildingState
                {
                    Id = value.Id, PlacementKey = identity.PlacementKey, X = value.X, Hp = value.Hp,
                    Progress = value.Progress, WorkerId = value.WorkerId, FarmSiteId = value.FarmSiteId, HitFlash = value.HitFlash,
                    TrainingQueue = value.Training.Select(t => new TrainingStateEntry(t.ActorId, t.Kind, t.Remaining)).ToArray()
                };
                result.Add(new ReplicaEntityState(identity, value.Kind,
                    (instance, context) => instance.GetBehaviour<BuildingBehaviour>().PrepareSessionState(context, state)));
            }
            foreach (WorksiteViewData value in world.Worksites)
            {
                EntityIdentityData identity = identities[value.Id];
                var state = new WorksiteState
                {
                    Id = value.Id, PlacementKey = identity.PlacementKey, X = value.X, WorkerId = value.WorkerId,
                    Amount = value.Amount, Progress = value.Progress, Variant = value.Variant, FarmId = value.FarmId
                };
                result.Add(new ReplicaEntityState(identity, value.Kind,
                    (instance, context) => instance.GetBehaviour<WorksiteBehaviour>().PrepareSessionState(context, state)));
            }
            return result;
        }

        internal static Action<ObjectInstance, ObjectSessionContext> FreezeRestore(ObjectInstance instance)
        {
            ActorState actor = instance.GetBehaviour<ActorBehaviour>()?.CaptureState();
            BuildingState building = instance.GetBehaviour<BuildingBehaviour>()?.CaptureState();
            WorksiteState site = instance.GetBehaviour<WorksiteBehaviour>()?.CaptureState();
            return (owner, context) =>
            {
                if (actor != null) owner.GetBehaviour<ActorBehaviour>().ApplySessionState(context, actor);
                if (building != null) owner.GetBehaviour<BuildingBehaviour>().ApplySessionState(context, building);
                if (site != null) owner.GetBehaviour<WorksiteBehaviour>().ApplySessionState(context, site);
            };
        }
    }
}
