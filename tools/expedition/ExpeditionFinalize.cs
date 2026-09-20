using System;
using System.IO;
using System.Linq;
using UnityEditor;

/// <summary>快验结束后恢复临时 Editor 选项，仅清理本轮测试框架生成的性能 JSON。</summary>
public static class ExpeditionFinalize
{
    public static string Run()
    {
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
        const string folder = "Assets/Resources";
        long released = 0;
        if (Directory.Exists(folder))
        {
            var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            if (files.All(f => Path.GetFileName(f).StartsWith("PerformanceTestRun", StringComparison.Ordinal)))
            {
                released = files.Sum(f => new FileInfo(f).Length);
                if (!AssetDatabase.DeleteAsset(folder)) throw new IOException("Cannot remove generated performance resources.");
            }
        }
        AssetDatabase.SaveAssets();
        return "Editor play options restored; generated performance JSON bytes removed: " + released;
    }
}
