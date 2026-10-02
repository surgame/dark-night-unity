using System;
using System.IO;
using System.Text;

namespace DarkNights.Tests
{
    /// <summary>保存存储回归产生的独占临时文件；退出时校验路径和链接，移入工程待清理目录并记账，不删除存档或共享临时内容。</summary>
    internal static class StorageTestArtifacts
    {
        internal static void Preserve(string directory)
        {
            if (!Directory.Exists(directory)) return;
            string source = Path.GetFullPath(directory);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string name = Path.GetFileName(source);
            if (!source.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith("dark-nights-storage-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(name.Substring("dark-nights-storage-".Length), "N", out _))
                throw new IOException("拒绝移动来源不明的测试目录。");
            var pending = new System.Collections.Generic.Stack<string>();
            pending.Push(source);
            long bytes = 0;
            while (pending.Count > 0)
            {
                string path = pending.Pop();
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("测试目录包含链接，保留原处。");
                if (Directory.Exists(path))
                    foreach (string child in Directory.EnumerateFileSystemEntries(path)) pending.Push(child);
                else bytes += new FileInfo(path).Length;
            }
            string artifacts = Path.GetFullPath(Path.Combine(RuleScenario.RepositoryRoot, "artifacts"));
            string batch = Path.Combine(artifacts, "待清理", DateTime.Today.ToString("yyyyMMdd") + "-session-storage-tests");
            string target = Path.GetFullPath(Path.Combine(batch, name));
            if (!target.StartsWith(artifacts + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(target)) throw new IOException("测试归档目标非法或已存在。");
            Directory.CreateDirectory(batch);
            Directory.Move(source, target);
            File.WriteAllText(Path.Combine(target, "清单.md"),
                "# 存储回归临时文件\n\n原路径：" + source + "\n\n归档路径：" + target +
                "\n\n体积：" + bytes + " 字节；产生任务：SessionStorageScenarios。" +
                "\n\n原因：保存、损坏存档及失败恢复的测试数据，不是玩家原始存档。" +
                "\n\n保留直到本批验收结束；永久删除需用户另行明确同意。归档未释放磁盘空间。\n", Encoding.UTF8);
        }
    }
}
