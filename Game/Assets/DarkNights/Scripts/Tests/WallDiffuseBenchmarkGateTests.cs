using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>用任务专属临时目录模拟启动拒绝和运行中越线；验证专项门槛及批次隔离，不耗尽真实内存、不启动 Play 或改作者文件。</summary>
    public sealed class WallDiffuseBenchmarkGateTests
    {
        private string directory;
        private readonly DateTime now = new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void CreateMonitor()
        {
            directory = Path.GetFullPath("../artifacts/light-pruning-20261013/gate-simulation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Write(2, 0, now);
        }

        private void Write(double ram, double commit, DateTime utc, bool ignore = true)
        {
            File.WriteAllText(Path.Combine(directory, "latest.json"), new JObject
            {
                ["pid"] = 123, ["utc"] = utc.ToString("o"), ["free_ram_gib"] = ram,
                ["free_commit_gib"] = commit, ["ignore_commit_limit"] = ignore, ["minimum_free_ram_gib"] = 1
            }.ToString());
        }

        [Test]
        public void ApprovedCommitExceptionStillEnforcesPhysicalReserve()
        {
            Assert.DoesNotThrow(() => WallDiffuseBenchmarkGate.Check(directory, now, 123));
            Write(.99, 100, now);
            Assert.Throws<OperationCanceledException>(() => WallDiffuseBenchmarkGate.Check(directory, now, 123));
            Write(1, 0, now);
            Assert.DoesNotThrow(() => WallDiffuseBenchmarkGate.Check(directory, now, 123));
        }

        [TestCase("stop.flag")] [TestCase("breach.json")] [TestCase("summary.json")]
        public void RejectsStoppedBatchAndAllowsFreshBatch(string marker)
        {
            Assert.DoesNotThrow(() => WallDiffuseBenchmarkGate.Check(directory, now, 123));
            File.WriteAllText(Path.Combine(directory, marker), "{}");
            Assert.Throws<OperationCanceledException>(() => WallDiffuseBenchmarkGate.Check(directory, now, 123));
            string fresh = Path.Combine(directory, "fresh"); Directory.CreateDirectory(fresh);
            File.Copy(Path.Combine(directory, "latest.json"), Path.Combine(fresh, "latest.json"));
            Assert.DoesNotThrow(() => WallDiffuseBenchmarkGate.Check(fresh, now, 123));
        }

        [Test]
        public void RejectsExpiredMissingOrWrongMonitor()
        {
            Assert.Throws<OperationCanceledException>(() => WallDiffuseBenchmarkGate.Check(Path.Combine(directory, "absent"), now, 123));
            Assert.Throws<OperationCanceledException>(() => WallDiffuseBenchmarkGate.Check(directory, now, 456));
            Write(2, 0, now.AddSeconds(-11));
            Assert.Throws<OperationCanceledException>(() => WallDiffuseBenchmarkGate.Check(directory, now, 123));
            Write(2, 0, now, false);
            Assert.Throws<OperationCanceledException>(() => WallDiffuseBenchmarkGate.Check(directory, now, 123));
        }
    }
}
