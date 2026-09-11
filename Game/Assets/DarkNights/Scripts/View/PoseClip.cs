using System;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// Prefab 所属原生动画及精确源时长；时长独立于末帧时间，保留最后一帧的停留区间。
    /// 仅供展示采样，不触发游戏事件或推进权威时钟。
    /// </summary>
    [Serializable]
    public sealed class PoseClip
    {
        public string Name;
        public AnimationClip Clip;
        public double Duration;
        public bool Loop;

        public void Sample(GameObject target, double seconds)
        {
            double time = Loop ? Math.Max(0, seconds) % Duration : Math.Max(0, Math.Min(seconds, Duration - 0.000001));
            Clip.SampleAnimation(target, (float)time);
        }
    }
}
