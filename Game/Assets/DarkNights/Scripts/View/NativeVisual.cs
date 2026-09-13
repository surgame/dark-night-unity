using System;
using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View
{
    /// <summary>
    /// 原生角色、建筑或工位 Prefab 的资源和绘制操作，提供动画采样、颜色及显式可见性控制。
    /// 选择边界、脚底、朝向和锚点使用像素到米的统一转换；生命周期由对象创建入口管理。
    /// </summary>
    public sealed class NativeVisual : MonoBehaviour
    {
        public const float PixelsPerUnit = 100;
        [SerializeField] private Rect pickBounds;
        [SerializeField] private Sprite portrait;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private SortingGroup sorting;
        [SerializeField] private Transform facing;
        [SerializeField] private Transform origin;
        [SerializeField] private Transform statusAnchor;
        [SerializeField] private Transform selectionAnchor;
        [SerializeField] private SpriteRenderer clothing;
        [SerializeField] private SpriteRenderer complete;
        [SerializeField] private SpriteRenderer foundation;
        [SerializeField] private SpriteRenderer rubble;
        [SerializeField] private SpriteRenderer[] variants = Array.Empty<SpriteRenderer>();
        [SerializeField] private GameObject depleted;
        [SerializeField] private SpriteRenderer[] sprites = Array.Empty<SpriteRenderer>();
        [SerializeField] private Color[] baseColors = Array.Empty<Color>();
        [SerializeField] private PoseClip[] clips = Array.Empty<PoseClip>();
        [SerializeField] private Vector2 standingOffset;
        [SerializeField] private Vector2 deathOffset;
        [SerializeField] private bool fadeConstruction;
        private static readonly Color[] Shirts =
        {
            new Color32(142, 173, 178, 255), new Color32(190, 140, 114, 255),
            new Color32(167, 174, 122, 255), new Color32(185, 164, 194, 255)
        };

        public Transform StatusAnchor => statusAnchor;
        public Sprite Portrait => portrait;
        public Transform SelectionAnchor => selectionAnchor;
        public PoseClip[] Clips => (PoseClip[])clips.Clone();
        public bool HasPoseClips => clips.Length != 0;
        public bool FadeConstruction => fadeConstruction;
        public Color Ambient { get; set; } = Color.white;
        public bool Contains(Vector2 worldPoint) => pickBounds.Contains(transform.InverseTransformPoint(worldPoint));
        public void PreviewTint(Color tint) { Preview(0, 0); Tint(0, false, false, tint, false); }

        public double PoseDuration(string pose) => RequiredClip(pose).Duration;
        public void SamplePose(string pose, double seconds) => RequiredClip(pose).Sample(gameObject, seconds);

        public void SetStanding(float face)
        {
            if (shadow != null) shadow.enabled = true;
            facing.localScale = new Vector3(face, 1, 1);
            origin.localPosition = standingOffset;
        }

        public void TintActor(int identity, bool hit, bool training) => Tint(identity, hit, training, Color.white, true);
        public void TintSurface(Color tint) => Tint(0, false, false, tint, false);

        public void SetBuildingVisibility(bool showComplete, bool showFoundation, bool showRubble)
        {
            complete.enabled = showComplete;
            if (foundation != null) foundation.enabled = showFoundation;
            if (rubble != null) rubble.enabled = showRubble;
        }

        public void SetWorksiteVisibility(bool showVariant, int variant, bool showDepleted)
        {
            for (int i = 0; i < variants.Length; i++)
                variants[i].enabled = showVariant && i == variant % variants.Length;
            depleted.SetActive(showDepleted);
        }

        public void Preview(int identity, int variant)
        {
            if (shadow != null) shadow.enabled = true;
            if (facing != null)
            {
                RequiredClip("idle").Sample(gameObject, 0);
                facing.localScale = Vector3.one;
                origin.localPosition = standingOffset;
                Tint(identity, false, false, Color.white, true);
            }
            else if (complete != null)
            {
                complete.enabled = true;
                if (foundation != null) foundation.enabled = false;
                if (rubble != null) rubble.enabled = false;
            }
            else
            {
                for (int i = 0; i < variants.Length; i++) variants[i].enabled = i == variant % variants.Length;
                depleted.SetActive(false);
            }
        }

        public void SampleDeath(double seconds, float face, int identity)
        {
            RequiredClip("die").Sample(gameObject, seconds);
            facing.localScale = new Vector3(face, 1, 1);
            origin.localPosition = deathOffset;
            Tint(identity, false, false, Color.white, true);
        }

        public void PresentRemnant(VisualCue cue, double age)
        {
            sorting.sortingOrder = -50;
            if (shadow != null) shadow.enabled = false;
            Color tint;
            if (cue.Kind == "corpse")
            {
                SampleDeath(age, cue.Face, 0);
                tint = new Color(0.75f, 0.75f, 0.75f, Mathf.Clamp01(8 - (float)age));
            }
            else
            {
                complete.enabled = rubble == null;
                if (foundation != null) foundation.enabled = false;
                if (rubble != null) rubble.enabled = true;
                tint = new Color(0.5490196f, 0.572549f, 0.5058824f, 1);
            }
            Tint(0, false, false, tint, false);
        }

        private PoseClip RequiredClip(string name)
        {
            foreach (PoseClip clip in clips) if (clip.Name == name) return clip;
            throw new InvalidOperationException("Missing native pose: " + name + " in " + gameObject.name);
        }

        private void Tint(int identity, bool hit, bool training, Color tint, bool actor)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                Color color = actor && sprites[i] == clothing ? Shirts[(int)((uint)identity % Shirts.Length)] : tint;
                if (hit) color = new Color(1.8f, 1.4f, 1.3f);
                if (training) color.a *= 0.55f;
                sprites[i].color = baseColors[i] * color * Ambient;
            }
        }
    }
}
