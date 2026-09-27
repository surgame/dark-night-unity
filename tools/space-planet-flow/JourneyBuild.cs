using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;

/// <summary>复用 Local 单一 Editor 的航程 Player 构建；后端和运行身份显式指定，结果原子落盘并拒绝覆盖旧产物。</summary>
public static class JourneyBuild
{
    public static string Run(string backend, string label)
    {
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Editor is occupied.");
        if (backend != "mono" && backend != "il2cpp") throw new ArgumentException("Unknown backend.");
        string root = Path.GetFullPath("../artifacts/space-planet-flow");
        string output = Path.Combine(root, "player-" + backend + "-" + label, "DarkNights.exe");
        string reportPath = Path.Combine(root, "build-" + backend + "-" + label + ".json");
        if (Directory.Exists(Path.GetDirectoryName(output)) || File.Exists(reportPath)) throw new IOException("Build identity exists.");
        Directory.CreateDirectory(root);
        var report = new JObject { ["startedUtc"] = DateTime.UtcNow.ToString("O"), ["backend"] = backend,
            ["status"] = "running", ["player"] = output };
        File.WriteAllText(reportPath, report.ToString());
        try
        {
            var method = typeof(DarkNights.Editor.GamePlayerBuild).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, new object[] { backend == "mono" ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP,
                backend, BuildOptions.Development, output });
            report["status"] = "completed"; report["success"] = true;
        }
        catch (Exception error)
        { report["status"] = "failed"; report["success"] = false; report["error"] = error.ToString(); }
        report["finishedUtc"] = DateTime.UtcNow.ToString("O");
        File.WriteAllText(reportPath + ".tmp", report.ToString()); File.Replace(reportPath + ".tmp", reportPath, null);
        return report.ToString();
    }
}
