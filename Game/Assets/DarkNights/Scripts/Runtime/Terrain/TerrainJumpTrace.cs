#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// Editor专用的本地采样、发送、权威接收和运动处理关联；由地面记录器启停，最多1024个值事件。
    /// 使用同一单调时钟和输入序号，只读查询真实地图；不修改输入、碰撞、权限或展示位置。
    /// </summary>
    public static class TerrainJumpTrace
    {
        public const int Capacity = 1024;
        private static TerrainJumpTraceSample[] samples;
        private static double started;
        public static int Count { get; private set; }
        public static int Dropped { get; private set; }
        public static long PendingSequence { get; private set; }
        public static int Epoch { get; private set; }
        public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - started;

        internal static void Reset(int epoch)
        {
            samples = new TerrainJumpTraceSample[Capacity]; Count = Dropped = 0; PendingSequence = 0;
            started = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            Epoch = epoch;
        }

        private static bool Matches(int actorId, int epoch = 0)
        {
            if (!TerrainMotionTrace.Recording || actorId != TerrainMotionTrace.ActorId) return false;
            if (epoch != 0 && Epoch != 0 && epoch != Epoch)
            { TerrainMotionTrace.Stop("世界代次变化，避免关联到旧输入"); return false; }
            return true;
        }

        public static void Press(ObjectSession world, ActorViewData replica, SessionViewData frame,
            long authorityTick, float? visualX, float? visualHeight)
        {
            if (replica == null || !Matches(replica.Id) || world?.Terrain?.Map == null) return;
            var actor = world.Index.Find<ActorBehaviour>(replica.Id);
            if (actor == null) return;
            CapturePress(world.Terrain.Map, actor.Read(), frame.Epoch, authorityTick, frame.ServerTick,
                visualX, visualHeight, replica.X, replica.Height);
        }

        /// <summary>在本地输入采样的同一调用栈冻结可见脚底与当前权威脚底；适配层只传值，不由诊断重放输入。</summary>
        public static void CapturePress(IReadOnlyGrid map, ActorState state, int epoch, long authorityTick,
            long observedTick, float? visualX, float? visualHeight, float replicaX, float replicaHeight)
        {
            if (!Matches(state.Id, epoch)) return;
            if (Count == Capacity) { Dropped++; return; }
            var sample = Pose("press", map, state);
            sample.Epoch = epoch; sample.AuthorityTick = authorityTick; sample.ObservedTick = observedTick;
            sample.VisualX = visualX; sample.VisualHeight = visualHeight;
            sample.ReplicaX = replicaX; sample.ReplicaHeight = replicaHeight;
            if (visualX.HasValue && visualHeight.HasValue) sample.VisualGap = Gap(map, visualX.Value, visualHeight.Value);
            sample.Geometry = Geometry(map, state.X, state.Height, visualX, visualHeight);
            Add(sample);
        }

        public static void Sent(int actorId, long sequence, int epoch, long observedTick)
        {
            if (!Matches(actorId, epoch)) return;
            for (int i = 0; i < Count; i++)
                if (samples[i].Phase == "press" && samples[i].InputSequence == 0) samples[i].InputSequence = sequence;
            Add(new TerrainJumpTraceSample { Phase = "send", ActorId = actorId, InputSequence = sequence,
                Epoch = epoch, ObservedTick = observedTick, WallTime = Now, TraceStep = TerrainMotionTrace.Count });
        }

        public static void Received(IReadOnlyGrid map, ActorState state, HeroInputRequest input, long tick)
        {
            if (!input.JumpPressed || !Matches(state.Id, input.Epoch)) return;
            PendingSequence = input.Sequence;
            var sample = Pose("receive", map, state);
            sample.InputSequence = input.Sequence; sample.Epoch = input.Epoch;
            sample.AuthorityTick = tick; sample.ObservedTick = input.ObservedTick;
            Add(sample);
        }

        internal static void Processed(IReadOnlyGrid map, ActorState state, TerrainMotionTraceFrame frame)
        {
            if ((!frame.Pressed && !frame.JumpStarted) || !Matches(state.Id)) return;
            var sample = Pose("process", map, state);
            sample.TraceStep = frame.Sequence; sample.InputSequence = PendingSequence;
            sample.AuthorityTick = -1; sample.GroundedAtStart = frame.GroundedAtStart; sample.JumpStarted = frame.JumpStarted;
            Add(sample);
        }

        public static TerrainJumpTraceSample[] Snapshot()
        {
            var result = new TerrainJumpTraceSample[Count];
            if (Count > 0) Array.Copy(samples, result, Count);
            return result;
        }

        public static bool Find(string phase, long sequence, out TerrainJumpTraceSample sample)
        {
            for (int i = 0; i < Count; i++)
                if (samples[i].Phase == phase && samples[i].InputSequence == sequence) { sample = samples[i]; return true; }
            sample = default; return false;
        }

        public static float? Gap(IReadOnlyGrid map, float x, float height) => map != null &&
            TerrainBodyCollision.Ground(map, x, height + 1.05f, height - 128,
                HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight, out float floor)
                ? height - floor : (float?)null;

        private static TerrainJumpTraceSample Pose(string phase, IReadOnlyGrid map, ActorState state) =>
            new TerrainJumpTraceSample { Phase = phase, ActorId = state.Id, TraceStep = TerrainMotionTrace.Count,
                WallTime = Now, X = state.X, Height = state.Height, Speed = state.VerticalSpeed,
                Support = state.SupportPlatform, Gap = Gap(map, state.X, state.Height), MapCommit = map?.CommitId ?? 0 };

        private static void Add(TerrainJumpTraceSample sample)
        { if (Count < Capacity) samples[Count++] = sample; else Dropped++; }

        private static string Geometry(IReadOnlyGrid map, float x, float height, float? visualX, float? visualHeight)
        {
            int center = (int)Math.Floor(x / PlayableTerrain.CellPixels + .5f);
            int visible = (int)Math.Floor((visualX ?? x) / PlayableTerrain.CellPixels + .5f);
            int left = Math.Max(0, Math.Min(center, visible) - 2), right = Math.Min(TerrainGenerationSettings.Width - 1, Math.Max(center, visible) + 2);
            right = Math.Min(right, left + 11);
            int row = (int)Math.Floor((PlayableTerrain.OriginY - Math.Max(height, visualHeight ?? height)) / PlayableTerrain.CellPixels + .5f);
            var text = new StringBuilder();
            for (int v = Math.Max(0, row - 4); v <= Math.Min(TerrainGenerationSettings.Height - 1, row + 9); v++)
                for (int u = left; u <= right; u++)
                {
                    var read = map.Read(new CellCoord(u, -v));
                    text.Append(u).Append(':').Append(v).Append(':');
                    if (read.TryGetCell(out var cell)) text.Append(cell.TileId).Append(':').Append(cell.Flags);
                    else text.Append("unknown");
                    text.Append(';');
                }
            return text.ToString();
        }
    }
}
#endif
