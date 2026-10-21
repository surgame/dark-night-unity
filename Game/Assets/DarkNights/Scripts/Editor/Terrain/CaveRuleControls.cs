using System;
using DarkNights.Core.Config.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>既有Tuner的规则制作分组；平铺目录和参数通过原草稿记录Undo，生成必须显式请求，内容重抽只改内容修订。</summary>
    internal static class CaveRuleControls
    {
        internal static void Draw(TerrainGenerationPreview model, Action changed)
        {
            using (var serialized = new SerializedObject(model.Draft))
            {
                serialized.Update(); EditorGUI.BeginChangeCheck();
                var root = serialized.FindProperty("Config.CaveMap.Rules");
                EditorGUILayout.PropertyField(root.FindPropertyRelative("Enabled"), new GUIContent("启用地图生成规则"));
                if (root.FindPropertyRelative("Enabled").boolValue)
                {
                    EditorGUILayout.LabelField("入口形状", EditorStyles.boldLabel);
                    Choices(root.FindPropertyRelative("Entrance"), CaveRuleSettings.Entrances,
                        new[] { "凹谷侧入口", "半塌陷洞口", "山脚横洞", "天然石桥", "干涸河床", "巨石压顶", "断层阶台", "根系裂口", "单向井对照" });
                    EditorGUILayout.LabelField("道路拓扑", EditorStyles.boldLabel);
                    Choices(root.FindPropertyRelative("Road"), CaveRuleSettings.Roads,
                        new[] { "侵蚀曲廊", "断层折返", "洞厅星簇", "裂谷侧坡", "密岔根网" });
                    EditorGUILayout.LabelField("矿室配比", EditorStyles.boldLabel);
                    Choices(root.FindPropertyRelative("RoomMix"), CaveRuleSettings.Mixes,
                        new[] { "混合地质", "裂隙与长缝", "大洞厅与岩台", "矿窝与双腔" });
                    Field(root, "SpaceRevision", "空间修订"); Field(root, "ContentSeed", "内容种子"); Field(root, "ContentRevision", "内容修订");
                    Integer(root, "RoomCount", "请求矿室数", 9, 22); Number(root, "RoomScale", "矿室尺度", .65, 1.25);
                    Integer(root, "RoadRadius", "道路半径", 2, 5); Number(root, "BranchShare", "支线占比", .2, .65);
                    Integer(root, "BranchDepth", "支线层级上限", 1, 4); Number(root, "RockGap", "支线封岩比例", 0, .4);
                    Integer(root, "VeinCount", "矿团预算", 1, 16); Integer(root, "Capacity", "基础储量", 96, 2000);
                    Number(root, "EmptyShare", "无矿区域比例", 0, .6); Number(root, "ExposedShare", "露矿格比例上限", .1, .8);
                    Number(root, "RichShare", "富矿区域比例", 0, .8); Field(root, "Nests", "富矿可伴侧巢");
                }
                bool edited = EditorGUI.EndChangeCheck(); bool applied = serialized.ApplyModifiedProperties();
                if (edited || applied) changed();
            }
            if (!model.RuleMode) return;
            EditorGUILayout.HelpBox("规则与.minemap互斥；旧入口步道在本模式跳过。参数变化只标记过期，点击生成才更新。空间／内容参数保存到正式作者草稿；不检查人物通行。", MessageType.Info);
            if (model.LastCandidateIdentity != model.Identity) EditorGUILayout.HelpBox("候选已过期，冻结按钮不可用。", MessageType.Warning);
            if (GUILayout.Button("生成规则候选")) { model.RequestGeneration(); changed(); }
            if (GUILayout.Button("只重抽内容（空间保持）"))
            {
                Undo.RecordObject(model.Draft, "重抽规则地图内容");
                model.Draft.Config.CaveMap.Rules.ContentRevision = checked(model.Draft.Config.CaveMap.Rules.ContentRevision + 1);
                EditorUtility.SetDirty(model.Draft); model.RequestGeneration(); changed();
            }
            using (new EditorGUI.DisabledScope(model.HasChanges || model.LastCandidateIdentity != model.Identity))
                if (GUILayout.Button("冻结当前候选并用于新局"))
                {
                    try { model.ExportJoint(); model.ObserveExternalChanges(); }
                    catch (Exception error) { Debug.LogWarning("规则地图未冻结：" + error.Message); }
                    changed();
                }
            CaveRuleOverlay.DrawControls();
            foreach (string line in model.ContentDiagnostics) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox("临时拆填不会随冻结导出；逐间拖拽、局部锁定与持久修整尚未实现。危险标记只作制作信息，塌方仍在原设计点页配置。", MessageType.None);
        }

        private static void Choices(SerializedProperty property, string[] values, string[] labels)
        {
            for (int row = 0; row < values.Length; row += 3)
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = row; i < Math.Min(row + 3, values.Length); i++)
                    if (GUILayout.Toggle(property.stringValue == values[i], labels[i], "Button")) property.stringValue = values[i];
                EditorGUILayout.EndHorizontal();
            }
        }
        private static void Field(SerializedProperty root, string name, string label) => EditorGUILayout.PropertyField(root.FindPropertyRelative(name), new GUIContent(label));
        private static void Integer(SerializedProperty root, string name, string label, int min, int max)
        {
            var field = root.FindPropertyRelative(name); field.intValue = EditorGUILayout.IntSlider(label, field.intValue, min, max);
        }
        private static void Number(SerializedProperty root, string name, string label, double min, double max)
        {
            var field = root.FindPropertyRelative(name);
            EditorGUI.BeginChangeCheck(); float next = EditorGUILayout.Slider(label, (float)field.doubleValue, (float)min, (float)max);
            if (EditorGUI.EndChangeCheck()) field.doubleValue = next;
        }
    }
}
