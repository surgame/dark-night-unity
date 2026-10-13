using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>真实预览画布的中键平移与指针锚定缩放；捕获只属于一次拖动，离开面板或失去捕获必定结束临时撤销手势。</summary>
    public sealed class FlashlightPreviewNavigation : PointerManipulator
    {
        private readonly Action<Vector2> pan;
        private readonly Action<float, Vector2> zoom;
        private readonly Action<string> begin;
        private readonly Action end;
        private int pointer = -1;
        private Vector2 previous;
        private IVisualElementScheduledItem wheelEnd;
        private bool wheeling;

        public FlashlightPreviewNavigation(Action<Vector2> pan, Action<float, Vector2> zoom, Action<string> begin, Action end)
        {
            this.pan = pan; this.zoom = zoom; this.begin = begin; this.end = end;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.MiddleMouse });
        }
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Down);
            target.RegisterCallback<PointerMoveEvent>(Move);
            target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCaptureOutEvent>(Lost);
            target.RegisterCallback<DetachFromPanelEvent>(Detached);
            target.RegisterCallback<WheelEvent>(Wheel);
        }
        protected override void UnregisterCallbacksFromTarget()
        {
            Stop();
            target.UnregisterCallback<PointerDownEvent>(Down);
            target.UnregisterCallback<PointerMoveEvent>(Move);
            target.UnregisterCallback<PointerUpEvent>(Up);
            target.UnregisterCallback<PointerCaptureOutEvent>(Lost);
            target.UnregisterCallback<DetachFromPanelEvent>(Detached);
            target.UnregisterCallback<WheelEvent>(Wheel);
        }
        private bool HasImage => target is Image image && image.image != null;
        private void Down(PointerDownEvent evt)
        {
            if (!HasImage || !CanStartManipulation(evt) || !FlashlightPreviewPick.ImageBounds(target).Contains(evt.localPosition)) return;
            Stop(); pointer = evt.pointerId; previous = evt.localPosition;
            begin("previewPan"); target.CapturePointer(pointer); target.AddToClassList("is-panning");
            target.Focus(); evt.StopImmediatePropagation();
        }
        private void Move(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer || !target.HasPointerCapture(pointer)) return;
            if ((evt.pressedButtons & 4) == 0) { Stop(); return; }
            Rect bounds = FlashlightPreviewPick.ImageBounds(target);
            Vector2 current = evt.localPosition;
            if (bounds.width > 0 && bounds.height > 0)
                pan(new Vector2((current.x - previous.x) / bounds.width, (current.y - previous.y) / bounds.height));
            previous = current; evt.StopImmediatePropagation();
        }
        private void Up(PointerUpEvent evt)
        { if (evt.pointerId == pointer && evt.button == 2) { Stop(); evt.StopImmediatePropagation(); } }
        private void Lost(PointerCaptureOutEvent evt) { if (evt.pointerId == pointer) Stop(); }
        private void Detached(DetachFromPanelEvent evt) => Stop();
        private void Wheel(WheelEvent evt)
        {
            Rect bounds = FlashlightPreviewPick.ImageBounds(target);
            if (!HasImage || pointer >= 0 || !bounds.Contains(evt.localMousePosition) || Mathf.Approximately(evt.delta.y, 0)) return;
            if (!wheeling) { begin("previewZoom"); wheeling = true; }
            zoom(evt.delta.y, new Vector2((evt.localMousePosition.x - bounds.x) / bounds.width, (evt.localMousePosition.y - bounds.y) / bounds.height));
            wheelEnd?.Pause(); wheelEnd = target.schedule.Execute(EndWheel).StartingIn(180);
            evt.StopImmediatePropagation();
        }
        private void EndWheel() { if (wheeling) { wheeling = false; end(); } }
        private void Stop()
        {
            wheelEnd?.Pause(); EndWheel();
            int captured = pointer; pointer = -1;
            if (captured < 0) return;
            if (target.HasPointerCapture(captured)) target.ReleasePointer(captured);
            target.RemoveFromClassList("is-panning"); end();
        }
    }
}
