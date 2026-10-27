using System;
using DarkNights.Core.Config.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>附属IMGUI制作入口的标准配方编辑；与主工作台共用同一布局和矿物参数，不提供版本模式切换。</summary>
    internal static class CaveRuleControls
    {
        internal static void Draw(TerrainGenerationPreview model, Action changed)
        {
            using (var serialized = new SerializedObject(model.Draft))
            {
                serialized.Update(); EditorGUI.BeginChangeCheck();
                var layout = serialized.FindProperty("Config.CaveMap.Layout");
                int index = Math.Max(0, Array.IndexOf(TerrainGenerationSettings.Layouts, layout.stringValue));
                int next = EditorGUILayout.Popup("布局算法", index, new[] { "连通洞室", "天然洞穴", "地形模板" });
                if (next != index) layout.stringValue = TerrainGenerationSettings.Layouts[next];
                if (model.HasRoomPlan) EditorGUILayout.PropertyField(serialized.FindProperty("Config.CaveMap.Structure"),
                    new GUIContent("洞穴结构"), true);
                EditorGUILayout.PropertyField(serialized.FindProperty("Config.CaveMap.Minerals"), new GUIContent("矿物参数"), true);
                bool edited = EditorGUI.EndChangeCheck(); bool applied = serialized.ApplyModifiedProperties();
                if (edited || applied) changed();
            }
            CaveRuleOverlay.DrawControls();
            foreach (string line in model.ContentDiagnostics) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
        }
    }
}
