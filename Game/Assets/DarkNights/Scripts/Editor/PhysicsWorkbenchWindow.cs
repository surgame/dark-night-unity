using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Physics;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 现有工作台登记的物理候选与有界诊断窗口；打开不启动Play、不录制、不覆盖原生作者资源。
    /// 导出只消费冻结记录，UI按250ms小状态更新，不监听或渲染自己的Unity日志。
    /// </summary>
    public sealed class PhysicsWorkbenchWindow : EditorWindow
    {
        private Label status, output;
        private Button start, stop;
        private IVisualElementScheduledItem refresh;
        private PhysicsTrace previous;
        public static void Open()
        {
            var window = GetWindow<PhysicsWorkbenchWindow>("合作物理");
            window.minSize = new Vector2(480, 340); window.Show();
        }

        public void CreateGUI()
        {
            refresh?.Pause(); rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12;
            var title = new Label("合作物理候选");
            title.style.fontSize = 18; rootVisualElement.Add(title);
            var instructions = new Label("A/D 移动，空格跳跃，靠近箱子即可推动。\n按 E 用绳索连接附近角色或箱子，再次按 E 解除。\n打开示例场景后，点击 Unity 顶部 ▶ 试玩。运动参数可在物理配置中调整。");
            instructions.style.whiteSpace = WhiteSpace.Normal; rootVisualElement.Add(instructions);
            AddButton("打开物理示例场景", () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止运行，再打开场景。");
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                SessionState.EraseString(Entry.MapModule.MapModuleEntry.SelectionKey);
                SessionState.EraseString(Entry.MapModule.MapModuleEntry.VersionKey);
                EditorSceneManager.OpenScene(PhysicsStudySetup.ScenePath);
            });
            AddButton("定位物理配置", () => Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(PhysicsStudySetup.ProfilePath));
            status = new Label(); status.style.whiteSpace = WhiteSpace.Normal; rootVisualElement.Add(status);
            start = AddButton("开始录制（最多600步）", () =>
            {
                var session = Host() ?? throw new InvalidOperationException("请先以房主身份运行物理示例，再开始录制。");
                session.ObjectWorld.Physics.StartRecording(); previous = session.ObjectWorld.Physics.Trace;
            });
            stop = AddButton("停止录制", () => { var trace = Active(); if (trace != null) trace.Recording = false; });
            AddButton("导出当前记录", Export);
            output = new Label(); output.style.whiteSpace = WhiteSpace.Normal; rootVisualElement.Add(output);
            refresh = rootVisualElement.schedule.Execute(Refresh).Every(250); Refresh();
        }

        private Button AddButton(string text, Action action)
        {
            var button = new Button(() =>
            {
                try { action(); }
                catch (Exception error) { if (output != null) output.text = error.Message; }
            }) { text = text };
            button.style.marginTop = 6; rootVisualElement.Add(button); return button;
        }

        private static SessionNetwork Host() => UnityEngine.Object.FindObjectsByType<SessionNetwork>(
            FindObjectsSortMode.None).FirstOrDefault(n => n.Hosting && n.ObjectWorld?.Physics?.Enabled == true);
        private static PhysicsTrace Active() => Host()?.ObjectWorld.Physics.Trace;

        private void Refresh()
        {
            var trace = Active();
            if (trace != null) previous = trace;
            status.text = trace == null ? "当前没有可录制的物理动作，请先以房主身份运行物理示例。" :
                "记录：" + trace.Count + "/" + PhysicsTrace.Capacity + (trace.Recording ? " · 录制中" : " · 已停止");
            start.SetEnabled(trace != null); stop.SetEnabled(trace?.Recording == true);
        }

        private void Export()
        {
            var trace = Active() ?? previous ?? throw new InvalidOperationException("暂无可导出的物理动作记录，请先开始录制。");
            var data = trace.Capture();
            if (data.Count == 0 && trace.Failure == null) throw new InvalidOperationException("先录制需要排查的动作。");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/physics-traces",
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, PhysicsTraceExport.Serialize(trace, Application.unityVersion), new System.Text.UTF8Encoding(false));
            output.text = path; EditorUtility.RevealInFinder(path);
        }
        private void OnDisable() => refresh?.Pause();
    }
}
