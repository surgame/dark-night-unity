using System;
using System.IO;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>本机启动的存储位置选择；快速局与正式槽位隔离，断开后恢复正式位置，不拥有存档内容。</summary>
    internal sealed class SessionSaveLocation
    {
        private string normal;
        public string Current { get; private set; }
        public void Initialize(string[] arguments)
        {
            int index = Array.IndexOf(arguments, "--dn-save-dir");
            normal = Path.Combine(Path.GetFullPath(index >= 0 && index + 1 < arguments.Length
                ? arguments[index + 1] : Path.Combine(Application.persistentDataPath, "Saves")),
                "v" + DarkNights.Runtime.Save.ObjectWorldSaveJson.FormatVersion);
            Select(null);
        }
        public void Select(string testId)
        {
            if (testId != null && testId != DarkNights.Runtime.Objects.QuickTestPreset.LandedPickaxeId &&
                testId != DarkNights.Runtime.Objects.QuickTestPreset.EmbeddedMineralsId)
                throw new ArgumentException("未知快速测试存储项。", nameof(testId));
            Current = testId == null ? normal : Path.Combine(normal, "QuickTests", testId);
        }
    }
}
