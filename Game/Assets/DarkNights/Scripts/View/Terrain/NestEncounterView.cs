using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>巢穴原生Prefab的只读表现；所有Sprite与脚底锚点显式绑定，业务阶段和位置仅来自冻结帧，编辑态保留可见布局。</summary>
    public sealed class NestEncounterView : MonoBehaviour
    {
        public SpriteRenderer Burrow;
        public SpriteRenderer Crawler;
        public Sprite EmptyBurrow;
        public Sprite OccupiedBurrow;
        public Sprite Alert;
        public Sprite CrawlA;
        public Sprite CrawlB;
        public Sprite Fallen;
        private float lastX;
        public void Present(NestViewData value)
        {
            if (Burrow == null || Crawler == null || EmptyBurrow == null || OccupiedBurrow == null || Alert == null ||
                CrawlA == null || CrawlB == null || Fallen == null) throw new InvalidOperationException("巢穴Prefab缺少显式表现绑定。");
            Burrow.transform.position = new Vector3((value.U + 2) * .16f,
                (Core.Config.Terrain.PlayableTerrain.OriginY - (value.Row + .5f) * 16) / 100, 0);
            Burrow.sprite = value.Phase == 0 ? OccupiedBurrow : EmptyBurrow;
            Crawler.enabled = value.Phase >= 1;
            Crawler.sprite = value.Phase == 1 ? Alert : value.Phase == 3 ? Fallen :
                ((int)(value.Clock * 6) % 2 == 0 ? CrawlA : CrawlB);
            if (Math.Abs(value.X - lastX) > .01f) Crawler.flipX = value.X < lastX;
            lastX = value.X;
            Crawler.transform.position = new Vector3(Mathf.Round(value.X) / 100, Mathf.Round(value.Height) / 100, 0);
        }
    }
}
