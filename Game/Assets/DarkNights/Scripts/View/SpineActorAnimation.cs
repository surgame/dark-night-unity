using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 主角唯一骨骼播放器；只消费展示动作与连续采样时间，不驱动角色根、物理、伤害或网络状态。
    /// AnimationState负责混合，外部物理未来通过Renderer的UpdateLocal／UpdateWorld钩子接管指定骨骼。
    /// 生命周期结束清除轨道与混合；不启动独立Update时钟或在每帧重设动画。
    /// </summary>
    public sealed class SpineActorAnimation : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private SkeletonRenderer skeletonRenderer;
        [SerializeField] private MeshRenderer lightReceiver;
        [SerializeField, Range(0, .3f)] private float mixSeconds = .1f;
        private string currentPose = "";
        private bool initialized;
        private bool meshDirty;
        private MaterialPropertyBlock tintProperties;
        private static readonly int TintKey = Shader.PropertyToID("_DNSpineTint");

        public SkeletonRenderer Renderer => skeletonRenderer;
        public MeshRenderer LightReceiver => lightReceiver;

        public bool HasPose(string pose) => pose == "jump" || pose == "idle" || pose == "move" ||
            pose == "jump_move" || pose == "jump_idle";

        public double Duration(string pose)
        {
            EnsureReady();
            return RequiredAnimation(pose).Duration;
        }

        public void Sample(string pose, double seconds, double elapsed)
        {
            EnsureReady();
            string name = pose == "jump" ? "jump_idle" : pose;
            TrackEntry track = skeletonAnimation.AnimationState.GetTrack(0);
            if (currentPose != name || track == null)
            {
                bool first = currentPose.Length == 0;
                track = skeletonAnimation.AnimationState.SetAnimation(0, RequiredAnimation(name), name == "idle" || name == "move");
                track.MixDuration = first ? 0 : mixSeconds;
                track.TimeScale = 0;
                currentPose = name;
            }
            // 轨道绝对时间来自展示采样；混合只用本帧经过时间，暂停时二者均不推进。
            track.TrackTime = (float)Math.Max(0, seconds);
            skeletonAnimation.Update((float)Math.Max(0, Math.Min(.25, elapsed)));
            meshDirty = true;
        }

        public void Tint(Color surface, Color ambient)
        {
            EnsureReady();
            Color illumination = Shader.GetGlobalFloat("_DNLightingReady") > .5f ? Color.white : ambient;
            skeletonAnimation.Skeleton.SetColor(Color.white);
            // 网格顶点是Color32，HDR受击值和淡出保留在属性块，避免量化裁掉大于1的颜色。
            Color tint = (surface * illumination).linear;
            tintProperties = tintProperties ?? new MaterialPropertyBlock();
            lightReceiver.GetPropertyBlock(tintProperties);
            tintProperties.SetVector(TintKey, new Vector4(tint.r, tint.g, tint.b, tint.a));
            lightReceiver.SetPropertyBlock(tintProperties);
            // Renderer自动LateUpdate关闭，在本帧姿态、染色均就绪后只生成一次网格。
            if (meshDirty)
            {
                skeletonRenderer.LateUpdate();
                meshDirty = false;
            }
        }

        public void Preview()
        {
            if (!Application.isPlaying && initialized && skeletonAnimation.IsValid &&
                currentPose == "idle" && skeletonAnimation.AnimationState.GetTrack(0)?.TrackTime == 0) return;
            ResetPresentation();
            Sample("idle", 0, 0);
            Tint(Color.white, Color.white);
        }

        public void ResetPresentation()
        {
            currentPose = "";
            meshDirty = true;
            if (!initialized || skeletonAnimation == null || !skeletonAnimation.IsValid) return;
            skeletonAnimation.AnimationState.ClearTracks();
            skeletonAnimation.Skeleton.SetupPose();
            foreach (PhysicsConstraint constraint in skeletonAnimation.Skeleton.PhysicsConstraints)
                constraint.Reset(skeletonAnimation.Skeleton);
        }

        private Spine.Animation RequiredAnimation(string pose)
        {
            string name = pose == "jump" ? "jump_idle" : pose;
            return skeletonAnimation.Skeleton.Data.FindAnimation(name) ??
                throw new InvalidOperationException("主角骨骼数据缺少已选动作：" + name);
        }

        private void EnsureReady()
        {
            if (skeletonAnimation == null || skeletonRenderer == null || lightReceiver == null)
                throw new InvalidOperationException("主角骨骼播放器必须显式绑定动画、渲染器与补光接收者。");
            if (initialized && skeletonAnimation.IsValid) return;
            skeletonAnimation.UpdateTiming = UpdateTiming.ManualUpdate;
            skeletonAnimation.Initialize(false);
            if (!skeletonAnimation.IsValid) throw new InvalidOperationException("主角Spine数据初始化失败。");
            skeletonRenderer.clearStateOnDisable = false;
            skeletonRenderer.enabled = false;
            skeletonRenderer.UpdateMode = UpdateMode.FullUpdate;
            initialized = true;
        }

        private void OnDisable() => ResetPresentation();
    }
}
