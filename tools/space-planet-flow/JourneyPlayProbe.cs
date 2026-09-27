using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Interactions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 显式 run_script 使用的真实 Bootstrap Play 交互探针；只读冻结投影，调用现有 UGUI 和 Input System。
/// 不创建世界、人物、并列 UI 或运行组件；临时输入与 Editor 输入选项总在 finally 恢复，证据只写本批独立目录。
/// confirm-flight 连续确认、驾驶、自动落地和步行出舱；每步验证前置条件，失败保留已完成步骤并停止。
/// </summary>
public static class JourneyPlayProbe
{
    private const string FolderKey = "DarkNights.JourneyPlayProbe.Output";

    public static async Task<string> Run(string operation, string value, int milliseconds)
    {
        if (!Application.isPlaying || EditorApplication.isPaused)
            throw new InvalidOperationException("先进入真实 Bootstrap Play，且不要暂停 Editor。");
        var network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
        if (network == null) throw new InvalidOperationException("真实 SessionNetwork 尚未装配。");
        if (milliseconds < 0 || milliseconds > 15000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        string folder = SessionState.GetString(FolderKey, "");
        if (operation == "begin" || string.IsNullOrEmpty(folder))
        {
            folder = Path.GetFullPath("../artifacts/space-planet-flow/play-ui-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(folder); SessionState.SetString(FolderKey, folder);
        }
        var report = new JObject { ["operation"] = operation, ["value"] = value,
            ["startedUtc"] = DateTime.UtcNow.ToString("O"), ["folder"] = folder,
            ["inputSource"] = "Unity UGUI/InputSystem event chain; no native OS click claim", ["before"] = Sample(network) };
        try
        {
            if (operation == "button") { Button(value).onClick.Invoke(); await Task.Delay(250); }
            else if (operation == "keys" || operation == "keys-capture")
                report["input"] = await Keys(network, value, milliseconds, operation == "keys-capture" ? folder : null);
            else if (operation == "capture") report["capture"] = await Capture(network, folder, value);
            else if (operation == "confirm-watch") report["observed"] = await ConfirmWatch(network, folder);
            else if (operation == "confirm-flight")
            { var steps = new JArray(); report["flight"] = steps; await ConfirmFlight(network, folder, steps); }
            else if (operation == "wait") await Task.Delay(milliseconds);
            else if (operation != "begin" && operation != "status") throw new ArgumentException("Unknown operation: " + operation);
            report["completed"] = true;
        }
        catch (Exception error) { report["completed"] = false; report["failed"] = error.Message; report["error"] = error.ToString(); }
        finally
        {
            report["after"] = Sample(network); report["finishedUtc"] = DateTime.UtcNow.ToString("O");
            string path = Path.Combine(folder, Stamp() + "-" + Safe(operation) + ".json");
            File.WriteAllText(path, report.ToString()); report["report"] = path;
        }
        return report.ToString();
    }

    private static JObject Sample(SessionNetwork network)
    {
        var frame = network.Client.Replica.Current;
        var world = frame?.World;
        var expedition = world?.Expedition;
        var hero = world?.Actors.FirstOrDefault(a => a.ControllerSlot == network.Client.PlayerSlot);
        var input = UnityEngine.Object.FindAnyObjectByType<GameInputActions>();
        var sessions = YYInteractionSessionService.Instance;
        return new JObject
        {
            ["utc"] = DateTime.UtcNow.ToString("O"), ["playing"] = Application.isPlaying,
            ["applicationFocused"] = Application.isFocused, ["focusedWindow"] = EditorWindow.focusedWindow?.GetType().FullName,
            ["width"] = Screen.width, ["height"] = Screen.height, ["ready"] = network.Client.Ready,
            ["status"] = network.Status, ["epoch"] = frame?.Epoch, ["paused"] = frame?.Paused,
            ["phase"] = expedition?.Journey?.Phase.ToString(), ["journey"] = expedition?.Journey == null ? null : JObject.FromObject(expedition.Journey),
            ["ship"] = expedition?.Ship == null ? null : JObject.FromObject(expedition.Ship),
            ["shipX"] = world?.Buildings.FirstOrDefault(b => b.Id == expedition?.Ship?.Id)?.X,
            ["shipHeight"] = expedition?.Devices.FirstOrDefault(d => d.Id == expedition.Ship?.Id)?.Height,
            ["hero"] = hero == null ? null : JObject.FromObject(hero),
            ["crew"] = expedition == null ? null : JArray.FromObject(expedition.Crew),
            ["terrainDataReady"] = network.Terrain?.DataReady, ["terrainVisible"] = network.Terrain?.PresentationReady,
            ["terrainWorldId"] = network.Terrain?.Replica?.World.WorldId.ToString(), ["terrainEpoch"] = network.Terrain?.Epoch,
            ["interactionSessions"] = sessions.ActiveSessions.Count,
            ["gameplayBlocked"] = sessions.IsBlocked(YYInteractionBlockFlags.GameplayActions),
            ["cameraBlocked"] = sessions.IsBlocked(YYInteractionBlockFlags.CameraInput),
            ["canReadMove"] = input != null && input.Move != null && input.CanRead(input.Move),
            ["moveValue"] = input?.Move?.ReadValue<float>(),
            ["buttons"] = new JArray(Buttons().Select(b => new JObject
            { ["path"] = NodePath(b.transform), ["label"] = Label(b), ["interactable"] = b.interactable }))
        };
    }

    private static Button[] Buttons() => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .Where(b => b.gameObject.activeInHierarchy).ToArray();

    private static string Label(Button button) => string.Join(" ", button.GetComponentsInChildren<Text>(false).Select(t => t.text));

    private static string NodePath(Transform node) => node.parent == null ? node.name : NodePath(node.parent) + "/" + node.name;

    private static Button Button(string selector)
    {
        var found = Buttons().Where(b => NodePath(b.transform) == selector || Label(b) == selector).ToArray();
        if (found.Length != 1) throw new InvalidOperationException("需要唯一可见按钮，先 status 查询 label/path: " + selector);
        if (!found[0].interactable) throw new InvalidOperationException("按钮当前不可交互: " + selector);
        return found[0];
    }

    private static async Task<JObject> Keys(SessionNetwork network, string names, int milliseconds, string captureFolder)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.enabled) throw new InvalidOperationException("没有现有可用 Keyboard 设备。");
        if (keyboard.anyKey.isPressed) throw new InvalidOperationException("已有按键按下，暂不覆盖用户输入。");
        var keys = names.Split(new[] { ',', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(name => (Key)Enum.Parse(typeof(Key), name, true)).ToArray();
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        var result = new JObject { ["before"] = Sample(network), ["keys"] = JArray.FromObject(keys.Select(k => k.ToString())) };
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            await FocusGame();
            if (!Application.isFocused) throw new InvalidOperationException("GameView 未获应用焦点；不会绕过正式输入门控。");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            double releaseAt = Time.realtimeSinceStartupAsDouble + milliseconds / 1000.0;
            if (captureFolder == null) await Task.Delay(milliseconds);
            else
            {
                await Task.Delay(Math.Min(250, milliseconds));
                result["capture"] = await Capture(network, captureFolder, "keys-" + names);
                await Task.Delay(Math.Max(0, (int)((releaseAt - Time.realtimeSinceStartupAsDouble) * 1000)));
            }
            result["held"] = Sample(network);
        }
        finally
        {
            if (keyboard.added) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(180); }
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
        }
        result["released"] = Sample(network); return result;
    }

