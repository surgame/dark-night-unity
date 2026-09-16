using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 单向平台的人工场景标记，水平中心与顶面高度取当前 Transform；Width 使用玩法像素坐标。
    /// 只提供几何和被动外观，运行碰撞由冻结布局的权威运动能力判断，不使用 Unity 物理结算。
    /// </summary>
    public sealed class HeroPlatform : MonoBehaviour
    {
        [Min(1)] public int Id = 1;
        [Min(1)] public float Width = 48;
    }
}
