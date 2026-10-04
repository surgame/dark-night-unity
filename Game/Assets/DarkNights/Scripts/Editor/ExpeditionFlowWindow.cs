using System;
using DarkNights.Editor.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 旧航程窗口的草稿转移入口；新导航直接打开 Tuner，不再维护第二套参数页面。
    /// 域重载后先复制尚未应用的作者草稿，成功转移才关闭旧窗口；冲突时保留旧草稿供后续处理。
    /// </summary>
    public sealed class ExpeditionFlowWindow : EditorWindow
    {
        [SerializeField] private ExpeditionFlowDraft draft;
        private string error;
        public static void Open() => TerrainStylePreviewWindow.OpenJourney();
        private void OnEnable() => EditorApplication.delayCall += Forward;
        private void OnDisable() => EditorApplication.delayCall -= Forward;
        private void OnDestroy() { if (draft != null) DestroyImmediate(draft); }

        private void Forward()
        {
            if (this == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try
            {
                TerrainStylePreviewWindow.OpenJourney();
                var target = GetWindow<TerrainStylePreviewWindow>();
                target.ImportLegacyDraft(draft);
                hasUnsavedChanges = false; Close();
            }
            catch (Exception failure) { error = failure.Message; CreateGUI(); }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.Add(new HelpBox(error ?? "星球与航程已合并到 Cave Wall Tuner。未应用草稿会先转移，再关闭此窗口。",
                error == null ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning));
            rootVisualElement.Add(new Button(Forward) { text = "转移草稿并打开 Tuner" });
        }
    }
}
