using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>可替换的本地过场采样合同；只决定星点位置和亮度，不提交网络命令、不完成权威航程。</summary>
    public interface ITravelTransitionPresentation
    {
        Vector2 StarPosition(Vector2 origin, float elapsed, float speed);
        float StarOpacity(float elapsed, float duration);
    }
}
