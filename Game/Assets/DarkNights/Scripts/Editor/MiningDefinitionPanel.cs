using System;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 项目专有的只读采集辅助区；装配、白名单与目标匹配读取原始 Definition，不补配置、不编辑或保存资产。
    /// 目标与输入归宿主导航状态；原生 Workshop 接受选定目标，刷新和关闭只释放本面板。
    /// </summary>
    internal sealed class MiningDefinitionPanel : VisualElement, IDisposable
    {
        private readonly MiningDefinitionPanelState selection;
        private readonly HelpBox result = new HelpBox("", HelpBoxMessageType.Info);
        private readonly HelpBox assembly = new HelpBox("", HelpBoxMessageType.Info);
        private readonly DropdownField kind = new DropdownField("目标类别", new System.Collections.Generic.List<string> { "矿床", "前景岩壁" }, 0);
        private readonly TextField material = new TextField("材料 Key");
        private readonly Toggle rare = new Toggle("使用稀有矿材料");
        private readonly IVisualElementScheduledItem refresh;
        private readonly Button editTool, editDeposit;
        private bool disposed;

        internal MiningDefinitionPanel() : this(new MiningDefinitionPanelState()) { }
        internal MiningDefinitionPanel(MiningDefinitionPanelState selection)
        {
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection)); selection.Initialize();
            AddToClassList("dn-mining-panel");
            kind.SetValueWithoutNotify(selection.Mineral ? "矿床" : "前景岩壁");
            rare.SetValueWithoutNotify(selection.Rare); material.SetValueWithoutNotify(selection.Material);
            var toolField = new ObjectField("工具 Definition") { name = "mining-tool", objectType = typeof(ObjectDefinition), value = selection.Tool, allowSceneObjects = false };
            var targetField = new ObjectField("矿床 Definition") { name = "mining-deposit", objectType = typeof(ObjectDefinition), value = selection.Deposit, allowSceneObjects = false };
            toolField.RegisterValueChangedCallback(change => { selection.Tool = change.newValue as ObjectDefinition; UpdateState(); });
            targetField.RegisterValueChangedCallback(change => { selection.Deposit = change.newValue as ObjectDefinition; DefaultMaterial(); UpdateState(); });
            Add(toolField); Add(targetField);
            var actions = new VisualElement(); actions.AddToClassList("dn-actions"); Add(actions);
            editTool = new Button(() => Open(selection.Tool)) { text = "在 Workshop 编辑工具", name = "mining-edit-tool" };
            editDeposit = new Button(() => Open(selection.Deposit)) { text = "在 Workshop 编辑矿床", name = "mining-edit-deposit" };
            actions.Add(editTool); actions.Add(editDeposit); Add(assembly);
            Add(kind); Add(rare); Add(material); Add(result);
            kind.RegisterValueChangedCallback(_ => { selection.Mineral = kind.index == 0; RefreshMatch(); });
            rare.RegisterValueChangedCallback(_ => { selection.Rare = rare.value; DefaultMaterial(); RefreshMatch(); });
            material.RegisterValueChangedCallback(_ => { selection.Material = material.value; RefreshMatch(); });
            UpdateState(); refresh = schedule.Execute(UpdateState).Every(500);
        }

        private void Open(ObjectDefinition definition)
        {
            if (definition == null || DarkNightsNativeWorkspace.Blocked) return;
            try { DarkNightsNativeWorkspace.Workshop(definition); }
            catch (Exception error) { result.text = error.Message; result.messageType = HelpBoxMessageType.Error; }
        }

        private void UpdateState()
        {
            if (disposed) return;
            editTool.SetEnabled(selection.Tool != null && !DarkNightsNativeWorkspace.Blocked);
            editDeposit.SetEnabled(selection.Deposit != null && !DarkNightsNativeWorkspace.Blocked);
            try
            {
                MiningDefinitionValidation.Validate(selection.Tool, true);
                MiningDefinitionValidation.Validate(selection.Deposit, false);
                assembly.text = "工具／矿床装配和矿床白名单校验通过。"; assembly.messageType = HelpBoxMessageType.Info;
            }
            catch (Exception error) { assembly.text = error.Message; assembly.messageType = HelpBoxMessageType.Warning; }
            RefreshMatch();
        }

        private void DefaultMaterial()
        {
            try
            {
                var target = selection.Deposit?.SharedConfigs?.OfType<MineralDepositRuleConfig>().SingleOrDefault();
                selection.Material = target == null ? "" : rare.value ? target.RareResource : target.CommonResource;
                material.SetValueWithoutNotify(selection.Material);
            }
            catch (Exception error) { result.text = error.Message; result.messageType = HelpBoxMessageType.Error; }
        }

        private void RefreshMatch()
        {
            try
            {
                bool mineral = kind.index == 0; rare.SetEnabled(mineral);
                var mining = selection.Tool?.SharedConfigs?.OfType<MiningToolConfig>().SingleOrDefault()?.Freeze();
                var target = selection.Deposit?.SharedConfigs?.OfType<MineralDepositRuleConfig>().SingleOrDefault();
                if (mining == null || mineral && target == null)
                { result.text = "请选择带采集配置的工具，以及带矿床配置的目标 Definition。"; result.messageType = HelpBoxMessageType.Warning; return; }
                if (mineral) target.Validate();
                string reason = mining.BlockReason(mineral ? HeroMiningTargetKind.MineralDeposit : HeroMiningTargetKind.Foreground,
                    material.value, mineral ? target.RequiredMiningLevel : 1, mineral ? selection.Deposit.Guid.ToString() : "");
                result.text = reason.Length == 0 ? "能力匹配通过；实际执行还需校验距离、遮挡、状态和权限。" : reason;
                result.messageType = reason.Length == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            }
            catch (Exception error) { result.text = error.Message; result.messageType = HelpBoxMessageType.Error; }
        }

        public void Dispose() { if (disposed) return; disposed = true; refresh.Pause(); Clear(); }
    }
}
