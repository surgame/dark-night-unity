using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using DarkNights.Core.Save;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 房主存储适配：在注入的专用目录使用固定槽位，同实例操作串行，读写只处理完整世界快照。
    /// 调用方在模拟边界捕获冻结数据后才可交给后台；临时文件落盘再原子替换，失败不删除原档，也不接管活动会话。
    /// </summary>
    public sealed class GameSaveStore
    {
        public const int SlotCount = 10;
        private readonly string directory;
        private readonly ObjectWorldSaveJson codec;
        private readonly object gate = new object();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public GameSaveStore(string directory, ObjectWorldSaveJson codec)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is required.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public void Save(int slot, SessionSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            string destination = SlotPath(slot);
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = Utf8.GetBytes(codec.Serialize(snapshot));
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(directory);
                string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                bool created = false;
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        created = true;
                        output.Write(bytes, 0, bytes.Length);
                        output.Flush(true);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (File.Exists(destination)) File.Replace(temporary, destination, null);
                    else File.Move(temporary, destination);
                    // 提交后不得再报告取消，否则调用方无法判断实际写入是否成功。
                    created = false;
                }
                finally
                {
                    if (created)
                    {
                        try { File.Delete(temporary); }
                        catch (IOException) { /* 清理失败保留本次孤立临时文件，不掩盖原始保存错误。 */ }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
        }

        public string Read(int slot, CancellationToken cancellationToken = default)
        {
            string source = SlotPath(slot);
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string text;
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (input.Length == 0 || input.Length > ObjectWorldSaveJson.MaximumBytes)
                        throw new FormatException("Save is empty or too large.");
                    var bytes = new byte[(int)input.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int count = input.Read(bytes, offset, bytes.Length - offset);
                        if (count == 0) throw new EndOfStreamException("Save ended before its declared size.");
                        offset += count;
                    }
                    if (input.ReadByte() != -1) throw new IOException("Save grew while reading.");
                    text = Utf8.GetString(bytes);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return text;
            }
        }

        private string SlotPath(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(directory, "slot-" + slot.ToString("D2", CultureInfo.InvariantCulture) + ".dnsave.json");
        }
    }
}
