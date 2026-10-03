namespace DarkNights.Editor
{
    /// <summary>聚合操作的交互职责；项目作者来源、原生工具、场景快捷操作及只定位资料，不依据标题推断行为。</summary>
    internal enum DarkNightsWorkbenchEntryKind
    {
        Editor,
        Tool,
        Scene,
        Reference
    }
}
