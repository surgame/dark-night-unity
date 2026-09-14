using System.Linq;
using DarkNights.View;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 只展示场景实例可编辑的初值，并把稳定放置身份显示为只读摘要。
    /// 身份缺失或复制冲突时提供显式修复，不允许制作人员直接输入内部键值。
    /// </summary>
    [CustomEditor(typeof(ScenePlacement))]
    public sealed class ScenePlacementEditor : UnityEditor.Editor
    {
        private SerializedProperty initialVariant;
        private SerializedProperty initialName;

        private void OnEnable()
        {
            initialVariant = serializedObject.FindProperty("initialVariant");
            initialName = serializedObject.FindProperty("initialName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(initialVariant, new GUIContent("初始外观变体"));
            EditorGUILayout.PropertyField(initialName, new GUIContent("初始名称"));
            serializedObject.ApplyModifiedProperties();

            var placement = (ScenePlacement)target;
            string key = placement.PlacementKey;
            bool valid = System.Guid.TryParseExact(key, "N", out _);
            string summary = !valid ? "未生成或格式无效" : key.Substring(0, 8) + "…（自动）";
            EditorGUILayout.LabelField("放置身份", summary);
            bool duplicate = valid && placement.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ScenePlacement>(true))
                .Count(other => other.PlacementKey == key) != 1;
            if (valid && !duplicate) return;

            EditorGUILayout.HelpBox(duplicate ? "复制产生了重复放置身份。" : "放置身份尚未生成或格式无效。", MessageType.Error);
            if (!GUILayout.Button("生成唯一放置身份")) return;
            Undo.RecordObject(placement, "Generate scene placement identity");
            do placement.EditorRegeneratePlacementKey();
            while (placement.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ScenePlacement>(true))
                .Count(other => other.PlacementKey == placement.PlacementKey) != 1);
            EditorUtility.SetDirty(placement);
        }
    }
}
