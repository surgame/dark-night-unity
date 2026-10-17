namespace DarkNights.Editor
{
    /// <summary>聚合操作的交互职责；诊断可在Play期间打开，作者编辑与场景切换仍受门控，资料只定位。不依据标题推断行为。</summary>
    internal enum DarkNightsWorkbenchEntryKind
    {
        Editor,
        Tool,
        Scene,
        Reference,
        Diagnostic
    }
}
