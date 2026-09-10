using System;
using System.Collections.Generic;

namespace DarkNights.Tools.ArchitectureGuard
{
    /// <summary>
    /// 用独立的违规样例证明守卫确实阻止磁盘访问、跨层调用和结构违规。
    /// 夹具只存在内存，不向正式目录写入错误脚本，也不替换游戏验证结果。
    /// </summary>
    internal static class SelfTests
    {
        public static int Run()
        {
            string valid = "namespace DarkNights.Core.Config {\n/// <summary>独立测试配置，不保存可写会话状态，供结构验证使用。</summary>\npublic class Good { }\n}";
            var sources = new Dictionary<string, string> { ["Core/Config/Good.cs"] = valid };
            if (SourceRules.Check(sources).Count != 0) throw new Exception("Guard rejects valid C#9 source");
            string[] invalid =
            {
                valid.Replace("public class Good { }", "public class Good { public object Read() => System.IO.File.ReadAllText(\"x\"); }"),
                "using Files = System.IO.File;\n" + valid.Replace("public class Good { }", "public class Good { public object Read() => Files.ReadAllText(\"x\"); }"),
                "using UnityEngine;\n" + valid,
                "using DarkNights.Runtime.Config;\n" + valid,
                valid.Replace("public class Good", "public class Wrong"),
                valid.Replace("/// <summary>", "// <summary>"),
                valid.Replace("DarkNights.Core.Config", "DarkNights.Core.Other"),
                valid + new string('\n', 301)
            };
            foreach (string content in invalid)
            {
                sources["Core/Config/Good.cs"] = content;
                if (SourceRules.Check(sources).Count == 0) throw new Exception("Guard accepted an invalid fixture");
            }
            var view = new Dictionary<string, string>
            {
                ["Core/Logic/WorldState.cs"] = "namespace DarkNights.Core.Logic {\n/// <summary>权威世界测试夹具，代表不能被表现层读取的可写实例。</summary>\npublic class WorldState { } }",
                ["View/BadView.cs"] = "namespace DarkNights.View {\n/// <summary>表现层测试夹具，故意访问了禁止的可写世界状态类型。</summary>\npublic class BadView { public DarkNights.Core.Logic.WorldState State; } }"
            };
            if (SourceRules.Check(view).Count == 0) throw new Exception("Guard accepted mutable world access from View");
            return invalid.Length + 2;
        }
    }
}
