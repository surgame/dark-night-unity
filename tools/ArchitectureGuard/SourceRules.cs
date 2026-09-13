using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DarkNights.Tools.ArchitectureGuard
{
    /// <summary>
    /// 用 Roslyn 检查手写类型、命名空间和项目依赖语义，包含 using 别名和全限定名。
    /// 不检查第三方或生成缓存；真实 API 可用性另由 Unity 与 netstandard2.1 构建验证。
    /// </summary>
    internal static class SourceRules
    {
        public static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>
        {
            ["Core"] = new[] { "Core" },
            ["Runtime"] = new[] { "Core", "Runtime" },
            ["View"] = new[] { "Core", "View" },
            ["Entry"] = new[] { "Core", "Runtime", "View", "Entry" },
            ["Editor"] = new[] { "Core", "Runtime", "View", "Entry", "Editor" },
            ["Tests"] = new[] { "Core", "Runtime", "View", "Entry", "Editor", "Tests" }
        };

        public static List<string> Check(IReadOnlyDictionary<string, string> sources)
        {
            var trees = sources.Select(pair => CSharpSyntaxTree.ParseText(pair.Value,
                new CSharpParseOptions(LanguageVersion.CSharp9, DocumentationMode.Parse), pair.Key)).ToArray();
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("ArchitectureInspection", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var errors = new List<string>();
            var declarations = new HashSet<string>();
            foreach (var tree in trees)
            {
                string path = tree.FilePath.Replace('\\', '/');
                string layer = path.Split('/')[0];
                var root = tree.GetRoot();
                var model = compilation.GetSemanticModel(tree);
                Action<string> fail = message => errors.Add(path + ": " + message);
                if (!Allowed.ContainsKey(layer)) { fail("Unknown layer"); continue; }
                string[] retired = { "GameSession", "WorldState", "SessionWorld", "LegacySessionWorld", "LegacySnapshotJson", "LegacyDisplayState", "GameSaveJson" };
                if (root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(n => retired.Contains(n.Identifier.Text)) ||
                    root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Any(n => retired.Contains(n.Identifier.Text)))
                    fail("Retired runtime model or legacy save entry is forbidden");
                if (layer == "Core" && (path.StartsWith("Core/Logic/Entities/") || path.StartsWith("Core/Logic/Commands/") ||
                    path.StartsWith("Core/Logic/Systems/"))) fail("Core cannot own runtime entities, command execution or system lifecycles");
                int lines = tree.GetText().Lines.Count - (sources[tree.FilePath].EndsWith("\n") ? 1 : 0);
                if (lines > 300) fail("Handwritten source exceeds 300 lines");
                var types = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToArray();
                if (types.Length != 1) fail("One main named type per file is required");
                foreach (var type in types)
                {
                    if (type.Identifier.Text != Path.GetFileNameWithoutExtension(path)) fail("Type must match filename");
                    string ns = model.GetDeclaredSymbol(type)?.ContainingNamespace.ToDisplayString();
                    string expected = "DarkNights." + path.Substring(0, path.LastIndexOf('/')).Replace('/', '.');
                    if (ns != expected) fail("Namespace must be " + expected);
                    if (!declarations.Add(ns + "." + type.Identifier.Text)) fail("Multiple handwritten bodies for one type");
                    bool summary = type.GetLeadingTrivia().Select(item => item.GetStructure())
                        .OfType<DocumentationCommentTriviaSyntax>().SelectMany(doc => doc.Content.OfType<XmlElementSyntax>())
                        .Any(xml => xml.StartTag.Name.ToString() == "summary" && xml.Content.ToFullString().Trim().Length >= 15);
                    if (!summary) fail("Meaningful Chinese XML summary is required");
                }
                foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    ISymbol symbol = model.GetAliasInfo(name)?.Target ?? model.GetSymbolInfo(name).Symbol;
                    string ns = symbol is INamespaceSymbol space ? space.ToDisplayString() : symbol?.ContainingNamespace?.ToDisplayString();
                    if (ns != null) Dependency(layer, ns, fail);
                }
                // 外部引擎符号未装入此工具的语义模型时，仍检查明确的命名空间引用。
                foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                    Dependency(layer, directive.Name.ToString().Replace("global::", ""), fail);
                if (root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().Any()) fail("C# 9 block namespace required");
                foreach (var diagnostic in tree.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
                    fail(diagnostic.ToString());
            }
            return errors.Distinct().ToList();
        }

        private static void Dependency(string layer, string ns, Action<string> fail)
        {
            string target = ns.Split('.').ElementAtOrDefault(1) ?? "";
            if (ns.StartsWith("DarkNights.") && !Allowed[layer].Contains(target)) fail("Forbidden dependency: " + ns);
            if (layer == "View" && (ns.StartsWith("DarkNights.Core.Logic") || ns.StartsWith("DarkNights.Core.Save")))
                fail("View must use Config/ViewData, not mutable world or save data: " + ns);
            if (layer == "Core")
            {
                string[] forbidden = { "Unity", "Godot", "GameCore", "FishNet", "R3", "VitalRouter", "Cysharp", "Newtonsoft",
                    "System.IO", "System.Net", "System.Threading", "System.Reflection", "Runtime" };
                if (forbidden.Any(prefix => ns == prefix || ns.StartsWith(prefix + ".") || (prefix == "Unity" && ns.StartsWith("Unity"))))
                    fail("Core cannot access: " + ns);
            }
            if (layer != "Editor" && layer != "Tests" && ns.StartsWith("UnityEditor")) fail("Editor API in runtime code");
        }
    }
}
