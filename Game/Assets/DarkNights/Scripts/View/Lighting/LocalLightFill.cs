using System;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>指定 Sprite 的局部补光；作者或挂载方明确绑定受光对象，参数不会写入环境光场或修改作者颜色。</summary>
    public sealed class LocalLightFill : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private SpriteRenderer[] targets = Array.Empty<SpriteRenderer>();
        private float radius;
        private float intensity;
        private Color color;
        private Transform boundOrigin;
        private SpriteRenderer[] boundTargets;
        public SpriteRenderer[] Targets => boundTargets ?? targets;
        public Vector4 OriginRadius => new Vector4(Position.x, Position.y, radius, radius);
        public Vector4 Energy
        {
            get { Color linear = color.linear; return new Vector4(linear.r, linear.g, linear.b, intensity); }
        }
        public void Apply(float range, float strength, Color tint)
        {
            radius = range; intensity = strength; color = tint;
        }
        private Vector3 Position => (boundOrigin != null ? boundOrigin : origin).position;
        public void Bind(Transform anchor, SpriteRenderer[] receivers)
        {
            if (anchor == null || receivers == null || Array.Exists(receivers, value => value == null))
                throw new ArgumentException("局部补光必须显式绑定有效挂点与受光对象。");
            boundOrigin = anchor; boundTargets = receivers;
        }
        public void Unbind() { boundOrigin = null; boundTargets = null; }
        public void Validate()
        {
            if (origin == null || Array.Exists(targets, value => value == null))
                throw new InvalidOperationException("局部补光作者绑定或参数无效。");
        }
    }
}
