using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调试台灯口草稿面板；仅改临时姿态及实例效果，Prefab Stage 只读用于预览定位，确认保存才交原生 Prefab 写回入口。</summary>
    public sealed class LightMountPanel : IDisposable
    {
        private readonly FlashlightDebugDraft draft;
        private readonly FlashlightDebugUndo undo;
        private readonly Vector3Field position = new Vector3Field("局部 Position");
        private readonly Vector3Field rotation = new Vector3Field("局部 Rotation");
        private readonly Vector3Field scale = new Vector3Field("局部 Scale");
        private LightEffectPreview preview;
        private GameObject previewAnchor;
        public VisualElement Root { get; } = new VisualElement();

        public LightMountPanel(FlashlightDebugDraft draft, FlashlightDebugUndo undo, Action save)
        {
            this.draft = draft; this.undo = undo;
            Root.Add(new Label("只调整运行实例／临时预览；确认保存后才写入物体 Prefab。"));
            Root.Add(new Button(OpenPrefab) { text = "打开物体 Prefab 查看预览" });
            Root.Add(position); Root.Add(rotation); Root.Add(scale);
            position.RegisterValueChangedCallback(evt => { draft.WriteMount("position", evt.newValue, undo); Refresh(); });
            rotation.RegisterValueChangedCallback(evt => { draft.WriteMount("rotation", evt.newValue, undo); Refresh(); });
            scale.RegisterValueChangedCallback(evt => { draft.WriteMount("scale", evt.newValue, undo); Refresh(); });
            Root.Add(new Button(() => { draft.ResetMount(undo); Refresh(); }) { text = "撤掉临时灯口调整" });
            Root.Add(new Button(save) { text = "确认保存灯口到 Prefab" });
            Refresh();
        }

        public void Refresh()
        {
            position.SetEnabled(draft.MountReady); rotation.SetEnabled(draft.MountReady); scale.SetEnabled(draft.MountReady);
            position.SetValueWithoutNotify(draft.MountPosition); rotation.SetValueWithoutNotify(draft.MountRotation); scale.SetValueWithoutNotify(draft.MountScale);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string path = draft.Definition?.PrefabRef != null ? AssetDatabase.GUIDToAssetPath(draft.Definition.PrefabRef.AssetGUID) : "";
            if (draft.ToolAlive || !draft.MountReady || stage == null || stage.assetPath != path) { ReleasePreview(); return; }
            previewAnchor = previewAnchor != null ? previewAnchor : new GameObject("灯口临时预览") { hideFlags = HideFlags.HideAndDontSave };
            previewAnchor.transform.position = stage.prefabContentsRoot.transform.TransformPoint(draft.MountPosition);
            previewAnchor.transform.rotation = stage.prefabContentsRoot.transform.rotation * Quaternion.Euler(draft.MountRotation);
            previewAnchor.transform.localScale = draft.MountScale;
            preview = preview ?? new LightEffectPreview(); preview.Bind(draft.Snapshot(), previewAnchor.transform);
        }

        private void OpenPrefab()
        {
            if (draft.Definition?.PrefabRef == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(draft.Definition.PrefabRef.AssetGUID));
            if (prefab != null) AssetDatabase.OpenAsset(prefab);
        }
        private void ReleasePreview()
        {
            preview?.Dispose(); preview = null;
            if (previewAnchor != null) UnityEngine.Object.DestroyImmediate(previewAnchor);
            previewAnchor = null;
        }
        public void Dispose() => ReleasePreview();
    }
}
