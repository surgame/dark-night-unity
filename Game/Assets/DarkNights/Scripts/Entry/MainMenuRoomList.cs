using System;
using System.Collections.Generic;
using DarkNights.Core.ViewData;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 房间浏览器的原生固定高度虚拟列表；仅为可见行创建控件，行复用不重复订阅点击。
    /// 名称、锁、人数和可选Ping来自冻结发现副本，点击仅向本面板宿主提交明确房间。
    /// </summary>
    internal sealed class MainMenuRoomList : IDisposable
    {
        private readonly ListView list;
        private readonly List<RoomListingData> rooms = new List<RoomListingData>();
        internal event Action<RoomListingData> Chosen;

        internal MainMenuRoomList(ListView list)
        {
            this.list = list;
            list.itemsSource = rooms;
            list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            list.fixedItemHeight = 34;
            list.selectionType = SelectionType.Single;
            list.reorderable = false;
            list.showBorder = false;
            list.makeItem = Make;
            list.bindItem = Bind;
            list.unbindItem = (element, index) => element.userData = null;
            list.itemsChosen += Choose;
        }

        private VisualElement Make()
        {
            var row = new Button();
            row.AddToClassList("room-row");
            var icon = new VisualElement { name = "lock", pickingMode = PickingMode.Ignore };
            icon.AddToClassList("room-lock"); row.Add(icon);
            var name = new Label { name = "roomTitle", pickingMode = PickingMode.Ignore, enableRichText = false };
            name.AddToClassList("room-title"); row.Add(name);
            var count = new Label { name = "players", pickingMode = PickingMode.Ignore };
            count.AddToClassList("room-count"); row.Add(count);
            var ping = new Label { name = "ping", pickingMode = PickingMode.Ignore };
            ping.AddToClassList("room-ping"); row.Add(ping);
            row.clicked += () => { if (row.userData is RoomListingData value) Chosen?.Invoke(value); };
            return row;
        }

        private void Bind(VisualElement row, int index)
        {
            RoomListingData value = rooms[index];
            row.userData = value;
            row.Q("lock").EnableInClassList("room-lock-on", value.RequiresPassword);
            row.Q<Label>("roomTitle").text = value.Name;
            row.Q<Label>("players").text = value.Players + " / 4";
            row.Q<Label>("ping").text = value.Ping.HasValue ? value.Ping.Value + " ms" : "—";
            row.tooltip = value.Compatible ? value.Name + (value.RequiresPassword ? " · 需要密码" : "") : "版本或内容不同";
            row.SetEnabled(value.Compatible);
        }

        private void Choose(IEnumerable<object> selected)
        {
            foreach (object value in selected)
                if (value is RoomListingData room && room.Compatible) { Chosen?.Invoke(room); break; }
        }

        internal void Present(IReadOnlyList<RoomListingData> values)
        {
            rooms.Clear();
            foreach (RoomListingData room in values) rooms.Add(room);
            list.RefreshItems();
        }

        internal void SetNarrow(bool narrow) { list.fixedItemHeight = narrow ? 44 : 34; }

        public void Dispose()
        {
            list.itemsChosen -= Choose; list.itemsSource = null;
            list.makeItem = null; list.bindItem = null; list.unbindItem = null;
            rooms.Clear(); Chosen = null;
        }
    }
}
