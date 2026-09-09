using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DarkNights.Tools.NetworkReview
{
    /// <summary>
    /// 用当前框架的真实命令特性和生成器 DLL 执行隔离 Roslyn 探针。
    /// 仅检查生成输出，不编译整个框架，也不运行 Unity 的导入或网络织入。
    /// </summary>
    internal static class GeneratorProbe
    {
        public static object Run(string frameworkPath, string outputPath, bool legacyNamespace)
        {
            string source = File.ReadAllText(Path.Combine(frameworkPath,
                "Runtime/NetworkCommands/NetworkCommandAttribute.cs"));
            source += @"
namespace Probe
{
    [GameCore.NetworkCommands.NetworkCommand(
        GameCore.NetworkCommands.CommandChannel.Reliable,
        GameCore.NetworkCommands.CommandScope.ServerOnly)]
    public partial record ExampleCommand
    {
        public int Value { get; set; }
    }
}";
            if (legacyNamespace)
            {
                source = source.Replace("GameCore.NetworkCommands", "YYRuntime.NetworkCommands");
            }
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);
            var syntax = CSharpSyntaxTree.ParseText(source, parseOptions);
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("CommandGeneratorProbe",
                new[] { syntax }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            string dll = Path.Combine(frameworkPath,
                "Runtime/NetworkCommands/SourceGenerators/YYGame.NetworkCommand.Generator.dll");
            var generators = Assembly.LoadFrom(dll).GetTypes()
                .Where(type => !type.IsAbstract &&
                    (typeof(IIncrementalGenerator).IsAssignableFrom(type) ||
                     typeof(ISourceGenerator).IsAssignableFrom(type)))
                .Select(type => Activator.CreateInstance(type))
                .Select(generator => generator is IIncrementalGenerator incremental
                    ? incremental.AsSourceGenerator() : (ISourceGenerator)generator)
                .ToArray();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generators,
                parseOptions: parseOptions);
            driver = driver.RunGenerators(compilation);
            var result = driver.GetRunResult();
            Directory.CreateDirectory(outputPath);
            foreach (var generated in result.GeneratedTrees)
            {
                File.WriteAllText(Path.Combine(outputPath, Path.GetFileName(generated.FilePath)),
                    generated.GetText().ToString());
            }
            return new
            {
                attributeNamespace = legacyNamespace ? "YYRuntime.NetworkCommands" : "GameCore.NetworkCommands",
                generatorCount = generators.Length,
                outputCount = result.GeneratedTrees.Length,
                diagnostics = result.Diagnostics.Select(value => value.ToString()).ToArray(),
                outputs = result.GeneratedTrees.Select(tree => new
                {
                    name = Path.GetFileName(tree.FilePath),
                    text = tree.GetText().ToString()
                }).ToArray()
            };
        }
    }
}