    private static async Task<JArray> ConfirmWatch(SessionNetwork network, string folder)
    {
        var result = new JArray();
        await FocusGame();
        Button("确认目的地并驾驶").onClick.Invoke();
        bool transit = false, descent = false;
        double end = Time.realtimeSinceStartupAsDouble + 45;
        while (Time.realtimeSinceStartupAsDouble < end)
        {
            string phase = network.Client.Replica.Current?.World.Expedition?.Journey?.Phase.ToString();
            if (phase == "Transit" && !transit)
            { result.Add(await Capture(network, folder, "03-star-transit")); transit = true; }
            if (phase == "Descent" && !descent && network.Client.Ready)
            { result.Add(await Capture(network, folder, "04-planet-airborne")); descent = true; break; }
            await Task.Delay(30);
        }
        result.Add(new JObject { ["transitObserved"] = transit, ["descentObserved"] = descent,
            ["note"] = "未观察或截图期间阶段改变的瞬态，不作为相应画面通过证据。" });
        return result;
    }

    private static async Task ConfirmFlight(SessionNetwork network, string folder, JArray steps)
    {
        float Number(JToken state, string key) => (float?)state?[key] ?? float.NaN;
        bool Air(JToken state) => (bool?)state["ready"] == true && (string)state["phase"] == "Descent" &&
            (bool?)state["paused"] == false && (bool?)state["terrainVisible"] == true && state["hero"] is JObject &&
            (int?)state["ship"]?["PilotId"] == (int?)state["hero"]?["Id"];
        void Check(string name, bool passed, JToken evidence)
        {
            steps.Add(new JObject { ["step"] = name, ["passed"] = passed, ["observed"] = evidence,
                ["failed"] = passed ? null : name + "未满足，停止后续操作。" });
            if (!passed) throw new InvalidOperationException(name + "未满足，停止后续操作。");
        }
        bool Open(JToken state) => (bool?)state["ready"] == true && (string)state["phase"] == "Landed" &&
            (int?)state["ship"]?["PilotId"] == 0 && (int?)state["ship"]?["Phase"] == 0 &&
            (double?)state["ship"]?["DoorClock"] == 0;
        var observed = await ConfirmWatch(network, folder);
        var initial = Sample(network);
        Check("UI确认、过场及就绪半空", (bool?)observed.Last?["transitObserved"] == true &&
            (bool?)observed.Last?["descentObserved"] == true && Air(initial), observed);
        int heroId = (int)initial["hero"]["Id"];
        var down = await Keys(network, "S", 1500, folder);
        Check("S加速下降", Air(down["held"]) && Number(down["held"]["ship"], "VelocityY") <
            Number(down["before"]["ship"], "VelocityY") && Number(down["held"], "shipHeight") <
            Number(down["before"], "shipHeight") - 2, down);
        Check("上升前置", Air(Sample(network)), Sample(network));
        var up = await Keys(network, "Space", 1500, folder);
        Check("空格上升", Air(up["held"]) && Number(up["held"]["ship"], "VelocityY") > 0 &&
            Number(up["held"], "shipHeight") > Number(up["before"], "shipHeight") + 2, up);
        Check("平移前置", Air(Sample(network)), Sample(network));
        var right = await Keys(network, "D", 1000, null);
        Check("D向右平移", Air(right["held"]) && Number(right["held"]["ship"], "VelocityX") > 0 &&
            Number(right["held"], "shipX") > Number(right["before"], "shipX") + 2, right);
        await Task.Delay(400);
        Check("返向前置", Air(Sample(network)), Sample(network));
        var left = await Keys(network, "A", 1000, null);
        Check("A向左返回", Air(left["held"]) && Number(left["held"]["ship"], "VelocityX") < 0 &&
            Number(left["held"], "shipX") < Number(left["before"], "shipX") - 2, left);
        await Task.Delay(400);
        var aligned = Sample(network);
        for (int i = 0; i < 12 && Math.Abs(Number(aligned, "shipX") - Number(aligned["ship"], "DockX")) > 4; i++)
        {
            Check("泊位微调前置", Air(aligned), aligned);
            string key = Number(aligned, "shipX") > Number(aligned["ship"], "DockX") ? "A" : "D";
            var correction = await Keys(network, key, 100, null); await Task.Delay(450);
            aligned = Sample(network);
            Check("有限泊位微调 " + (i + 1), Air(aligned), correction);
        }
        Check("回到泊位中心", Air(aligned) && Math.Abs(Number(aligned, "shipX") - Number(aligned["ship"], "DockX")) <= 4,
            aligned);
        await Task.Delay(1000);
        var idle = Sample(network);
        Check("松手缓降", Air(idle) && Number(idle["ship"], "VelocityY") < 0 &&
            Number(idle, "shipHeight") < Number(aligned, "shipHeight") - 1, idle);
        steps.Add(new JObject { ["step"] = "松手缓降画面", ["passed"] = true,
            ["capture"] = await Capture(network, folder, "07-idle-descent") });
        double landingDeadline = Time.realtimeSinceStartupAsDouble + 45;
        var landed = Sample(network);
        while (!Open(landed) && Time.realtimeSinceStartupAsDouble < landingDeadline)
        {
            bool waiting = (bool?)landed["ready"] == true && (bool?)landed["paused"] == false &&
                ((string)landed["phase"] == "Descent" || (string)landed["phase"] == "Landed");
            if (!waiting) Check("等待自动着陆前置", false, landed);
            await Task.Delay(250); landed = Sample(network);
        }
        Check("45秒内自动着陆、离座和开舱", Open(landed), landed);
        steps.Add(new JObject { ["step"] = "自动着陆开舱画面", ["passed"] = true,
            ["capture"] = await Capture(network, folder, "08-auto-landed-open") });
        var exit = await Keys(network, "A", 10000, null);
        var final = Sample(network);
        var crew = final["crew"]?.FirstOrDefault(c => (int?)c["Id"] == heroId);
        Check("原驾驶者步行下船", Open(final) && crew != null && (bool?)crew["Boarded"] == false, exit);
        steps.Add(new JObject { ["step"] = "步行下船画面", ["passed"] = true,
            ["capture"] = await Capture(network, folder, "09-walked-off-ship") });
    }

    private static async Task<JObject> Capture(SessionNetwork network, string folder, string label)
    {
        await FocusGame();
        string path = Path.Combine(folder, Stamp() + "-" + Safe(label) + ".png");
        var before = Sample(network);
        ScreenCapture.CaptureScreenshot(path);
        double deadline = Time.realtimeSinceStartupAsDouble + 8;
        while (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            if (!Application.isPlaying || Time.realtimeSinceStartupAsDouble > deadline)
                throw new TimeoutException("GameView 截图未输出: " + path);
            await Task.Delay(50);
        }
        var after = Sample(network);
        return new JObject { ["path"] = path, ["method"] = "ScreenCapture.CaptureScreenshot actual GameView",
            ["before"] = before, ["after"] = after,
            ["phaseStableDuringCapture"] = JToken.DeepEquals(before["phase"], after["phase"]),
            ["nativeOsInteraction"] = false };
    }

    private static async Task FocusGame()
    {
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        await Task.Delay(100);
    }

    private static string Stamp() => DateTime.Now.ToString("HHmmss-fff");
    private static string Safe(string value) => string.Concat((value ?? "capture").Take(60)
        .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-'));
}
