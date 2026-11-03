using System;
using System.Collections.Generic;
using DarkNights.Core.Physics;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 可显式启停的有限物理记录片段；600步或16次地形变化后自动停止，保留起始检查点而不轮换丢帧。
    /// 导出由现有工作台中的诊断窗口负责，记录不驱动玩法或改变输入。
    /// </summary>
    public sealed class PhysicsTrace
    {
        public const int Capacity = 600;
        private readonly Queue<PhysicsTraceFrame> frames = new Queue<PhysicsTraceFrame>();
        private readonly List<PhysicsTerrainTrace> terrain = new List<PhysicsTerrainTrace>();
        public bool Recording { get; set; }
        public int Count => frames.Count;
        public string InitialWorldJson { get; private set; } = "";
        public PhysicsWorldDefinition Rules { get; private set; }
        public string MovementFingerprint { get; private set; } = "";
        public ulong LastTerrainRevision { get; private set; }
        public PhysicsTraceFailure Failure { get; private set; }
        public IReadOnlyList<PhysicsTerrainTrace> TerrainChanges => Array.AsReadOnly(terrain.ToArray());
        internal void Begin(string worldJson, PhysicsWorldDefinition rules, string movement, ulong revision)
        {
            Clear(); InitialWorldJson = worldJson; Rules = rules; MovementFingerprint = movement;
            LastTerrainRevision = revision; Recording = true;
        }
        public void Clear()
        {
            Recording = false; frames.Clear(); terrain.Clear();
            InitialWorldJson = MovementFingerprint = ""; Rules = null; LastTerrainRevision = 0;
            Failure = null;
        }
        internal void Fail(double delta, IReadOnlyList<ActorMotionInput> inputs, Exception error)
        {
            if (!Recording) return;
            Failure = new PhysicsTraceFailure(delta, inputs, error); Recording = false;
        }
        internal void Record(PhysicsTraceFrame frame, PhysicsTerrainTrace change)
        {
            if (!Recording || frame == null) return;
            if (frames.Count == Capacity || change != null && terrain.Count == 16)
            { Recording = false; return; }
            if (change != null) { terrain.Add(change); LastTerrainRevision = change.Revision; }
            frames.Enqueue(frame);
            if (frames.Count == Capacity) Recording = false;
        }
        public IReadOnlyList<PhysicsTraceFrame> Capture() => Array.AsReadOnly(frames.ToArray());
    }
}
