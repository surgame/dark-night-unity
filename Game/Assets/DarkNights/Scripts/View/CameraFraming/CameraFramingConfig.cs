using System;
using UnityEngine;

namespace DarkNights.View.CameraFraming
{
    /// <summary>
    /// 正式地面及MapModule试验场共享的本地镜头取景配置。
    /// 百分比以相机实际画面高度为基准，参考人高只用于取景换算，不改变人物或碰撞尺寸。
    /// 运行镜头只读此资产，玩家临时滚轮缩放由各自Stage持有。
    /// </summary>
    public sealed class CameraFramingConfig : ScriptableObject
    {
        [SerializeField] private CameraFramingPreset preset = CameraFramingPreset.Screenshot105;
        [SerializeField, Range(0.1f, 100)] private float heroHeightPercent = 10.5f;
        [SerializeField, Min(0.001f)] private float referenceHeroHeight = 0.48f;

        public CameraFramingPreset Preset => preset;
        public float HeroHeightPercent => heroHeightPercent;
        public float ReferenceHeroHeight => referenceHeroHeight;

        /// <summary>将占屏百分比转换为原Stage使用的逻辑像素缩放，兼容人物表现倍率。</summary>
        public float ZoomForViewport(int pixelHeight, float presentationScale)
        {
            float height = Mathf.Max(0.001f, referenceHeroHeight * presentationScale);
            return Mathf.Max(1, pixelHeight) * heroHeightPercent / 100 / (height * 100);
        }

        /// <summary>返回可视化方案的起始值；自定义值由资产单独保存。</summary>
        public static float PresetHeightPercent(CameraFramingPreset value)
        {
            switch (value)
            {
                case CameraFramingPreset.Close16: return 16;
                case CameraFramingPreset.Reference124: return 12.4f;
                case CameraFramingPreset.Screenshot105: return 10.5f;
                case CameraFramingPreset.Wide8: return 8;
                case CameraFramingPreset.Distant6: return 6;
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private void OnValidate()
        {
            if (float.IsNaN(heroHeightPercent) || float.IsInfinity(heroHeightPercent)) heroHeightPercent = 10.5f;
            if (float.IsNaN(referenceHeroHeight) || float.IsInfinity(referenceHeroHeight)) referenceHeroHeight = 0.48f;
            heroHeightPercent = Mathf.Clamp(heroHeightPercent, 0.1f, 100);
            referenceHeroHeight = Mathf.Max(0.001f, referenceHeroHeight);
        }
    }
}
