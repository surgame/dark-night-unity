using System;
using System.Linq;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Editor.Lighting
{
    /// <summary>
    /// 调试台唯一的资产写回入口；先展示目标及字段并确认，再合并草稿修改，保留其他作者值。
    /// 保存不是一次运行时调参，不登记草稿 Undo；灯口通过原生 Prefab API 写入，拒绝覆盖未保存的 Stage。
    /// </summary>
    public static class FlashlightDebugSave
    {
        public static readonly string[] SceneFields = { "Backend", "Ambient", "WallDepth", "WallStrength", "Bounce", "Relief" };

        private static bool Confirm(UnityEngine.Object target, string fields, string impact)
        {
            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path) || !EditorUtility.IsPersistent(target)) throw new InvalidOperationException("保存目标必须是现有项目资产。");
            return EditorUtility.DisplayDialog("确认保存到资产", "目标：" + path + "\n修改项：" + fields + "\n" + impact
                + "\n保存后结束本轮临时 Undo/Redo 历史，其他尚未保存的调整仍保留。", "确认保存", "取消");
        }

        private static string Fields(FlashlightDebugDraft draft) => string.Join("、", LightParameterSchema.Fields
            .Where(item => draft.Values.Has(LightParameterSchema.Mask(item.Name))).Select(item => item.Label));

        public static bool Definition(FlashlightDebugDraft draft)
        {
            draft.Snapshot();
            if (draft.Definition == null || !draft.HasLightChanges) return false;
            var candidate = LightDefinitionEditor.Copy(LightDefinitionEditor.Config(draft.Definition));
            bool presetChanged = draft.HasPresetChange;
            if (presetChanged) candidate.Profile = new AssetReference(draft.Preset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(draft.Preset)) : "");
            foreach (var item in LightParameterSchema.Fields)
                if (draft.Values.Has(LightParameterSchema.Mask(item.Name)))
                {
                    typeof(LightOverrides).GetField(item.Name).SetValue(candidate.Overrides, typeof(LightOverrides).GetField(item.Name).GetValue(draft.Values));
                    candidate.Overrides.Mask |= LightParameterSchema.Mask(item.Name);
                }
            LightProfileResolver.Resolve(draft.Preset, candidate.Overrides, LightDefinitionEditor.DefaultTemplate);
            if (!Confirm(draft.Definition, (presetChanged ? "预设引用；" : "") + Fields(draft), "保存后影响使用该 Definition 的手电。该资产已有的其他未保存修改也会一并保存。")) return false;
            if (presetChanged && draft.Preset != null) LightDefinitionEditor.Register(draft.Preset);
            var current = LightDefinitionEditor.Config(draft.Definition);
            draft.Definition.SharedConfigs[draft.Definition.SharedConfigs.IndexOf(current)] = candidate;
            EditorUtility.SetDirty(draft.Definition); AssetDatabase.SaveAssetIfDirty(draft.Definition);
            LightingEditorRefresh.Request(); draft.AcceptSavedLight(); return true;
        }

        public static bool Profile(FlashlightDebugDraft draft)
        {
            draft.Snapshot();
            if (draft.Preset == null || draft.Values.Mask == Core.Config.LightOverrideMask.None) return false;
            var candidate = UnityEngine.Object.Instantiate(draft.Preset); candidate.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                foreach (var item in LightParameterSchema.Fields)
                    if (draft.Values.Has(LightParameterSchema.Mask(item.Name)))
                        typeof(LightProfile).GetField(item.Name).SetValue(candidate, typeof(LightOverrides).GetField(item.Name).GetValue(draft.Values));
                candidate.Freeze();
                if (!Confirm(draft.Preset, Fields(draft), "保存后影响继承这些参数的道具；Definition 已覆盖的项仍使用其覆盖值。")) return false;
                foreach (var item in LightParameterSchema.Fields)
                    if (draft.Values.Has(LightParameterSchema.Mask(item.Name)))
                        typeof(LightProfile).GetField(item.Name).SetValue(draft.Preset, typeof(LightProfile).GetField(item.Name).GetValue(candidate));
                EditorUtility.SetDirty(draft.Preset); AssetDatabase.SaveAssetIfDirty(draft.Preset);
                LightingEditorRefresh.Request(); draft.AcceptSavedLight(); return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        public static bool Scene(SceneLightingProfile source, SceneLightingProfile draft, ExplorationLightSettings original)
        {
            if (source == null || draft == null) return false;
            var changed = SceneFields.Where(name => !Equals(typeof(ExplorationLightSettings).GetField(name).GetValue(original),
                typeof(ExplorationLightSettings).GetField(name).GetValue(draft.Settings))).ToArray();
            if (changed.Length == 0) return false;
            var candidate = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(source.Settings));
            foreach (string name in changed) typeof(ExplorationLightSettings).GetField(name).SetValue(candidate, typeof(ExplorationLightSettings).GetField(name).GetValue(draft.Settings));
            candidate.Validate();
            if (!Confirm(source, string.Join("、", changed), "保存后影响引用该场景光照资产的关卡。")) return false;
            source.Settings = candidate; EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
            LightingEditorRefresh.Request(); return true;
        }

        public static bool Mount(FlashlightDebugDraft draft)
        {
            if (draft.Definition?.PrefabRef == null || !draft.HasMountChanges) return false;
            string path = AssetDatabase.GUIDToAssetPath(draft.Definition.PrefabRef.AssetGUID);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty) throw new InvalidOperationException("对应 Prefab Stage 有未保存修改，请先处理后再保存灯口。");
            if (!Confirm(prefab, "灯口 Position／Rotation／Scale", "仅写入明确绑定的灯口，保留原 Prefab 结构及 GUID。")) return false;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<FlashlightView>();
                if (view == null || view.Emitter == null) throw new InvalidOperationException("Prefab 缺少明确的手电主视图或灯口绑定。");
                view.Emitter.localPosition = draft.MountPosition; view.Emitter.localEulerAngles = draft.MountRotation; view.Emitter.localScale = draft.MountScale;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success) throw new InvalidOperationException("灯口 Prefab 保存失败。");
                draft.AcceptSavedMount();
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
