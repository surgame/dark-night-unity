namespace DarkNights.Editor
{
    /// <summary>工作台入口的交互职责；内嵌编辑、专用工具、可打开场景及只定位资料分别布局，不依据标题推断行为。</summary>
    internal enum DarkNightsWorkbenchEntryKind
    {
        Editor,
        Tool,
        Scene,
        Reference
    }
}
