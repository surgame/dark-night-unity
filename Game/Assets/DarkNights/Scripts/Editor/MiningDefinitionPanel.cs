using System;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 可嵌入的采集 Definition 编辑与匹配面板；原生 Inspector 编辑唯一资产，匹配复用正式冻结规则。
    /// 面板不保存配置副本，目标切换释放 Editor，宿主关闭必须 Dispose；后台刷新只更新只读状态。
    /// </summary>
    internal sealed class MiningDefinitionPanel : VisualElement, IDisposable
    {
        private readonly MiningDefinitionPanelState selection;
        private ObjectDefinition tool { get => selection.Tool; set => selection.Tool = value; }
        private ObjectDefinition deposit { get => selection.Deposit; set => selection.Deposit = value; }
        private DarkNightsDefinitionEditor inspector;
        private readonly VisualElement content = new VisualElement();
        private readonly HelpBox result = new HelpBox("", HelpBoxMessageType.Info);
        private readonly Label dirty = new Label();
        private readonly DropdownField kind = new DropdownField("目标类别", new System.Collections.Generic.List<string> { "矿床", "前景岩壁" }, 0);
        private readonly TextField material = new TextField("材料 Key");
        private readonly Toggle rare = new Toggle("使用稀有矿材料");
        private readonly IVisualElementScheduledItem refresh;
        private Button save;
        private string tab { get => selection.Tab; set => selection.Tab = value; }
        private bool disposed;

        internal MiningDefinitionPanel() : this(new MiningDefinitionPanelState()) { }
        internal MiningDefinitionPanel(MiningDefinitionPanelState selection)
        {
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection)); selection.Initialize();
            AddToClassList("dn-mining-panel");
            kind.SetValueWithoutNotify(selection.Mineral ? "矿床" : "前景岩壁");
            rare.SetValueWithoutNotify(selection.Rare); material.SetValueWithoutNotify(selection.Material);
            Add(new HelpBox("直接编辑工具和矿床的原生 YYGC Definition；保存当前资产后新会话生效。", HelpBoxMessageType.Info));
            var toolField = new ObjectField("工具 Definition") { name = "mining-tool", objectType = typeof(ObjectDefinition), value = tool, allowSceneObjects = false };
            var targetField = new ObjectField("矿床 Definition") { name = "mining-deposit", objectType = typeof(ObjectDefinition), value = deposit, allowSceneObjects = false };
            toolField.RegisterValueChangedCallback(change => { tool = change.newValue as ObjectDefinition; ShowTab(tab); });
            targetField.RegisterValueChangedCallback(change => { deposit = change.newValue as ObjectDefinition; DefaultMaterial(); ShowTab(tab); });
            Add(toolField); Add(targetField);
            var toolbar = new Toolbar();
            toolbar.AddToClassList("dn-mining-tabs");
            toolbar.Add(new ToolbarButton(() => ShowTab("tool")) { text = "工具配置", name = "mining-tool-tab" });
            toolbar.Add(new ToolbarButton(() => ShowTab("deposit")) { text = "矿床配置", name = "mining-deposit-tab" });
            toolbar.Add(new ToolbarButton(() => ShowTab("match")) { text = "匹配验证", name = "mining-match-tab" });
            Add(toolbar); Add(content);
            kind.RegisterValueChangedCallback(_ => { selection.Mineral = kind.index == 0; RefreshMatch(); });
            rare.RegisterValueChangedCallback(_ => { selection.Rare = rare.value; DefaultMaterial(); RefreshMatch(); });
            material.RegisterValueChangedCallback(_ => { selection.Material = material.value; RefreshMatch(); });
            ShowTab(tab);
            refresh = schedule.Execute(UpdateState).Every(250);
        }

        private void ShowTab(string selected)
        {
            if (disposed) return;
            tab = selected == "deposit" || selected == "match" ? selected : "tool";
            foreach (var button in this.Query<ToolbarButton>().ToList())
                button.EnableInClassList("dn-chip-active", button.name == "mining-" + tab + "-tab");
            ClearInspector(); content.Clear(); save = null;
            if (tab == "match")
            {
                content.Add(kind); content.Add(rare); content.Add(material); content.Add(result);
                RefreshMatch(); return;
            }
            var asset = tab == "tool" ? tool : deposit;
            if (asset == null) { content.Add(new HelpBox("请选择对应 Definition。", HelpBoxMessageType.Info)); return; }
            content.Add(new Label("Key: " + asset.Key + "\nGUID: " + asset.Guid) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 8, marginBottom = 8 } });
            content.Add(new Button(() => DarkNightsWorkbenchWindow.Locate(asset)) { text = "在 Project 中定位原始资产" });
            inspector = new DarkNightsDefinitionEditor(asset, false); content.Add(inspector);
            save = new Button(SaveSelected) { text = "校验并保存当前 Definition", name = "mining-save" };
            content.Add(dirty); content.Add(save); content.Add(result);
            result.text = ""; UpdateState();
        }

        private void UpdateState()
        {
            if (disposed) return;
            if (tab == "match") { RefreshMatch(); return; }
            var asset = tab == "tool" ? tool : deposit;
            bool blocked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
            save?.SetEnabled(!blocked && asset != null);
            dirty.text = blocked ? "Play／编译期间禁止编辑与保存。" : asset != null && EditorUtility.IsDirty(asset) ? "此原始资产有未保存修改。" : "原始资产已保存。";
        }

        private void SaveSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                var asset = tab == "tool" ? tool : deposit;
                if (asset == null) return;
                MiningDefinitionValidation.Validate(asset, tab == "tool");
                if (inspector == null || !inspector.TrySave(value => MiningDefinitionValidation.Validate(value, tab == "tool")))
                    throw new InvalidOperationException(inspector?.Error ?? "当前编辑器不可保存，请重新选择 Definition。");
                result.text = "已校验并保存当前 Definition，新会话生效。"; result.messageType = HelpBoxMessageType.Info;
            }
            catch (Exception error) { result.text = error.Message; result.messageType = HelpBoxMessageType.Error; }
            UpdateState();
        }

        private void DefaultMaterial()
        {
            try
            {
                var target = deposit?.SharedConfigs.OfType<MineralDepositRuleConfig>().SingleOrDefault();
                selection.Material = target == null ? "" : rare.value ? target.RareResource : target.CommonResource;
                material.SetValueWithoutNotify(selection.Material);
            }
            catch (Exception error) { result.text = error.Message; result.messageType = HelpBoxMessageType.Error; }
        }

        private void RefreshMatch()
        {
            try
            {
                var mining = tool?.SharedConfigs.OfType<MiningToolConfig>().SingleOrDefault()?.Freeze();
                var target = deposit?.SharedConfigs.OfType<MineralDepositRuleConfig>().SingleOrDefault();
                bool mineral = kind.index == 0;
                if (mining == null || mineral && target == null) { result.text = "请选择带采集配置的工具，以及带矿床配置的目标 Definition。"; result.messageType = HelpBoxMessageType.Warning; return; }
                if (mineral) target.Validate();
                string reason = mining.BlockReason(mineral ? HeroMiningTargetKind.MineralDeposit : HeroMiningTargetKind.Foreground,
                    material.value, mineral ? target.RequiredMiningLevel : 1, mineral ? deposit.Guid.ToString() : "");
                result.text = reason.Length == 0 ? "能力匹配通过；实际执行还需校验距离、遮挡、状态和权限。" : reason;
                result.messageType = reason.Length == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
                rare.SetEnabled(mineral);
            }
            catch (Exception error) { result.text = error.Message; result.messageType = HelpBoxMessageType.Error; }
        }

        private void ClearInspector() { inspector?.Dispose(); inspector = null; }
        public void Dispose() { if (disposed) return; disposed = true; refresh.Pause(); ClearInspector(); Clear(); }
    }
}
