using System;
using System.Linq;
using DarkNights.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>退出旧洞口装饰及环境静态引用，原始美术作为来源证据保留；保存重开检查引用。</summary>
public static class ExpeditionRemoveTomb
{
    public static string Run()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup(); int removed = 0;
        try
        {
            foreach (string path in new[] { "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity",
                "Assets/DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity", DarkNights.Entry.Terrain.RandomLevelEntry.ExpeditionScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var tombs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Tomb").ToArray();
                foreach (var env in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NativeEnvironment>(true)))
                {
                    var data = new SerializedObject(env); var sprites = data.FindProperty("staticSprites"); var colors = data.FindProperty("staticColors");
                    for (int i = sprites.arraySize - 1; i >= 0; i--)
                    {
                        var sprite = sprites.GetArrayElementAtIndex(i).objectReferenceValue as SpriteRenderer;
                        if (sprite == null || tombs.Contains(sprite.transform))
                        {
                            sprites.GetArrayElementAtIndex(i).objectReferenceValue = null;
                            sprites.DeleteArrayElementAtIndex(i); colors.DeleteArrayElementAtIndex(i);
                        }
                    }
                    data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(env);
                }
                foreach (var tomb in tombs) { UnityEngine.Object.DestroyImmediate(tomb.gameObject); removed++; }
                EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene(path);
                if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "Tomb")) throw new Exception("Legacy tomb survived save/reopen.");
            }
            return "Removed Tomb instances: " + removed + "; all three scenes saved/reopened.";
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
}
