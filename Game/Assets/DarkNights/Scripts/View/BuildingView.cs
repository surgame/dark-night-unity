using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 建筑唯一的 YYGC 主视图，保存成品、地基、废墟及可选施工动画配置。
    /// 施工进度和受击状态由冻结副本决定；本视图只切换外观，并可被动呈现废墟。
    /// </summary>
    public sealed class BuildingView : EntityView, IRemnantView
    {
        [SerializeField] private SpriteRenderer complete;
        [SerializeField] private SpriteRenderer foundation;
        [SerializeField] private SpriteRenderer rubble;
        [SerializeField] private PoseClip[] clips = Array.Empty<PoseClip>();
        [SerializeField] private bool fadeConstruction;

        public PoseClip[] Clips => (PoseClip[])clips.Clone();
        public bool HasPoseClips => clips.Length != 0;
        public bool FadeConstruction => fadeConstruction;

        public void SamplePose(string pose, double seconds)
        {
            RequiredClip(pose).Sample(gameObject, seconds);
        }

        public void SetVisibility(bool showComplete, bool showFoundation, bool showRubble)
        {
            complete.enabled = showComplete;
            if (foundation != null) foundation.enabled = showFoundation;
            if (rubble != null) rubble.enabled = showRubble;
        }

        public override void Preview(int identity, int variant)
        {
            SetVisibility(true, false, false);
            TintSurface(Color.white);
        }

        public void PresentRemnant(VisualCue cue, double age)
        {
            if (cue.Kind != "rubble") throw new InvalidOperationException("BuildingView only presents rubble remnants.");
            UseRemnantSorting();
            SetVisibility(rubble == null, false, rubble != null);
            TintSurface(new Color(0.5490196f, 0.572549f, 0.5058824f, 1));
        }

        private PoseClip RequiredClip(string name)
        {
            foreach (PoseClip clip in clips) if (clip.Name == name) return clip;
            throw new InvalidOperationException("Missing building pose: " + name + " in " + gameObject.name);
        }
    }
}
