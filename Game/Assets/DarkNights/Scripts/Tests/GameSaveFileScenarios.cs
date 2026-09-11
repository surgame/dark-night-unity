using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Save;
using DarkNights.Runtime.Save;

namespace DarkNights.Tests
{
    /// <summary>
    /// 在每次独立的测试目录验证真实文件替换、锁冲突、取消、损坏输入和并发保存。
    /// 失败证据留在忽略的 artifacts，不读写玩家目录；同一组检查用于 .NET 与 Unity Editor 的文件系统实现。
    /// </summary>
    public static class GameSaveFileScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            string directory = Path.Combine(RuleScenario.RepositoryRoot, "artifacts/migration/save-store", Guid.NewGuid().ToString("N"));
            var store = new GameSaveStore(directory, catalog, layout);
            var codec = new GameSaveJson(catalog, layout);
            var active = codec.ImportLegacy(RuleScenario.Fixture("legacy-v1.json").ToString());
            SessionSnapshot first = SnapshotMapper.Capture(active);
            string firstText = codec.Serialize(first);
            string path = Path.Combine(directory, "slot-00.dnsave.json");
            check(!Directory.Exists(directory), "Store construction does not create storage directories");
            store.Save(0, first);
            check(File.ReadAllText(path) == firstText && codec.Serialize(SnapshotMapper.Capture(store.Load(0))) == firstText,
                "First save writes and restores the complete frozen world");
            RuleScenario.Step(active, 1);
            SessionSnapshot second = SnapshotMapper.Capture(active);
            string secondText = codec.Serialize(second);
            store.Save(0, second);
            check(File.ReadAllText(path) == secondText && codec.Serialize(SnapshotMapper.Capture(store.Load(0))) == secondText,
                "Existing save is atomically replaced with the complete next snapshot");
            check(!Directory.GetFiles(directory, "*.tmp").Any(), "Successful saves leave no temporary files");

            bool lockedFailure = false;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try { store.Save(0, first); }
                catch (IOException) { lockedFailure = true; }
            }
            check(lockedFailure && File.ReadAllText(path) == secondText && !Directory.GetFiles(directory, "*.tmp").Any(),
                "Commit sharing violation preserves original save and removes its temporary file");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                bool saveCancelled = false;
                bool loadCancelled = false;
                try { store.Save(0, first, cancellation.Token); }
                catch (OperationCanceledException) { saveCancelled = true; }
                try { store.Load(0, cancellation.Token); }
                catch (OperationCanceledException) { loadCancelled = true; }
                check(saveCancelled && loadCancelled && File.ReadAllText(path) == secondText,
                    "Cancelled storage operations preserve existing file");
            }
            var invalidSnapshot = RuleScenario.Fixture("legacy-v1.json");
            invalidSnapshot["economy"]["resources"]["food"] = -1;
            check(GameSaveScenarios.Rejected(() => store.Save(0, LegacySnapshotJson.Deserialize(invalidSnapshot.ToString()))) &&
                File.ReadAllText(path) == secondText,
                "Invalid snapshot fails before touching existing save");
            foreach (int invalid in new[] { -1, GameSaveStore.SlotCount, int.MaxValue })
            {
                check(GameSaveScenarios.Rejected(() => store.Save(invalid, first)) &&
                    GameSaveScenarios.Rejected(() => store.Load(invalid)), "Out of range slot cannot select a file path");
            }
            bool missing = false;
            try { store.Load(9); }
            catch (FileNotFoundException) { missing = true; }
            check(missing && codec.Serialize(SnapshotMapper.Capture(active)) == secondText,
                "Missing slot reports failure without changing active world");

            string corruptPath = Path.Combine(directory, "slot-01.dnsave.json");
            foreach (byte[] invalid in new[]
            {
                Array.Empty<byte>(), Encoding.UTF8.GetBytes("{\"format\":"), new byte[] { 0xC3, 0x28 },
                Encoding.UTF8.GetBytes(firstText + "{}"), new byte[GameSaveJson.MaximumBytes + 1]
            })
            {
                File.WriteAllBytes(corruptPath, invalid);
                check(GameSaveScenarios.Rejected(() => store.Load(1)) && codec.Serialize(SnapshotMapper.Capture(active)) == secondText,
                    "Empty, truncated, invalid UTF8, trailing or oversized file preserves active world");
            }
            string blockedRoot = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blockedRoot, "preserve");
            bool blocked = false;
            try { new GameSaveStore(blockedRoot, catalog, layout).Save(0, first); }
            catch (IOException) { blocked = true; }
            check(blocked && File.ReadAllText(blockedRoot) == "preserve", "Directory creation failure preserves existing path");

            Task.WaitAll(Task.Run(() => store.Save(0, first)), Task.Run(() => store.Save(0, second)));
            string concurrent = codec.Serialize(SnapshotMapper.Capture(store.Load(0)));
            check((concurrent == firstText || concurrent == secondText) && !Directory.GetFiles(directory, "*.tmp").Any(),
                "Concurrent saves serialize and leave one complete loadable world");
            string orphan = path + ".interrupted.tmp";
            File.WriteAllText(orphan, "incomplete");
            check(codec.Serialize(SnapshotMapper.Capture(store.Load(0))) == concurrent && File.ReadAllText(orphan) == "incomplete",
                "Interrupted-save orphan is never loaded or removed by another operation");
            check(codec.Serialize(first) == firstText, "Frozen snapshot remains stable across background file operations");
        }
    }
}
