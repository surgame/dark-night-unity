using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// UI Toolkit 单面板星球目录；表格选择、序列化详情、草稿应用和纯蓝图预览共用一份作者配置。
    /// 生命周期仅管理临时草稿与纹理；正式会话定义由显式应用修改，普通导入与构建不会调用。
    /// </summary>
    public sealed class ExpeditionFlowWindow : EditorWindow
    {
        [SerializeField] private ExpeditionFlowDraft draft;
        private SerializedObject serialized;
        private MultiColumnListView table;
        private ScrollView details;
        private Label status;
        private ExpeditionPlanetPreview preview;

        [MenuItem("Dark Nights/配置/星球与航程")]
        public static void Open()
        {
            var window = GetWindow<ExpeditionFlowWindow>("星球与航程");
            window.minSize = new Vector2(760, 620);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            saveChangesMessage = "星球航程草稿尚未应用到正式会话定义。";
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            rootVisualElement.Unbind(); serialized?.Dispose(); serialized = null;
            preview?.Dispose(); preview = null;
            table = null; details = null; status = null;
        }

        private void OnDestroy()
        {
            if (draft != null) DestroyImmediate(draft);
        }

        public void CreateGUI()
        {
            rootVisualElement.Unbind(); preview?.Dispose(); preview = null;
            rootVisualElement.Clear();
            try
            {
                if (draft == null)
                {
                    draft = CreateInstance<ExpeditionFlowDraft>();
                    draft.hideFlags = HideFlags.HideAndDontSave;
                    draft.Load(AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FormalObjectContentSetup.SessionDefinitionPath));
                }
                serialized?.Dispose(); serialized = new SerializedObject(draft);
                BuildPanel();
                rootVisualElement.TrackSerializedObjectValue(serialized, _ => DraftChanged());
                rootVisualElement.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
                DraftChanged();
            }
            catch (Exception error) { rootVisualElement.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
        }

        private void BuildPanel()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = 10;
            root.style.paddingTop = root.style.paddingBottom = 8;
            var source = new ObjectField("正式会话定义")
            { objectType = typeof(ObjectDefinition), value = draft.Source, allowSceneObjects = false };
            source.SetEnabled(false); root.Add(source);
            root.Add(new HelpBox("编辑的是草稿。应用时只更新会话 SharedConfigs；首次使用会显式新增航程配置。飞行速度、舱内几何和经济规则仍使用既有来源。", HelpBoxMessageType.Info));
            var common = new VisualElement();
            common.style.flexDirection = FlexDirection.Row;
            AddProperty(common, "Config.Enabled", "启用流程");
            AddProperty(common, "Config.PreparationTimeoutSeconds", "准备超时（秒）");
            AddProperty(common, "Config.ArrivalTimeoutSeconds", "到达超时（秒）");
            root.Add(common);
            table = new MultiColumnListView { itemsSource = draft.Config.Planets, fixedItemHeight = 24,
                selectionType = SelectionType.Single, reorderable = false };
            table.style.height = 156;
            AddColumn("enabled", "启用", 58, p => p.Enabled ? "是" : "否");
            AddColumn("id", "稳定 ID", 180, p => p.Id);
            AddColumn("name", "星球名称", 150, p => p.DisplayName);
            AddColumn("seed", "种子（空白为随机）", 180, p => p.Seed);
            AddColumn("landing", "降落区", 150, p => $"{p.DockColumn}, {p.DockRow} / {p.LandingWidth} 格");
            table.selectionChanged += _ => ShowSelection();
            root.Add(table);
            var actions = new Toolbar();
            actions.Add(new ToolbarButton(AddPlanet) { text = "新增" });
            actions.Add(new ToolbarButton(DuplicatePlanet) { text = "复制" });
            actions.Add(new ToolbarButton(() => ChangeSelected(p => p.Enabled = !p.Enabled, "切换星球启用")) { text = "启用／停用" });
            actions.Add(new ToolbarButton(RemovePlanet) { text = "移除" });
            actions.Add(new ToolbarButton(() => MovePlanet(-1)) { text = "上移" });
            actions.Add(new ToolbarButton(() => MovePlanet(1)) { text = "下移" });
            actions.Add(new ToolbarButton(GeneratePreview) { text = "生成蓝图预览" });
            actions.Add(new ToolbarButton(() => SaveChanges()) { text = "应用／安装配置" });
            actions.Add(new ToolbarButton(() => DiscardChanges()) { text = "取消草稿" });
            root.Add(actions);
            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row; content.style.flexGrow = 1;
            details = new ScrollView(); details.style.width = Length.Percent(55); content.Add(details);
            preview?.Dispose(); preview = new ExpeditionPlanetPreview();
            preview.Surface.style.width = Length.Percent(45); content.Add(preview.Surface);
            root.Add(content);
            status = new Label(); status.style.whiteSpace = WhiteSpace.Normal; root.Add(status);
            if (draft.Config.Planets.Count > 0) table.selectedIndex = 0;
        }

        private void AddColumn(string name, string title, float width, Func<PlanetPreset, string> value)
        {
            table.columns.Add(new Column { name = name, title = title, width = width,
                makeCell = () => new Label(), bindCell = (element, index) =>
                ((Label)element).text = index >= 0 && index < draft.Config.Planets.Count && draft.Config.Planets[index] != null
                    ? value(draft.Config.Planets[index]) : "空行" });
        }

        private void ShowSelection()
        {
            if (details == null || serialized == null) return;
            preview?.Clear();
            if (status != null) status.text = "选中项已改变，旧预览已清除；可显式生成当前星球蓝图。";
            details.Unbind(); details.Clear(); serialized.Update();
            int index = table.selectedIndex;
            if (index < 0 || index >= draft.Config.Planets.Count) return;
            var item = serialized.FindProperty("Config.Planets").GetArrayElementAtIndex(index);
            details.Add(new HelpBox("ID 是网络／存档引用的稳定标识。修改 ID 会使旧引用失效，请在有意更换内容身份时修改。", HelpBoxMessageType.Info));
            string[] names = { "Id", "DisplayName", "Description", "Enabled", "Seed", "DockColumn", "DockRow", "LandingWidth",
                "ArrivalHeight", "HorizontalRange", "MaximumLift", "TransitSeconds", "TransitionKind", "StarCount", "StarSpeed", "SpaceColorHex", "SkyColorHex" };
            string[] labels = { "稳定 ID", "名称", "说明", "启用", "固定种子", "泊位列", "地表行", "平台宽度（格）",
                "到达高度", "水平范围", "最大升高", "最短过场（秒）", "过场策略", "星点数量", "星点速度", "太空颜色", "天空颜色" };
            for (int i = 0; i < names.Length; i++)
            {
                var field = new PropertyField(item.FindPropertyRelative(names[i]), labels[i]);
                field.Bind(serialized); details.Add(field);
            }
        }

        private void AddProperty(VisualElement target, string path, string label)
        {
            var field = new PropertyField(serialized.FindProperty(path), label);
            field.style.flexGrow = 1; field.Bind(serialized); target.Add(field);
        }

        private void AddPlanet()
        {
            Undo.RecordObject(draft, "新增星球");
            var planet = new PlanetPreset { Id = UniqueId("planet"), DisplayName = "新星球" };
            draft.Config.Planets.Add(planet); RowsChanged(draft.Config.Planets.Count - 1);
        }

        private void DuplicatePlanet()
        {
            int index = table.selectedIndex;
            if (index < 0 || index >= draft.Config.Planets.Count) return;
            Undo.RecordObject(draft, "复制星球");
            var planet = ExpeditionFlowDraft.Clone(draft.Config.Planets[index]);
            planet.Id = UniqueId("planet-copy"); planet.DisplayName += " 副本";
            draft.Config.Planets.Insert(index + 1, planet); RowsChanged(index + 1);
        }

        private void ChangeSelected(Action<PlanetPreset> change, string label)
        {
            int index = table.selectedIndex;
            if (index < 0 || index >= draft.Config.Planets.Count) return;
            Undo.RecordObject(draft, label); change(draft.Config.Planets[index]); RowsChanged(index);
        }

        private void RemovePlanet()
        {
            int index = table.selectedIndex;
            if (index < 0 || index >= draft.Config.Planets.Count) return;
            Undo.RecordObject(draft, "移除星球"); draft.Config.Planets.RemoveAt(index);
            RowsChanged(Math.Min(index, draft.Config.Planets.Count - 1));
        }

        private void MovePlanet(int direction)
        {
            int index = table.selectedIndex, next = index + direction;
            if (index < 0 || next < 0 || next >= draft.Config.Planets.Count) return;
            Undo.RecordObject(draft, "调整星球排序");
            var value = draft.Config.Planets[index]; draft.Config.Planets.RemoveAt(index);
            draft.Config.Planets.Insert(next, value); RowsChanged(next);
        }

        private string UniqueId(string prefix)
        {
            int suffix = 1;
            while (draft.Config.Planets.Any(p => p != null && p.Id == prefix + "-" + suffix)) suffix++;
            return prefix + "-" + suffix;
        }

        private void RowsChanged(int selection)
        {
            EditorUtility.SetDirty(draft); serialized.Update();
            table.itemsSource = draft.Config.Planets; table.RefreshItems(); table.selectedIndex = selection;
            ShowSelection(); DraftChanged();
        }

        private void DraftChanged()
        {
            if (draft == null || table == null || status == null) return;
            hasUnsavedChanges = draft.HasChanges; table.RefreshItems(); preview.Clear();
            try { draft.Config.Validate(); status.text = "配置格式有效；尚未运行验收。选中星球后可显式生成静态蓝图。"; }
            catch (Exception error) { status.text = "草稿待修正：" + error.Message; }
        }

        private void GeneratePreview()
        {
            int index = table.selectedIndex;
            if (index < 0 || index >= draft.Config.Planets.Count) return;
            try
            {
                status.text = "正在后台生成静态蓝图；编辑或关闭面板会取消本次预览。";
                preview.Generate(draft.Config.Planets[index].Freeze(), message => status.text = message);
            }
            catch (Exception error) { status.text = "预览失败：" + error.Message; }
        }

        public override void SaveChanges()
        {
            try { draft.Apply(); hasUnsavedChanges = false; status.text = "配置已应用并保存到正式会话定义；运行／联机验收仍待执行。"; }
            catch (Exception error) { status.text = "未应用：" + error.Message; }
        }

        public override void DiscardChanges()
        {
            try { Undo.ClearUndo(draft); draft.Load(draft.Source); RowsChanged(0); }
            catch (Exception error) { status.text = "重新读取失败：" + error.Message; }
        }

        private void OnUndoRedo()
        {
            if (draft != null && table != null) RowsChanged(table.selectedIndex);
        }

        private void OnPlayModeChanged(PlayModeStateChange _) =>
            rootVisualElement.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
    }
}
