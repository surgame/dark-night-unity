using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 星图专用指针手势；区分点击与超过6像素的拖动，避免松手误选节点。
    /// 关闭、取消、失去捕获及脱离面板均清理本地手势，不触发导航命令。
    /// </summary>
    internal sealed class StarMapManipulator : PointerManipulator
    {
        private readonly StarMapElement map;
        private int pointer = -1, button;
        private Vector2 start, last;
        private string pressedNode;
        private bool dragging;

        public StarMapManipulator(StarMapElement map) { this.map = map; }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(Move);
            target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCancelEvent>(OnCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
            target.RegisterCallback<WheelEvent>(Wheel, TrickleDown.TrickleDown);
            target.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            Cancel();
            target.UnregisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(Move);
            target.UnregisterCallback<PointerUpEvent>(Up);
            target.UnregisterCallback<PointerCancelEvent>(OnCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
            target.UnregisterCallback<WheelEvent>(Wheel, TrickleDown.TrickleDown);
            target.UnregisterCallback<DetachFromPanelEvent>(Detached);
        }

        private void Down(PointerDownEvent evt)
        {
            if (pointer >= 0 || (evt.button != 0 && evt.button != 2)) return;
            pointer = evt.pointerId; button = evt.button;
            start = last = target.WorldToLocal(evt.position);
            dragging = false;
            var element = evt.target as VisualElement;
            while (element != null && element != target && !(element is Button)) element = element.parent;
            pressedNode = element is Button ? element.userData as string : null;
            (element is Button ? element : target).Focus();
            target.CapturePointer(pointer);
            evt.StopImmediatePropagation();
        }

        private void Move(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer) return;
            Vector2 current = target.WorldToLocal(evt.position);
            if (!dragging && (current - start).sqrMagnitude > 36)
            {
                dragging = true;
                target.AddToClassList("is-dragging");
                map.Pan(current - start);
            }
            else if (dragging) map.Pan(current - last);
            last = current;
            evt.StopPropagation();
        }

        private void Up(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer || evt.button != button) return;
            Vector2 end = target.WorldToLocal(evt.position);
            string selection = !dragging && button == 0 && (end - start).sqrMagnitude <= 36 &&
                target.contentRect.Contains(end) ? pressedNode : null;
            bool coordinate = !dragging && button == 0 && (end - start).sqrMagnitude <= 36 &&
                target.contentRect.Contains(end) && pressedNode == null;
            Cancel();
            if (selection != null) map.Choose(selection);
            else if (coordinate) map.ChooseCoordinate(end);
            evt.StopImmediatePropagation();
        }

        private void Wheel(WheelEvent evt)
        {
            map.ZoomAt(target.WorldToLocal(evt.mousePosition), Mathf.Pow(1.12f, -evt.delta.y));
            evt.StopPropagation();
        }

        private void OnCancel(PointerCancelEvent evt) { if (evt.pointerId == pointer) Cancel(); }
        private void CaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == pointer) Cancel(); }
        private void Detached(DetachFromPanelEvent evt) => Cancel();

        public void Cancel()
        {
            int previous = pointer;
            pointer = -1; pressedNode = null; dragging = false;
            target?.RemoveFromClassList("is-dragging");
            if (previous >= 0 && target != null && target.HasPointerCapture(previous)) target.ReleasePointer(previous);
        }
    }
}
