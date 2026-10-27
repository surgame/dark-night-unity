using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DarkNights.View.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 一次地图读取至完整呈现的阶段进度与有界耗时；地形矿层就绪不等于轮廓开始可见，不按时间伪造百分比。
    /// 仅由Editor主线程写入，最近结果供诊断读取，不保存作者配置或逐帧日志。
    /// </summary>
    internal sealed class MapWorkbenchProgress
    {
        private readonly Stopwatch watch = new Stopwatch();
        private readonly Dictionary<string, double> phases = new Dictionary<string, double>();
        private readonly Queue<string> history = new Queue<string>();
        private string phase;
        private double phaseStarted, nextObservation;
        private string workTitle;
        private bool designReceipt;
        internal bool Active { get; private set; }
        internal string Target { get; private set; } = "";
        internal string Detail { get; set; } = "";
        internal float Value { get; private set; }
        internal double PeakMainMilliseconds { get; private set; }
        internal double TerrainReadyMilliseconds { get; private set; } = -1;
        internal string PeakMainStep { get; private set; } = "";
        internal string LastReport { get; private set; } = "";
        internal double ElapsedMilliseconds => watch.Elapsed.TotalMilliseconds;
        internal IReadOnlyDictionary<string, double> PhaseMilliseconds => phases;
        internal string Title => (workTitle ?? phase + "（无法估算比例）") + " · " + watch.Elapsed.TotalSeconds.ToString("0.0") + "秒";

        internal void Begin(string target)
        {
            Target = target; Active = true; Detail = ""; phase = null;
            phases.Clear(); Value = 0; workTitle = null; nextObservation = 0; PeakMainMilliseconds = 0; PeakMainStep = "";
            TerrainReadyMilliseconds = -1;
            designReceipt = false;
            watch.Restart(); Phase("准备读取");
        }

        internal void Phase(string name)
        {
            if (!Active || phase == name) return;
            ClosePhase(); phase = name; phaseStarted = ElapsedMilliseconds;
            workTitle = null; Value = 0; nextObservation = 0;
        }

        internal void Observe(TerrainStylePreviewStage stage, bool force = false)
        {
            if (!Active || !force && ElapsedMilliseconds < nextObservation) return;
            nextObservation = ElapsedMilliseconds + 200;
            if (stage.Terrain == null) { Unknown("初始化地形"); return; }
            var progress = stage.Terrain.Progress;
            Detail = Pages("地形规则", progress.Terrain) + " · " + Pages("墙体", progress.Rock) + "\n" +
                (stage.MineralInputPending ? "矿层：等待地形输入就绪" : Pages("矿层", progress.Minerals)) + " · " +
                (stage.TerrainReady ? Pages("背景", progress.Background) : "背景：等待地形与矿层就绪");
            if (stage.Ready) { Value = 100; workTitle = "完整画面已就绪"; }
            else if (stage.TerrainReady)
            {
                if (!progress.Background.Known) Unknown("背景布局计算中");
                else if (!progress.Background.Finished) Show("背景分页", progress.Background);
                else Unknown("等待背景绘制确认");
            }
            else if (!progress.Terrain.Known) Unknown("准备地形输入");
            else if (!progress.Terrain.Finished) Show("地形规则", progress.Terrain);
            else if (!progress.Rock.Known) Unknown("墙体轮廓计算中");
            else if (!progress.Rock.Finished) Show("墙体分页", progress.Rock);
            else if (stage.MineralInputPending) Unknown("准备矿层输入");
            else if (!stage.Terrain.MineralsReady && !progress.Minerals.Known) Unknown("准备矿层页面");
            else if (progress.Minerals.Known && !progress.Minerals.Finished) Show("矿层分页", progress.Minerals);
            else Unknown("等待资源收尾与相机绘制确认");
        }

        private void Unknown(string name) { Value = 0; workTitle = name + "（无法估算比例）"; }
        private void Show(string name, TerrainPageProgress pages)
        { Value = pages.Percent; workTitle = "当前阶段 · " + Pages(name, pages) + " · " + Value.ToString("0") + "%"; }
        private static string Pages(string name, TerrainPageProgress pages) => name + "：" +
            (!pages.Known ? "计算中" : pages.Total == 0 ? "无需分页" : pages.Completed + "/" + pages.Total + "页");

        internal void MainStep(string name, double milliseconds)
        {
            if (!Active || milliseconds <= PeakMainMilliseconds) return;
            PeakMainMilliseconds = milliseconds; PeakMainStep = name;
        }

        internal void MarkTerrainReady()
        { if (Active && TerrainReadyMilliseconds < 0) TerrainReadyMilliseconds = ElapsedMilliseconds; }

        internal void MarkDesignReady()
        { designReceipt = true; MarkTerrainReady(); }

        internal void Finish(string result = "已就绪")
        {
            if (!Active) return;
            ClosePhase(); watch.Stop(); Active = false;
            LastReport = Target + " · " + result + " · 总计 " + ElapsedMilliseconds.ToString("0") + "ms；" +
                (TerrainReadyMilliseconds >= 0 ? (designReceipt ? "结构白模绘制 " : "地形与矿层就绪 ") +
                    TerrainReadyMilliseconds.ToString("0") + "ms；" : "") +
                string.Join("；", phases.Select(x => x.Key + " " + x.Value.ToString("0") + "ms")) +
                "；最长主线程步骤 " + PeakMainStep + " " + PeakMainMilliseconds.ToString("0") + "ms";
            history.Enqueue(LastReport); while (history.Count > 8) history.Dequeue();
        }

        private void ClosePhase()
        {
            if (phase == null) return;
            phases.TryGetValue(phase, out double prior);
            phases[phase] = prior + ElapsedMilliseconds - phaseStarted;
        }
    }
}
