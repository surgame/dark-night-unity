using System;
using System.Linq;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Editor.Lighting
{
    /// <summary>Definition 灯光制作操作；以原资产为 Undo 目标，保留未覆盖值与配发规则，所有写入显式通知预览和运行配置缓存。</summary>
    public static class LightDefinitionEditor
    {
        public static FlashlightToolConfig Config(ObjectDefinition owner) => owner?.SharedConfigs.OfType<FlashlightToolConfig>().SingleOrDefault();
        public static LightProfile Preset(ObjectDefinition owner) => LightingEditorRefresh.LoadPreset(Config(owner)?.Profile?.AssetGUID);
        public static FlashlightToolConfig Copy(FlashlightToolConfig value) => JsonUtility.FromJson<FlashlightToolConfig>(JsonUtility.ToJson(value));
        public static LightEffect DefaultTemplate => AssetDatabase.LoadAssetAtPath<GameObject>(ReusableLightContentSetup.EffectPath)?.GetComponent<LightEffect>();
        public static object InheritedValue(LightProfile preset, string field) => preset != null
            ? typeof(LightProfile).GetField(field).GetValue(preset)
            : typeof(LightOverrides).GetField(field).GetValue(new LightOverrides());

        public static bool Apply(ObjectDefinition owner, FlashlightToolConfig candidate, FlashlightDebugUndo undo = null, string control = "config")
        {
            var current = Config(owner) ?? throw new InvalidOperationException("Definition 缺少照明道具配置。");
            if (JsonUtility.ToJson(current) == JsonUtility.ToJson(candidate)) return false;
            var preset = LightingEditorRefresh.LoadPreset(candidate.Profile?.AssetGUID);
            LightProfileResolver.Resolve(preset, candidate.Overrides, DefaultTemplate);
            Action write = () => owner.SharedConfigs[owner.SharedConfigs.IndexOf(current)] = candidate;
            (undo ?? new FlashlightDebugUndo()).Change(owner, control, "修改照明道具配置", write);
            return true;
        }

        public static void SelectPreset(ObjectDefinition owner, LightProfile preset, FlashlightDebugUndo undo = null)
        {
            var candidate = Copy(Config(owner));
            candidate.Profile = new AssetReference(preset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preset)) : "");
            if (preset != null) Register(preset);
            Apply(owner, candidate, undo, "profile");
        }

        public static void EnableOverride(ObjectDefinition owner, string field, bool enabled, FlashlightDebugUndo undo)
        {
            var candidate = Copy(Config(owner)); var mask = LightParameterSchema.Mask(field);
            if (enabled && !candidate.Overrides.Has(mask))
            {
                typeof(LightOverrides).GetField(field).SetValue(candidate.Overrides, InheritedValue(Preset(owner), field));
            }
            candidate.Overrides.Mask = enabled ? candidate.Overrides.Mask | mask : candidate.Overrides.Mask & ~mask;
            Apply(owner, candidate, undo, field);
        }

        public static void SetValue(ObjectDefinition owner, string field, object value, FlashlightDebugUndo undo)
        {
            var candidate = Copy(Config(owner));
            var mask = LightParameterSchema.Mask(field);
            var member = typeof(LightOverrides).GetField(field);
            object current = candidate.Overrides.Has(mask) ? member.GetValue(candidate.Overrides) : InheritedValue(Preset(owner), field);
            member.SetValue(candidate.Overrides, value);
            if (Equals(current, member.GetValue(candidate.Overrides))) return;
            candidate.Overrides.Mask |= mask;
            Apply(owner, candidate, undo, field);
        }

        public static void Register(LightProfile profile)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("缺少现有 Addressables 配置。");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("预设必须是已保存的项目资产。");
            if (settings.FindAssetEntry(guid) != null) return;
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = "dark_nights.lighting." + guid;
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(entry.parentGroup);
            AssetDatabase.SaveAssetIfDirty(entry.parentGroup); AssetDatabase.SaveAssetIfDirty(settings);
        }
    }
}
