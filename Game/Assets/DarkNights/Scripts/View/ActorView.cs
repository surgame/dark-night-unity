using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 角色唯一的 YYGC 主视图，保存朝向、姿态根、衣着、阴影与动画采样配置。
    /// 调用方决定动作和采样时间；本视图只操作 Prefab 结构，并可被动呈现尸体。
    /// </summary>
    public sealed class ActorView : EntityView, IRemnantView
    {
        [SerializeField] private HandheldView handheld;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private Transform facing;
        [SerializeField] private Transform poseRoot;
        [SerializeField] private SpriteRenderer clothing;
        [SerializeField] private PoseClip[] clips = Array.Empty<PoseClip>();
        [SerializeField] private Vector2 standingOffset;
        [SerializeField] private Vector2 deathOffset;

        private static readonly Color[] Shirts =
        {
            new Color32(142, 173, 178, 255), new Color32(190, 140, 114, 255),
            new Color32(167, 174, 122, 255), new Color32(185, 164, 194, 255)
        };

        public void PresentHandheld(ActorViewData actor, double elapsed) => handheld?.Present(actor, elapsed, Ambient);
        public void PresentBoarded(bool boarded)
        {
            facing.gameObject.SetActive(!boarded);
            if (shadow != null) shadow.enabled = !boarded;
            if (boarded) handheld?.Hide();
        }

        public PoseClip[] Clips => (PoseClip[])clips.Clone();

        public double PoseDuration(string pose) => RequiredClip(pose).Duration;

        public void SamplePose(string pose, double seconds)
        {
            handheld?.RestorePose();
            RequiredClip(pose).Sample(gameObject, seconds);
        }

        public void SetStanding(float face)
        {
            if (shadow != null) shadow.enabled = true;
            facing.localScale = new Vector3(face, 1, 1);
            poseRoot.localPosition = standingOffset;
        }

        public void TintActor(int identity, bool hit, bool training)
        {
            Color surface = hit ? new Color(1.8f, 1.4f, 1.3f) : Color.white;
            Color shirt = hit ? surface : Shirts[(int)((uint)identity % Shirts.Length)];
            if (training)
            {
                surface.a *= 0.55f;
                shirt.a *= 0.55f;
            }
            TintSurface(surface, clothing, shirt);
        }

        public override void Preview(int identity, int variant)
        {
            handheld?.Hide();
            RequiredClip("idle").Sample(gameObject, 0);
            if (shadow != null) shadow.enabled = true;
            facing.localScale = Vector3.one;
            poseRoot.localPosition = standingOffset;
            TintActor(identity, false, false);
        }

        public void PresentRemnant(VisualCue cue, double age)
        {
            if (cue.Kind != "corpse") throw new InvalidOperationException("ActorView only presents corpse remnants.");
            handheld?.Hide();
            UseRemnantSorting();
            if (shadow != null) shadow.enabled = false;
            RequiredClip("die").Sample(gameObject, age);
            facing.localScale = new Vector3(cue.Face, 1, 1);
            poseRoot.localPosition = deathOffset;
            TintSurface(new Color(0.75f, 0.75f, 0.75f, Mathf.Clamp01(8 - (float)age)));
        }

        private PoseClip RequiredClip(string name)
        {
            foreach (PoseClip clip in clips) if (clip.Name == name) return clip;
            throw new InvalidOperationException("Missing actor pose: " + name + " in " + gameObject.name);
        }
    }
}
