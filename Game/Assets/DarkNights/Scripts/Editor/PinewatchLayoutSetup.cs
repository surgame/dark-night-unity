using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Runtime.Config;
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

        [MenuItem("Dark Nights/Content/Create Initial Pinewatch Layout")]
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
                return authoring.CreateLayout(LoadCatalog());
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

            Marker(buildings, "House1", LevelPlacementCategory.Building, "house", 55, 0);
            Marker(buildings, "Tavern2", LevelPlacementCategory.Building, "tavern", 130, 1);
            Marker(buildings, "Barracks3", LevelPlacementCategory.Building, "barracks", 245, 2);
            Marker(buildings, "Farm4", LevelPlacementCategory.Building, "farm", 330, 3);
            Marker(worksites, "Wood1", LevelPlacementCategory.Worksite, "wood", 402, 0, 1);
            Marker(worksites, "Wood2", LevelPlacementCategory.Worksite, "wood", 449, 1);
            Marker(worksites, "Wood3", LevelPlacementCategory.Worksite, "wood", 488, 2, 2);
            Marker(worksites, "Stone4", LevelPlacementCategory.Worksite, "stone", 548, 3);
            Marker(worksites, "Iron5", LevelPlacementCategory.Worksite, "iron", 592, 4);
            Marker(actors, "Worker1", LevelPlacementCategory.Actor, "worker", 170, 0, 0, "艾达");
            Marker(actors, "Worker2", LevelPlacementCategory.Actor, "worker", 187, 1, 0, "罗恩");
            Marker(actors, "Worker3", LevelPlacementCategory.Actor, "worker", 293, 2, 0, "米娅");
            Marker(actors, "Worker4", LevelPlacementCategory.Actor, "worker", 311, 3, 0, "伊恩");
            Marker(actors, "Worker5", LevelPlacementCategory.Actor, "worker", 357, 4, 0, "莉娜");
            Marker(actors, "Spearman6", LevelPlacementCategory.Actor, "spearman", 660, 5, 0, "奥斯");
            Marker(actors, "Archer7", LevelPlacementCategory.Actor, "archer", 625, 6, 0, "薇拉");
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

        private static void Marker(Transform parent, string name, LevelPlacementCategory category,
            string contentId, float x, int order, int variant = 0, string actorName = "")
        {
            Transform markerTransform = Anchor(parent, name, x);
            var marker = markerTransform.gameObject.AddComponent<LevelPlacementMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.FindProperty("contentId").stringValue = contentId;
            serialized.FindProperty("spawnOrder").intValue = order;
            serialized.FindProperty("variant").intValue = variant;
            serialized.FindProperty("actorName").stringValue = actorName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
