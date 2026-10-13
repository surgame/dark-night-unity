using DarkNights.Core.Config;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>显式迁移灰松谷旧场景返光到手电覆盖和设备预设，保留其他作者值及 GUID。旧 YAML 只读，验证和备份后经 Editor API 保存，普通导入及启动不运行迁移。</summary>
    public static class WallDiffuseMigration
    {
        [MenuItem("Dark Nights/Tools/迁移灰松谷返光到单灯配置")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("退出 Play 并等待编译、导入后迁移单灯配置。");
            var scene = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>(LightProfileMigration.ScenePath);
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FlashlightContentSetup.DefinitionPath);
            if (scene == null || definition == null) throw new InvalidOperationException("灰松谷光照或手电 Definition 不存在。");
            string original = File.ReadAllText(LightProfileMigration.ScenePath);
            if (!Regex.IsMatch(original, @"(?m)^    BounceMode:")) return;
            if (EditorUtility.IsDirty(scene) || EditorUtility.IsDirty(definition) || EditorUtility.IsDirty(scene.DevicePreset))
                throw new InvalidOperationException("迁移目标有未保存的作者修改，保留并等待处理。");
            scene.Validate();
            var candidate = LightDefinitionEditor.Copy(LightDefinitionEditor.Config(definition));
            var legacy = ReadLegacy(original);
            foreach (var field in LightParameterSchema.Fields.Where(item => IsBounce(item.Name)))
            {
                var mask = LightParameterSchema.Mask(field.Name);
                if (candidate.Overrides.Has(mask)) continue;
                typeof(LightOverrides).GetField(field.Name).SetValue(candidate.Overrides, typeof(LightOverrides).GetField(field.Name).GetValue(legacy));
                candidate.Overrides.Mask |= mask;
            }
            LightProfileResolver.Resolve(LightDefinitionEditor.Preset(definition), candidate.Overrides, scene.DefaultEffectTemplate);
            var device = UnityEngine.Object.Instantiate(scene.DevicePreset);
            try
            {
                foreach (var field in LightParameterSchema.Fields.Where(item => IsBounce(item.Name)))
                    typeof(LightProfile).GetField(field.Name).SetValue(device, typeof(LightOverrides).GetField(field.Name).GetValue(legacy));
                device.Freeze();
                string backup = Path.GetFullPath("../artifacts/light-bounce-per-source-20261013/migration-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(backup);
                foreach (var asset in new UnityEngine.Object[] { scene, definition, scene.DevicePreset })
                {
                    string path = AssetDatabase.GetAssetPath(asset);
                    File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), false);
                }
                Undo.RecordObjects(new UnityEngine.Object[] { scene, definition, scene.DevicePreset }, "迁移返光到单灯配置");
                var current = LightDefinitionEditor.Config(definition);
                definition.SharedConfigs[definition.SharedConfigs.IndexOf(current)] = candidate;
                foreach (var field in LightParameterSchema.Fields.Where(item => IsBounce(item.Name)))
                    typeof(LightProfile).GetField(field.Name).SetValue(scene.DevicePreset, typeof(LightProfile).GetField(field.Name).GetValue(device));
                // 原非定向设备在连续模式下没有候选墙段；保留模式，避免意外启用单点返光。
                EditorUtility.SetDirty(definition); EditorUtility.SetDirty(scene.DevicePreset); EditorUtility.SetDirty(scene);
                AssetDatabase.SaveAssetIfDirty(definition); AssetDatabase.SaveAssetIfDirty(scene.DevicePreset); AssetDatabase.SaveAssetIfDirty(scene);
                LightingEditorRefresh.Request();
                File.WriteAllText(Path.Combine(backup, "result.json"), "{\"completed\":true,\"source\":\"PinewatchLighting\",\"destination\":\"Flashlight Overrides and DeviceLighting\"}");
            }
            finally { UnityEngine.Object.DestroyImmediate(device); }
        }

        private static bool IsBounce(string name) => name == "Bounce" || name == "BounceMode" || name.StartsWith("Diffuse", StringComparison.Ordinal);

        private static LightOverrides ReadLegacy(string yaml)
        {
            var result = new LightOverrides();
            foreach (var field in LightParameterSchema.Fields.Where(item => IsBounce(item.Name)))
            {
                var match = Regex.Match(yaml, @"(?m)^    " + field.Name + @": ([^\r\n]+)");
                if (!match.Success) continue;
                var member = typeof(LightOverrides).GetField(field.Name);
                object value = member.FieldType.IsEnum ? Enum.ToObject(member.FieldType, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)) :
                    member.FieldType == typeof(bool) ? (object)(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) != 0) :
                    member.FieldType == typeof(int) ? (object)int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) :
                    float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                member.SetValue(result, value);
            }
            return result;
        }
    }
}
