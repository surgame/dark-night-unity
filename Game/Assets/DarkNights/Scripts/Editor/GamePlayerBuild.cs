using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 使用既有 Bootstrap 和已维护的 Addressables 构建正式宿主验证 Player。
    /// 每个后端只构建一次内容和程序，退出时恢复后端与裁剪选项；不创建场景或游戏美术。
    /// </summary>
    public static class GamePlayerBuild
    {
        public static void Mono() => Build(ScriptingImplementation.Mono2x, "mono", BuildOptions.Development);

        /// <summary>为地图联网验收创建不会覆盖旧 Player 的独立 Mono 目录。</summary>
        public static void MapStateMono()
        {
            string run = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string output = Path.GetFullPath("../artifacts/map-state/player-mono-" + run + "/DarkNights.exe");
            if (Directory.Exists(Path.GetDirectoryName(output))) throw new IOException("地图联网 Player 输出目录必须是新的。");
            Build(ScriptingImplementation.Mono2x, "mono", BuildOptions.Development, output);
        }

        /// <summary>供干净源码验收使用；调用方通过 -darkNightsOutput 指定空目录中的 DarkNights.exe。</summary>
        public static void MonoToEmptyDirectory()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-darkNightsOutput");
            if (index < 0 || index + 1 >= arguments.Length || !Path.IsPathRooted(arguments[index + 1]))
                throw new ArgumentException("必须通过 -darkNightsOutput 指定绝对 Player 路径。");
            string output = Path.GetFullPath(arguments[index + 1]);
            string directory = Path.GetDirectoryName(output);
            if (Path.GetFileName(output) != "DarkNights.exe" ||
                (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()))
                throw new InvalidOperationException("验收输出必须是空目录中的 DarkNights.exe。");
            Build(ScriptingImplementation.Mono2x, "mono", BuildOptions.Development, output);
        }

        public static void Il2Cpp() => Build(ScriptingImplementation.IL2CPP, "il2cpp", BuildOptions.None);

        private static void Build(ScriptingImplementation backend, string folder, BuildOptions options, string explicitOutput = null)
        {
            GameContentSetup.Validate();
            const string settingsPath = "ProjectSettings/ProjectSettings.asset";
            byte[] settingsBeforeBuild = File.ReadAllBytes(settingsPath);
            const string editorSettingsPath = "ProjectSettings/EditorSettings.asset";
            byte[] editorSettingsBeforeBuild = File.ReadAllBytes(editorSettingsPath);
            var previousPlayOptions = EditorSettings.enterPlayModeOptions;
            bool previousPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            var target = NamedBuildTarget.Standalone;
            var previousBackend = PlayerSettings.GetScriptingBackend(target);
            var previousStripping = PlayerSettings.GetManagedStrippingLevel(target);
            var previousPreloaded = PlayerSettings.GetPreloadedAssets();
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            var previousContent = addressables.BuildAddressablesWithPlayerBuild;
            try
            {
                PlayerSettings.SetScriptingBackend(target, backend);
                if (backend == ScriptingImplementation.IL2CPP)
                    PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
                DarkNightsEnvironmentSetup.BuildAddressablesContent();
                addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                string output = explicitOutput ?? Path.GetFullPath("../artifacts/migration/player-" + folder + "/DarkNights.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { EnvironmentValidation.ScenePath, "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity", DarkNights.Entry.Terrain.RandomLevelEntry.ScenePath, DarkNights.Entry.Terrain.RandomLevelEntry.ExpeditionScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = options
                });
                if (result.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Game host build failed: " + result.summary.result);
                Debug.Log("DARK_NIGHTS_PLAYER_BUILD=" + output);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, previousBackend);
                PlayerSettings.SetManagedStrippingLevel(target, previousStripping);
                PlayerSettings.SetPreloadedAssets(previousPreloaded);
                EditorSettings.enterPlayModeOptions = previousPlayOptions;
                EditorSettings.enterPlayModeOptionsEnabled = previousPlayOptionsEnabled;
                addressables.BuildAddressablesWithPlayerBuild = previousContent;
                // Unity 会在构建中保存临时选项；恢复内存 API 不会同步撤销磁盘序列化。
                // 同一 Editor 内构建串行执行，结束时原样还原该次调用前的项目设置文件。
                File.WriteAllBytes(settingsPath, settingsBeforeBuild);
                File.WriteAllBytes(editorSettingsPath, editorSettingsBeforeBuild);
            }
        }
    }
}
