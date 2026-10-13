using DarkNights.Core.ViewData;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.View.Terrain
{
    /// <summary>地质事件原生Prefab的被动表现；显式绑定警示框、松岩与落点，读取冻结阶段做抖动和落石动画。没有碰撞回调或业务写权限，未运行时保留编辑态布局。</summary>
    public sealed class GeologyEventView : ObjectView
    {
        public SpriteRenderer Warning;
        public Transform Falling;
        private Vector3 source, destination;
        private double warningSeconds, fallSeconds;

        public void Bind(Vector3 sourcePosition, Vector3 landingPosition, double warningDuration, double fallDuration)
        {
            source = sourcePosition; destination = landingPosition;
            warningSeconds = warningDuration; fallSeconds = fallDuration;
        }

        public void Present(GeologyEventViewData value)
        {
            bool active = value.Phase >= 1 && value.Phase <= 3;
            Warning.enabled = active;
            Warning.color = new Color(1, .4f, .12f, .3f + .3f * Mathf.Abs(Mathf.Sin((float)value.Clock * 10)));
            Falling.gameObject.SetActive(active);
            if (value.Phase == 1)
                Falling.position = source + Vector3.right * Mathf.Sin((float)(value.Clock / warningSeconds) * 80) * .015f;
            else Falling.position = Vector3.Lerp(source, destination, value.Phase == 3 ? 1 :
                Mathf.Clamp01((float)(value.Clock / fallSeconds)));
        }
    }
}
