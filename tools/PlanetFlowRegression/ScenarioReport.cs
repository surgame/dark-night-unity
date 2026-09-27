using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DarkNights.Tools.PlanetFlowRegression
{
    /// <summary>逐场景捕获错误并输出可追溯 JSON；不因首项失败省略后续独立场景，失败返回非零退出码。</summary>
    internal sealed class ScenarioReport
    {
        private readonly List<object> rows = new List<object>();
        public int Total => rows.Count;
        public int Failures { get; private set; }

        public void Check(bool condition, string name)
        {
            rows.Add(new { name, passed = condition });
            if (condition) return;
            Failures++; Console.WriteLine("FAIL: " + name);
        }

        public void Run(string name, Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                rows.Add(new { name, passed = false, error = error.ToString() });
                Failures++; Console.WriteLine("FAIL: " + name + " " + error.Message);
            }
        }

        public void Reject(Action action, string name)
        {
            try { action(); Check(false, name); }
            catch (ArgumentException) { Check(true, name); }
        }

        public void Write(string path, double seconds)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                utc = DateTime.UtcNow.ToString("O"), runtime = Environment.Version.ToString(),
                scope = "Pure Core config, generation, geometry sampling and frozen-state validation; no Unity/network execution",
                passed = Failures == 0, total = Total, failures = Failures, seconds, checks = rows
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
