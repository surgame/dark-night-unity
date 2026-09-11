using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 原生角色、建筑或工位 Prefab 的显式表现绑定，只消费冻结副本并采样原始动画。
    /// 选择边界、脚底、朝向和锚点使用像素到米的统一转换；生命周期由对象创建入口管理。
    /// </summary>
    public sealed class NativeVisual : MonoBehaviour
    {
        public const float PixelsPerUnit = 100;
        [SerializeField] private Rect pickBounds;
        [SerializeField] private Sprite portrait;
        [SerializeField] private SpriteRenderer shadow;
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
        public Color Ambient { get; set; } = Color.white;
        public bool Contains(Vector2 worldPoint) => pickBounds.Contains(transform.InverseTransformPoint(worldPoint));
        public void PreviewTint(Color tint) { Preview(0, 0); Tint(0, false, false, tint, false); }

        public void Apply(ActorViewData actor, GameCatalog catalog, string workKind, double? displayTime = null)
        {
            if (shadow != null) shadow.enabled = true;
            string pose = actor.Walking || actor.Activity == "Move" || actor.Activity == "WorkMove" ||
                actor.Activity == "BuildMove" || actor.Activity == "TrainingMove" ? "move" :
                actor.Activity == "Attack" ? "attack" : actor.Kind == "worker" && actor.Activity == "Build" ? "build" :
                actor.Kind == "worker" && actor.Activity == "Work" ?
                    workKind == "wood" ? "work_wood" : workKind == "food" ? "work_farm" : "work_mine" : "idle";
            PoseClip clip = RequiredClip(pose);
            double seconds = displayTime ?? actor.ActionTime;
            if (actor.Activity == "Attack") seconds = seconds / catalog.Balance.Units[actor.Kind].AttackSeconds * clip.Duration;
            clip.Sample(gameObject, seconds);
            facing.localScale = new Vector3(actor.Face, 1, 1);
            origin.localPosition = standingOffset;
            Tint(actor.Id, actor.HitFlash > 0, actor.Activity == "Training", Color.white, true);
        }

        public void Apply(BuildingViewData building)
        {
            if (clips.Length != 0) RequiredClip("construction").Sample(gameObject, Math.Min(building.Progress, 0.999999));
            complete.enabled = building.Progress >= 1 || fadeConstruction;
            if (foundation != null) foundation.enabled = building.Progress < 1 && !fadeConstruction;
            if (rubble != null) rubble.enabled = false;
            Color tint = building.HitFlash > 0 ? new Color(1.4f, 1.15f, 1.1f) : Color.white;
            if (fadeConstruction && building.Progress < 1) tint.a = 0.4f + (float)building.Progress * 0.6f;
            Tint(0, false, false, tint, false);
        }

        public void Apply(WorksiteViewData site)
        {
            bool empty = site.Amount == 0;
            for (int i = 0; i < variants.Length; i++)
                variants[i].enabled = site.FarmId == 0 && !empty && i == site.Variant % variants.Length;
            depleted.SetActive(site.FarmId == 0 && empty);
            Tint(0, false, false, Color.white, false);
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
