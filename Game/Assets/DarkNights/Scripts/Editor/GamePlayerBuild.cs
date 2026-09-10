using System;
using System.IO;
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
        [MenuItem("Dark Nights/Build/Windows Mono")]
        public static void Mono() => Build(ScriptingImplementation.Mono2x, "mono", BuildOptions.Development);

        [MenuItem("Dark Nights/Build/Windows IL2CPP")]
        public static void Il2Cpp() => Build(ScriptingImplementation.IL2CPP, "il2cpp", BuildOptions.None);

        private static void Build(ScriptingImplementation backend, string folder, BuildOptions options)
        {
            GameContentSetup.Validate();
            const string settingsPath = "ProjectSettings/ProjectSettings.asset";
            byte[] settingsBeforeBuild = File.ReadAllBytes(settingsPath);
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
                string output = Path.GetFullPath("../artifacts/migration/player-" + folder + "/DarkNights.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { EnvironmentValidation.ScenePath },
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
                addressables.BuildAddressablesWithPlayerBuild = previousContent;
                // Unity 会在构建中保存临时选项；恢复内存 API 不会同步撤销磁盘序列化。
                // 同一 Editor 内构建串行执行，结束时原样还原该次调用前的项目设置文件。
                File.WriteAllBytes(settingsPath, settingsBeforeBuild);
            }
        }
    }
}
