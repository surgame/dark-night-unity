#if UNITY_EDITOR
using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// Editor 主角地面诊断的有界值记录器；默认关闭，只观察指定角色每个真实模拟步的求解前后状态。
    /// 最多7200步，达到上限冻结；不写日志、文件或玩法状态，不进入Player，也不改变跳跃资格。
    /// </summary>
    public static class TerrainMotionTrace
    {
        public const int Capacity = 7200;
        private static TerrainMotionTraceFrame[] frames;
        public static bool Recording { get; private set; }
        public static int Count { get; private set; }
        public static int ActorId { get; private set; }
        public static int Generation { get; private set; }
        public static string StopReason { get; private set; } = "";

        public static void Start(int actorId, int epoch = 0)
        {
            frames = new TerrainMotionTraceFrame[Capacity];
            Generation++;
            Count = 0; ActorId = actorId; StopReason = ""; Recording = true;
            TerrainJumpTrace.Reset(epoch);
        }

        public static void Stop(string reason = "用户停止")
        { Recording = false; StopReason = reason; }

        public static TerrainMotionTraceFrame[] Snapshot()
        {
            var result = new TerrainMotionTraceFrame[Count];
            if (Count > 0) Array.Copy(frames, result, Count);
            return result;
        }

        /// <summary>浮窗逐步读取值副本，避免实时刷新反复复制整段记录；越界查询不分配也不改变记录。</summary>
        public static bool TryRead(int index, out TerrainMotionTraceFrame frame)
        {
            frame = index >= 0 && index < Count ? frames[index] : default;
            return index >= 0 && index < Count;
        }

        internal static TerrainMotionTraceFrame Begin(IReadOnlyGrid map, ActorState state, double delta,
            bool pressed, bool held, float? targetX)
        {
            if (!Recording || !state.ManualControl || state.Id != ActorId) return default;
            return new TerrainMotionTraceFrame
            {
                Captured = true, ActorId = state.Id, Delta = delta, Pressed = pressed, Held = held,
                XBefore = state.X, HeightBefore = state.Height, SpeedBefore = state.VerticalSpeed,
                SupportBefore = state.SupportPlatform, BufferBefore = state.JumpBufferRemaining,
                AscendingBefore = state.JumpAscending, TargetX = targetX ?? state.X,
                GapBefore = FootGap(map, state), MapCommitBefore = map.CommitId,
                WallTime = TerrainJumpTrace.Now, InputSequence = state.LastInputSequence,
                InputReceivedTick = state.LastInputTick, JumpSequence = TerrainJumpTrace.PendingSequence
            };
        }

        internal static void End(IReadOnlyGrid map, ActorState state, TerrainMotionTraceFrame frame, bool started)
        {
            if (!frame.Captured || !Recording) return;
            frame.Sequence = Count + 1; frame.JumpStarted = started;
            frame.XAfter = state.X; frame.HeightAfter = state.Height; frame.SpeedAfter = state.VerticalSpeed;
            frame.SupportAfter = state.SupportPlatform; frame.BufferAfter = state.JumpBufferRemaining;
            frame.AscendingAfter = state.JumpAscending; frame.GapAfter = FootGap(map, state);
            frame.MapCommitAfter = map.CommitId;
            frame.BodyBlocked = TerrainBodyCollision.Blocked(map, state.X, state.Height,
                HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight);
            frame.LeftShape = Shape(map, state.X - HeroControlDefinition.BodyHalfWidth + .001f, state.Height);
            frame.RightShape = Shape(map, state.X + HeroControlDefinition.BodyHalfWidth - .001f, state.Height);
            frames[Count++] = frame;
            TerrainJumpTrace.Processed(map, state, frame);
            if (Count == Capacity) Stop("达到7200步上限");
        }

        private static float? FootGap(IReadOnlyGrid map, ActorState state) => TerrainJumpTrace.Gap(map, state.X, state.Height);

        private static int Shape(IReadOnlyGrid map, float x, float height)
        {
            int u = (int)Math.Floor(x / PlayableTerrain.CellPixels + .5f);
            int row = (int)Math.Floor((PlayableTerrain.OriginY - height + .25f) / PlayableTerrain.CellPixels + .5f);
            var sample = map.Read(new CellCoord(u, -row));
            if (!sample.TryGetCell(out var cell)) return -2;
            return cell.IsEmpty ? -1 : (int)TerrainShapeGeometry.Decode(cell.Flags);
        }
    }
}
#endif
