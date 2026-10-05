using System;
using DarkNights.Runtime.Terrain;
using Newtonsoft.Json;
using UnityEditor;

namespace DarkNights.Editor
{
    /// <summary>Editor内存中的一轮冻结诊断；跨Play和脚本重载由SessionState恢复，不创建导出文件或保存玩法状态。</summary>
    internal sealed class HeroGroundTraceBuffer
    {
        private const string Key = "DarkNights.HeroGroundTrace.Buffer";
        public string Scene, StartedUtc, StoppedUtc, Reason, TerrainContext;
        public int ActorId, InputEpoch, InputDropped;
        public TerrainMotionTraceFrame[] Frames = Array.Empty<TerrainMotionTraceFrame>();
        public TerrainJumpTraceSample[] Inputs = Array.Empty<TerrainJumpTraceSample>();

        internal void Remember() => SessionState.SetString(Key, JsonConvert.SerializeObject(this));

        internal static HeroGroundTraceBuffer Restore()
        {
            string text = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(text)) return new HeroGroundTraceBuffer();
            var result = JsonConvert.DeserializeObject<HeroGroundTraceBuffer>(text) ?? new HeroGroundTraceBuffer();
            result.Frames ??= Array.Empty<TerrainMotionTraceFrame>(); result.Inputs ??= Array.Empty<TerrainJumpTraceSample>();
            if (result.Frames.Length > TerrainMotionTrace.Capacity || result.Inputs.Length > TerrainJumpTrace.Capacity)
                throw new FormatException("内存记录超过诊断容量上限。");
            return result;
        }
    }
}
