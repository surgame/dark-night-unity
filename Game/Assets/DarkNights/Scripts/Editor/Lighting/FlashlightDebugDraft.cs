using System;
using System.Collections.Generic;
using DarkNights.Core.Config;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>
    /// 调试台独占的临时单灯草稿；复用物体覆盖合同，按实例叠加到持久化冻结基线。
    /// 原资产只作为读取来源，Undo 只登记本对象；关闭、换实例及退出运行撤除效果并释放草稿。
    /// </summary>
    public sealed class FlashlightDebugDraft : ScriptableObject, IDisposable
    {
        [SerializeField] private LightOverrides values = new LightOverrides();
        [SerializeField] private Vector3 mountPosition;
        [SerializeField] private Vector3 mountRotation;
        [SerializeField] private Vector3 mountScale = Vector3.one;
        private Vector3 originalPosition;
        private Vector3 originalRotation;
        private Vector3 originalScale;
        private Vector3 prefabPosition, prefabRotation, prefabScale;
        private ObjectDefinition definition;
        [SerializeField] private LightProfile preset;
        private FlashlightToolConfig initialConfig;
        private readonly Dictionary<LightProfile, string> profileBaselines = new Dictionary<LightProfile, string>();
        private LightProfile originalPreset;
        private FlashlightView tool;
        private string instanceId;
        private LightProfileSnapshot basis;
        private LightProfileSnapshot cached;
        private LightProfileSnapshot applied;
        private LightOverrides inherited;
        private string basisKey;
        private string valueKey;
        public ObjectDefinition Definition => definition;
        public LightProfile Preset => preset;
        public FlashlightView Tool => tool;
        public LightOverrides Values => values;
        public bool HadRuntimeTool => instanceId != null;
        public bool ToolAlive => tool != null && tool.isActiveAndEnabled && tool.Owner != null && tool.Owner.InstanceId == instanceId && tool.Owner.Definition == definition;
        public bool HasPresetChange => definition != null && preset != originalPreset;
        public bool HasLightChanges => values.Mask != LightOverrideMask.None || HasPresetChange;
        public bool MountReady { get; private set; }
        public Vector3 MountPosition => mountPosition;
        public Vector3 MountRotation => mountRotation;
        public Vector3 MountScale => mountScale;
        public Vector3 SavedMountPosition => prefabPosition;
        public Vector3 SavedMountRotation => prefabRotation;
        public Vector3 SavedMountScale => prefabScale;
        public bool HasMountChanges => MountReady && (mountPosition != originalPosition || mountRotation != originalRotation || mountScale != originalScale);
        public bool HasChanges => HasLightChanges || HasMountChanges;

        public void Initialize(ObjectDefinition source, LightProfile profile, FlashlightView instance)
        {
            hideFlags = HideFlags.HideAndDontSave; name = "手电运行时调参";
            definition = source; preset = profile; originalPreset = LightDefinitionEditor.Preset(source);
            initialConfig = LightDefinitionEditor.Config(source) != null ? LightDefinitionEditor.Copy(LightDefinitionEditor.Config(source)) : null;
            RememberProfile(profile);
            tool = instance; instanceId = instance != null ? instance.Owner?.InstanceId : null;
            var prefab = source?.PrefabRef != null ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(source.PrefabRef.AssetGUID)) : null;
            var sourceView = instance != null ? instance : prefab != null ? prefab.GetComponent<FlashlightView>() : null;
            var anchor = sourceView != null ? sourceView.Emitter : null;
            var prefabView = prefab != null ? prefab.GetComponent<FlashlightView>() : null;
            if (prefabView?.Emitter != null)
            { prefabPosition = prefabView.Emitter.localPosition; prefabRotation = prefabView.Emitter.localEulerAngles; prefabScale = prefabView.Emitter.localScale; }
            MountReady = anchor != null;
            if (MountReady)
            {
                originalPosition = mountPosition = anchor.localPosition;
                originalRotation = mountRotation = anchor.localEulerAngles;
                originalScale = mountScale = anchor.localScale;
            }
        }

        public bool Matches(ObjectDefinition source, LightProfile profile, FlashlightView instance) =>
            definition == source && preset == profile && tool == instance;

        private void RememberProfile(LightProfile profile)
        { if (profile != null && !profileBaselines.ContainsKey(profile)) profileBaselines.Add(profile, JsonUtility.ToJson(profile)); }

        public void SelectPreset(LightProfile selected, FlashlightDebugUndo undo)
        {
            if (selected == preset) return;
            var candidate = LightProfileResolver.Resolve(selected, LightDefinitionEditor.Config(definition)?.Overrides ?? new LightOverrides(), LightDefinitionEditor.DefaultTemplate);
            LightProfileResolver.Apply(candidate, values);
            RememberProfile(selected);
            undo.Change(this, "preset", "切换单灯参数来源", () => { preset = selected; basis = null; cached = null; });
            Snapshot(); ApplyRuntime();
        }

        public void LoadRecord(FlashlightDebugRecord record)
        {
            record.Validate(); values = JsonUtility.FromJson<LightOverrides>(JsonUtility.ToJson(record.EffectiveLight));
            if (record.HasMount && MountReady)
            { mountPosition = record.MountPosition; mountRotation = record.MountRotation; mountScale = record.MountScale; }
            Snapshot(); ApplyRuntime();
        }

        public void ValidateSaveTarget(bool shared)
        {
            var current = LightDefinitionEditor.Config(definition);
            if (HasPresetChange && initialConfig != null && current?.Profile?.AssetGUID != initialConfig.Profile?.AssetGUID)
                throw new InvalidOperationException("Definition 的预设引用已被其他编辑修改，请重新核对来源后保存。");
            LightProfile profileBase = null;
            if (shared && preset != null && profileBaselines.TryGetValue(preset, out string json))
            { profileBase = CreateInstance<LightProfile>(); JsonUtility.FromJsonOverwrite(json, profileBase); }
            try
            {
                foreach (var field in LightParameterSchema.Fields)
                {
                    var mask = LightParameterSchema.Mask(field.Name);
                    if (!values.Has(mask)) continue;
                    var member = typeof(LightOverrides).GetField(field.Name);
                    bool conflict = shared ? profileBase != null && !Equals(typeof(LightProfile).GetField(field.Name).GetValue(profileBase), typeof(LightProfile).GetField(field.Name).GetValue(preset)) :
                        initialConfig != null && current != null && (initialConfig.Overrides.Has(mask) != current.Overrides.Has(mask) ||
                        initialConfig.Overrides.Has(mask) && !Equals(member.GetValue(initialConfig.Overrides), member.GetValue(current.Overrides)));
                    if (conflict) throw new InvalidOperationException("保存字段已被其他编辑修改：" + field.Label + "。草稿保留，请先处理冲突。");
                }
            }
            finally { if (profileBase != null) DestroyImmediate(profileBase); }
        }

        public void ValidateMountSource(Vector3 position, Vector3 rotation, Vector3 scale)
        {
            if (position != prefabPosition || rotation != prefabRotation || scale != prefabScale)
                throw new InvalidOperationException("Prefab 灯口已被其他编辑修改；草稿保留，请重新核对。");
        }

        private LightProfileSnapshot Basis()
        {
            if (definition != null)
            {
                var current = LightDefinitionEditor.Preset(definition);
                if (preset == originalPreset) preset = current;
                originalPreset = current;
            }
            RememberProfile(preset);
            var config = LightDefinitionEditor.Config(definition);
            var template = ToolAlive && tool.BaseLightProfile != null ? tool.BaseLightProfile.Template : LightDefinitionEditor.DefaultTemplate;
            string key = JsonUtility.ToJson(config?.Overrides) + ":" + (preset != null ? JsonUtility.ToJson(preset) : "") + ":" + (template != null ? template.GetEntityId().ToString() : "");
            if (basis == null || basisKey != key)
            {
                basis = LightProfileResolver.Resolve(preset, config?.Overrides ?? new LightOverrides(), template);
                inherited = LightProfileResolver.Values(basis); basisKey = key; cached = null;
            }
            return basis;
        }

        public object Read(string field)
        {
            Basis();
            return typeof(LightOverrides).GetField(field).GetValue(values.Has(LightParameterSchema.Mask(field)) ? values : inherited);
        }

        public LightProfileSnapshot Snapshot()
        {
            var source = Basis();
            string key = JsonUtility.ToJson(values);
            if (cached == null || valueKey != key) { cached = LightProfileResolver.Apply(source, values); valueKey = key; }
            return cached;
        }

        public LightProfileSnapshot PersistentSnapshot() => LightProfileResolver.Resolve(LightDefinitionEditor.Preset(definition),
            LightDefinitionEditor.Config(definition)?.Overrides ?? new LightOverrides(), LightDefinitionEditor.DefaultTemplate);

        public void Write(string field, object value, FlashlightDebugUndo undo)
        {
            var candidate = JsonUtility.FromJson<LightOverrides>(JsonUtility.ToJson(values));
            var member = typeof(LightOverrides).GetField(field);
            member.SetValue(candidate, value);
            if (Equals(Read(field), member.GetValue(candidate))) return;
            candidate.Mask |= LightParameterSchema.Mask(field);
            LightProfileResolver.Apply(Basis(), candidate);
            undo.Change(this, field, "修改手电运行时参数", () => values = candidate);
            ApplyRuntime();
        }

        public void Revert(string field, FlashlightDebugUndo undo)
        {
            if (!values.Has(LightParameterSchema.Mask(field))) return;
            undo.EndGesture();
            undo.Change(this, field, "恢复手电运行时参数", () => values.Mask &= ~LightParameterSchema.Mask(field));
            ApplyRuntime();
        }

        public void Reset(FlashlightDebugUndo undo)
        {
            if (values.Mask == LightOverrideMask.None) return;
            undo.EndGesture();
            undo.Change(this, "reset", "撤掉手电临时覆盖", () => values = new LightOverrides());
            ApplyRuntime();
        }

        public void ApplyRuntime()
        {
            var snapshot = Snapshot();
            if (ToolAlive)
            {
                tool.SetDebugLighting(this, snapshot);
                if (HasMountChanges) tool.SetDebugMount(this, mountPosition, mountRotation, mountScale);
                else tool.ClearDebugMount(this);
                if (!ReferenceEquals(applied, snapshot)) { applied = snapshot; LightingEditorRefresh.Request(); }
            }
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }

        public void WriteMount(string field, Vector3 value, FlashlightDebugUndo undo)
        {
            if (!MountReady) return;
            if (!float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(value.z)) throw new ArgumentException("灯口参数必须是有限数值。");
            if (field == "position" && mountPosition == value || field == "rotation" && mountRotation == value || field == "scale" && mountScale == value) return;
            undo.Change(this, "mount" + field, "修改运行时灯口", () =>
            {
                if (field == "position") mountPosition = value;
                else if (field == "rotation") mountRotation = value;
                else mountScale = value;
            });
            ApplyRuntime();
        }

        public void ResetMount(FlashlightDebugUndo undo)
        {
            if (!HasMountChanges) return;
            undo.EndGesture();
            undo.Change(this, "mountReset", "撤掉临时灯口调整", () =>
            { mountPosition = originalPosition; mountRotation = originalRotation; mountScale = originalScale; });
            ApplyRuntime();
        }

        public void Dispose()
        {
            if (ToolAlive) { tool.SetDebugLighting(this, null); tool.ClearDebugMount(this); }
            LightingEditorRefresh.Request();
            tool = null; instanceId = null;
            Undo.ClearUndo(this); DestroyImmediate(this);
        }

        public void AcceptSavedLight()
        {
            Undo.ClearUndo(this); values = new LightOverrides();
            originalPreset = LightDefinitionEditor.Preset(definition);
            initialConfig = LightDefinitionEditor.Config(definition) != null ? LightDefinitionEditor.Copy(LightDefinitionEditor.Config(definition)) : null;
            profileBaselines.Clear(); RememberProfile(preset);
            basis = null; cached = null; basisKey = null; ApplyRuntime();
        }

        public void AcceptSavedMount()
        {
            Undo.ClearUndo(this);
            originalPosition = mountPosition; originalRotation = mountRotation; originalScale = mountScale;
            prefabPosition = mountPosition; prefabRotation = mountRotation; prefabScale = mountScale;
            if (ToolAlive) tool.AcceptDebugMount(this);
        }
    }
}
