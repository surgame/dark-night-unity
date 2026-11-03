using System;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>指定Renderer的局部补光；保留旧Sprite作者绑定，运行挂载也接受骨骼网格，不写环境光场或作者颜色。</summary>
    public sealed class LocalLightFill : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private SpriteRenderer[] targets = Array.Empty<SpriteRenderer>();
        private float radius;
        private float intensity;
        private Color color;
        private Transform boundOrigin;
        private Renderer[] boundTargets;
        public Renderer[] Targets => boundTargets ?? targets;
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
        public void Bind(Transform anchor, Renderer[] receivers)
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
