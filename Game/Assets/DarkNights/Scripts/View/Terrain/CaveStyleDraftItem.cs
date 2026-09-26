using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>一个源样式资产及其运行草稿、取消基线和保存冲突基线；临时副本归 CaveStyleDraft，随工作台统一释放。</summary>
    public sealed class CaveStyleDraftItem
    {
        public ScriptableObject Source, Working, Baseline, Original;
    }
}
