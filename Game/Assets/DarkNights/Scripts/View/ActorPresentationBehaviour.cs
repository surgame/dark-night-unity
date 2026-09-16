using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 角色拥有自己的冻结副本、姿态选择、攻击动画时间映射和明确实体 ID 的现有操作入口。
    /// 位置与动作时间来自公共展示时间线，生命和任务只读；转职或解绑即丢弃旧职业配置与副本。
    /// </summary>
    public sealed class ActorPresentationBehaviour : EntityPresentationBehaviour
    {
        private UnitDefinition rules;
        private ActorView ActorVisual => Visual as ActorView ??
            throw new InvalidOperationException("Actor presentation is missing ActorView.");
        public ActorViewData Current { get; private set; }
        public string Pose { get; private set; } = "";
        public double MaxHp => rules?.Hp ?? 0;
        public override bool IsAvailable => IsBound && Current != null;

        public void Bind(int id, int epoch, string kind, UnitDefinition definition, Action<InputIntent> submit)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            BindEntity(id, epoch, kind, submit);
            rules = definition;
        }

        public bool Present(ActorViewData actor, int epoch, string workKind, float x, double actionTime, Color ambient, float? height = null)
        {
            if (actor == null || !Accept(actor.Id, epoch, actor.Kind)) return false;
            Current = actor;
            Pose = actor.ManualControl && actor.Activity == "Attack" ? "attack" :
                actor.Walking || actor.Activity == "Move" || actor.Activity == "WorkMove" ||
                actor.Activity == "BuildMove" || actor.Activity == "TrainingMove" ? "move" :
                actor.Activity == "Attack" ? "attack" : actor.Kind == "worker" && actor.Activity == "Build" ? "build" :
                actor.Kind == "worker" && actor.Activity == "Work" ?
                    workKind == "wood" ? "work_wood" : workKind == "food" ? "work_farm" : "work_mine" : "idle";
            Position(x, ambient, height ?? actor.Height);
            double seconds = actor.Activity == "Attack"
                ? actionTime / rules.AttackSeconds * ActorVisual.PoseDuration(Pose) : actionTime;
            ActorVisual.SamplePose(Pose, seconds);
            ActorVisual.SetStanding(actor.Face);
            ActorVisual.TintActor(actor.Id, actor.HitFlash > 0, actor.Activity == "Training");
            return true;
        }

        public bool IssueOrder(int target, float x) => IsAvailable && !Current.Enemy &&
            Submit(new InputIntent("Orders", new[] { Id }, target, x));

        public bool Train(string kind) => IsAvailable && !Current.Enemy &&
            Submit(new InputIntent("Train" + kind, new[] { Id }));

        protected override bool SupportsView(EntityView value) => value is ActorView;

        protected override void ClearState()
        {
            Current = null;
            rules = null;
            Pose = "";
        }
    }
}
