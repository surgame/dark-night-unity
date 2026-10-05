using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>主角地面诊断的UI Toolkit独立浮窗；以10Hz增量读取有界值记录，支持时间线检查及最近CSV回看。</summary>
    public sealed class HeroGroundProfilerWindow : EditorWindow
    {
        private const string Layout = "Assets/DarkNights/Res/Editor/HeroGroundTrace/HeroGroundProfiler.uxml";
        private List<TerrainMotionTraceFrame> frames = new List<TerrainMotionTraceFrame>();
        private HeroGroundTraceGraph graph;
        private Label status, counters, details, inputDetails, notice, output;
        private TerrainJumpTraceSample[] inputArchive = Array.Empty<TerrainJumpTraceSample>();
        private Button start, stop, export, load;
        private Toggle follow;
        private SliderInt selection;
        private ProgressBar progress;
        private IVisualElementScheduledItem refresh;
        private int generation = -1, losses, nearAir, jumps;
        private bool archive;
        private string viewNotice;

        [MenuItem("Dark Nights/Debug/主角地面记录/打开 Profiler 浮窗", false, 0)]
        [MenuItem("Window/Analysis/Dark Nights 主角地面 Profiler")]
        public static void Open()
        {
            var window = GetWindow<HeroGroundProfilerWindow>(true, "主角地面 Profiler", false);
            window.minSize = new Vector2(640, 560);
            if (window.position.width < 640 || window.position.height < 560)
                window.position = new Rect(window.position.x, window.position.y, 800, 580);
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            refresh?.Pause(); rootVisualElement.Clear();
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout);
            if (asset == null) { rootVisualElement.Add(new Label("未找到主角地面Profiler界面。")); return; }
            asset.CloneTree(rootVisualElement);
            rootVisualElement.AddToClassList("trace-window");
            status = rootVisualElement.Q<Label>("status"); counters = rootVisualElement.Q<Label>("counters");
            details = rootVisualElement.Q<Label>("details"); notice = rootVisualElement.Q<Label>("notice");
            inputDetails = rootVisualElement.Q<Label>("inputDetails");
            output = rootVisualElement.Q<Label>("output"); progress = rootVisualElement.Q<ProgressBar>("progress");
            start = rootVisualElement.Q<Button>("start"); stop = rootVisualElement.Q<Button>("stop");
            export = rootVisualElement.Q<Button>("export");
            start.tooltip = "开始新一轮会替换当前内存记录；需要保留时请先导出。";
            load = rootVisualElement.Q<Button>("load"); follow = rootVisualElement.Q<Toggle>("follow");
            selection = rootVisualElement.Q<SliderInt>("selection");
            graph = new HeroGroundTraceGraph(); rootVisualElement.Q("graphHost").Add(graph);
            start.clicked += () => Act(HeroGroundTraceTool.Start);
            stop.clicked += () => Act(HeroGroundTraceTool.Stop);
            export.clicked += () => Act(HeroGroundTraceTool.Export);
            load.clicked += () => Act(() => { if (archive) ResetView(); else LoadRecent(); });
            rootVisualElement.Q<Button>("folder").clicked += HeroGroundTraceTool.OpenFolder;
            selection.RegisterValueChangedCallback(e => { follow.SetValueWithoutNotify(false); ShowFrame(e.newValue); });
            graph.Selected = i => { follow.SetValueWithoutNotify(false); selection.SetValueWithoutNotify(i); ShowFrame(i); };
            UpdateView(); refresh = rootVisualElement.schedule.Execute(UpdateView).Every(100);
        }

        private void OnDisable() { refresh?.Pause(); refresh = null; }

        private void Act(Action action)
        {
            viewNotice = null;
            try { action(); }
            catch (Exception error) { viewNotice = error.Message; }
            UpdateView();
        }

        private void LoadRecent()
        {
            var loadedFrames = HeroGroundTraceArchive.Read(HeroGroundTraceTool.LastOutput);
            var loadedInputs = HeroGroundTraceArchive.ReadInputs(HeroGroundTraceTool.LastOutput);
            frames = loadedFrames; inputArchive = loadedInputs;
            archive = true; generation = HeroGroundTraceTool.Generation;
            follow.SetValueWithoutNotify(false); Recount();
            selection.highValue = Math.Max(0, frames.Count - 1); selection.SetValueWithoutNotify(selection.highValue);
            ShowFrame(selection.value);
        }

        private void ResetView()
        {
            frames = new List<TerrainMotionTraceFrame>(); inputArchive = Array.Empty<TerrainJumpTraceSample>();
            archive = false; losses = nearAir = jumps = 0; generation = HeroGroundTraceTool.Generation;
            viewNotice = null;
            selection.highValue = 0; selection.SetValueWithoutNotify(0); follow.SetValueWithoutNotify(true);
        }

        private void UpdateView()
        {
            if (graph == null) return;
            if (generation != HeroGroundTraceTool.Generation) ResetView();
            if (!archive)
            {
                while (HeroGroundTraceTool.TryRead(frames.Count, out var frame)) { frames.Add(frame); Count(frame); }
            }
            bool recording = TerrainMotionTrace.Recording;
            start.SetEnabled(EditorApplication.isPlaying && !recording); stop.SetEnabled(recording);
            export.SetEnabled(!archive && HeroGroundTraceTool.Count > 0);
            load.text = archive ? "当前记录" : "最近记录";
            load.SetEnabled(!recording && (archive ? HeroGroundTraceTool.Count > 0 : Directory.Exists(HeroGroundTraceTool.LastOutput)));
            status.text = recording ? "● 录制中" : archive ? "回看已保存记录" : frames.Count > 0 ? "录制已停止" : "等待录制";
            progress.value = frames.Count; progress.title = $"{frames.Count} / {TerrainMotionTrace.Capacity} 模拟步";
            counters.text = $"起跳 {jumps}    未起跳丢支撑 {losses}    近地空中 {nearAir} 步";
            output.text = string.IsNullOrEmpty(HeroGroundTraceTool.LastOutput) ? "尚未导出" : "最近导出：" + Path.GetFileName(HeroGroundTraceTool.LastOutput);
            output.tooltip = HeroGroundTraceTool.LastOutput;
            selection.highValue = Math.Max(0, frames.Count - 1); selection.SetEnabled(frames.Count > 0);
            if (follow.value) selection.SetValueWithoutNotify(selection.highValue);
            ShowFrame(Math.Min(selection.value, selection.highValue));
            notice.text = viewNotice ?? (archive ? "已载入最近记录，可拖动模拟步或点击曲线检查。" : HeroGroundTraceTool.Notice);
        }

        private void Recount() { losses = nearAir = jumps = 0; foreach (var frame in frames) Count(frame); }

        private void Count(TerrainMotionTraceFrame frame)
        {
            if (frame.JumpStarted) jumps++;
            if (frame.SupportBefore >= 0 && frame.SupportAfter < 0 && !frame.JumpStarted) losses++;
            if (frame.SupportAfter < 0 && frame.SpeedAfter <= 0 && frame.GapAfter >= -.002f && frame.GapAfter <= .251f) nearAir++;
        }

        private void ShowFrame(int index)
        {
            graph.Present(frames, index);
            if (index < 0 || index >= frames.Count)
            { details.text = "进入Play，开始记录后可查看每个模拟步。"; inputDetails.text = "本地采样、发送、接收与处理将按同一输入序号记录。"; return; }
            var f = frames[index];
            string gap = f.GapAfter.HasValue ? f.GapAfter.Value.ToString("F3") : "无近距离支撑";
            details.text = $"步 {f.Sequence}  ·  角色 {f.ActorId}  ·  {(f.SupportAfter >= 0 ? "地面" : "空中")}  ·  脚底间隙 {gap}\n" +
                $"X {f.XAfter:F2}  高度 {f.HeightAfter:F2}  竖直速度 {f.SpeedAfter:F2}  支撑 {f.SupportBefore} → {f.SupportAfter}\n" +
                $"跳跃按下 {(f.Pressed ? "是" : "否")}  当步可起跳 {(f.GroundedAtStart ? "是" : "否")}  实际起跳 {(f.JumpStarted ? "是" : "否")}  缓冲 {f.BufferAfter:F3}s";
            inputDetails.text = HeroGroundTraceInputSummary.Describe(f.JumpSequence,
                archive ? inputArchive : TerrainMotionTrace.Recording ? null : HeroGroundTraceTool.FrozenInputs);
        }
    }
}
