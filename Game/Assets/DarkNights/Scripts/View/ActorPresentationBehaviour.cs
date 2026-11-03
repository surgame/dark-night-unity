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
        private bool hasWalkPosition;
        private float lastWalkX, lastWalkHeight;
        private double walkCycles;
        private double airborneAge, lastActionTime;
        private bool movingInAir;
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

        public bool Present(ActorViewData actor, int epoch, string workKind, float x, double actionTime, Color ambient, float? height = null, float visualScale = 1, float heroWalkSpeed = 0)
        {
            if (actor == null || !Accept(actor.Id, epoch, actor.Kind)) return false;
            if (Current == null || Current.ManualControl != actor.ManualControl) hasWalkPosition = false;
            if (hasWalkPosition && (Math.Abs(x - lastWalkX) >= 32 || Math.Abs((height ?? actor.Height) - lastWalkHeight) >= 32))
            {
                ActorVisual.ResetAnimation();
                hasWalkPosition = false; walkCycles = 0;
            }
            bool airborne = actor.ManualControl && actor.SupportPlatform < 0 && ActorVisual.HasPose("jump");
            double elapsed = hasWalkPosition ? Math.Max(0, actionTime - lastActionTime) : 0;
            if (!hasWalkPosition) movingInAir = actor.Walking;
            else if (elapsed > .000001) movingInAir = Math.Abs(x - lastWalkX) / elapsed > .1;
            if (!airborne || !hasWalkPosition || Current?.SupportPlatform >= 0) airborneAge = 0;
            else airborneAge += elapsed;
            Current = actor;
            string airPose = movingInAir ? "jump_move" : "jump_idle";
            Pose = airborne ? ActorVisual.HasPose(airPose) ? airPose : "jump" :
                actor.ManualControl && actor.Activity == "Attack" && ActorVisual.HasPose("attack") ? "attack" :
                actor.Walking || actor.Activity == "Move" || actor.Activity == "WorkMove" ||
                actor.Activity == "BuildMove" || actor.Activity == "TrainingMove" ? "move" :
                actor.Activity == "Attack" ? ActorVisual.HasPose("attack") ? "attack" : "idle" : actor.Kind == "worker" && actor.Activity == "Build" ? "build" :
                actor.Kind == "worker" && actor.Activity == "Work" ?
                    workKind == "wood" ? "work_wood" : workKind == "food" ? "work_farm" : "work_mine" : "idle";
            Position(x, ambient, height ?? actor.Height);
            Visual.transform.localScale = Vector3.one * (actor.ManualControl ? HeroControlDefinition.VisualScale : visualScale);
            // 沿用已选预览的jump2腾空区间，长落保持末姿，不播放落地后小跑的收尾。
            double seconds = airborne ? .12 + Math.Min(1, airborneAge) * .48 : Pose == "attack"
                ? actionTime / rules.AttackSeconds * ActorVisual.PoseDuration(Pose) : actionTime;
            float footHeight = height ?? actor.Height;
            if (actor.ManualControl && Pose == "move")
            {
                float distance = hasWalkPosition ? Mathf.Abs(x - lastWalkX) : 0;
                double cycleDistance = ActorVisual.HasPose("jump_move") && heroWalkSpeed > 0
                    ? heroWalkSpeed * ActorVisual.PoseDuration("move") : HeroControlDefinition.WalkCycleDistance;
                // 只累计插值后的实际地面路程；传送、切图和空中移动不快进行走动画。
                if (hasWalkPosition && actor.SupportPlatform >= 0 && distance < 32 && Mathf.Abs(footHeight - lastWalkHeight) < 32)
                    walkCycles = (walkCycles + distance / cycleDistance) % 1;
                seconds = walkCycles * ActorVisual.PoseDuration("move");
            }
            else if (!ActorVisual.UsesSkeleton) walkCycles = 0;
            lastWalkX = x; lastWalkHeight = footHeight; hasWalkPosition = true;
            lastActionTime = actionTime;
            if (ActorVisual.UsesSkeleton) ActorVisual.SetStanding(actor.Face);
            ActorVisual.SamplePose(Pose, seconds, elapsed);
            if (!ActorVisual.UsesSkeleton) ActorVisual.SetStanding(actor.Face);
            ActorVisual.TintActor(actor.Id, actor.HitFlash > 0, actor.Activity == "Training");
            ActorVisual.PresentHandheld(actor, Math.Max(0, actionTime - actor.ActionTime));
            return true;
        }

        public bool IssueOrder(int target, float x) => IsAvailable && !Current.Enemy &&
            Submit(new InputIntent("Orders", new[] { Id }, target, x));

        public bool Train(string kind) => IsAvailable && !Current.Enemy &&
            Submit(new InputIntent("Train" + kind, new[] { Id }));

        protected override bool SupportsView(EntityView value) => value is ActorView;

        protected override void ClearState()
        {
            (Visual as ActorView)?.ResetAnimation();
            Current = null;
            rules = null;
            Pose = "";
            hasWalkPosition = false; walkCycles = airborneAge = lastActionTime = 0;
            movingInAir = false;
        }
    }
}
