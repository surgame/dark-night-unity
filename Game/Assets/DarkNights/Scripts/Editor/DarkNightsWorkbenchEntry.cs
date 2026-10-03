using System;
using System.Collections.Generic;

namespace DarkNights.Editor
{
    /// <summary>
    /// 工作台的只读导航项；仅描述用途、原始资产和已有编辑入口，不保存业务配置或编辑草稿。
    /// 路径指向唯一作者来源，打开动作由各专用编辑器继续管理生命周期。
    /// </summary>
    internal sealed class DarkNightsWorkbenchEntry
    {
        internal string Id { get; }
        internal DarkNightsWorkbenchEntryKind Kind { get; }
        internal string Group { get; }
        internal string Title { get; }
        internal string Description { get; }
        internal string SaveHint { get; }
        internal string ActionLabel { get; }
        internal Action OpenEditor { get; }
        internal IReadOnlyList<string> Assets { get; }

        internal DarkNightsWorkbenchEntry(string id, DarkNightsWorkbenchEntryKind kind, string group, string title, string description,
            string saveHint, string actionLabel, Action openEditor, params string[] assets)
        {
            Id = id; Kind = kind; Group = group; Title = title; Description = description; SaveHint = saveHint;
            ActionLabel = actionLabel; OpenEditor = openEditor;
            Assets = Array.AsReadOnly((string[])assets.Clone());
        }

        internal bool Matches(string query) => string.IsNullOrWhiteSpace(query) ||
            (Group + " " + Title + " " + Description + " " + string.Join(" ", Assets))
                .IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
