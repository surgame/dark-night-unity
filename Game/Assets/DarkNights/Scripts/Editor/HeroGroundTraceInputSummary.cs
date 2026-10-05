using System;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Editor
{
    /// <summary>将同一输入序号的显示脚底、权威脚底与阶段时间呈现为只读摘要；旧记录明确保留未采集状态。</summary>
    internal static class HeroGroundTraceInputSummary
    {
        internal static string Describe(long sequence, TerrainJumpTraceSample[] archive)
        {
            bool Find(string phase, out TerrainJumpTraceSample result)
            {
                if (archive == null) return TerrainJumpTrace.Find(phase, sequence, out result);
                foreach (var sample in archive)
                    if (sample.InputSequence == sequence && sample.Phase == phase) { result = sample; return true; }
                result = default; return false;
            }
            if (sequence == 0 || !Find("press", out var press))
                return "本记录未采集对应的本地按键；新录制将对齐显示位置与权威输入。";
            string milliseconds(double seconds) => (seconds * 1000).ToString("F1") + "ms";
            string gap(float? value) => value.HasValue ? value.Value.ToString("F3") + "px" : "128px内无可支撑地面";
            string timing = "尚未发送";
            if (Find("send", out var sent))
            {
                timing = "采样→发送 " + milliseconds(sent.WallTime - press.WallTime);
                if (Find("receive", out var received))
                {
                    timing += "  发送→接收 " + milliseconds(received.WallTime - sent.WallTime);
                    if (Find("process", out var processed)) timing += "  接收→处理 " + milliseconds(processed.WallTime - received.WallTime);
                }
            }
            string shown = press.VisualGap.HasValue || press.VisualX.HasValue ? gap(press.VisualGap) : "未取得显示对象";
            return $"对应输入 {sequence}：采样时显示脚底 {shown} / 权威脚底 {gap(press.Gap)}\n" +
                $"显示 X {press.VisualX:F2} / 权威 X {press.X:F2}  快照 tick {press.ObservedTick} / 权威 tick {press.AuthorityTick}\n" + timing;
        }
    }
}
