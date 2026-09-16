using System;
using System.IO;
using System.Linq;
using AnyRules.Next.Authoring;
using DarkNights.Entry.Terrain;
using DarkNights.View;
using DarkNights.View.Terrain;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>显式创建独立灰松谷随机模板并添加原生选图按钮；只运行一次，不覆盖既有随机场景或修改原 Pinewatch。</summary>
    public static class RandomLevelSetup
    {
        [MenuItem("Dark Nights/Terrain/Create random Pinewatch template")]
        public static void Install()
        {
            const string original = "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity";
            if (File.Exists(RandomLevelEntry.ScenePath)) throw new InvalidOperationException("随机关卡模板已经存在，拒绝覆盖。");
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i =>
                UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("先保存已修改的场景。");
            AssetDatabase.CreateFolder("Assets/DarkNights/Res/Scenes", "RandomPinewatch");
            if (!AssetDatabase.CopyAsset(original, RandomLevelEntry.ScenePath)) throw new IOException("无法复制独立关卡模板。");
            var scene = EditorSceneManager.OpenScene(RandomLevelEntry.ScenePath, OpenSceneMode.Additive);
            try
            {
                var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PinewatchStage>(true)).Single();
                var template = stage.gameObject.AddComponent<RandomLevelTemplate>();
                template.Definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(TerrainTestAssets.DefinitionPath);
                var layout = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelLayoutAuthoring>(true)).Single();
                foreach (var platform in layout.GetComponentsInChildren<HeroPlatform>(true)) UnityEngine.Object.DestroyImmediate(platform.gameObject);
                var serialized = new SerializedObject(layout); serialized.FindProperty("platforms").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // 仅在新模板隐藏平地装饰；原始场景和共用 Prefab 不变。
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<Transform>(true))
                        if (item.name == "Ground" || item.name.StartsWith("Grass", StringComparison.Ordinal)) item.gameObject.SetActive(false);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            const string path = "Assets/DarkNights/Res/UI/MainMenu/MainMenu.prefab";
            var menu = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = menu.GetComponent<UGUIView>(); var bindings = view.Bindings.ToDictionary(b => b.Key, b => b.Target);
                if (bindings.ContainsKey("Map")) throw new InvalidOperationException("选图按钮已经存在。");
                Font font = AssetDatabase.LoadAssetAtPath<Font>(NativeUiSetup.Root + "/Shared/UIFont.fontsettings");
                NativeUiExtras.AddButton(menu.transform, "Map", "选择地图：灰松谷 · 生成新地图", 488, 62, 410, 34, font, bindings);
                NativeUiBuilder.Bind(view, bindings); PrefabUtility.SaveAsPrefabAsset(menu, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(menu); }
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(RandomLevelEntry.ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
