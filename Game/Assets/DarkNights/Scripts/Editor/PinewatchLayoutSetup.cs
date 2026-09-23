using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Runtime.Config;
using DarkNights.Runtime.Framework;
using GameCore.Objects.Definition;
using DarkNights.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 仅在指定空目录创建首版灰松谷布局场景，并在保存重开后对照正式配置校验。日常验证只读场景，绝不覆盖人工移动后的标记。
    /// </summary>
    public static class PinewatchLayoutSetup
    {
        public const string Root = "Assets/DarkNights/Res/Scenes/Pinewatch";
        public const string ScenePath = Root + "/Pinewatch.unity";

        public static void Create()
        {
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("Pinewatch layout output must be empty; authored scenes are never overwritten.");
            Scene previous = SceneManager.GetActiveScene();
            if (!previous.IsValid() || string.IsNullOrEmpty(previous.path) || previous.isDirty)
                throw new InvalidOperationException("Open and save a clean project scene before creating initial layout assets.");
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateSceneObjects();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Could not save Pinewatch layout scene.");
                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Validate();
            }
            finally
            {
                if (File.Exists(previous.path))
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
            Debug.Log("DARK_NIGHTS_PINEWATCH_LAYOUT_CREATED placements=16");
        }

        public static LevelLayout Validate()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = scene.IsValid() && scene.isLoaded;
            if (!opened)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                LevelLayoutAuthoring authoring = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LevelLayoutAuthoring>(true)).Single();
                foreach (ScenePlacement placement in authoring.GetComponentsInChildren<ScenePlacement>(true))
                    SceneDefinitionAuthoring.ValidatePlacement(placement);
                return authoring.CreateLayout(LoadCatalog(), DefinitionRuleIndex.RuleKey);
            }
            finally
            {
                if (!opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CreateSceneObjects()
        {
            var root = new GameObject("Pinewatch");
            var layoutObject = Child(root.transform, "Layout");
            var authoring = layoutObject.gameObject.AddComponent<LevelLayoutAuthoring>();
            Transform ground = Anchor(layoutObject, "GroundBaseline", 0);
            Transform worldEnd = Anchor(layoutObject, "WorldEnd", 1100);
            Transform buildStart = Anchor(layoutObject, "BuildStart", 30);
            Transform buildEnd = Anchor(layoutObject, "BuildEnd", 850);
            Transform enemySpawn = Anchor(layoutObject, "EnemySpawn", 1020);
            Transform cameraStart = Anchor(layoutObject, "CameraStart", 255);
            Transform buildings = Child(layoutObject, "Buildings");
            Transform worksites = Child(layoutObject, "Worksites");
            Transform actors = Child(layoutObject, "Actors");
            Configure(authoring, ground, worldEnd, buildStart, buildEnd, enemySpawn, cameraStart, buildings, worksites, actors);

            Marker(buildings, "House1", "house", 55);
            Marker(buildings, "Tavern2", "tavern", 130);
            Marker(buildings, "Barracks3", "barracks", 245);
            Marker(buildings, "Farm4", "farm", 330);
            Marker(worksites, "Wood1", "wood", 402, 1);
            Marker(worksites, "Wood2", "wood", 449);
            Marker(worksites, "Wood3", "wood", 488, 2);
            Marker(worksites, "Stone4", "stone", 548);
            Marker(worksites, "Iron5", "iron", 592);
            Marker(actors, "Worker1", "worker", 170, 0, "艾达");
            Marker(actors, "Worker2", "worker", 187, 0, "罗恩");
            Marker(actors, "Worker3", "worker", 293, 0, "米娅");
            Marker(actors, "Worker4", "worker", 311, 0, "伊恩");
            Marker(actors, "Worker5", "worker", 357, 0, "莉娜");
            Marker(actors, "Spearman6", "spearman", 660, 0, "奥斯");
            Marker(actors, "Archer7", "archer", 625, 0, "薇拉");
        }

        private static GameCatalog LoadCatalog()
        {
            var balance = AssetDatabase.LoadAssetAtPath<TextAsset>(GameContentSetup.ConfigRoot + "balance.json");
            var level = AssetDatabase.LoadAssetAtPath<TextAsset>(GameContentSetup.ConfigRoot + "pinewatch.json");
            if (balance == null || level == null)
                throw new InvalidOperationException("Import formal Pinewatch configuration before validating its layout.");
            return GameCatalogJson.Parse(balance.text, level.text);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform Anchor(Transform parent, string name, float x)
        {
            Transform anchor = Child(parent, name);
            anchor.localPosition = new Vector3(x, 320, 0);
            return anchor;
        }

        private static void Marker(Transform parent, string name,
            string contentId, float x, int variant = 0, string initialName = "")
        {
            var definitions = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance);
            SceneDefinitionAuthoring.Create(definitions.GetRequired(contentId), parent,
                new Vector3(x, 320, 0), name, variant, initialName);
        }

        private static void Configure(LevelLayoutAuthoring authoring, params Transform[] references)
        {
            string[] fields = { "groundBaseline", "worldEnd", "buildStart", "buildEnd", "enemySpawn",
                "cameraStart", "buildings", "worksites", "actors" };
            var serialized = new SerializedObject(authoring);
            for (int index = 0; index < fields.Length; index++)
                serialized.FindProperty(fields[index]).objectReferenceValue = references[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
