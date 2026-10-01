using System;
using System.Linq;
using AnyRules.Next.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>网格业务配置工作台；联动原生 AnyRuleD 编辑器，草稿、编译应用与离线单格试采严格分开，不启动正式会话。</summary>
    internal sealed class TerrainBusinessWindow : EditorWindow
    {
        private TerrainProfileDraft draft;
        private SerializedObject serialized;
        private TerrainMiningPreview preview;
        private Vector2 scroll;
        private int tab, material;
        private bool contour;
        private string error = "";
        [MenuItem("Dark Nights/Terrain/网格业务配置工作台")]
        public static void Open() => GetWindow<TerrainBusinessWindow>("网格业务配置").Show();
        private void OnEnable() { minSize = new Vector2(680, 460); Reload(); }
        private void Reload()
        {
            preview?.Dispose(); preview = null;
            if (draft == null) { draft = CreateInstance<TerrainProfileDraft>(); draft.hideFlags = HideFlags.HideAndDontSave; }
            try { draft.Load(); serialized = new SerializedObject(draft); error = ""; contour = false; }
            catch (Exception exception) { error = exception.Message; }
        }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新加载／取消草稿")) Reload();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || serialized == null))
                    if (GUILayout.Button("编译并应用（创建新目录）")) Run(() => { serialized.ApplyModifiedProperties(); draft.Apply(); serialized.Update(); preview?.Dispose(); preview = null; });
            }
            if (error.Length > 0) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (serialized == null) return;
            tab = GUILayout.Toolbar(tab, new[] { "Profile 绑定", "规则瓦片与耐久", "工具与矿床", "单格试采" });
            serialized.Update(); scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) EditorGUILayout.PropertyField(serialized.FindProperty("Profile"), true);
            else if (tab == 1) DrawTerrain();
            else if (tab == 2)
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("Tools"), true);
                EditorGUILayout.PropertyField(serialized.FindProperty("Deposits"), true);
            }
            else DrawPreview();
            EditorGUILayout.EndScrollView(); serialized.ApplyModifiedProperties();
        }
        private void DrawTerrain()
        {
            bool selected = EditorGUILayout.Toggle("轮廓目录", contour);
            if (selected != contour) { contour = selected; Run(() => draft.LoadCatalog(contour)); }
            if (GUILayout.Button("重新读取当前目录的作者材质")) Run(() => draft.LoadCatalog(contour));
            using (new EditorGUI.DisabledScope(draft.Catalog == null))
                if (GUILayout.Button("打开原生 AnyRuleD 规则瓦片工作台"))
                    AnyRuleDWorkbench.OpenForCatalog(draft.Catalog);
            EditorGUILayout.HelpBox("HP 唯一来源为绑定的 GameplayDefinition；没有绑定时使用原生 TerrainDefinition 回退。应用会编译新目录，旧目录与 GUID 保留。", MessageType.Info);
            foreach (var entry in draft.Durability)
            {
                EditorGUILayout.LabelField(entry.Source.DisplayName + " · " + entry.Source.Key, EditorStyles.boldLabel);
                entry.HitPoints = EditorGUILayout.IntField("最大耐久", entry.HitPoints);
                using (new EditorGUI.DisabledScope(entry.Gameplay == null))
                {
                    entry.Hardness = EditorGUILayout.IntField("硬度", entry.Hardness);
                    entry.Stages = EditorGUILayout.IntSlider("受损阶段", entry.Stages, 0, 16);
                }
                if (entry.Source.Key == "bedrock") EditorGUILayout.LabelField("基岩不可破坏，HP 不用于免疫判断");
                if (GUILayout.Button("定位原生材质资产")) { Selection.activeObject = entry.Source; EditorGUIUtility.PingObject(entry.Source); }
            }
        }
        private void DrawPreview()
        {
            EditorGUILayout.HelpBox("独立单格试采，不写玩家存档，不代表正式鼠标、联机或事务验证。修改耐久后先编译应用，再重建试采格。", MessageType.Info);
            var keys = draft.Profile.Materials.Select(rule => rule.MaterialKey).ToArray();
            if (keys.Length == 0) return;
            material = EditorGUILayout.Popup("试采材质", Mathf.Clamp(material, 0, keys.Length - 1), keys);
            if (GUILayout.Button("从当前编译配置重建试采格"))
                Run(() => { preview?.Dispose(); preview = new TerrainMiningPreview(draft.Profile, draft.Tools, keys[material], contour); });
            if (preview == null) return;
            EditorGUILayout.LabelField("耐久", preview.Durability + "/" + preview.Maximum);
            EditorGUILayout.LabelField("命中次数", preview.Hits.ToString());
            EditorGUILayout.LabelField("累计产出", preview.Resource + " " + preview.Harvested);
            using (new EditorGUI.DisabledScope(!preview.CanMine || preview.Durability == 0))
                if (GUILayout.Button("矿镐命中一次")) Run(preview.Hit);
        }
        private void Run(Action action) { try { action(); error = ""; } catch (Exception exception) { error = exception.Message; } }
        private void OnDisable()
        {
            preview?.Dispose(); preview = null;
            if (draft != null) DestroyImmediate(draft);
            draft = null; serialized = null;
        }
    }
}
