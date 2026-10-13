using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>工程本机调试历史的显式文件入口；旧记录不覆盖或自动裁剪，新记录与最近预览通过同目录临时文件安装。</summary>
    public static class FlashlightDebugRecordStore
    {
        public const string Root = "UserSettings/DarkNights/LightingDebug";
        public static string RecordPath(FlashlightDebugRecord record)
        {
            if (record.Id.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new ArgumentException("历史记录身份非法。");
            return Path.Combine(Root, "records", record.Id + ".json");
        }
        public static FlashlightPreviewSettings LastPreview()
        {
            string path = Path.Combine(Root, "last-preview.json");
            if (!File.Exists(path)) return new FlashlightPreviewSettings();
            var result = JsonUtility.FromJson<FlashlightPreviewSettings>(File.ReadAllText(path));
            if (result == null) throw new InvalidDataException("最近预览配置为空。");
            result.Validate(); return result;
        }
        public static IReadOnlyList<string> Files()
        {
            string path = Path.Combine(Root, "records");
            return Directory.Exists(path) ? Directory.GetFiles(path, "*.json").OrderByDescending(Path.GetFileName).ToArray() : Array.Empty<string>();
        }
        public static FlashlightDebugRecord Read(string path)
        {
            string full = Path.GetFullPath(path), approved = Path.GetFullPath(Path.Combine(Root, "records")) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(approved, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("记录路径不属于本工程调试历史。");
            var record = JsonUtility.FromJson<FlashlightDebugRecord>(File.ReadAllText(full));
            if (record == null) throw new InvalidDataException("调试记录为空。");
            record.Validate(); return record;
        }
        public static void Save(FlashlightDebugRecord record)
        {
            record.Validate(); string destination = RecordPath(record);
            if (File.Exists(destination)) throw new IOException("旧调试记录受保护，不能覆盖。");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string pending = destination + ".pending";
            File.WriteAllText(pending, JsonUtility.ToJson(record, true));
            string last = Path.Combine(Root, "last-preview.json"), next = last + "." + record.Id + ".pending";
            File.WriteAllText(next, JsonUtility.ToJson(record.Preview, true));
            bool installed = false;
            try
            {
                File.Move(pending, destination); installed = true;
                if (File.Exists(last)) File.Replace(next, last, last + "." + record.Id + ".before");
                else File.Move(next, last);
            }
            catch
            {
                if (installed) File.Move(destination, destination + ".failed");
                throw;
            }
        }
    }
}
