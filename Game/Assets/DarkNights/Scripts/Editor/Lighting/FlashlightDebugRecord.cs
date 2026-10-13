using System;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>用户显式保存的一次光照调试记录；冻结当时生效参数、来源身份和右侧姿态，历史重放只进入临时草稿。</summary>
    [Serializable]
    public sealed class FlashlightDebugRecord
    {
        public int Version = 1;
        public string Id;
        public string Name;
        public string CapturedUtc;
        public string DefinitionGuid;
        public string ProfileGuid;
        public string SceneGuid;
        public bool SharedTarget;
        public LightOverrides EffectiveLight;
        public ExplorationLightSettings SceneSettings;
        public Vector3 MountPosition;
        public Vector3 MountRotation;
        public Vector3 MountScale;
        public bool HasMount;
        public FlashlightPreviewSettings Preview;

        public static FlashlightDebugRecord Capture(FlashlightDebugDraft draft, SceneLightingProfile scene,
            SceneLightingProfile source, FlashlightPreviewDraft preview, bool shared)
        {
            var record = new FlashlightDebugRecord
            {
                Id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(preview.RecordName) ? "未命名光照调试" : preview.RecordName,
                CapturedUtc = DateTime.UtcNow.ToString("O"), SharedTarget = shared,
                DefinitionGuid = GuidOf(draft?.Definition), ProfileGuid = GuidOf(draft?.Preset), SceneGuid = GuidOf(source),
                Preview = preview.Settings.Copy(), HasMount = draft?.MountReady == true
            };
            if (draft != null)
            {
                record.EffectiveLight = LightProfileResolver.Values(draft.Snapshot());
                foreach (var field in LightParameterSchema.Fields) record.EffectiveLight.Mask |= LightParameterSchema.Mask(field.Name);
                record.MountPosition = draft.MountPosition; record.MountRotation = draft.MountRotation; record.MountScale = draft.MountScale;
            }
            if (scene != null) record.SceneSettings = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(scene.Settings));
            record.Validate(); return record;
        }
        public void Validate()
        {
            if (Version != 1 || string.IsNullOrWhiteSpace(Id) || Preview == null) throw new ArgumentException("调试记录版本或内容无效。");
            Preview.Validate(); SceneSettings?.Validate();
            if (EffectiveLight != null) LightProfileResolver.Apply(LightProfileSnapshot.FromDefaults(LightDefinitionEditor.DefaultTemplate), EffectiveLight);
            if (HasMount && (!Finite(MountPosition) || !Finite(MountRotation) || !Finite(MountScale)))
                throw new ArgumentException("历史灯口姿态无效。");
        }
        public static T Asset<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)) ??
                throw new InvalidOperationException("调试记录引用的资产已不可用：" + guid);
        }
        private static string GuidOf(UnityEngine.Object value) => value == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
