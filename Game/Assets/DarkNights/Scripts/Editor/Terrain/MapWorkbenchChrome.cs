using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>单地图工作台的文件命令与可见模块导航；切换模块只修改导航，不修改作者输入。</summary>
    internal sealed class MapWorkbenchChrome
    {
        private readonly MapWorkbenchWindow owner;
        private readonly VisualElement root;
        private readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        private readonly MapWorkbenchDisplayTools display;

        internal MapWorkbenchChrome(MapWorkbenchWindow owner, VisualElement root)
        {
            this.owner = owner; this.root = root;
            Group("地图配方");
            Tab("recipe", "算法与种子");
            Group("空间生成");
            Tab("surface", "地表轮廓");
            Tab("rooms", "地下洞室");
            Tab("roads", "道路连接");
            Tab("entrance", "入口形状");
            Group("地图内容");
            Tab("minerals", "矿物分布");
            Tab("nest", "侧巢");
            Tab("collapse", "塌方设计");
            Group("地图表现");
            Tab("environment", "天空与远景");
            Tab("appearance", "岩壁与后壁");
            Tab("scenery", "植被与装饰");
            Group("文件与输出");
            Tab("release", "地图文件");
            var tools = new Foldout { text = "实验工具与暂存设置", value = false };
            tools.AddToClassList("map-module-tools");
            root.Q("taskNavigation").Add(tools);
            Tab("paint", "临时拆填 · 不保存", tools);
            Tab("study", "矿巢实验 · 仅本机", tools);
            Tab("planet", "保留星球参数 · 航程暂停", tools);
            root.Q<Button>("saveDraft").clicked += () => owner.Execute(owner.Commands.SaveDraft);
            root.Q<Button>("updateCandidate").clicked += () => owner.Execute(owner.Commands.Reseed);
            var file = Menu("文件", "fileMenu");
            ActionItem(file, "新建配置", () => MapWorkbenchFiles.New(owner));
            ActionItem(file, "打开配置…", () => MapWorkbenchFiles.OpenPicker(owner));
            ActionItem(file, "保存", owner.Commands.SaveDraft);
            ActionItem(file, "另存为…", () => TerrainDocumentStore.SaveAs(owner));
            ActionItem(file, "用当前种子重新生成", owner.Commands.Generate);
            ActionItem(file, "配置与预设…", () => owner.SelectPage("release"));
            file.menu.AppendAction("恢复上次保存", _ => owner.Execute(() =>
            { if (MapWorkbenchFiles.CanReplace(owner)) owner.DiscardChanges(); }),
                _ => owner.State.Document != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            file.menu.AppendSeparator();
            ActionItem(file, "将当前地图用于新局", owner.Commands.Install);
            ActionItem(file, "检查配置", owner.Commands.Check);
            ActionItem(file, "打开 Bootstrap", owner.Commands.OpenBootstrap);
            display = new MapWorkbenchDisplayTools(owner, root);
            root.Q<Button>("undo").clicked += () => Undo.PerformUndo();
            root.Q<Button>("redo").clicked += () => Undo.PerformRedo();
            var compare = root.Q<Button>("compare");
            compare.RegisterCallback<PointerDownEvent>(e =>
            { compare.CapturePointer(e.pointerId); owner.Preview.Compare = true; });
            compare.RegisterCallback<PointerUpEvent>(e =>
            { owner.Preview.Compare = false; compare.ReleasePointer(e.pointerId); });
            compare.RegisterCallback<PointerCaptureOutEvent>(_ => owner.Preview.Compare = false);
        }

        private void Advanced(string page)
        {
            owner.State.AdvancedPage = page; owner.State.ShowStudy = page == "study"; owner.SelectPage("advanced");
        }

        private ToolbarMenu Menu(string text, string name, string parent = "headerActions")
        {
            var menu = new ToolbarMenu { text = text, name = name };
            menu.AddToClassList("map-more"); root.Q(parent).Add(menu); return menu;
        }

        private void ActionItem(ToolbarMenu menu, string label, Action action)
        { menu.menu.AppendAction(label, _ => owner.Execute(action)); }

        private void Group(string title)
        {
            var label = new Label(title); label.AddToClassList("map-module-group");
            root.Q("taskNavigation").Add(label);
        }

        private void Tab(string page, string title, VisualElement parent = null)
        {
            var button = new Button(() =>
            {
                if (page == "nest" || page == "collapse" || page == "scenery")
                { owner.State.Extra = page; owner.SelectPage("extras"); }
                else if (page == "planet" || page == "paint" || page == "study") Advanced(page);
                else owner.SelectPage(page);
            }) { text = title, name = "module-" + page };
            button.AddToClassList("map-module");
            (parent ?? root.Q("taskNavigation")).Add(button); tabs.Add(page, button);
        }

        internal void Refresh()
        {
            string page = owner.State.Page == "extras" ? owner.State.Extra :
                owner.State.Page == "advanced" ? owner.State.AdvancedPage : owner.State.Page;
            foreach (var pair in tabs) pair.Value.EnableInClassList("map-selected", pair.Key == page);
            root.Q<Button>("compare").SetEnabled(owner.Preview.HasPrevious);
            RefreshDocument();
            root.Q("fileMenu").SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            owner.State.Grid = owner.Preview.Canvas.ShowGrid;
            display.Refresh();
        }

        private void RefreshDocument()
        {
            var state = owner.State;
            var label = root.Q<Label>("mapDocumentName");
            if (state.ViewedMap != null)
            {
                string path = AssetDatabase.GetAssetPath(state.ViewedMap);
                label.text = "当前地图：" + System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) +
                    " / " + state.ViewedMap.name;
                label.tooltip = path + "；只读查看地图结果，原编辑地图及未保存输入仍保留。";
                return;
            }
            label.text = "当前地图：" + (state.Document != null ? state.Document.name : "未命名地图");
            label.tooltip = state.Document != null ? AssetDatabase.GetAssetPath(state.Document) :
                "尚未保存为地图文件；首次保存时选择文件位置。";
        }
    }
}
