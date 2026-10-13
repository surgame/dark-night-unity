using System;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调参台左侧纵向页签栏；只改变页面可见性并汇总草稿标记，切页不重建配置或保存资产。</summary>
    public sealed class FlashlightDebugNavigation
    {
        private static readonly string[] Names = { "Beam", "Bounce", "Fill", "Scene", "Mount", "Preset", "History" };
        private readonly VisualElement root;
        private readonly Action<string> changed;
        public string Selected { get; private set; }
        public FlashlightDebugNavigation(VisualElement root, string selected, Action<string> changed)
        {
            this.root = root; this.changed = changed;
            foreach (string name in Names) { string captured = name; root.Q<Button>("nav" + name).clicked += () => Select(captured); }
            Select(Array.IndexOf(Names, selected) >= 0 ? selected : "Beam");
        }
        public void Select(string name)
        {
            Selected = name;
            foreach (string page in Names)
            {
                root.Q("page" + page).EnableInClassList("is-hidden", page != name);
                root.Q<Button>("nav" + page).EnableInClassList("is-selected", page == name);
            }
            changed(name);
        }
        public void Changed(string name, bool dirty) => root.Q<Button>("nav" + name).EnableInClassList("has-changes", dirty);
    }
}
