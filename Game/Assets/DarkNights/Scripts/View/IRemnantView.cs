using DarkNights.Core.ViewData;

namespace DarkNights.View
{
    /// <summary>
    /// 标记能够把同一实体 Prefab 作为被动尸体或废墟呈现的主视图。
    /// 残骸只解释冻结表现提示和显示年龄，不绑定实体身份，也不参与对象状态结算。
    /// </summary>
    public interface IRemnantView
    {
        void PresentRemnant(VisualCue cue, double age);
    }
}
