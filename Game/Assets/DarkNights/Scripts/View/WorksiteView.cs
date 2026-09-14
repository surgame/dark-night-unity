using System;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 工位唯一的 YYGC 主视图，保存资源外观变体和耗尽标记。
    /// 资源数量、农田关联及生产均来自只读副本；本视图不承担残骸或角色动画职责。
    /// </summary>
    public sealed class WorksiteView : EntityView
    {
        [SerializeField] private SpriteRenderer[] variants = Array.Empty<SpriteRenderer>();
        [SerializeField] private GameObject depleted;

        public void SetVisibility(bool showVariant, int variant, bool showDepleted)
        {
            if (variants.Length == 0) throw new InvalidOperationException("Worksite view requires at least one variant.");
            int selected = (int)((uint)variant % variants.Length);
            for (int i = 0; i < variants.Length; i++)
                variants[i].enabled = showVariant && i == selected;
            depleted.SetActive(showDepleted);
        }

        public override void Preview(int identity, int variant)
        {
            SetVisibility(true, variant, false);
            TintSurface(Color.white);
        }
    }
}
