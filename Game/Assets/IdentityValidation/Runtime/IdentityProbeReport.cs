using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YYGC.IdentityValidation
{
    /// <summary>独立 Player 的可审计结果；仅记录真实断言与异常，输出到命令行指定的隔离 artifacts。</summary>
    [Serializable]
    public sealed class IdentityProbeReport
    {
        public string result;
        public string scenario;
        public string unityVersion;
        public bool developmentBuild;
        public int wireVersion;
        public int initializedObjects;
        public int acceptedConnections;
        public int rejectedConnections;
        public List<string> checks = new List<string>();
        public string error;

        public void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
            checks.Add(detail);
            Debug.Log("IDENTITY_CHECK " + detail);
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        public static string Argument(string key, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }
}
