// 读取已经完成一帧布局的真实 Hub；调用方只调整临时窗口宽度，检查固定格子及单一滚动。
var doc = UnityEngine.Object.FindAnyObjectByType<GameCore.Debugging.RuntimeDebugHub>().GetComponent<UnityEngine.UIElements.UIDocument>();
var root = doc.rootVisualElement;
var grid = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, "objectGrid");
var tiles = grid.Children().ToArray();
if (tiles.Length != 30) throw new System.InvalidOperationException("Unexpected filtered catalog");
if (tiles.Any(v => System.Math.Abs(v.resolvedStyle.width - 48) > 1 || System.Math.Abs(v.resolvedStyle.height - 48) > 1))
    throw new System.InvalidOperationException("Tile size changes with width");
if (tiles.Any(v => v.layout.x < -1 || v.layout.xMax > grid.layout.width + 1))
    throw new System.InvalidOperationException("Grid overflows horizontally");
if (tiles.Count(v => v.ClassListContains("dn-objects-selected")) != 1)
    throw new System.InvalidOperationException("Selection leaked beyond one tile");
var scroll = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.ScrollView>(root, "contentHost");
var dragger = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(scroll.verticalScroller, "unity-dragger");
if (dragger.layout.width > 8 || scroll.verticalScroller.layout.width > 10)
    throw new System.InvalidOperationException("Scroller internal slider kept default width");
return new
{
    passed = 4, width = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, "window").resolvedStyle.width,
    gridWidth = grid.layout.width, columns = tiles.Count(v => System.Math.Abs(v.layout.y - tiles[0].layout.y) < 1),
    tile = tiles[0].layout.ToString(), draggerWidth = dragger.layout.width,
    selected = ((GameCore.Objects.Definition.ObjectDefinition)tiles.Single(v => v.ClassListContains("dn-objects-selected")).userData).Key
};
