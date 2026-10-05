using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DarkNights.Entry;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Terrain;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 主角地面诊断控制与显式导出；停止只冻结内存，导出只复制当前快照，不停止录制。
    /// Play退出、重载和容量上限只保留内存；文件仅由用户点击导出产生，不修改游戏状态。
    /// </summary>
    [InitializeOnLoad]
    public static class HeroGroundTraceTool
    {
        private const string Menu = "Dark Nights/Debug/主角地面记录/";
        private static bool capturing;
        private static HeroGroundTraceBuffer buffer;
        public static int Generation { get; private set; }
        public static int Count => capturing ? TerrainMotionTrace.Count : buffer.Frames.Length;
        internal static TerrainJumpTraceSample[] FrozenInputs => buffer.Inputs;
        public static string LastOutput { get; private set; } = "";
        public static string Notice { get; private set; } = "进入Play后开始记录；也可回看最近保存的记录。";

        static HeroGroundTraceTool()
        {
            LastOutput = SessionState.GetString("DarkNights.HeroGroundTrace.LastOutput", "");
            buffer = HeroGroundTraceBuffer.Restore();
            if (buffer.Frames.Length > 0) Notice = $"内存中保留{buffer.Frames.Length}步，可查看或导出。";
            EditorApplication.update += CheckLimit;
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += () => { StopCapture("脚本重载"); buffer.Remember(); };
        }

        [MenuItem(Menu + "开始记录")]
        public static void Start()
        {
            if (!EditorApplication.isPlaying) { Notice = "请先进入Play并操控主角。"; Debug.LogWarning(Notice); return; }
            var network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
            var player = UnityEngine.Object.FindAnyObjectByType<HeroPlayerController>();
            var workshop = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
            if (network?.Hosting != true && workshop?.Workshop == null)
            { Notice = "需要本地Host或离线洞穴工作台，客户端没有权威运动步骤。"; Debug.LogWarning(Notice); return; }
            int id = workshop?.Workshop != null ? 0 : player?.Current?.Id ?? -1;
            if (id < 0) { Notice = "尚未取得本地主角。"; Debug.LogWarning(Notice); return; }
            StartCapture(id, network?.Client?.Replica.Current?.Epoch ?? 0,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, HeroGroundTraceContext.Capture());
        }

        private static void StartCapture(int id, int epoch, string scene, string terrainContext)
        {
            buffer = new HeroGroundTraceBuffer
            {
                Scene = scene, StartedUtc = DateTime.UtcNow.ToString("O"), TerrainContext = terrainContext, ActorId = id,
                InputEpoch = epoch
            };
            TerrainMotionTrace.Start(id, epoch);
            capturing = true; Generation++; buffer.Remember();
            LastOutput = "";
            SessionState.EraseString("DarkNights.HeroGroundTrace.LastOutput");
            Notice = "记录中：在目标位置来回走、冲刺和点跳。";
            Debug.Log("地面记录已开始：最多7200模拟步（1倍速约2分钟）。停止会保留内存记录，点击导出才生成文件。");
        }

        [MenuItem(Menu + "停止记录")]
        public static void Stop() => StopCapture("用户停止");

        /// <summary>浮窗读取当前记录值副本；录制中读运行缓冲，停止后读冻结缓冲，均不产生文件。</summary>
        public static bool TryRead(int index, out TerrainMotionTraceFrame frame)
        {
            if (capturing) return TerrainMotionTrace.TryRead(index, out frame);
            frame = index >= 0 && index < buffer.Frames.Length ? buffer.Frames[index] : default;
            return index >= 0 && index < buffer.Frames.Length;
        }

        [MenuItem(Menu + "打开记录目录")]
        public static void OpenFolder()
        {
            string root = Root(); Directory.CreateDirectory(root); EditorUtility.RevealInFinder(root);
        }

        [MenuItem(Menu + "查看记录状态")]
        public static void Status() => Debug.Log($"地面记录：{(TerrainMotionTrace.Recording ? "记录中" : "已停止")}，{Count}/{TerrainMotionTrace.Capacity}步。最近结果：{LastOutput}");

        private static void CheckLimit()
        { if (capturing && !TerrainMotionTrace.Recording) StopCapture(TerrainMotionTrace.StopReason); }

        private static void OnPlayState(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingPlayMode) StopCapture("退出Play"); }

        private static string Root() => Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/hero-ground-trace"));

        private static void StopCapture(string reason)
        {
            if (!capturing) return;
            TerrainMotionTrace.Stop(reason);
            buffer = CurrentSnapshot(); buffer.StoppedUtc = DateTime.UtcNow.ToString("O"); buffer.Reason = reason;
            capturing = false; buffer.Remember();
            Notice = $"录制已停止，保留{buffer.Frames.Length}步；点击导出才生成文件。";
        }

        private static HeroGroundTraceBuffer CurrentSnapshot() => capturing ? new HeroGroundTraceBuffer
        {
            Scene = buffer.Scene, StartedUtc = buffer.StartedUtc, TerrainContext = buffer.TerrainContext,
            ActorId = TerrainMotionTrace.ActorId, InputEpoch = TerrainJumpTrace.Epoch, InputDropped = TerrainJumpTrace.Dropped,
            Frames = TerrainMotionTrace.Snapshot(), Inputs = TerrainJumpTrace.Snapshot()
        } : buffer;

        [MenuItem(Menu + "导出记录")]
        public static void Export()
        {
            var snapshot = CurrentSnapshot();
            var frames = snapshot.Frames;
            if (frames.Length == 0) { Notice = "当前没有可导出的记录。"; return; }
            try
            {
                string folder = Path.Combine(Root(), DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(folder);
                var suspicious = frames.Where(f => f.SupportAfter < 0 && f.SpeedAfter <= 0 &&
                    f.GapAfter.HasValue && f.GapAfter.Value >= -.002f && f.GapAfter.Value <= .251f).Select(f => f.Sequence).ToArray();
                var losses = frames.Where(f => f.SupportBefore >= 0 && f.SupportAfter < 0 && !f.JumpStarted).Select(f => f.Sequence).ToArray();
                var summary = new { scene = snapshot.Scene, startedUtc = snapshot.StartedUtc, stoppedUtc = snapshot.StoppedUtc,
                    reason = snapshot.Reason, exportedUtc = DateTime.UtcNow.ToString("O"), recordingAtExport = TerrainMotionTrace.Recording,
                    actorId = snapshot.ActorId, steps = frames.Length, seconds = frames.Sum(f => f.Delta),
                    diagnosticSchema = 2, footGapRange = 128, inputEpoch = snapshot.InputEpoch,
                    inputEvents = snapshot.Inputs.Length, inputEventsDropped = snapshot.InputDropped,
                    jumps = frames.Count(f => f.JumpStarted), walkSupportLossSteps = losses, nearGroundAirSteps = suspicious,
                    scope = "真实权威地形运动；近地空中只作待检查标记，不直接认定为BUG。所有距离为逻辑像素。" };
                File.WriteAllText(Path.Combine(folder, "summary.json"), JsonConvert.SerializeObject(summary, Formatting.Indented));
                File.WriteAllText(Path.Combine(folder, "terrain.json"), snapshot.TerrainContext ?? "{}");
                File.WriteAllText(Path.Combine(folder, "jump-inputs.json"), JsonConvert.SerializeObject(snapshot.Inputs, Formatting.Indented));
                WriteCsv(Path.Combine(folder, "steps.csv"), frames);
                LastOutput = folder;
                SessionState.SetString("DarkNights.HeroGroundTrace.LastOutput", folder);
                Notice = $"已导出{frames.Length}步；{(TerrainMotionTrace.Recording ? "录制继续" : "内存记录保留")}。";
                Debug.Log($"地面记录已保存：{frames.Length}步，未起跳丢支撑{losses.Length}次，近地空中{suspicious.Length}步。目录：{folder}");
            }
            catch (Exception error)
            {
                Notice = "导出失败：" + error.Message;
                Debug.LogError("地面记录导出失败，内存副本仍可用：" + error.Message);
            }
        }

        private static void WriteCsv(string path, TerrainMotionTraceFrame[] frames)
        {
            var text = new StringBuilder("step,actor,dt,pressed,held,grounded_start,jump_started,x0,h0,vy0,support0,buffer0,ascending0,gap0,target_x,x1,h1,vy1,support1,buffer1,ascending1,gap1,body_blocked,left_shape,right_shape,map_commit0,map_commit1,wall_time,input_received_tick,input_seq,jump_seq\n");
            foreach (var f in frames)
                text.AppendLine(string.Join(",", new object[] { f.Sequence, f.ActorId, f.Delta, f.Pressed, f.Held,
                    f.GroundedAtStart, f.JumpStarted, f.XBefore, f.HeightBefore, f.SpeedBefore, f.SupportBefore,
                    f.BufferBefore, f.AscendingBefore, f.GapBefore, f.TargetX, f.XAfter, f.HeightAfter, f.SpeedAfter,
                    f.SupportAfter, f.BufferAfter, f.AscendingAfter, f.GapAfter, f.BodyBlocked, f.LeftShape, f.RightShape,
                    f.MapCommitBefore, f.MapCommitAfter, f.WallTime, f.InputReceivedTick, f.InputSequence, f.JumpSequence }
                    .Select(v => v is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "")));
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }
    }
}
