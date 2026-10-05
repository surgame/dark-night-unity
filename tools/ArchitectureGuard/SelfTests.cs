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
            sources["Core/Config/Good.cs"] = valid.Replace("public class Good { }",
                "public class Good {\n/// <summary>内部辅助值，不形成第二个主要类型或运行状态所有者。</summary>\nprivate struct Inner { } }");
            if (SourceRules.Check(sources).Count != 0) throw new Exception("Guard rejects a nested helper as a second main type");
            sources["Core/Config/Good.cs"] = "#if UNITY_EDITOR || DEVELOPMENT_BUILD\n" + valid + "\n#endif";
            if (SourceRules.Check(sources).Count != 0) throw new Exception("Guard ignores development-only handwritten types");
            string[] invalid =
            {
                valid.Replace("public class Good { }", "public class Good { public object Read() => System.IO.File.ReadAllText(\"x\"); }"),
                "using Files = System.IO.File;\n" + valid.Replace("public class Good { }", "public class Good { public object Read() => Files.ReadAllText(\"x\"); }"),
                "using UnityEngine;\n" + valid,
                "using DarkNights.Runtime.Config;\n" + valid,
                valid.Replace("public class Good", "public class Wrong"),
                valid.Replace("public class Good { }", "public class Good { } public class Second { }"),
                valid.Replace("public class Good { }", "public class Good { private struct Undocumented { } }"),
                valid.Replace("/// <summary>", "// <summary>"),
                valid.Replace("DarkNights.Core.Config", "DarkNights.Core.Other"),
                valid + new string('\n', 301),
                valid.Replace("public class Good { }", "public class Good { public DarkNights.Core.Logic.GameSession Value; }"),
                valid.Replace("public class Good { }", "public class Good { public DarkNights.Runtime.Save.LegacySnapshotJson Value; }")
            };
            foreach (string content in invalid)
            {
                sources["Core/Config/Good.cs"] = content;
                if (SourceRules.Check(sources).Count == 0) throw new Exception("Guard accepted an invalid fixture");
            }
            var view = new Dictionary<string, string>
            {
                ["Core/Logic/InternalRule.cs"] = "namespace DarkNights.Core.Logic {\n/// <summary>规则测试夹具，代表表现层不能直接引用的内部计算。</summary>\npublic class InternalRule { } }",
                ["View/BadView.cs"] = "namespace DarkNights.View {\n/// <summary>表现层测试夹具，故意访问了禁止的内部规则类型。</summary>\npublic class BadView { public DarkNights.Core.Logic.InternalRule Rule; } }"
            };
            if (SourceRules.Check(view).Count == 0) throw new Exception("Guard accepted mutable world access from View");
            return invalid.Length + 4;
        }
    }
}
