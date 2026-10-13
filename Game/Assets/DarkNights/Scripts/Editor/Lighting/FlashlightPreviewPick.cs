using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>右侧 Image 的单点定位手势；排除 ScaleToFit 的留白，只上报预览坐标，不把事件转发给运行角色输入。</summary>
    public sealed class FlashlightPreviewPick : PointerManipulator
    {
        private readonly Action<Vector2> picked;
        public FlashlightPreviewPick(Action<Vector2> picked)
        { this.picked = picked; activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse }); }
        protected override void RegisterCallbacksOnTarget() => target.RegisterCallback<PointerDownEvent>(Down);
        protected override void UnregisterCallbacksFromTarget() => target.UnregisterCallback<PointerDownEvent>(Down);
        private void Down(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;
            Rect bounds = ImageBounds(target);
            if (bounds.width <= 0 || !bounds.Contains(evt.localPosition)) return;
            picked(new Vector2((evt.localPosition.x - bounds.x) / bounds.width, 1 - (evt.localPosition.y - bounds.y) / bounds.height));
            evt.StopPropagation();
        }
        internal static Rect ImageBounds(VisualElement element)
        {
            Rect bounds = element.contentRect;
            var image = element as Image;
            float aspect = image?.image != null ? (float)image.image.width / image.image.height : 1.6f;
            float width = Mathf.Min(bounds.width, bounds.height * aspect), height = width / aspect;
            return new Rect(bounds.x + (bounds.width - width) * .5f, bounds.y + (bounds.height - height) * .5f, width, height);
        }
    }
}
