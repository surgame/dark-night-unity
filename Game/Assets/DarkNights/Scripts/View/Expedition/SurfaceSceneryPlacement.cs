using System;
using System.Globalization;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>逐图纯装饰的稳定锚点；格坐标绑定原始支撑形状，不拥有碰撞、掉落或资源状态。</summary>
    [Serializable]
    public sealed class SurfaceSceneryPlacement
    {
        public string Id;
        public Texture2D Texture;
        public RectInt Pixels;
        public Vector2 Pivot = new Vector2(.5f, 0);
        public int Column;
        public int Row;
        public byte Shape;
        [Range(0, 2)] public int SupportRadius;
        [Range(.5f, 2)] public float Scale = 1;
        public int Order = -20;
        public bool Enabled = true;
        public bool Flip;
        public bool Conform;
        public float GroundOffsetPixels;

        public SurfaceSceneryPlacement Capture()
        {
            if (string.IsNullOrWhiteSpace(Id) || Id.Length > 80 || Texture == null ||
                Column < 3 || Column >= 317 || Row < 0 || Row >= 192 || Shape > 6 ||
                SupportRadius < 0 || SupportRadius > 2 || float.IsNaN(Scale) || Scale < .5f || Scale > 2 ||
                Pixels.x < 0 || Pixels.y < 0 || Pixels.width < 1 || Pixels.height < 1 ||
                Pixels.xMax > Texture.width || Pixels.yMax > Texture.height ||
                !float.IsFinite(GroundOffsetPixels) || Mathf.Abs(GroundOffsetPixels) > 64 ||
                !float.IsFinite(Pivot.x) || !float.IsFinite(Pivot.y) || Pivot.x < 0 || Pivot.x > 1 || Pivot.y < 0 || Pivot.y > 1)
                throw new ArgumentException("装饰身份、资源裁切或支撑坐标无效：" + Id);
            return (SurfaceSceneryPlacement)MemberwiseClone();
        }

        public string Identity => string.Join(",", Id, Texture == null ? "" : Texture.name, Pixels.x, Pixels.y,
            Pixels.width, Pixels.height, Pivot.x.ToString("R", CultureInfo.InvariantCulture),
            Pivot.y.ToString("R", CultureInfo.InvariantCulture), Column, Row, Shape, SupportRadius,
            Scale.ToString("R", CultureInfo.InvariantCulture), Order, Enabled, Flip, Conform) +
            (GroundOffsetPixels == 0 ? "" : "|ground-v1:" + GroundOffsetPixels.ToString("R", CultureInfo.InvariantCulture));
    }
}
