using System;
using System.Linq;
using DarkNights.View;
using GameCore.UI.UGUI;
using GameCore.Objects.Views;
using DarkNights.Entry.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>显式维护现有 HUD 根绑定及远征场景；保存后重开核对，不重建人工资源 GUID。</summary>
public static class ExpeditionPresentationRepair
{
    public static string Run()
    {
        const string chrome = "Assets/DarkNights/Res/UI/Chrome/Chrome.prefab";
        var root = PrefabUtility.LoadPrefabContents(chrome);
        try
        {
            var view = root.GetComponent<UGUIView>();
            var bindings = view.Bindings.Where(b => b.Key != "TopBar" && b.Key != "BottomBar").ToList();
            foreach (string key in new[] { "TopBar", "BottomBar" })
                bindings.Add(new ViewComponentBinding(key, root.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == key)));
            view.EditorSetBindings(bindings.ToArray(), false); PrefabUtility.SaveAsPrefabAsset(root, chrome);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        const string expedition = "Assets/DarkNights/Res/UI/Expedition/Expedition.prefab";
        root = PrefabUtility.LoadPrefabContents(expedition);
        try
        {
            var panel = root.GetComponent<ExpeditionPanel>();
            ((RectTransform)panel.Panel.transform).anchoredPosition = new Vector2(12, -12);
            PrefabUtility.SaveAsPrefabAsset(root, expedition);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
            var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
            var fields = new SerializedObject(stage);
            foreach (string key in new[] { "sky", "environment" })
            {
                var component = (Component)fields.FindProperty(key).objectReferenceValue;
                component.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
            }
            var backgrounds = fields.FindProperty("backgrounds");
            for (int i = 0; i < backgrounds.arraySize; i++)
            {
                var component = (Component)backgrounds.GetArrayElementAtIndex(i).objectReferenceValue;
                component.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
            }
            EditorSceneManager.SaveScene(scene); EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
        return "HUD bindings and cave scene saved/reopened; legacy forest disabled only in expedition.";
    }
}
