using System;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>原生 YYGC Definition 的工具与目标编辑入口；只编辑对应资产，匹配预览不运行会话或编译地形。</summary>
    internal sealed class MiningDefinitionWindow : EditorWindow
    {
        private ObjectDefinition tool, deposit;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        private int tab;
        private bool rare;
        private string message = "";
        [MenuItem("Dark Nights/配置/工具与采集能力")]
        public static void Open() => GetWindow<MiningDefinitionWindow>("工具 Definition").Show();
        private void OnEnable()
        {
            tool = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/ShipTrade/item-pickaxe.asset");
            deposit = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/MineralDeposit/MineralDeposit.asset");
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("直接编辑下方选中的 YYGC ObjectDefinition。能力装配在 BehaviourTypes，参数在 SharedConfigs；新会话生效，保存不编译地形。", MessageType.Info);
            tab = GUILayout.Toolbar(tab, new[] { "工具 Definition", "矿床 Definition", "匹配预览" });
            tool = (ObjectDefinition)EditorGUILayout.ObjectField("工具 Definition", tool, typeof(ObjectDefinition), false);
            if (tab != 0) deposit = (ObjectDefinition)EditorGUILayout.ObjectField("矿床 Definition", deposit, typeof(ObjectDefinition), false);
            if (tab == 2) { DrawMatch(); return; }
            var selected = tab == 0 ? tool : deposit;
            if (selected == null) return;
            if (GUILayout.Button("在 Project / Inspector 中定位")) { Selection.activeObject = selected; EditorGUIUtility.PingObject(selected); }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                UnityEditor.Editor.CreateCachedEditor(selected, null, ref inspector);
                inspector.OnInspectorGUI();
                if (GUILayout.Button("校验并保存此 Definition"))
                {
                    try
                    {
                        selected.SharedConfigs.OfType<MiningToolConfig>().SingleOrDefault()?.Freeze();
                        selected.SharedConfigs.OfType<MineralDepositRuleConfig>().SingleOrDefault()?.Validate();
                        EditorUtility.SetDirty(selected); AssetDatabase.SaveAssetIfDirty(selected); message = "已保存，新会话生效。";
                    }
                    catch (Exception error) { message = error.Message; }
                }
            }
            if (message.Length > 0) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        private void DrawMatch()
        {
            rare = EditorGUILayout.Toggle("稀有矿床材料", rare);
            try
            {
                var mining = tool?.SharedConfigs.OfType<MiningToolConfig>().SingleOrDefault()?.Freeze();
                var target = deposit?.SharedConfigs.OfType<MineralDepositRuleConfig>().SingleOrDefault();
                if (mining == null || target == null) { EditorGUILayout.HelpBox("请选择带采集配置的工具与矿床 Definition。", MessageType.Info); return; }
                target.Validate();
                string material = rare ? target.RareResource : target.CommonResource;
                string reason = mining.BlockReason(HeroMiningTargetKind.MineralDeposit, material, target.RequiredMiningLevel, deposit.Guid.ToString());
                EditorGUILayout.LabelField("目标材料", material);
                EditorGUILayout.HelpBox(reason.Length == 0 ? "能力匹配通过；实际执行还会校验距离、遮挡、目标状态和权限。" : reason, MessageType.Info);
            }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        }
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    }
}
