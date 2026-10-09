using System;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>指定 Sprite 的局部补光；作者或挂载方明确绑定受光对象，参数不会写入环境光场或修改作者颜色。</summary>
    public sealed class LocalLightFill : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private SpriteRenderer[] targets = Array.Empty<SpriteRenderer>();
        [SerializeField, Range(.25f, 5), Tooltip("半径以当前地图格计，由相机换算为世界距离。")] private float radius = 2.2f;
        [SerializeField, Range(0, 2)] private float intensity = .55f;
        [SerializeField] private Color color = new Color(1, .88f, .69f, 1);
        private Transform boundOrigin;
        private SpriteRenderer[] boundTargets;
        public SpriteRenderer[] Targets => boundTargets ?? targets;
        public Vector4 OriginRadius => new Vector4(Position.x, Position.y, radius, radius);
        public Vector4 Energy
        {
            get { Color linear = color.linear; return new Vector4(linear.r, linear.g, linear.b, intensity); }
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
            if (origin == null || Array.Exists(targets, value => value == null) ||
                !float.IsFinite(radius) || radius < .25f || radius > 5 ||
                !float.IsFinite(intensity) || intensity < 0 || intensity > 2)
                throw new InvalidOperationException("局部补光作者绑定或参数无效。");
        }
    }
}
