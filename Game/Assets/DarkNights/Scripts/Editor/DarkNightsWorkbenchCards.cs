using System;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>工作台工具和搜索结果的共用操作卡片；只描述入口，业务执行和错误反馈由宿主拥有。</summary>
    internal static class DarkNightsWorkbenchCards
    {
        internal static VisualElement Create(DarkNightsWorkbenchEntry entry, Action activate, string actionLabel = null)
        {
            var row = new VisualElement { name = "tool-" + entry.Id }; row.AddToClassList("dn-tool-row");
            var info = new VisualElement(); info.AddToClassList("dn-entry-info"); row.Add(info);
            var title = new Label(entry.Title); title.AddToClassList("dn-entry-title"); info.Add(title);
            var description = new Label(entry.Description); description.AddToClassList("dn-description"); info.Add(description);
            var action = new Button(activate)
            {
                text = actionLabel ?? entry.ActionLabel, name = "action-" + entry.Id, tooltip = entry.SaveHint
            };
            action.AddToClassList("dn-native-action"); action.AddToClassList("dn-primary"); row.Add(action);
            return row;
        }
    }
}
