using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Lighting;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.Universal;

namespace DarkNights.Editor.Lighting
{
    /// <summary>显式光照迁移与场景配置修复；缺失资源可独立补齐，复用已有有效配置，保留原 GUID、人工覆盖和场景草稿，不在运行时写资产。</summary>
    public static class LightProfileMigration
    {
        public const string Root = "Assets/DarkNights/Res/Shared/Lighting/Profiles";
        public const string FlashlightPath = Root + "/FlashlightLighting.asset";
        public const string DevicePath = Root + "/DeviceLighting.asset";
        public const string ScenePath = Root + "/PinewatchLighting.asset";

        [MenuItem("Dark Nights/Tools/修复关卡光照配置")]
        public static void RepairSceneLighting()
        {
            CheckEditable();
            BindScenes(EnsureSceneProfile());
        }

        [MenuItem("Dark Nights/Tools/迁移持久光照配置")]
        public static void Apply()
        {
            try { Migrate(); }
            catch (Exception error)
            {
                string report = Path.GetFullPath("../artifacts/light-profile-refactor-20261009/migration-failed.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(report));
                File.AppendAllText(report, DateTimeOffset.Now + "\n" + error + "\n");
                throw;
            }
        }

        private static void Migrate()
        {
            CheckEditable();
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FlashlightContentSetup.DefinitionPath);
            if (definition == null) throw new InvalidOperationException("缺少手电 Definition。");
            var config = definition.SharedConfigs.OfType<FlashlightToolConfig>().Single();
            if (config.Profile != null && config.Profile.RuntimeKeyIsValid()) { BindScenes(EnsureSceneProfile()); Validate(); return; }
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(FlashlightPath) != null)
                throw new InvalidOperationException("目标路径已有资产但迁移未完成；保留候选，需根据原报告续接。");
            string prefabPath = AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID);
            Backup(prefabPath); Backup(FlashlightContentSetup.DefinitionPath);
            EnsureFolder(Root);
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(ReusableLightContentSetup.EffectPath).GetComponent<LightEffect>();
            var preset = ScriptableObject.CreateInstance<LightProfile>(); preset.Template = template;
            LegacyLightProfileReader.Read(preset, LegacyLightProfileReader.Baseline(), File.ReadAllText(prefabPath));
            preset.Freeze();
            CheckPrefab(prefabPath);
            var scene = EnsureSceneProfile();
            AssetDatabase.CreateAsset(preset, FlashlightPath); LightDefinitionEditor.Register(preset);
            LightDefinitionEditor.Register(scene.DevicePreset);
            config.Profile = new AssetReference(AssetDatabase.AssetPathToGUID(FlashlightPath));
            config.Overrides = config.Overrides ?? new LightOverrides(); EditorUtility.SetDirty(definition);
            MigratePrefab(prefabPath);
            BindScenes(scene);
            AssetDatabase.SaveAssetIfDirty(definition);
            AssetDatabase.SaveAssetIfDirty(preset);
            AssetDatabase.SaveAssetIfDirty(UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings);
            Validate();
            File.WriteAllText("../artifacts/light-profile-refactor-20261009/migration-complete.json",
                "{\"completed\":true,\"definition\":\"item.flashlight\",\"scene_profile\":\"" + ScenePath + "\"}");
        }

        private static void CheckEditable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("退出 Play 并等待编译、导入后修复或迁移。");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                EditorSceneManager.GetSceneManagerSetup().Any(item => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(item.path).isDirty))
                throw new InvalidOperationException("存在 Prefab 编辑或未保存场景，保留草稿。");
        }

        private static SceneLightingProfile EnsureSceneProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>(ScenePath);
            if (existing != null) { existing.Validate(); return existing; }
            if (File.Exists(ScenePath)) throw new InvalidOperationException("场景光照目标已存在其他资源，保留：" + ScenePath);
            var device = AssetDatabase.LoadAssetAtPath<LightProfile>(DevicePath);
            bool createDevice = device == null;
            if (createDevice && File.Exists(DevicePath)) throw new InvalidOperationException("设备光目标已存在其他资源，保留：" + DevicePath);
            var scene = ScriptableObject.CreateInstance<SceneLightingProfile>();
            try
            {
                var template = AssetDatabase.LoadAssetAtPath<GameObject>(ReusableLightContentSetup.EffectPath)?.GetComponent<LightEffect>();
                if (createDevice)
                {
                    device = ScriptableObject.CreateInstance<LightProfile>(); device.Template = template;
                    device.Directional = false; device.LocalFillEnabled = false; device.Range = 8.375f; device.Cone = 90;
                    device.Intensity = .45f; device.NearRange = .25f; device.NearIntensity = 0; device.ApertureWidth = 0;
                    device.Color = new Color(1, .78f, .48f, 1);
                }
                scene.DevicePreset = device; scene.DefaultEffectTemplate = template;
                scene.LightingShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute");
                scene.UrpLightTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(LightingBackendContentSetup.LightPath)?.GetComponent<Light2D>();
                scene.UrpShadowTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(LightingBackendContentSetup.ShadowPath)?.GetComponent<ShadowCaster2D>();
                scene.Validate(); EnsureFolder(Root);
                if (createDevice) { AssetDatabase.CreateAsset(device, DevicePath); AssetDatabase.SaveAssetIfDirty(device); }
                AssetDatabase.CreateAsset(scene, ScenePath); AssetDatabase.SaveAssetIfDirty(scene); return scene;
            }
            finally
            {
                if (!EditorUtility.IsPersistent(scene)) UnityEngine.Object.DestroyImmediate(scene);
                if (createDevice && device != null && !EditorUtility.IsPersistent(device)) UnityEngine.Object.DestroyImmediate(device);
            }
        }

        private static void MigratePrefab(string path)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = contents.GetComponent<FlashlightView>();
                if (view == null || view.Emitter == null) throw new InvalidOperationException("手电明确灯口绑定缺失。");
                foreach (var effect in contents.GetComponentsInChildren<LightEffect>(true))
                {
                    string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(effect.gameObject);
                    if (source != ReusableLightContentSetup.EffectPath) throw new InvalidOperationException("检测到非本次共用模板的人工光效，保留：" + path);
                    UnityEngine.Object.DestroyImmediate(effect.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void CheckPrefab(string path)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = contents.GetComponent<FlashlightView>();
                if (view == null || view.Emitter == null) throw new InvalidOperationException("手电明确灯口绑定缺失。");
                foreach (var effect in contents.GetComponentsInChildren<LightEffect>(true))
                {
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(effect.gameObject) != ReusableLightContentSetup.EffectPath)
                        throw new InvalidOperationException("手电包含人工光效，拒绝覆盖：" + path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void BindScenes(SceneLightingProfile profile)
        {
            const string scriptGuid = "1202738e8e90c3a408c8ba5ac0561079";
            string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/DarkNights/Res" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => File.ReadAllText(path).Contains(scriptGuid)).ToArray();
            foreach (string path in paths)
            {
                Backup(path);
                var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    bool changed = false;
                    foreach (var template in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RandomLevelTemplate>(true)))
                    {
                        if (template.Lighting != null) { template.Lighting.Validate(); continue; }
                        Undo.RegisterCompleteObjectUndo(template, "补齐关卡场景光照配置");
                        template.Lighting = profile; EditorUtility.SetDirty(template); changed = true;
                    }
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("场景光照引用保存失败：" + path);
                    }
                }
                finally
                {
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                }
            }
        }

        public static void Validate()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FlashlightContentSetup.DefinitionPath);
            var preset = LightDefinitionEditor.Preset(definition);
            if (preset == null) throw new InvalidOperationException("手电 Definition 尚未引用光照预设。");
            Entry.LightProfileResolver.Resolve(preset, LightDefinitionEditor.Config(definition).Overrides);
            var scene = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>(ScenePath);
            if (scene == null) throw new InvalidOperationException("场景光照配置尚未生成。");
            scene.Validate();
        }

        private static void Backup(string path)
        {
            string destination = Path.GetFullPath("../artifacts/light-profile-refactor-20261009/migration-backup/" + path);
            if (File.Exists(destination)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path, destination);
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", destination + ".meta");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
