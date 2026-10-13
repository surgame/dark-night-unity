using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DarkNights.Tests
{
    /// <summary>照明专项每批独立的只读监控门控；仅忽略获批的提交余量，物理内存不足、监控停止或心跳过期均拒绝新样本，模拟验证不消耗实际内存。</summary>
    public static class WallDiffuseBenchmarkGate
    {
        public static void Check(string directory, DateTime now, int editorPid)
        {
            if (File.Exists(Path.Combine(directory, "breach.json")) || File.Exists(Path.Combine(directory, "stop.flag")) ||
                File.Exists(Path.Combine(directory, "summary.json")))
                throw new OperationCanceledException("本批监控已停止或越线，保留中止证据。");
            string heartbeat = Path.Combine(directory, "latest.json");
            if (!File.Exists(heartbeat)) throw new OperationCanceledException("本批独立内存监控尚未就绪。");
            JObject state;
            // Windows 上监控原子替换文件时，读者必须允许删除共享；内容仍来自打开时的完整旧版或新版。
            using (var stream = new FileStream(heartbeat, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream)) state = JObject.Parse(reader.ReadToEnd());
            if ((int?)state["pid"] != editorPid || (bool?)state["ignore_commit_limit"] != true ||
                (double?)state["minimum_free_ram_gib"] != 1)
                throw new OperationCanceledException("监控身份或本专项门槛不匹配。");
            var age = now.ToUniversalTime() - ((DateTime)state["utc"]).ToUniversalTime();
            if (age.TotalSeconds < -2 || age.TotalSeconds > 10 || (double?)state["free_ram_gib"] < 1 || state["free_ram_gib"] == null)
                throw new OperationCanceledException("监控心跳过期或物理内存低于 1 GiB。");
        }
    }
}
