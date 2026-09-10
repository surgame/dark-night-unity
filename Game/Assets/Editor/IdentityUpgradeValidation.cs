using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Singletons;
using GameCore.UI.UGUI;
using Runtime.Middlewares;
using UnityEditor;
using UnityEngine;

namespace YYGC.IdentityValidation.Editor
{
    /// <summary>在隔离 Unity 宿主执行旧 DLL 和资产验收；基线只读，失败不重新生成期望结果。</summary>
    public static class IdentityUpgradeValidation
    {
        public static void Run()
        {
            string baseline = Argument("-identityBaseline", "../artifacts/identity-baseline");
            string output = Argument("-identityOutput", "../artifacts/identity-upgrade.json");
            var previous = ObjectDefinitionDatabase.Instance;
            var database = ScriptableObject.CreateInstance<ObjectDefinitionDatabase>();
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            try
            {
                string[] current = PublicApi();
                string[] frozen = File.ReadAllLines(Path.Combine(baseline, "public-api.txt"));
                string[] missing = frozen.Except(current, StringComparer.Ordinal).ToArray();
                if (missing.Length != 0) throw new InvalidOperationException("旧公开签名丢失：\n" + string.Join("\n", missing));
                Assembly consumerAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "FrozenIdentityCompatibility");
                Type consumer = consumerAssembly.GetType("FrozenIdentityCompatibility.LegacyConsumer", true);
                consumer.GetMethod("WriteLegacyField").Invoke(null, new object[] { definition, 1001 });
                database.AddDefinition(definition);
                var resolved = consumer.GetMethod("Lookup").Invoke(null, new object[] { database, 1001 });
                if (!ReferenceEquals(resolved, definition)) throw new InvalidOperationException("冻结 DLL 的旧查找结果错误。");
                object oldInitializer = Activator.CreateInstance(consumer);
                ((IObjectInstanceInitializer)oldInitializer).Initialize(1001);
                if ((int)consumer.GetField("LastId").GetValue(oldInitializer) != 1001)
                    throw new InvalidOperationException("旧接口实现不兼容。");
                var record = new SingletonRecord { DefinitionId = 1001, IsEnabled = false, BehaviourTypeName = "Frozen.Old" };
                if ((int)consumer.GetMethod("ReadRecord").Invoke(null, new object[] { record }) != 1001)
                    throw new InvalidOperationException("旧 SingletonRecord 字段不兼容。");
                var hashes = JsonUtility.FromJson<HashList>("{\"items\":" + File.ReadAllText(Path.Combine(baseline, "hashes.json")) + "}");
                int checkedAssets = 0;
                foreach (HashEntry entry in hashes.items)
                {
                    if (!entry.path.Contains("IdCompatibilityFixtures")) continue;
                    if (Hash(entry.path) != entry.sha256) throw new InvalidOperationException("冻结旧资产被改写：" + entry.path);
                    checkedAssets++;
                }
                var report = new Report { unityVersion = Application.unityVersion, oldPublicSignatures = frozen.Length,
                    newPublicSignatures = current.Length, frozenAssetHashesChecked = checkedAssets,
                    oldBinaryCallsPassed = 4, result = "passed" };
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("IDENTITY_UPGRADE_PASSED " + JsonUtility.ToJson(report));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(database);
                UnityEngine.Object.DestroyImmediate(definition);
                typeof(GlobalScriptableObject<ObjectDefinitionDatabase>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previous);
            }
        }

        public static string[] PublicApi()
        {
            var output = new List<string>();
            Type[] types = { typeof(ObjectDefinition), typeof(ObjectDefinitionDatabase), typeof(ObjectInstance),
                typeof(IObjectInstanceInitializer), typeof(ObjectInstanceFactory), typeof(StateSynchronizer),
                typeof(SingletonRecord), typeof(UGUIManager), typeof(DefinitionIDAttribute) };
            foreach (Type type in types)
                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    string entry = type.FullName + "|" + member.MemberType + "|" + member;
                    if (member is MethodBase method)
                        entry += "|" + string.Join(",", method.GetParameters().Select(p => p.Name + "=" +
                            (p.HasDefaultValue ? Convert.ToString(p.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) : "required")));
                    output.Add(entry);
                }
            output.Sort(StringComparer.Ordinal);
            return output.ToArray();
        }

        public static string Argument(string key, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>读取原冻结哈希，不生成或覆盖基线。</summary>
        [Serializable] private sealed class HashList { public HashEntry[] items; }
        /// <summary>一个冻结文件的来源和哈希。</summary>
        [Serializable] private sealed class HashEntry { public string path; public string sha256; }
        /// <summary>实际执行的兼容检查结果，不代表全部旧宿主通过。</summary>
        [Serializable] private sealed class Report
        {
            public string unityVersion;
            public int oldPublicSignatures;
            public int newPublicSignatures;
            public int frozenAssetHashesChecked;
            public int oldBinaryCallsPassed;
            public string result;
        }
    }
}
