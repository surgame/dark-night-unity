#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using GameCore.Debugging;
using GameCore.Objects.Definition;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>统一物体网格及原持有装备区；只读取冻结副本与开发者权限，所有更改发送显式服务端请求。</summary>
    internal sealed class DebugObjectPanel : IRuntimeDebugPanel
    {
        private readonly SessionNetwork network;
        private RuntimeDebugPanelContext context;
        private VisualElement root;
        private DebugObjectGrid grid;
        private DebugHeldEquipment equipment;
        private VisualElement themedRoot;
        private StyleSheet theme;
        private ObjectDefinition[] definitions;
        private ObjectDefinition selected;
        private TextField search;
        private DropdownField target, instance;
        private IntegerField quantity;
        private Toggle mode;
        private Button add, remove;
        private IVisualElementScheduledItem refresh;
        private List<ActorViewData> actors = new List<ActorViewData>();
        private List<int> instances = new List<int>();
        private ActorViewData actor;
        private long pendingSequence;
        private bool pending;
        private int activationRevision;
        private long connection = -1;
        private int epoch = -1;
        private float sentAt;
        private CommandFeedback earlyFeedback;
        private readonly Queue<string> history = new Queue<string>();
        internal DebugObjectPanel(SessionNetwork network) { this.network = network; }

        public VisualElement CreateView(RuntimeDebugPanelContext context)
        {
            this.context = context;
            var template = Resources.Load<VisualTreeAsset>("DarkNights/Debugging/Objects");
            if (template == null) throw new InvalidOperationException("缺少物体调试 UXML。");
            root = template.CloneTree(); definitions = DebugObjectCatalog.Definitions();
            search = root.Q<TextField>("search"); target = root.Q<DropdownField>("target");
            instance = root.Q<DropdownField>("instance"); quantity = root.Q<IntegerField>("quantity"); mode = root.Q<Toggle>("godMode");
            add = root.Q<Button>("addObject"); remove = root.Q<Button>("removeObject");
            grid = new DebugObjectGrid(root.Q("objectGrid"), Select);
            theme = Resources.Load<StyleSheet>("DarkNights/Debugging/HubTheme");
            root.RegisterCallback<AttachToPanelEvent>(AttachTheme);
            equipment = new DebugHeldEquipment(root, RemoveEquipment);
            search.RegisterValueChangedCallback(Search); target.RegisterValueChangedCallback(Target);
            mode.RegisterValueChangedCallback(Mode); add.clicked += Add; remove.clicked += Remove;
            refresh = root.schedule.Execute(Present).Every(150); refresh.Pause();
            Select(definitions.FirstOrDefault(value => DebugObjectCatalog.Item(value) != null && value.Icon != null) ?? definitions.FirstOrDefault());
            return root;
        }
        private void Select(ObjectDefinition definition)
        {
            selected = definition;
            root.Q<Label>("selectedName").text = selected?.Name ?? "—";
            root.Q<Label>("selectedKey").text = selected?.Key ?? "";
            Filter(); Present();
        }
        private void Search(ChangeEvent<string> value) => Filter();
        private void AttachTheme(AttachToPanelEvent value)
        {
            if (theme == null || root.panel == null || themedRoot != null) return;
            themedRoot = root.panel.visualTree;
            themedRoot.AddToClassList("dn-debug-hub");
            themedRoot.styleSheets.Add(theme);
        }
        private void Filter()
        {
            string text = (search.value ?? "").Trim();
            var values = definitions.Where(value => (value.Name ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (value.Key ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            if (selected != null && !values.Contains(selected))
            {
                selected = null;
                root.Q<Label>("selectedName").text = "请选择物体";
                root.Q<Label>("selectedKey").text = "";
                Present();
            }
            grid.SetDefinitions(values, selected?.Guid.ToString() ?? "");
            root.Q<Label>("definitionCount").text = values.Length == 0 ? "没有匹配的物体" : values.Length + " / " + definitions.Length + " 个物体";
        }
        private void Target(ChangeEvent<string> value) => Present();
        private void Mode(ChangeEvent<bool> value)
        { Send(SessionOperation.DebugSetEnabled, null, value: value.newValue ? 1 : 0); }
        private bool Allowed()
        {
            var client = network?.Client; var frame = client?.Replica.Current;
            return client?.Ready == true && network.Hosting && client.PlayerSlot == 0 && frame != null && !frame.Paused && !pending;
        }
        private bool Enabled() => network?.ObjectWorld?.DeveloperModeEnabled(epoch) == true;
        private void Present()
        {
            var frame = network?.Client?.Replica.Current;
            if (frame == null)
            {
                actor = null; mode.SetEnabled(false); mode.SetValueWithoutNotify(false);
                SetAvailability(false); equipment.Present(null, false); return;
            }
            if (epoch != frame.Epoch || connection != network.Client.ConnectionGeneration)
            {
                activationRevision++; epoch = frame.Epoch; connection = network.Client.ConnectionGeneration;
                pending = false; pendingSequence = 0; earlyFeedback = null;
            }
            if (pending && Time.unscaledTime - sentAt > 12) { pending = false; Report("请求等待确认超时；未自动重发。"); }
            actors = frame.World.Actors.Where(value => value.ControllerSlot >= 0 && value.Hp > 0).ToList();
            var labels = actors.Select(value => "主角 " + value.ControllerSlot + " · #" + value.Id).ToList();
            if (!target.choices.SequenceEqual(labels)) target.choices = labels;
            if (!labels.Contains(target.value)) target.SetValueWithoutNotify(labels.FirstOrDefault() ?? "");
            int index = labels.IndexOf(target.value); actor = index < 0 ? null : actors[index];
            root.Q<Label>("equipmentTarget").text = target.value;
            mode.SetValueWithoutNotify(Enabled()); mode.SetEnabled(Allowed());
            instances = DebugObjectCatalog.Instances(frame.World, selected);
            var instanceLabels = instances.Select(value => "#" + value).ToList();
            if (!instance.choices.SequenceEqual(instanceLabels)) instance.choices = instanceLabels;
            if (!instanceLabels.Contains(instance.value)) instance.SetValueWithoutNotify(instanceLabels.FirstOrDefault() ?? "");
            instance.EnableInClassList("rdh-hidden", DebugObjectCatalog.Item(selected) != null || instances.Count == 0);
            bool allowed = Allowed() && Enabled(); SetAvailability(allowed); equipment.Present(actor, allowed);
        }
        private void SetAvailability(bool allowed)
        {
            var item = DebugObjectCatalog.Item(selected);
            bool owned = actor != null && item != null && (item.Jetpack ? actor.JetpackOwned :
                Enumerable.Range(0, 4).Any(slot => DebugObjectCatalog.Slot(actor, slot) == selected.Guid.ToString()));
            bool projectile = DebugObjectCatalog.IsProjectile(selected);
            quantity.SetEnabled(allowed && (projectile || item?.RuleKey == "bomb"));
            add.SetEnabled(allowed && actor != null && (item != null && (!owned || item.RuleKey == "bomb") || projectile));
            remove.SetEnabled(allowed && (owned || projectile && instances.Count > 0));
            root.Q<Label>("selectedStatus").text = selected == null ? "" : DebugObjectCatalog.Status(selected);
        }
        private void Add()
        {
            if (!Allowed() || !Enabled() || selected == null || actor == null) return;
            int count = Math.Max(1, Math.Min(100, quantity.value)); quantity.SetValueWithoutNotify(count);
            if (DebugObjectCatalog.IsProjectile(selected))
                Send(SessionOperation.DebugSpawnProjectile, selected, value: count);
            else Send(SessionOperation.DebugGiveEquipment, selected, DebugObjectCatalog.Item(selected)?.RuleKey == "bomb" ? count : 1);
        }
        private void Remove()
        {
            if (DebugObjectCatalog.Item(selected) != null) { RemoveEquipment(selected); return; }
            int index = instance.choices.IndexOf(instance.value);
            if (index >= 0 && index < instances.Count) Send(SessionOperation.DebugRemoveObject, selected, targetId: instances[index]);
        }
        private void RemoveEquipment(ObjectDefinition definition)
        {
            if (!Allowed() || !Enabled() || actor == null || definition == null) return;
            Send(SessionOperation.DebugRemoveEquipment, definition);
        }
        private async void Send(SessionOperation operation, ObjectDefinition definition, int count = 0, int value = -1, int targetId = 0)
        {
            if (!Allowed()) return;
            int revision = activationRevision;
            pending = true; pendingSequence = 0; earlyFeedback = null; sentAt = Time.unscaledTime;
            try
            {
                bool world = operation == SessionOperation.DebugRemoveObject || operation == SessionOperation.DebugSetEnabled;
                int expected = value >= 0 ? value : world ? 0 : actor.InventoryRevision;
                long sequence = await network.Client.Send(operation, world ? Array.Empty<int>() : new[] { actor.Id },
                    targetId, count, definition?.Guid.ToString() ?? "", expected);
                if (revision != activationRevision || network.Client.Replica.Current?.Epoch != epoch ||
                    network.Client.ConnectionGeneration != connection) return;
                pendingSequence = sequence;
                if (earlyFeedback?.Sequence == pendingSequence) Complete(earlyFeedback);
            }
            catch (Exception error) { if (revision != activationRevision) return; pending = false; Report(error.Message); }
            Present();
        }
        private void Feedback(CommandFeedback value)
        {
            if (!pending || value.ReadyReply) return;
            if (pendingSequence == 0) earlyFeedback = value;
            else if (value.Sequence == pendingSequence) Complete(value);
        }
        private void Complete(CommandFeedback value)
        {
            pending = false; Report(value.AffectedCount > 0 ? "已完成 · " + value.AffectedCount : "未执行 · " + value.Code); Present();
        }
        private void Report(string value)
        {
            value = value == null ? "" : value.Substring(0, Math.Min(256, value.Length));
            history.Enqueue(value); while (history.Count > 4) history.Dequeue();
            root.Q<Label>("recentActions").text = string.Join("\n", history.Reverse()); context.Report(value);
        }
        public void OnActivated(CancellationToken token)
        { network.Client.Feedback += Feedback; Present(); refresh.Resume(); }
        public void OnDeactivated()
        { activationRevision++; refresh?.Pause(); if (network?.Client != null) network.Client.Feedback -= Feedback; pending = false; }
        public void Dispose()
        {
            OnDeactivated(); grid?.Dispose();
            root?.UnregisterCallback<AttachToPanelEvent>(AttachTheme);
            if (themedRoot != null) { themedRoot.styleSheets.Remove(theme); themedRoot.RemoveFromClassList("dn-debug-hub"); }
            search?.UnregisterValueChangedCallback(Search); target?.UnregisterValueChangedCallback(Target); mode?.UnregisterValueChangedCallback(Mode);
            if (add != null) add.clicked -= Add; if (remove != null) remove.clicked -= Remove;
            root?.Clear(); history.Clear();
        }
    }
}
#endif
