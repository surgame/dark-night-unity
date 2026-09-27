using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.Editor;
using DarkNights.View;
using GameCore.UI.UGUI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Assets 外、显式 run_script 的按钮 Play 探针；只实例化隔离 PauseMenu，记录真实帧中无指针变化的文字刷新。
/// 不调用 Refresh、Update 或指针事件，不注册 YYGC 面板或会话，不修改冻结颜色与源 Prefab；临时实例在 finally 回收。
/// </summary>
public static class NativeButtonPlayProbe
{
    private static readonly Color Normal = new Color32(228, 229, 215, 255);
    private static readonly Color Disabled = new Color32(104, 123, 123, 255);
    private static readonly FieldInfo CachedInteractable = typeof(NativePanelTheme)
        .GetField("interactable", BindingFlags.Instance | BindingFlags.NonPublic);

    public static async Task<string> Run()
    {
        RequirePlay();
        string source = NativeUiSetup.Root + "/PauseMenu/PauseMenu.prefab";
        string original = File.ReadAllText(source);
        string output = Path.GetFullPath("../artifacts/space-planet-flow/native-button-play-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        var checks = new JArray();
        var report = new JObject { ["startedUtc"] = DateTime.UtcNow.ToString("O"), ["source"] = source,
            ["method"] = "isolated formal prefab, real Play frames, no Refresh/Update/pointer dispatch",
            ["nativeOsInteraction"] = false, ["checks"] = checks, ["report"] = output };
        GameObject root = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (prefab == null) throw new InvalidOperationException("PauseMenu source missing.");
            root = UnityEngine.Object.Instantiate(prefab);
            root.name = "NativeButton Play Probe (temporary)";
            // Authoring Canvas 会按正式生命周期剥离；若仍有 Raycaster，也仅隔离本临时实例的指针入口。
            foreach (var raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
            var view = root.GetComponent<UGUIView>();
            if (view == null) throw new InvalidOperationException("Formal UGUIView binding missing.");
            Button button = view.Get<Button>("ControlMode");
            Text label = view.Get<Text>("ControlModeLabel");
            var theme = button.GetComponent<NativePanelTheme>();
            if (theme == null || label == null || !theme.isActiveAndEnabled)
                throw new InvalidOperationException("Formal bound theme/label is not active.");
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            report["initial"] = Sample(button, label, theme, group);
            await Observe(checks, "initial_normal", button, label, theme, group, Normal);
            button.interactable = false;
            await Observe(checks, "button_false_without_pointer", button, label, theme, group, Disabled);
            button.interactable = true;
            await Observe(checks, "button_true_without_pointer", button, label, theme, group, Normal);
            group.interactable = false;
            await Observe(checks, "canvas_group_false_without_pointer", button, label, theme, group, Disabled);
            group.interactable = true;
            await Observe(checks, "canvas_group_true_without_pointer", button, label, theme, group, Normal);
            report["passed"] = checks.Count == 5 && checks.All(c => (bool)c["passed"]);
        }
        catch (Exception error) { report["passed"] = false; report["error"] = error.ToString(); }
        finally
        {
            if (root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            double end = EditorApplication.timeSinceStartup + 2;
            while (root != null && Application.isPlaying && EditorApplication.timeSinceStartup < end) await Task.Delay(10);
            report["temporaryInstanceDestroyed"] = root == null;
            report["sourceUnchanged"] = File.ReadAllText(source) == original;
            report["passed"] = (bool?)report["passed"] == true && root == null && (bool)report["sourceUnchanged"];
            report["finishedUtc"] = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, report.ToString());
        }
        return report.ToString();
    }

    private static async Task Observe(JArray checks, string name, Button button, Text label,
        NativePanelTheme theme, CanvasGroup group, Color expected)
    {
        int frame = Time.frameCount;
        double start = EditorApplication.timeSinceStartup;
        var before = Sample(button, label, theme, group);
        while (EditorApplication.timeSinceStartup - start < 2)
        {
            RequirePlay();
            if (Time.frameCount >= frame + 2 && Same(label.color, expected)) break;
            await Task.Delay(10);
        }
        checks.Add(new JObject { ["name"] = name, ["expectedColor"] = ColorData(expected),
            ["passed"] = Time.frameCount >= frame + 2 && Same(label.color, expected),
            ["elapsedSeconds"] = EditorApplication.timeSinceStartup - start, ["framesAdvanced"] = Time.frameCount - frame,
            ["before"] = before, ["after"] = Sample(button, label, theme, group) });
    }

    private static JObject Sample(Button button, Text label, NativePanelTheme theme, CanvasGroup group) => new JObject
    {
        ["frame"] = Time.frameCount, ["playingObject"] = Application.IsPlaying(button.gameObject),
        ["buttonInteractable"] = button.interactable, ["effectiveInteractable"] = button.IsInteractable(),
        ["groupInteractable"] = group.interactable, ["themeEnabled"] = theme.isActiveAndEnabled,
        ["themeCachedInteractable"] = CachedInteractable == null ? null : JToken.FromObject(CachedInteractable.GetValue(theme)),
        ["labelColor"] = ColorData(label.color), ["active"] = button.gameObject.activeInHierarchy
    };

    private static JArray ColorData(Color value) => new JArray(value.r, value.g, value.b, value.a);

    private static bool Same(Color actual, Color expected) => Math.Abs(actual.r - expected.r) < .0001f &&
        Math.Abs(actual.g - expected.g) < .0001f && Math.Abs(actual.b - expected.b) < .0001f && Math.Abs(actual.a - expected.a) < .0001f;

    private static void RequirePlay()
    {
        if (!Application.isPlaying || EditorApplication.isPaused)
            throw new InvalidOperationException("Probe requires real, unpaused Play; Editor scheduling is not substituted.");
    }
}
