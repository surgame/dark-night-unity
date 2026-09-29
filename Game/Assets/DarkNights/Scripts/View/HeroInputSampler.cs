using System;
using System.Linq;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 本地逐帧采样、短按缓冲和输入发送节流；只持有客户端输入，不修改角色权威状态。
    /// 切换角色或输入失效时生成一次归零意图，发送仍由 Entry 交给会话客户端。
    /// </summary>
    public sealed class HeroInputSampler
    {
        /// <summary>一次发送所需的冻结输入；避免网络调用读取下一帧已变化的采样状态。</summary>
        public readonly struct Packet
        {
            public readonly int Direction;
            public readonly bool JumpHeld, UseHeld, JumpPressed, DropPressed, SprintHeld;
            public readonly float Aim;
            public readonly int SelectionRevision;
            public readonly bool UsePressed, UseReleased, CancelUse;

            internal Packet(int direction, bool jumpHeld, bool useHeld, bool jumpPressed, bool dropPressed,
                float aim, int selectionRevision, bool usePressed, bool useReleased, bool cancelUse, bool sprintHeld = false)
            {
                Direction = direction; JumpHeld = jumpHeld; UseHeld = useHeld;
                JumpPressed = jumpPressed; DropPressed = dropPressed; Aim = aim;
                SprintHeld = sprintHeld;
                SelectionRevision = selectionRevision; UsePressed = usePressed;
                UseReleased = useReleased; CancelUse = cancelUse;
            }
        }

        private readonly Camera camera;
        private readonly EquipmentInput equipment = new EquipmentInput();
        private bool jumpPending, dropPending, sentJump, sentUse, sentDrop, sentSprint;
        private int sentDirection;
        private double nextSend, heartbeat;
        public int SelectedItem { get; private set; } = -1;
        public bool UseItemRequested { get; private set; }

        public HeroInputSampler(Camera camera) { this.camera = camera; }

        public void ResetControl()
        {
            jumpPending = dropPending = sentJump = sentUse = sentDrop = sentSprint = false;
            sentDirection = 0; nextSend = heartbeat = 0;
            SelectedItem = -1; UseItemRequested = false;
            equipment.Cancel();
        }

        public Packet Stop(int selectionRevision)
        {
            jumpPending = dropPending = sentJump = sentUse = sentDrop = sentSprint = false;
            sentDirection = 0; SelectedItem = -1; UseItemRequested = false;
            equipment.Cancel();
            return new Packet(0, false, false, false, false, equipment.Aim, selectionRevision,
                equipment.Pressed, equipment.Released, equipment.Cancelled);
        }

        public bool Sample(GameInputActions.HeroFrame controls, ActorViewData actor, SessionViewData frame, IEntityVisuals visuals,
            bool selectionPending, double now, out Packet packet)
        {
            bool allowed = controls.Allowed && !frame.Paused;
            bool pilot = frame.World.Expedition?.Ship?.PilotId == actor.Id;
            bool aboard = frame.World.Expedition?.Crew.Any(a => a.Id == actor.Id && a.Boarded) == true;
            Vector3 hand = visuals.Visual(actor.Id)?.transform.position ??
                new Vector3(actor.X / 100, actor.Height / 100, 0);
            int direction = allowed ? Math.Sign(controls.Move) : 0;
            bool jump = allowed && controls.JumpHeld;
            bool sprint = allowed && controls.SprintHeld && !pilot;
            equipment.Sample(controls, camera, hand + Vector3.up * .09f, allowed && !selectionPending && !aboard);
            SampleEdges(controls, allowed, pilot, aboard, frame.World.Expedition != null);
            SelectedItem = allowed && !aboard ? controls.ItemPressed : -1;
            UseItemRequested = false;

            bool changed = direction != sentDirection || jump != sentJump || sprint != sentSprint || equipment.Held != sentUse ||
                jumpPending || dropPending != sentDrop || (!pilot && dropPending) || equipment.Changed;
            if (now < nextSend || (!changed && now < heartbeat))
            {
                packet = default;
                return false;
            }

            packet = new Packet(direction, jump, equipment.Held, jumpPending, dropPending, equipment.Aim,
                actor.SelectionRevision, equipment.Pressed, equipment.Released, equipment.Cancelled, sprint);
            equipment.Consume();
            sentDirection = direction; sentJump = jump; sentSprint = sprint; sentUse = packet.UseHeld; sentDrop = dropPending;
            jumpPending = dropPending = false;
            nextSend = now + 1.0 / 30; heartbeat = now + 0.1;
            return true;
        }

        private void SampleEdges(GameInputActions.HeroFrame controls, bool allowed, bool pilot, bool aboard, bool expedition)
        {
            if (!allowed) { jumpPending = dropPending = false; return; }
            jumpPending |= controls.JumpPressed;
            if (pilot || (!aboard && expedition))
                dropPending = controls.DropHeld;
            else
                dropPending |= controls.DropPressed;
        }

        /// <summary>瞄准与使用动作的本地边沿；阻塞后要求松开再按，取消不解释为释放。</summary>
        private sealed class EquipmentInput
        {
            private bool suppress;
            private float sentAim;
            public float Aim { get; private set; }
            public bool Held { get; private set; }
            public bool Pressed { get; private set; }
            public bool Released { get; private set; }
            public bool Cancelled { get; private set; }
            public bool Changed => Pressed || Released || Cancelled || Mathf.Abs(Mathf.DeltaAngle(sentAim, Aim)) > 1;

            public void Sample(GameInputActions.HeroFrame controls, Camera camera, Vector3 hand, bool allowed)
            {
                bool raw = controls.UseHeld;
                if (!allowed || !controls.UseAllowed) { Cancel(); return; }
                if (suppress)
                {
                    if (!raw) suppress = false;
                    Held = false;
                    return;
                }
                Vector3 aim = camera.ScreenToWorldPoint(controls.Pointer) - hand;
                if (aim.sqrMagnitude > .0001f) Aim = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                Pressed |= controls.UsePressed;
                Released |= controls.UseReleased;
                Held = raw;
            }

            public void Consume() { Pressed = Released = Cancelled = false; sentAim = Aim; }
            public void Cancel()
            {
                Held = Pressed = Released = false;
                Cancelled = suppress = true;
            }
        }
    }
}
