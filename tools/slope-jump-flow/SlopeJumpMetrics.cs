using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using DarkNights.Core.Config;
using DarkNights.Runtime.Terrain;
using DarkNights.Tests;

/// <summary>以同一组连续长坡记录候选逐步位移，用于与分析基线比较；不将合成场景统计当作实机验收。</summary>
internal static class SlopeJumpMetrics
{
    public static void Write(string output)
    {
        const double delta = 1.0 / 60;
        var summaries = new List<object>();
        var rows = new List<string> { "case,tick,input_jump,x,height,v_y,support,dx" };
        foreach (float slope in new[] { 0, .5f, 1f })
        foreach (bool sprint in new[] { false, true })
        foreach (int direction in new[] { -1, 1 })
        foreach (string mode in new[] { "walk", "jump", "repeat", "held-jetpack" })
        {
            var map = TerrainMotionTestMap.Ramp(slope);
            var rules = TerrainMotionTestMap.Rules();
            float x = direction == 1 ? 1760 : 2048;
            float floor = TerrainMotionTestMap.BaseHeight + slope * (x + 8 - 1592);
            var state = TerrainMotionTestMap.Actor(x, floor, mode == "held-jetpack");
            string name = string.Format(CultureInfo.InvariantCulture, "m{0}-{1}-{2}-{3}", slope,
                sprint ? "sprint" : "walk", direction == 1 ? "up" : "down", mode);
            float intended = direction * rules.MoveSpeed(sprint) * (float)delta;
            int partial = 0, stopped = 0, grounded = 0, presses = 0, accepted = 0;
            for (int tick = 0; tick < 60; tick++)
            {
                bool jump = mode != "walk" && (tick == 0 || mode == "repeat" && tick % 6 == 0);
                float before = state.X;
                TerrainHeroMotion.Tick(map, state, rules, delta, jump, mode == "held-jetpack", targetX: state.X + intended);
                float dx = Math.Abs(state.X - before);
                if (dx < Math.Abs(intended) - .01f) partial++;
                if (dx < .01f) stopped++;
                if (state.SupportPlatform >= 0) grounded++;
                if (jump)
                {
                    presses++;
                    if (Math.Abs(state.VerticalSpeed - (rules.JumpSpeed - rules.Gravity * (float)delta)) < .001f) accepted++;
                }
                rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F5},{4:F5},{5:F5},{6},{7:F5}",
                    name, tick, jump ? 1 : 0, state.X, state.Height, state.VerticalSpeed, state.SupportPlatform, dx));
            }
            summaries.Add(new { name, totalHorizontal = Math.Abs(state.X - x), partialHorizontalTicks = partial,
                stoppedHorizontalTicks = stopped, groundedTicks = grounded, presses, accepted });
        }
        File.WriteAllLines(Path.Combine(output, "trajectory.csv"), rows);
        File.WriteAllText(Path.Combine(output, "metrics.json"), JsonSerializer.Serialize(new {
            scope = "正式运动源码、最小状态适配与合成连续长坡；非Unity/联机验收", ticksPerCase = 60, cases = summaries
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("48种轨迹、2880个固定步已记录。");
    }
}
