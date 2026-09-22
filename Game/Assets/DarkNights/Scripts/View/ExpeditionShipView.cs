using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>原生分层船体的纯表现绑定；每名玩家独立剖切舱壳，舱门、坡道和推焰只消费服务器冻结阶段。</summary>
    public sealed class ExpeditionShipView : MonoBehaviour
    {
        public SpriteRenderer WorkShell, CockpitShell, Ramp, Door, Hatch, LeftFlame, RightFlame;
        public Sprite RampOpen, RampClosed, DoorOpen, DoorHalf, DoorClosed, HatchOpen, HatchClosed;
        public Sprite[] FlameLow = Array.Empty<Sprite>(), FlameMedium = Array.Empty<Sprite>(), FlameHigh = Array.Empty<Sprite>();
        public GameObject RobotDock, CargoLocker;
        public void Present(ExpeditionShipData ship, bool cutaway, int modules, double elapsed)
        {
            if (ship == null) return;
            bool open = ship.Phase <= 1;
            WorkShell.enabled = CockpitShell.enabled = !cutaway;
            Ramp.sprite = open ? RampOpen : RampClosed;
            // 两个坡道帧各自的进口 pivot 都是铰链，位置不随状态跳动。
            Door.sprite = open ? DoorOpen : ship.Phase == 2 ? DoorHalf : DoorClosed;
            Hatch.sprite = open ? HatchOpen : HatchClosed;
            RobotDock.SetActive((modules & 1) != 0); CargoLocker.SetActive((modules & 2) != 0);
            bool flying = ship.Phase == 3;
            LeftFlame.enabled = RightFlame.enabled = flying;
            if (!flying) return;
            var frames = ship.VelocityY > 4 ? FlameHigh : ship.VelocityY < -4 ? FlameLow : FlameMedium;
            int frame = (int)(elapsed * 9) % frames.Length;
            LeftFlame.sprite = RightFlame.sprite = frames[frame];
        }
        public void Preview()
        {
            WorkShell.enabled = CockpitShell.enabled = false; Ramp.sprite = RampOpen;
            Door.sprite = DoorOpen; Hatch.sprite = HatchOpen; LeftFlame.enabled = RightFlame.enabled = false;
        }
    }
}
