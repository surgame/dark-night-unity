using System;
using System.IO;
using System.Reflection;
using DarkNights.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;

/// <summary>后台静音Mono验收构建；临时关闭候选项目音频并在finally恢复原始设置，不改变其他应用声音或游戏业务合同。</summary>
public static class OxygenSilentBuild
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Editor is occupied.");
        string root = Path.GetFullPath("../artifacts/oxygen-removal-20261003");
        string player = Path.Combine(root, "player-mono-silent-r1", "DarkNights.exe");
        string reportPath = Path.Combine(root, "build-mono-silent-r1.json");
        if (File.Exists(reportPath) || Directory.Exists(Path.GetDirectoryName(player)))
            throw new IOException("Build identity already exists.");
        Directory.CreateDirectory(root);
        const string audioPath = "ProjectSettings/AudioManager.asset";
        byte[] original = File.ReadAllBytes(audioPath);
        var audio = AssetDatabase.LoadAllAssetsAtPath(audioPath)[0];
        var serialized = new SerializedObject(audio);
        bool disabled = serialized.FindProperty("m_DisableAudio").boolValue;
        var report = new JObject { ["status"] = "running", ["player"] = player,
            ["audioDisabledForValidation"] = true, ["startedUtc"] = DateTime.UtcNow.ToString("O") };
        File.WriteAllText(reportPath, report.ToString());
        try
        {
            serialized.FindProperty("m_DisableAudio").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(audio);
            typeof(GamePlayerBuild).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                new object[] { ScriptingImplementation.Mono2x, "mono", BuildOptions.Development, player });
            report["success"] = true; report["status"] = "completed";
        }
        catch (Exception error)
        { report["success"] = false; report["status"] = "failed"; report["error"] = error.ToString(); }
        finally
        {
            serialized.Update(); serialized.FindProperty("m_DisableAudio").boolValue = disabled;
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(audio);
            File.WriteAllBytes(audioPath, original);
            report["audioSettingsRestored"] = File.ReadAllBytes(audioPath).AsSpan().SequenceEqual(original);
            report["finishedUtc"] = DateTime.UtcNow.ToString("O");
            File.WriteAllText(reportPath, report.ToString());
        }
        return reportPath;
    }
}
