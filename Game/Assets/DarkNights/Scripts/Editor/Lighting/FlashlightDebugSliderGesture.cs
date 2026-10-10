using System;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>
    /// 手电滑条一次指针拖动的边界。旁听原生滑条事件，不接管其捕获；释放、失去捕获或脱离面板时结束，
    /// 只结束当前指针及其实际拖动对象的捕获；数值输入框单独提交，避免文字选择或其他控件的捕获变化拆散历史。
    /// </summary>
    public sealed class FlashlightDebugSliderGesture : PointerManipulator
    {
        private readonly Action begin;
        private readonly Action end;
        private int pointerId = -1;
        private VisualElement pressed;

        public FlashlightDebugSliderGesture(Action begin, Action end)
        {
            this.begin = begin; this.end = end;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Begin, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(End, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(Canceled, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(CaptureEnded);
            target.RegisterCallback<DetachFromPanelEvent>(Detached);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(Begin, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(End, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCancelEvent>(Canceled, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(CaptureEnded);
            target.UnregisterCallback<DetachFromPanelEvent>(Detached);
            Finish();
        }

        private void Begin(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            var element = evt.target as VisualElement;
            if (element == null || element is TextField || element.GetFirstAncestorOfType<TextField>() != null) return;
            Finish(); pointerId = evt.pointerId; pressed = element;
            begin();
        }

        private void End(PointerUpEvent evt) { if (evt.button == 0 && evt.pointerId == pointerId) Finish(); }
        private void Canceled(PointerCancelEvent evt) { if (evt.pointerId == pointerId) Finish(); }
        private void CaptureEnded(PointerCaptureOutEvent evt)
        {
            var captured = evt.target as VisualElement;
            if (evt.pointerId == pointerId && pressed != null && captured != null
                && (captured == target || captured == pressed || captured.Contains(pressed))) Finish();
        }
        private void Detached(DetachFromPanelEvent evt) => Finish();
        private void Finish()
        {
            if (pointerId < 0) return;
            pointerId = -1; pressed = null; end();
        }
    }
}
