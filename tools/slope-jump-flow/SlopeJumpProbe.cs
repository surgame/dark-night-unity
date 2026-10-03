using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DarkNights.Tests;
using NUnit.Framework;

/// <summary>有限源码回归入口；直接执行与 Editor 相同的运动测试主体，输出逐项结果，不启动 Unity 或修改资产。</summary>
internal static class SlopeJumpProbe
{
    public static int Main(string[] args)
    {
        if (args.Length > 0) { SlopeJumpMetrics.Write(args[0]); return 0; }
        int passed = 0, failed = 0;
        foreach (var type in new[] { typeof(TerrainSlopeRecoveryTests), typeof(TerrainJumpFlowTests) })
        {
            object fixture = Activator.CreateInstance(type);
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var cases = method.GetCustomAttributes(typeof(TestCaseAttribute), true).Cast<TestCaseAttribute>().Select(c => c.Arguments).ToList();
                if (method.IsDefined(typeof(TestAttribute), true)) cases.Add(Array.Empty<object>());
                foreach (var arguments in cases)
                {
                    try
                    {
                        method.Invoke(fixture, arguments); passed++;
                        Console.WriteLine(JsonSerializer.Serialize(new { test = type.Name + "." + method.Name, arguments, passed = true }));
                    }
                    catch (TargetInvocationException error)
                    {
                        failed++;
                        Console.WriteLine(JsonSerializer.Serialize(new { test = type.Name + "." + method.Name, arguments, passed = false, error = error.InnerException.ToString() }));
                    }
                }
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new { summary = true, passed, failed }));
        return failed == 0 ? 0 : 1;
    }
}
