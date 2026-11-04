using System;
using AnyRules.Next;
using Spine;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 已选主角骨架的局部脚底适配，双腿插槽及小腿骨骼来自显式配置。
    /// 站立参考姿势决定固定翻转中心；逐帧只在已接地的连续坡面补偿外观，
    /// 站立时分别调整小腿高度，采样前恢复上帧改动，所有结果随播放器释放。
    /// </summary>
    internal sealed class SpineFootPlacement
    {
        private readonly Skeleton skeleton;
        private readonly Transform art;
        private readonly Slot leftSlot, rightSlot;
        private readonly Bone leftShin, rightShin;
        private readonly Vector3 restPosition;
        private readonly float[] vertices;
        private IReadOnlyGrid ground;
        private Transform actor;
        private bool standing, legsAdjusted;
        private Vector2 previousLeft, previousRight;

        internal SpineFootPlacement(Skeleton skeleton, Transform art, string leftFoot, string rightFoot,
            string leftLeg, string rightLeg)
        {
            this.skeleton = skeleton;
            this.art = art;
            leftSlot = RequiredSlot(skeleton, leftFoot);
            rightSlot = RequiredSlot(skeleton, rightFoot);
            leftShin = skeleton.FindBone(leftLeg) ?? throw new InvalidOperationException("缺少主角小腿骨骼：" + leftLeg);
            rightShin = skeleton.FindBone(rightLeg) ?? throw new InvalidOperationException("缺少主角小腿骨骼：" + rightLeg);
            vertices = new float[Math.Max(RequiredMesh(leftSlot).WorldVerticesLength,
                RequiredMesh(rightSlot).WorldVerticesLength)];
            restPosition = art.localPosition;
        }

        internal static Vector2 ReferenceOrigin(SkeletonData data, string leftFoot, string rightFoot)
        {
            var reference = new Skeleton(data);
            var state = new Spine.AnimationState(new AnimationStateData(data));
            state.SetAnimation(0, "idle", false);
            state.Apply(reference);
            reference.UpdateWorldTransform(Spine.Physics.Reset);
            Slot left = RequiredSlot(reference, leftFoot), right = RequiredSlot(reference, rightFoot);
            var buffer = new float[Math.Max(RequiredMesh(left).WorldVerticesLength, RequiredMesh(right).WorldVerticesLength)];
            Vector2 first = Sole(reference, left, buffer), second = Sole(reference, right, buffer);
            return new Vector2((first.x + second.x) * .5f, Math.Min(first.y, second.y));
        }

        internal void Prepare(IReadOnlyGrid map, Transform root, bool idle)
        {
            ground = map;
            actor = root;
            standing = idle;
        }

        internal void Restore()
        {
            art.localPosition = restPosition;
            if (!legsAdjusted) return;
            leftShin.Pose.SetPosition(previousLeft.x, previousLeft.y);
            rightShin.Pose.SetPosition(previousRight.x, previousRight.y);
            legsAdjusted = false;
        }

        internal void Apply()
        {
            if (ground == null || actor == null) return;
            float x = actor.position.x * EntityView.PixelsPerUnit;
            float height = actor.position.y * EntityView.PixelsPerUnit;
            if (!ActorGroundSurface.TrySlope(ground, x, height, out float floor)) return;
            art.position += Vector3.up * ((floor - height) / EntityView.PixelsPerUnit);
            if (!standing) return;
            Vector2 left = Sole(skeleton, leftSlot, vertices), right = Sole(skeleton, rightSlot, vertices);
            Vector3 leftWorld = art.TransformPoint(left), rightWorld = art.TransformPoint(right);
            if (!ActorGroundSurface.TryHeight(ground, leftWorld.x * EntityView.PixelsPerUnit, height, out float leftFloor) ||
                !ActorGroundSurface.TryHeight(ground, rightWorld.x * EntityView.PixelsPerUnit, height, out float rightFloor)) return;
            previousLeft = new Vector2(leftShin.Pose.X, leftShin.Pose.Y);
            previousRight = new Vector2(rightShin.Pose.X, rightShin.Pose.Y);
            MoveShin(leftShin, (leftFloor / EntityView.PixelsPerUnit) - leftWorld.y);
            MoveShin(rightShin, (rightFloor / EntityView.PixelsPerUnit) - rightWorld.y);
            legsAdjusted = true;
        }

        private void MoveShin(Bone bone, float worldHeight)
        {
            Vector3 delta = art.InverseTransformVector(Vector3.up *
                Mathf.Clamp(worldHeight, -.08f, .08f));
            BonePose parent = bone.Parent.AppliedPose;
            parent.WorldToLocal(bone.AppliedPose.WorldX + delta.x, bone.AppliedPose.WorldY + delta.y,
                out float localX, out float localY);
            bone.Pose.SetPosition(localX, localY);
        }

        private static Vector2 Sole(Skeleton skeleton, Slot slot, float[] buffer)
        {
            MeshAttachment mesh = RequiredMesh(slot);
            mesh.ComputeWorldVertices(skeleton, slot, 0, mesh.WorldVerticesLength, buffer, 0, 2);
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int i = 1; i < mesh.WorldVerticesLength; i += 2)
            {
                bottom = Math.Min(bottom, buffer[i]);
                top = Math.Max(top, buffer[i]);
            }
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float band = (top - bottom) * .12f;
            for (int i = 0; i < mesh.WorldVerticesLength; i += 2)
            {
                if (buffer[i + 1] > bottom + band) continue;
                left = Math.Min(left, buffer[i]);
                right = Math.Max(right, buffer[i]);
            }
            return new Vector2((left + right) * .5f, bottom);
        }

        private static Slot RequiredSlot(Skeleton skeleton, string name) =>
            skeleton.FindSlot(name) ?? throw new InvalidOperationException("缺少主角脚部插槽：" + name);

        private static MeshAttachment RequiredMesh(Slot slot) =>
            slot.AppliedPose.Attachment as MeshAttachment ??
            throw new InvalidOperationException("主角脚部插槽必须绑定已选腿部网格：" + slot.Data.Name);
    }
}
