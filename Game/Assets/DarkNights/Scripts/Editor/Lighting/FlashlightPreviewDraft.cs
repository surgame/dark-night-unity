using System;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>右侧预览独占的临时副本；与单灯、场景共用有限撤销历史，只在统一保存成功后更新持久化基线。</summary>
    public sealed class FlashlightPreviewDraft : ScriptableObject
    {
        [SerializeField] private FlashlightPreviewSettings settings = new FlashlightPreviewSettings();
        [SerializeField] private string recordName;
        private string baseline;
        public FlashlightPreviewSettings Settings => settings;
        public string RecordName => recordName;
        public bool HasChanges => baseline != JsonUtility.ToJson(this);

        public void Initialize(FlashlightPreviewSettings source)
        {
            hideFlags = HideFlags.HideAndDontSave;
            settings = source?.Copy() ?? new FlashlightPreviewSettings(); settings.Validate();
            recordName = "光照调试 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"); AcceptSaved();
        }
        public void Write(string control, FlashlightDebugUndo undo, Action<FlashlightPreviewSettings> edit)
        {
            var candidate = settings.Copy(); edit(candidate); candidate.Validate();
            if (JsonUtility.ToJson(candidate) == JsonUtility.ToJson(settings)) return;
            undo.Change(this, control, "修改光照预览参数", () => settings = candidate);
        }
        public void Rename(string value, FlashlightDebugUndo undo)
        {
            value = (value ?? "").Trim();
            if (value == recordName) return;
            undo.Change(this, "previewName", "命名调试记录", () => recordName = value);
        }
        public void Load(FlashlightPreviewSettings source, string label)
        {
            source.Validate(); settings = source.Copy(); recordName = label;
        }
        public void AcceptSaved() => baseline = JsonUtility.ToJson(this);
    }
}
