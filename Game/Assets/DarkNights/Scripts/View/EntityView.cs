using UnityEngine;
using UnityEngine.Rendering;
using YY.Features.Players.View;

namespace DarkNights.View
{
    /// <summary>
    /// 三类本地实体共享的 YYGC 主视图，拥有拾取范围、公共锚点、头像和环境染色入口。
    /// 它不猜测对象类别，也不保存权威状态；类别专用结构由具体派生视图唯一配置。
    /// </summary>
    public abstract class EntityView : ObjectView
    {
        public const float PixelsPerUnit = 100;

        [SerializeField] private Rect pickBounds;
        [SerializeField] private Sprite portrait;
        [SerializeField] private SortingGroup sorting;
        [SerializeField] private Transform statusAnchor;
        [SerializeField] private Transform selectionAnchor;
        [SerializeField] private SpriteTintGroup tintTargets = new SpriteTintGroup();

        public Transform StatusAnchor => statusAnchor;
        public Sprite Portrait => portrait;
        public Transform SelectionAnchor => selectionAnchor;
        public int TintTargetCount => tintTargets.Count;
        public Color Ambient { get; set; } = Color.white;

        public bool Contains(Vector2 worldPoint) =>
            pickBounds.Contains(transform.InverseTransformPoint(worldPoint));

        public void PreviewTint(Color tint)
        {
            Preview(0, 0);
            TintSurface(tint);
        }

        public abstract void Preview(int identity, int variant);

        public void TintSurface(Color tint)
        {
            tintTargets.Apply(tint, Ambient);
        }

        protected void TintSurface(Color tint, SpriteRenderer accent, Color accentTint)
        {
            tintTargets.Apply(tint, Ambient, accent, accentTint);
        }

        protected void UseRemnantSorting()
        {
            sorting.sortingOrder = -50;
        }
    }
}
