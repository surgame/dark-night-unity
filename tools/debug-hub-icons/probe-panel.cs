// 在正式 Host 已 Ready 且物体页可见时运行；仅操作 UITK 选择／筛选，不注入权威状态。
var doc = UnityEngine.Object.FindAnyObjectByType<GameCore.Debugging.RuntimeDebugHub>().GetComponent<UnityEngine.UIElements.UIDocument>();
var root = doc.rootVisualElement;
var grid = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, "objectGrid");
var search = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextField>(root, "search");
var checks = new System.Collections.Generic.List<string>();
void Check(bool value, string name)
{
    if (!value) throw new System.InvalidOperationException(name);
    checks.Add(name);
}
void Click(UnityEngine.UIElements.Button button)
{
    typeof(UnityEngine.UIElements.Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Invoke(button.clickable, new object[] { null });
}
string SelectedKey() => UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, "selectedKey").text;
search.value = "";
Check(grid.childCount == 30, "30 physical definitions");
Check(!grid.Children().Any(v => ((GameCore.Objects.Definition.ObjectDefinition)v.userData).Key == "effect.command"), "command excluded");
Check(!grid.Children().Any(v => ((GameCore.Objects.Definition.ObjectDefinition)v.userData).Key.StartsWith("ui.") ||
    ((GameCore.Objects.Definition.ObjectDefinition)v.userData).Key.StartsWith("ship-service.")), "UI and services excluded");
Check(!UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.ListView>(root).ToList().Any(), "no row selection container");
Check(UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.ScrollView>(root).ToList().Count == 1, "one Hub scroll view");
Check(grid.Children().Count(v => UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Image>(v) != null) == 30, "30 definitions have icons");
Check(grid.Children().All(v => !UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Label>(v).ToList().Any()), "icon only tiles without placeholders");
search.value = "item.pickaxe";
Check(grid.childCount == 1, "Key search finds pickaxe");
Check(SelectedKey() == "", "hidden selection cleared");
Click((UnityEngine.UIElements.Button)grid[0]);
Check(SelectedKey() == "item.pickaxe", "filtered button selects current definition");
Check(grid.Children().Count(v => v.ClassListContains("dn-objects-selected")) == 1, "only one selected tile");
Check(grid[0].tooltip.Contains("item.pickaxe") && grid[0].tooltip.Contains("装备"), "tooltip carries identity and capability");
search.value = "矿镐";
Check(grid.childCount == 1 && SelectedKey() == "item.pickaxe", "name search keeps matching selection");
search.value = "  ITEM.PICKAXE  ";
Check(grid.childCount == 1, "trimmed case insensitive search");
search.value = "no_such_object_20261009";
Check(grid.childCount == 0 && SelectedKey() == "", "empty result has no hidden target");
Check(!UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "addObject").enabledSelf &&
    !UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "removeObject").enabledSelf, "empty result actions disabled");
search.value = "";
Click((UnityEngine.UIElements.Button)grid.Children().Single(v => ((GameCore.Objects.Definition.ObjectDefinition)v.userData).Key == "item.pickaxe"));
Check(grid.childCount == 30 && SelectedKey() == "item.pickaxe", "clearing search restores catalog with correct target");
Check(root.panel.visualTree.ClassListContains("dn-debug-hub"), "game theme attached to Hub panel");
var scroller = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.ScrollView>(root, "contentHost").verticalScroller;
Check(scroller.resolvedStyle.width < 10, "thin vertical scroller");
return new { passed = checks.Count, checks, selected = SelectedKey(), missingIcons = 0 };
