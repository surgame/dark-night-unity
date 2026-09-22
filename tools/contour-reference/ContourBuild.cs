using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>单次 Mono 构建入口；复用当前 Editor，结果落独立原子文件，不启动第二个 Unity。</summary>
public static class ContourBuild
{
    public static string Run()
    {
        string result = Path.GetFullPath("../artifacts/contour/build-result-r3.json");
        if (File.Exists(result)) throw new InvalidOperationException("本批已有构建结果，请先核对其身份。");
        if (BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("已有 Player 构建正在执行。");
        Directory.CreateDirectory(Path.GetDirectoryName(result));
        File.WriteAllText(Path.GetFullPath("../artifacts/contour/build-started-r3.json"), DateTime.UtcNow.ToString("O"));
            var report = new JObject { ["startedUtc"] = DateTime.UtcNow.ToString("O"), ["backend"] = "Mono" };
            try
            {
                string output = Path.GetFullPath("../artifacts/contour/player-mono-r3/DarkNights.exe");
                typeof(DarkNights.Editor.GamePlayerBuild).GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { ScriptingImplementation.Mono2x, "mono", BuildOptions.Development, output });
                report["success"] = true; report["player"] = output;
            }
            catch (Exception error) { report["success"] = false; report["error"] = error.ToString(); }
            report["finishedUtc"] = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(Path.GetDirectoryName(result));
            File.WriteAllText(result + ".tmp", report.ToString()); File.Move(result + ".tmp", result);
        return report.ToString();
    }
}
