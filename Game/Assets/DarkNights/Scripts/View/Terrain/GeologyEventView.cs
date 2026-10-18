using DarkNights.Core.ViewData;
using DarkNights.Core.Config.Terrain;
using System;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.View.Terrain
{
    /// <summary>地质事件原生Prefab的被动表现；显式绑定警示框、松岩与落点，读取冻结阶段做抖动和落石动画。没有碰撞回调或业务写权限，未运行时保留编辑态布局。</summary>
    public sealed class GeologyEventView : ObjectView
    {
        public SpriteRenderer Warning;
        public Transform Falling;
        public SpriteRenderer[] Fragments = Array.Empty<SpriteRenderer>();
        public GeologyDebrisView Debris;
        public GeologyPileView Pile;
        private FallingRockViewData[] rocks;
        private RubbleFragmentViewData[] pieces;
        private RubbleCollapseViewData collapse;
        private bool[] landed;
        private int previousPhase = -1, shedBeat = -1;
        public int VisibleFragments { get; private set; }

        public void Bind(FallingRockViewData[] motions, TerrainCellShape[] shapes, RubbleCollapseViewData frozen)
        {
            if (motions == null || motions.Length == 0 || Debris == null || Pile == null)
                throw new InvalidOperationException("地质碎岩缺少明确的原生表现绑定。");
            rocks = (FallingRockViewData[])motions.Clone(); collapse = frozen ?? throw new ArgumentNullException(nameof(frozen));
            pieces = new RubbleFragmentViewData[collapse.Count];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = collapse.Fragment(i);
            if (Fragments.Length < pieces.Length) throw new InvalidOperationException("原生混合碎岩槽位不足。");
            landed = new bool[pieces.Length]; previousPhase = -1; shedBeat = -1;
            Pile.Bind(motions, shapes, collapse);
            Debris.ResetEffects();
            float minimumX = float.MaxValue, maximumX = float.MinValue, minimumY = float.MaxValue, maximumY = float.MinValue;
            foreach (var rock in rocks)
            {
                minimumX = Math.Min(minimumX, rock.SourceX - 8); maximumX = Math.Max(maximumX, rock.SourceX + 8);
                minimumY = Math.Min(minimumY, rock.SourceY - 8); maximumY = Math.Max(maximumY, rock.SourceY + 8);
            }
            Warning.transform.position = new Vector3((minimumX + maximumX) / 200, (minimumY + maximumY) / 200, 0);
            Warning.transform.localScale = new Vector3((maximumX - minimumX) / 16, (maximumY - minimumY) / 16, 1);
            Warning.enabled = false;
            Debris.GroundY = float.MaxValue;
            foreach (var rock in rocks) Debris.GroundY = Math.Min(Debris.GroundY, (rock.LandingY - 8) / 100);
            foreach (var fragment in Fragments) fragment.enabled = false;
            Falling.gameObject.SetActive(true);
        }

        public void Present(GeologyEventViewData value, double simulationTime)
        {
            if (rocks == null) return;
            Pile.Present(value.SoilMask);
            Debris.Advance(simulationTime);
            bool active = value.Phase >= 1 && value.Phase <= 3;
            Warning.enabled = active;
            Warning.color = new Color(1, .4f, .12f, .1f + .12f * Mathf.Abs(Mathf.Sin((float)value.Clock * 10)));
            VisibleFragments = 0;
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i]; var rock = piece.Motion; var fragment = Fragments[i];
                double clock = value.Phase == 1 ? 0 : value.Phase == 2 ? value.Clock : collapse.Duration;
                collapse.Sample(i, clock, out float x, out float y, out float angle);
                if (value.Phase == 1) x += Mathf.Sin((float)value.Clock * 28 + i * 2) * .8f;
                fragment.transform.position = new Vector3(Mathf.Round(x) / 100, Mathf.Round(y) / 100, 0);
                fragment.transform.localScale = new Vector3(piece.Width / 16, piece.Height / 16, 1);
                fragment.transform.localEulerAngles = Vector3.forward * angle;
                fragment.enabled = active && (value.Phase == 1 || clock >= rock.Delay);
                if (fragment.enabled) VisibleFragments++;
                if (!landed[i] && (value.Phase == 2 && clock >= collapse.ImpactAt(i) || value.Phase >= 3 && value.Phase <= 5 && previousPhase == 2))
                { landed[i] = true; Debris.Impact(new Vector3(x / 100, (y - piece.Height * .5f) / 100, 0), piece.Width); }
            }
            int beat = (int)(value.Clock * 7);
            if ((value.Phase == 1 || value.Phase == 2) && beat != shedBeat)
            {
                shedBeat = beat;
                var rock = rocks[beat % rocks.Length];
                Debris.Shed(new Vector3(rock.SourceX / 100, (rock.SourceY - 8) / 100, 0));
            }
            previousPhase = value.Phase;
        }
    }
}
