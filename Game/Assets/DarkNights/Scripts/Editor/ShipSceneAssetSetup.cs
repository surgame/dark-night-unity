using System;
using System.Linq;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>显式更新远征模板中的船体和原生 HUD；保留场景 GUID、放置身份与其他地图作者数据，并保存重开验证。</summary>
    internal static class ShipSceneAssetSetup
    {
        internal static void Install(ObjectDefinition ship)
        {
            string hudPath = CreateHud();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
                var template = UnityEngine.Object.FindAnyObjectByType<RandomLevelTemplate>();
                if (template.ContourDefinition == null || template.StaticBackgroundStyle == null) throw new InvalidOperationException("当前岩层配置缺失。");
                template.Definition = template.ContourDefinition; template.CaveStyle = template.StaticBackgroundStyle;
                EditorUtility.SetDirty(template);
                var old = UnityEngine.Object.FindObjectsByType<ScenePlacement>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(p => DefinitionRuleIndex.RuleKey(p.Loader.ResolveDefinition()) == "ship");
                var parent = old.transform.parent; var position = old.transform.localPosition; string key = old.PlacementKey;
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                var placement = SceneDefinitionAuthoring.Create(ship, parent, position, "可步入远征飞船", 0, "");
                var data = new SerializedObject(placement); data.FindProperty("placementKey").stringValue = key; data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var panel in UnityEngine.Object.FindObjectsByType<ExpeditionPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(panel.gameObject);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(hudPath), scene);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
                var check = UnityEngine.Object.FindObjectsByType<ScenePlacement>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                SceneDefinitionAuthoring.ValidatePlacement(check);
                if (check.PlacementKey != key || UnityEngine.Object.FindAnyObjectByType<ExpeditionPanel>().Commands.Length != 17)
                    throw new InvalidOperationException("场景重开后身份或 HUD 绑定不匹配。");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static string CreateHud()
        {
            const string source = "Assets/DarkNights/Res/UI/Expedition/Expedition.prefab";
            const string path = ShipAssetSetup.Root + "/ShipHud.prefab";
            var root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                var panel = root.GetComponent<ExpeditionPanel>(); var original = panel.Actions[0];
                string[] commands = { "depart", "unload", "board", "recall", "launch", "emergency", "robot", "cargo", "crew", "relay", "mine", "resupply", "pilot", "takeoff", "land", "cancel-flight", "deploy" };
                string[] labels = { "开始远征", "坡道卸货", "船内卸货", "召回", "返航结算", "紧急返航", "机器人舱 10铁", "货舱 10铁", "船员舱 10铁", "搬迁中继", "矿工派工", "补充损失", "驾驶 / 离座", "收舱试飞", "泊位着陆", "取消收舱", "重新派出设备" };
                var buttons = new Button[commands.Length];
                for (int i = 0; i < commands.Length; i++)
                {
                    var button = UnityEngine.Object.Instantiate(original, original.transform.parent);
                    button.name = commands[i]; ((RectTransform)button.transform).anchoredPosition = new Vector2(10 + i % 3 * 141, -168 - i / 3 * 30);
                    button.GetComponentInChildren<Text>().text = labels[i]; buttons[i] = button;
                }
                foreach (var button in panel.Actions) UnityEngine.Object.DestroyImmediate(button.gameObject);
                panel.Commands = commands; panel.Actions = buttons;
                ((RectTransform)panel.Panel.transform).sizeDelta = new Vector2(440, 352);
                panel.Status.rectTransform.sizeDelta = new Vector2(420, 152);
                panel.Panel.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return path;
        }
    }
}
