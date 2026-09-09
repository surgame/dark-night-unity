using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MemoryPack;
using R3;

namespace DarkNights.Tools.NetworkReview
{
    /// <summary>
    /// 对复评发现的依赖语义执行小型对照实验，输出原始观察而不是联机验收结果。
    /// .NET 8 是评估工具宿主；不代表 Unity 使用相同依赖版本或已能构建。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: NetworkReviewProbe <YYGC path> <report.json>");
                return 2;
            }

            var nullInitial = Observe(null);
            var seededInitial = Observe(new ProbePayload { Value = 0 });
            var concrete = new ProbePayload { Value = 42 };
            var concreteBytes = MemoryPackSerializer.Serialize(concrete);
            var roundtrip = MemoryPackSerializer.Deserialize<ProbePayload>(concreteBytes);
            string interfaceError = null;
            try
            {
                SerializeAs<IProbePayload>(concrete);
            }
            catch (MemoryPackSerializationException exception)
            {
                interfaceError = exception.Message;
            }

            string reportPath = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var generator = GeneratorProbe.Run(Path.GetFullPath(args[0]),
                Path.Combine(Path.GetDirectoryName(reportPath), "command-generator-output"), false);
            var legacyGenerator = GeneratorProbe.Run(Path.GetFullPath(args[0]),
                Path.Combine(Path.GetDirectoryName(reportPath), "legacy-command-generator-output"), true);
            var result = new
            {
                observedAt = DateTimeOffset.Now,
                scope = "Dependency semantics and actual YYGC command-generator output only; no Unity/FishNet runtime",
                runtime = Environment.Version.ToString(),
                r3Package = "1.3.0",
                memoryPackPackage = "1.21.4",
                whereThenSkipWithNull = nullInitial,
                whereThenSkipWithSeed = seededInitial,
                concreteRoundtrip = roundtrip.Value,
                unregisteredInterfaceException = interfaceError,
                commandGenerator = generator,
                legacyNamespaceControl = legacyGenerator
            };
            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(reportPath, json);
            Console.WriteLine(json);
            return nullInitial.Count == 1 && nullInitial[0] == 2 &&
                seededInitial.Count == 2 && seededInitial[0] == 1 &&
                roundtrip.Value == 42 && interfaceError != null ? 0 : 1;
        }

        private static List<int> Observe(ProbePayload initial)
        {
            var received = new List<int>();
            using var state = new ReactiveProperty<ProbePayload>(initial);
            using var subscription = state.Where(value => value != null).Skip(1)
                .Subscribe(value => received.Add(value.Value));
            state.OnNext(new ProbePayload { Value = 1 });
            state.OnNext(new ProbePayload { Value = 2 });
            return received;
        }

        private static byte[] SerializeAs<T>(T value)
        {
            return MemoryPackSerializer.Serialize(value);
        }
    }
}
