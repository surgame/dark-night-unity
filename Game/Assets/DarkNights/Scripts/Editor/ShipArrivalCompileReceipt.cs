using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace DarkNights.Editor
{
    /// <summary>本次飞船到达候选的有限编译回执；仅在显式请求文件存在时核验已导入源码和程序集，不触发构建或 Play。</summary>
    [InitializeOnLoad]
    public static class ShipArrivalCompileReceipt
    {
        private const string DirectoryPath = "../artifacts/ship-arrival-20261003";
        static ShipArrivalCompileReceipt() { EditorApplication.delayCall += Write; }

        private static void Write()
        {
            string requestPath = Path.GetFullPath(Path.Combine(DirectoryPath, "compile-request.json"));
            string reportPath = Path.GetFullPath(Path.Combine(DirectoryPath, "unity-compile.json"));
            if (!File.Exists(requestPath) || File.Exists(reportPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Write; return; }
            var request = JObject.Parse(File.ReadAllText(requestPath));
            var sources = (JObject)request["sources"];
            var errors = new JArray();
            foreach (var source in sources.Properties())
                if (!File.Exists(source.Name) || Hash(source.Name) != (string)source.Value)
                    errors.Add("Source changed: " + source.Name);
            var assemblies = new JArray();
            var imported = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (string name in new[] { "Core", "Runtime", "View", "Entry", "Editor", "Tests" })
            {
                var assembly = imported.FirstOrDefault(a => a.name == "DarkNights." + name);
                if (assembly == null || !File.Exists(assembly.outputPath)) { errors.Add("Missing assembly: " + name); continue; }
                assemblies.Add(new JObject { ["name"] = assembly.name, ["path"] = Path.GetFullPath(assembly.outputPath),
                    ["sha256"] = Hash(assembly.outputPath), ["modifiedUtc"] = File.GetLastWriteTimeUtc(assembly.outputPath).ToString("O") });
            }
            var report = new JObject { ["success"] = errors.Count == 0, ["scope"] = "Unity Editor import and script compilation; no Play or Player",
                ["editor"] = UnityEngine.Application.unityVersion, ["finishedUtc"] = DateTime.UtcNow.ToString("O"),
                ["assemblies"] = assemblies, ["errors"] = errors };
            File.WriteAllText(reportPath + ".tmp", report.ToString()); File.Move(reportPath + ".tmp", reportPath);
        }
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path); using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
