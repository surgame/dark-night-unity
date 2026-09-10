using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DarkNights.Tools.ArchitectureGuard
{
    /// <summary>
    /// 执行正式源码与 asmdef 守卫并输出可归档摘要。仅检查已实施的目录，不要求空层占位；
    /// Core 的 C#9 / .NET Standard 2.1 实际构建由 CoreBuild 项目负责，两项均需通过。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
            string scripts = Path.Combine(root, "Game/Assets/DarkNights/Scripts");
            var sources = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/Generated/"))
                .ToDictionary(path => Path.GetRelativePath(scripts, path).Replace('\\', '/'), File.ReadAllText);
            var errors = SourceRules.Check(sources);
            int selfTests = SelfTests.Run();
            foreach (string file in Directory.GetFiles(scripts, "*.asmdef", SearchOption.AllDirectories))
                CheckAssembly(file, errors);
            string res = Path.Combine(root, "Game/Assets/DarkNights/Res");
            if (Directory.Exists(res) && Directory.GetFiles(res, "*.cs", SearchOption.AllDirectories).Length != 0)
                errors.Add("Code must not be placed under Res");
            string report = Path.Combine(root, "artifacts/migration/architecture.json");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                passed = errors.Count == 0, handwrittenFiles = sources.Count, selfTests, errors,
                scope = "Formal Scripts only; external API compilation verified separately by Unity and CoreBuild"
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Architecture: files={sources.Count} selfTests={selfTests} errors={errors.Count}; {report}");
            foreach (string error in errors) Console.Error.WriteLine(error);
            return errors.Count == 0 ? 0 : 1;
        }

        private static void CheckAssembly(string path, List<string> errors)
        {
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                var value = document.RootElement;
                string layer = new DirectoryInfo(Path.GetDirectoryName(path)).Name;
                string name = value.GetProperty("name").GetString();
                if (!SourceRules.Allowed.ContainsKey(layer) || name != "DarkNights." + layer)
                {
                    errors.Add(path + ": unexpected assembly name or directory");
                    return;
                }
                foreach (var item in value.GetProperty("references").EnumerateArray())
                {
                    string reference = item.GetString();
                    if (layer == "Core" || (reference.StartsWith("DarkNights.") &&
                        !SourceRules.Allowed[layer].Select(entry => "DarkNights." + entry).Contains(reference)))
                        errors.Add(path + ": forbidden assembly reference " + reference);
                }
                if (layer == "Core" && (!value.GetProperty("noEngineReferences").GetBoolean() ||
                    !value.GetProperty("overrideReferences").GetBoolean() || value.GetProperty("precompiledReferences").GetArrayLength() != 0))
                    errors.Add(path + ": Core must exclude engine and plugin references");
                if (layer == "Editor" || layer == "Tests")
                {
                    if (!value.TryGetProperty("includePlatforms", out var platforms) || platforms.GetArrayLength() != 1 || platforms[0].GetString() != "Editor")
                        errors.Add(path + ": Editor and Tests must not enter Player");
                }
            }
        }
    }
}
