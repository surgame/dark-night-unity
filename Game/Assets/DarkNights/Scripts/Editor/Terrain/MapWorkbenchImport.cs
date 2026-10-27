using System;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using UnityEditor;

namespace DarkNights.Editor.Terrain
{
    /// <summary>工作台文档和未保存窗口的历史导入边界；先核对原资源，再转换独立输入，不写回来源。</summary>
    internal static class MapWorkbenchImport
    {
        internal static ExpeditionFlowConfig Read(TerrainMapDocument document)
        {
            if (document == null || (document.Version != 1 && document.Version != 2) ||
                document.Input == null || document.Map == null || document.Style == null)
                throw new InvalidOperationException("地图配置缺少有效配方、地图或表现引用。");
            document.Input.Validate(); document.Map.VerifySource(document.Input);
            var input = ExpeditionFlowDraft.Clone(document.Input);
            if (document.Version == 2 && (string.IsNullOrEmpty(input.CaveMap.Layout) ||
                input.CaveMap.Structure == null || input.CaveMap.Minerals == null || input.CaveMap.Rules?.Enabled == true))
                throw new InvalidOperationException("配置未使用完整的统一生成配方。");
            using (var serialized = new SerializedObject(document))
                TerrainAuthoringImport.Normalize(input, serialized.FindProperty("importedVeinCount").intValue,
                    serialized.FindProperty("importedNests").boolValue);
            return input;
        }

        internal static void NormalizeDraft(ExpeditionFlowDraft draft)
        {
            if (!string.IsNullOrEmpty(draft.Config.CaveMap.Layout) &&
                !string.IsNullOrEmpty(draft.Config.CaveMap.SurfaceAlgorithm)) return;
            bool changed = draft.HasChanges;
            using (var data = new SerializedObject(draft))
            {
                int count = data.FindProperty("importedVeinCount").intValue;
                bool nests = data.FindProperty("importedNests").boolValue;
                if (count >= 0)
                    changed |= count != data.FindProperty("importedSavedCount").intValue ||
                        nests != data.FindProperty("importedSavedNests").boolValue;
                TerrainAuthoringImport.Normalize(draft.Config, count < 0 ? 6 : count, count < 0 || nests);
            }
            if (!changed) draft.MarkSaved();
        }
    }
}
