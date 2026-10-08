#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Objects.Definition;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>按行虚拟化的物体网格；列数随实际宽度变化，复用按钮读取当前绑定定义，筛选不会遗留旧点击目标。</summary>
    internal sealed class DebugObjectGrid : IDisposable
    {
        private readonly ListView list;
        private readonly Action<ObjectDefinition> select;
        private ObjectDefinition[] definitions = Array.Empty<ObjectDefinition>();
        private string selectedGuid = "";
        private int columns = 4;
        internal DebugObjectGrid(ListView list, Action<ObjectDefinition> select)
        {
            this.list = list; this.select = select;
            list.fixedItemHeight = 86; list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            list.selectionType = SelectionType.None; list.makeItem = MakeRow; list.bindItem = BindRow;
            list.unbindItem = UnbindRow;
            list.RegisterCallback<GeometryChangedEvent>(Resize);
        }
        private VisualElement MakeRow()
        {
            var row = new VisualElement(); row.AddToClassList("dn-objects-grid-row");
            for (int index = 0; index < columns; index++)
            {
                var button = new Button(); button.AddToClassList("dn-objects-tile");
                var icon = new Image { name = "icon" }; icon.AddToClassList("dn-objects-icon"); button.Add(icon);
                var name = new Label { name = "name" }; name.AddToClassList("dn-objects-name"); button.Add(name);
                var state = new Label { name = "state" }; state.AddToClassList("rdh-muted"); button.Add(state);
                button.clicked += () => { if (button.userData is ObjectDefinition definition) select(definition); };
                row.Add(button);
            }
            return row;
        }
        private void BindRow(VisualElement row, int rowIndex)
        {
            for (int column = 0; column < row.childCount; column++)
            {
                int index = rowIndex * columns + column;
                var button = (Button)row[column]; var definition = index < definitions.Length ? definitions[index] : null;
                button.userData = definition; button.EnableInClassList("dn-objects-empty-tile", definition == null);
                button.Q<Label>("name").text = definition?.Name ?? "";
                button.Q<Label>("state").text = definition == null ? "" : DebugObjectCatalog.Status(definition);
                button.Q<Image>("icon").sprite = definition?.Icon;
                button.EnableInClassList("dn-objects-selected", definition != null && definition.Guid.ToString() == selectedGuid);
            }
        }
        internal void SetDefinitions(ObjectDefinition[] values, string guid)
        {
            definitions = values; selectedGuid = guid;
            list.itemsSource = Enumerable.Range(0, (values.Length + columns - 1) / columns).ToList(); list.RefreshItems();
        }
        private void UnbindRow(VisualElement row, int index)
        {
            foreach (var child in row.Children())
            { child.userData = null; child.Q<Image>("icon").sprite = null; }
        }
        private void Resize(GeometryChangedEvent value)
        {
            int next = Math.Max(1, Math.Min(6, (int)(value.newRect.width / 150)));
            if (columns == next) return; columns = next; list.Rebuild(); SetDefinitions(definitions, selectedGuid);
        }
        public void Dispose()
        {
            list.UnregisterCallback<GeometryChangedEvent>(Resize); list.itemsSource = null;
            list.makeItem = null; list.bindItem = null; list.unbindItem = null;
        }
    }
}
#endif
