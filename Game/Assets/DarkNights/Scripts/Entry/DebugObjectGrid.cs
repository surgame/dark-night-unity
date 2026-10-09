#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using GameCore.Objects.Definition;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 固定尺寸、自动换行的物体图标网格；滚动由 Hub 负责，只有实际选中按钮获得高亮。
    /// 筛选只重建当前少量定义的按钮，点击捕获该定义，避免行复用遗留旧目标。
    /// </summary>
    internal sealed class DebugObjectGrid : IDisposable
    {
        private readonly VisualElement root;
        private readonly Action<ObjectDefinition> select;
        private ObjectDefinition[] definitions = Array.Empty<ObjectDefinition>();
        internal DebugObjectGrid(VisualElement root, Action<ObjectDefinition> select)
        {
            this.root = root;
            this.select = select;
        }
        internal void SetDefinitions(ObjectDefinition[] values, string selectedGuid)
        {
            if (!SameDefinitions(values))
            {
                definitions = values;
                root.Clear();
                foreach (var definition in definitions) root.Add(CreateButton(definition));
            }
            foreach (var child in root.Children())
                child.EnableInClassList("dn-objects-selected",
                    ((ObjectDefinition)child.userData).Guid.ToString() == selectedGuid);
        }
        private bool SameDefinitions(ObjectDefinition[] values)
        {
            if (values.Length != definitions.Length) return false;
            for (int index = 0; index < values.Length; index++)
                if (values[index] != definitions[index]) return false;
            return true;
        }
        private Button CreateButton(ObjectDefinition definition)
        {
            var button = new Button(() => select(definition))
            {
                userData = definition,
                tooltip = definition.Name + "\n" + definition.Key + "\n" + DebugObjectCatalog.Status(definition)
            };
            button.AddToClassList("dn-objects-tile");
            if (definition.Icon != null)
            {
                var icon = new Image { sprite = definition.Icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("dn-objects-icon");
                button.Add(icon);
            }
            else
            {
                var missing = new Label("?") { pickingMode = PickingMode.Ignore };
                missing.AddToClassList("dn-objects-missing-icon");
                button.Add(missing);
                button.tooltip += "\n图标待补";
            }
            return button;
        }
        public void Dispose()
        {
            root.Clear();
            definitions = Array.Empty<ObjectDefinition>();
        }
    }
}
#endif
