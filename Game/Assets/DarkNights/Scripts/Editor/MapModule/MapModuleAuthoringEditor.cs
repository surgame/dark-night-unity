using System;
using DarkNights.View.MapModule;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 模块Prefab的原生轮廓编辑器；格点手柄只在Prefab模式编辑作者顶点，支持Undo及原生保存。
    /// 派生预览经显式按钮写为新网格资产，不覆盖旧资源；场景实例只用于摆放，不承接轮廓作者权。
    /// </summary>
    [CustomEditor(typeof(MapModuleAuthoring))]
    internal sealed class MapModuleAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            bool authoring = InPrefab();
            EditorGUILayout.HelpBox(authoring ? "在Scene视图拖动轮廓格点；完成后更新预览并保存Prefab。编译不支持的坡度会在工作台检查时报错。" :
                "Scene实例负责摆放。编辑内部轮廓请进入模块Prefab；不要在实例上创建形状覆盖。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!authoring || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                DrawDefaultInspector();
                if (GUILayout.Button("根据当前轮廓生成新预览"))
                {
                    try { RebuildPreview((MapModuleAuthoring)target); }
                    catch (Exception error) { EditorUtility.DisplayDialog("轮廓预览未生成", error.Message, "知道了"); }
                }
            }
        }
        private bool InPrefab()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.IsPartOfPrefabContents(((MapModuleAuthoring)target).gameObject);
        }
        private void OnSceneGUI()
        {
            if (!InPrefab() || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var author = (MapModuleAuthoring)target;
            foreach (var solid in author.Solids)
            {
                if (solid?.Vertices == null) continue;
                for (int i = 0; i < solid.Vertices.Length; i++)
                {
                    Vector3 original = author.WorldPoint(solid.Vertices[i]);
                    Handles.color = new Color(.98f, .8f, .35f);
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.FreeMoveHandle(original, .035f, Vector3.zero, Handles.DotHandleCap);
                    if (!EditorGUI.EndChangeCheck()) continue;
                    Undo.RecordObject(author, "编辑模块轮廓格点");
                    Vector3 local = moved - author.transform.position;
                    solid.Vertices[i] = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(local.x / .16f + .5f), 0, author.Footprint.x * 20),
                        Mathf.Clamp(Mathf.RoundToInt(local.y / .16f), 0, author.Footprint.y * 20));
                    EditorUtility.SetDirty(author); EditorSceneManager.MarkSceneDirty(author.gameObject.scene);
                }
            }
        }
        private static void RebuildPreview(MapModuleAuthoring author)
        {
            author.Validate();
            if (author.PreviewRenderer == null) throw new InvalidOperationException("缺少原生预览绑定。");
            var filter = author.PreviewRenderer.GetComponent<MeshFilter>();
            if (filter == null) throw new InvalidOperationException("预览绑定需要同根MeshFilter。");
            var mesh = MapModulePrefabFactory.BuildPreview(author);
            string path = AssetDatabase.GenerateUniqueAssetPath(MapModulePaths.Modules + "/" + author.ModuleId + "-preview.asset");
            AssetDatabase.CreateAsset(mesh, path);
            Undo.RecordObject(filter, "安装新轮廓预览"); filter.sharedMesh = mesh; EditorUtility.SetDirty(filter);
            EditorSceneManager.MarkSceneDirty(author.gameObject.scene); SceneView.RepaintAll();
        }
    }
}
