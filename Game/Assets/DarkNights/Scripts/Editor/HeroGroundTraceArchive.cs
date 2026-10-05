using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DarkNights.Runtime.Terrain;
using Newtonsoft.Json;

namespace DarkNights.Editor
{
    /// <summary>只读载入本工具导出的有界CSV；保留逐步状态数值，不执行输入或修改会话，格式异常明确拒绝。</summary>
    internal static class HeroGroundTraceArchive
    {
        private const string OldHeader = "step,actor,dt,pressed,held,grounded_start,jump_started,x0,h0,vy0,support0,buffer0,ascending0,gap0,target_x,x1,h1,vy1,support1,buffer1,ascending1,gap1,body_blocked,left_shape,right_shape,map_commit0,map_commit1";
        internal static List<TerrainMotionTraceFrame> Read(string folder)
        {
            var result = new List<TerrainMotionTraceFrame>();
            using (var reader = new StreamReader(Path.Combine(folder, "steps.csv")))
            {
                string header = reader.ReadLine();
                bool extended = header == OldHeader + ",wall_time,input_received_tick,input_seq,jump_seq";
                if (header != OldHeader && !extended)
                    throw new FormatException("记录格式不匹配。");
                string row;
                while ((row = reader.ReadLine()) != null)
                {
                    if (result.Count == TerrainMotionTrace.Capacity) throw new FormatException("记录超过7200步上限。");
                    var c = row.Split(',');
                    if (c.Length != (extended ? 31 : 27)) throw new FormatException("记录列数不匹配。");
                    result.Add(new TerrainMotionTraceFrame
                    {
                        Sequence = Int(c[0]), ActorId = Int(c[1]), Delta = Double(c[2]),
                        Pressed = bool.Parse(c[3]), Held = bool.Parse(c[4]), GroundedAtStart = bool.Parse(c[5]), JumpStarted = bool.Parse(c[6]),
                        XBefore = Float(c[7]), HeightBefore = Float(c[8]), SpeedBefore = Float(c[9]), SupportBefore = Int(c[10]),
                        BufferBefore = Double(c[11]), AscendingBefore = bool.Parse(c[12]), GapBefore = Gap(c[13]), TargetX = Float(c[14]),
                        XAfter = Float(c[15]), HeightAfter = Float(c[16]), SpeedAfter = Float(c[17]), SupportAfter = Int(c[18]),
                        BufferAfter = Double(c[19]), AscendingAfter = bool.Parse(c[20]), GapAfter = Gap(c[21]), BodyBlocked = bool.Parse(c[22]),
                        LeftShape = Int(c[23]), RightShape = Int(c[24]), MapCommitBefore = ulong.Parse(c[25]), MapCommitAfter = ulong.Parse(c[26]),
                        WallTime = extended ? Double(c[27]) : 0, InputReceivedTick = extended ? long.Parse(c[28]) : 0,
                        InputSequence = extended ? long.Parse(c[29]) : 0, JumpSequence = extended ? long.Parse(c[30]) : 0
                    });
                }
            }
            return result;
        }

        internal static TerrainJumpTraceSample[] ReadInputs(string folder)
        {
            string path = Path.Combine(folder, "jump-inputs.json");
            if (!File.Exists(path)) return Array.Empty<TerrainJumpTraceSample>();
            var result = JsonConvert.DeserializeObject<TerrainJumpTraceSample[]>(File.ReadAllText(path)) ?? Array.Empty<TerrainJumpTraceSample>();
            if (result.Length > TerrainJumpTrace.Capacity) throw new FormatException("输入事件超过1024条上限。");
            return result;
        }

        private static int Int(string text) => int.Parse(text, CultureInfo.InvariantCulture);
        private static float Float(string text) => float.Parse(text, CultureInfo.InvariantCulture);
        private static double Double(string text) => double.Parse(text, CultureInfo.InvariantCulture);
        private static float? Gap(string text) => text.Length == 0 ? (float?)null : Float(text);
    }
}
