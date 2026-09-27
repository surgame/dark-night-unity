using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>实际 UI Toolkit 窗口的有限交互批次；只改临时草稿，核对列表绑定、行操作和缩放，始终放弃草稿并关闭本次窗口。</summary>
public static class JourneyPanelProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static async Task<string> Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            Resources.FindObjectsOfTypeAll<ExpeditionFlowWindow>().Length != 0)
            throw new InvalidOperationException("Editor occupied or author window already open.");
        string folder = Path.GetFullPath("../artifacts/space-planet-flow/panel-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(folder);
        var checks = new JArray();
        var report = new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["checks"] = checks,
            ["scope"] = "actual UI Toolkit binding/callback/layout; no native mouse, OS DPI or pixel screenshot claim" };
        ExpeditionFlowWindow window = null;
        string sourcePath = null, author = null;
        void Check(string name, bool passed)
        {
            checks.Add(new JObject { ["name"] = name, ["passed"] = passed });
            if (!passed) throw new InvalidOperationException(name);
        }
        try
        {
            Check("menu opens", EditorApplication.ExecuteMenuItem("Dark Nights/配置/星球与航程"));
            window = Resources.FindObjectsOfTypeAll<ExpeditionFlowWindow>().Single();
            window.Show(); await Task.Delay(350);
            var draft = (ExpeditionFlowDraft)typeof(ExpeditionFlowWindow).GetField("draft", Private).GetValue(window);
            var source = (UnityEngine.Object)typeof(ExpeditionFlowDraft).GetField("source", Private).GetValue(draft);
            sourcePath = AssetDatabase.GetAssetPath(source); author = File.ReadAllText(sourcePath);
            report["sourcePath"] = sourcePath; report["sourceGuid"] = AssetDatabase.AssetPathToGUID(sourcePath);
            Check("formal author source", sourcePath == "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            string initial = JsonUtility.ToJson(draft.Config); int count = draft.Config.Planets.Count;
            MultiColumnListView Table() => window.rootVisualElement.Q<MultiColumnListView>();
            async Task Click(string label)
            {
                Undo.IncrementCurrentGroup();
                var button = window.rootVisualElement.Query<Button>().ToList().Single(b => b.text == label);
                if (!button.enabledInHierarchy) throw new InvalidOperationException("Disabled: " + label);
                typeof(Clickable).GetMethod("Invoke", Private).Invoke(button.clickable, new object[] { null });
                Undo.FlushUndoRecordObjects(); await Task.Delay(180);
            }
            Check("five author columns", Table().columns.Count == 5);
            await Click("新增"); Check("add selects new row", draft.Config.Planets.Count == count + 1 && Table().selectedIndex == count);
            await Click("复制"); Check("duplicate has new identity", draft.Config.Planets.Count == count + 2 &&
                draft.Config.Planets.Select(p => p.Id).Distinct().Count() == count + 2);
            string copied = draft.Config.Planets[Table().selectedIndex].Id;
            await Click("启用／停用"); Check("disable selected only", !draft.Config.Planets[Table().selectedIndex].Enabled);
            await Click("启用／停用"); Check("enable selected", draft.Config.Planets[Table().selectedIndex].Enabled);
            await Click("上移"); Check("move up keeps selection identity", Table().selectedIndex == count && draft.Config.Planets[count].Id == copied);
            await Click("下移"); Check("move down keeps selection identity", Table().selectedIndex == count + 1 && draft.Config.Planets[count + 1].Id == copied);
            string reordered = JsonUtility.ToJson(draft.Config);
            Undo.PerformUndo(); await Task.Delay(160); Undo.PerformRedo(); await Task.Delay(160);
            Check("undo redo restores rows", JsonUtility.ToJson(draft.Config) == reordered);
            foreach (int row in new[] { 0, count, count + 1, 0, count + 1 })
            {
                Table().selectedIndex = row; await Task.Delay(180);
                var id = window.rootVisualElement.Query<TextField>().ToList().Single(f => f.label == "稳定 ID");
                Check("detail matches selected row " + row, id.value == draft.Config.Planets[row].Id);
            }
            foreach (var size in new[] { new Vector2(760, 620), new Vector2(1280, 800) })
            {
                window.position = new Rect(window.position.position, size); await Task.Delay(220);
                Check("table layout at " + size, Table().worldBound.width > 0 && Table().worldBound.height > 100 &&
                    window.rootVisualElement.worldBound.width >= 740);
            }
            window.CreateGUI(); await Task.Delay(200);
            Check("rebuild retains reorder and draft", JsonUtility.ToJson(draft.Config) == reordered);
            Table().selectedIndex = count + 1; await Click("移除");
            Check("remove selected draft", draft.Config.Planets.Count == count + 1 && !draft.Config.Planets.Any(p => p.Id == copied));
            Check("formal author untouched before discard", author == File.ReadAllText(sourcePath));
            await Click("取消草稿"); Check("discard restores author", JsonUtility.ToJson(draft.Config) == initial && !window.hasUnsavedChanges);
            report["success"] = true;
        }
        catch (Exception error) { report["success"] = false; report["error"] = error.ToString(); }
        finally
        {
            if (window != null) { window.DiscardChanges(); window.Close(); }
            report["authorUnchanged"] = sourcePath != null && author == File.ReadAllText(sourcePath);
            report["finishedUtc"] = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.Combine(folder, "result.json"), report.ToString());
        }
        report["folder"] = folder; return report.ToString();
    }
}
