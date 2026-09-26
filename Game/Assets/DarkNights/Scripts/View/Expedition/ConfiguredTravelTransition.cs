using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>首轮配置策略：星点平移、淡出或静止；只采样本地展示时间，不使用玩法随机数或世界状态。</summary>
    public sealed class ConfiguredTravelTransition : ITravelTransitionPresentation
    {
        private readonly string kind;
        public ConfiguredTravelTransition(string kind) { this.kind = kind; }

        public Vector2 StarPosition(Vector2 origin, float elapsed, float speed) => kind == "star-shift" ?
            new Vector2(Mathf.Repeat(origin.x - elapsed * speed / 1000f, 1), origin.y) : origin;

        public float StarOpacity(float elapsed, float duration) => kind == "fade" ?
            1 - Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration)) : 1;
    }
}
