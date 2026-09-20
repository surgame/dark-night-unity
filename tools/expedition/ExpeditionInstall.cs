using System;
using System.IO;
using System.Linq;
using DarkNights.Editor;
using DarkNights.Editor.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Terrain;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

/// <summary>远征原生资产的显式首版安装器；仅输出新目录，原 Prefab 与场景不覆盖，完成后保存并重开核对。</summary>
public static class ExpeditionInstall
{
    private const string Art = "Assets/DarkNights/Res/Art/Custom/ExpeditionPrototype";
    private const string Objects = "Assets/DarkNights/Res/Objects/Expedition";
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)) throw new Exception("先退出 Play 并保存场景。");
        if (Directory.Exists(Objects) || File.Exists(RandomLevelEntry.ExpeditionScenePath)) throw new Exception("远征资产已存在，拒绝覆盖人工编辑。");
        foreach (string path in Directory.GetFiles(Art, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.sRGBTexture = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = 9; settings.spritePivot = new Vector2(.5f, 0); importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        Folder(Objects);
        var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
        foreach (string key in new[] { "ship", "oxygen", "storage", "turret", "lamp", "hauler", "miner" }) Create(key, database);
        EditorUtility.SetDirty(database); AssetDatabase.SaveAssets(); database.RebuildLookup();
        Folder("Assets/DarkNights/Res/Scenes/Expedition");
        if (!AssetDatabase.CopyAsset(RandomLevelEntry.ScenePath, RandomLevelEntry.ExpeditionScenePath)) throw new Exception("复制独立模板失败。");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
            var layout = UnityEngine.Object.FindAnyObjectByType<LevelLayoutAuthoring>(); layout.Expedition = true;
            foreach (var placement in layout.GetComponentsInChildren<ScenePlacement>(true)) UnityEngine.Object.DestroyImmediate(placement.gameObject);
            var so = new SerializedObject(layout);
            foreach (var spec in new[] { ("worldEnd", 5120f), ("buildStart", 380f), ("buildEnd", 770f), ("enemySpawn", 900f), ("cameraStart", 568f) })
            {
                var t = (Transform)so.FindProperty(spec.Item1).objectReferenceValue;
                t.localPosition = new Vector3(spec.Item2, t.localPosition.y, 0);
            }
            var definitions = new DefinitionRuleIndex(database);
            SceneDefinitionAuthoring.Create(definitions.GetRequired("ship"), layout.PlacementGroup(ObjectType.Placeable_CompositeStructure),
                new Vector3(568, ((Transform)so.FindProperty("groundBaseline").objectReferenceValue).localPosition.y, 0), "远征飞船", 0, "");
            var template = UnityEngine.Object.FindAnyObjectByType<RandomLevelTemplate>();
            template.Expedition = true;
            template.Definition = AssetDatabase.LoadAssetAtPath<AnyRules.Next.Authoring.ARDMapDefinition>(CaveTerrainAssets.DefinitionPath);
            template.CaveStyle = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(CaveWorkshopSetup.StylePath);
            CreatePanel(); EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
            var check = UnityEngine.Object.FindAnyObjectByType<LevelLayoutAuthoring>();
            var catalog = DarkNights.Runtime.Config.GameCatalogJson.Parse(File.ReadAllText("Assets/DarkNights/Res/Config/balance.json"), File.ReadAllText("Assets/DarkNights/Res/Config/pinewatch.json"));
            check.CreateLayout(catalog, DefinitionRuleIndex.RuleKey).Validate(catalog);
            if (UnityEngine.Object.FindAnyObjectByType<ExpeditionPanel>().Actions.Length != 11) throw new Exception("HUD 绑定数量错误。");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(RandomLevelEntry.ExpeditionScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        return "7 definitions and prefabs, expedition scene, 11 HUD actions; scene saved/reopened and layout validated.";
    }
    private static void Create(string key, ObjectDefinitionDatabase database)
    {
        string folder = Objects + "/" + key; Folder(folder);
        bool actor = key == "hauler" || key == "miner";
        string source = "Assets/DarkNights/Res/Objects/" + (actor ? "Worker/Worker" : "House/House") + ".prefab";
        string path = folder + "/" + key + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(source);
        try
        {
            root.name = key;
            if (!actor)
            {
                var view = root.GetComponent<BuildingView>(); var data = new SerializedObject(view);
                var renderer = (SpriteRenderer)data.FindProperty("complete").objectReferenceValue;
                Sprite sprite = Sprite(key == "ship" ? "ship-0" : key);
                renderer.sprite = sprite; renderer.transform.localPosition = Vector3.zero; renderer.transform.localScale = Vector3.one;
                data.FindProperty("portrait").objectReferenceValue = sprite;
                data.FindProperty("pickBounds").rectValue = new Rect(-sprite.rect.width / 200, 0, sprite.rect.width / 100, sprite.rect.height / 100);
                data.FindProperty("clips").arraySize = 0;
                if (key == "ship")
                {
                    var variants = data.FindProperty("expeditionVariants"); variants.arraySize = 8;
                    for (int i = 0; i < 8; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = Sprite("ship-" + i);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (key == "hauler")
            {
                var view = root.GetComponent<ActorView>(); var data = new SerializedObject(view);
                foreach (var old in root.GetComponentsInChildren<SpriteRenderer>(true)) old.gameObject.SetActive(false);
                var body = new GameObject("Robot body"); body.transform.SetParent(root.transform, false);
                var renderer = body.AddComponent<SpriteRenderer>(); renderer.sprite = Sprite("hauler");
                renderer.sharedMaterial = root.GetComponentsInChildren<SpriteRenderer>(true).First(r => r != renderer).sharedMaterial;
                data.FindProperty("portrait").objectReferenceValue = renderer.sprite;
                var tint = data.FindProperty("tintTargets.targets"); int index = tint.arraySize; tint.arraySize++;
                tint.GetArrayElementAtIndex(index).FindPropertyRelative("renderer").objectReferenceValue = renderer;
                tint.GetArrayElementAtIndex(index).FindPropertyRelative("baseColor").colorValue = Color.white;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            root.GetComponent<EntityView>().Preview(0, 0);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
        definition.Name = key; definition.NetType = NetworkType.Local;
        definition.Type = actor ? ObjectType.Unit : ObjectType.Placeable_CompositeStructure;
        definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path));
        AssetDatabase.CreateAsset(definition, folder + "/" + key + ".asset");
        definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), "expedition." + key, false);
        definition.BehaviourTypes.Add((actor ? typeof(ActorPresentationBehaviour) : typeof(BuildingPresentationBehaviour)).FullName);
        ObjectCapabilitySetup.ConfigureLocal(definition, key); database.AddDefinition(definition); EditorUtility.SetDirty(definition);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup).address = "dark_nights.expedition." + key;
        EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
        var reopened = PrefabUtility.LoadPrefabContents(path);
        try
        { if (reopened.GetComponent<EntityView>().Portrait == null) throw new Exception("Prefab 重开缺失精灵。"); }
        finally { PrefabUtility.UnloadPrefabContents(reopened); }
    }
    private static void CreatePanel()
    {
        var root = new GameObject("Expedition HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
        var panel = root.AddComponent<ExpeditionPanel>();
        var background = Rect(root.transform, "Panel", 12, -80, 440, 266); background.gameObject.AddComponent<Image>().color = new Color(.025f, .045f, .065f, .92f);
        panel.Panel = background.gameObject;
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/DarkNights/Res/UI/Shared/UIFont.fontsettings");
        var text = Rect(background, "Status", 10, -8, 420, 122).gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = 14; text.color = new Color(.85f, .9f, .83f); text.raycastTarget = false; panel.Status = text;
        panel.Commands = new[] { "depart", "unload", "board", "recall", "launch", "emergency", "robot", "cargo", "crew", "relay", "mine", "resupply" };
        string[] names = { "出发", "卸货", "登船", "召回", "正常起飞", "紧急起飞", "机器人舱 10铁", "货舱 10铁", "船员舱 10铁", "搬迁中继", "派工最近矿床", "补充损失" };
        panel.Actions = new Button[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var rect = Rect(background, panel.Commands[i], 10 + i % 3 * 141, -132 - i / 3 * 32, 133, 28);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.13f, .23f, .28f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Rect(rect, "Label", 0, 0, 133, 28).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = 13; label.alignment = TextAnchor.MiddleCenter; label.text = names[i]; label.raycastTarget = false;
            panel.Actions[i] = button;
        }
        Folder("Assets/DarkNights/Res/UI/Expedition");
        const string path = "Assets/DarkNights/Res/UI/Expedition/Expedition.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        background.gameObject.SetActive(false);
    }
    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(width, height); return r;
    }
    private static Sprite Sprite(string key) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/" + key + ".png");
    public static string RepairDock()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
            var layout = UnityEngine.Object.FindAnyObjectByType<LevelLayoutAuthoring>();
            var so = new SerializedObject(layout);
            var ground = (Transform)so.FindProperty("groundBaseline").objectReferenceValue;
            var ship = layout.GetComponentsInChildren<ScenePlacement>(true).Single();
            ship.transform.position = new Vector3(ship.transform.position.x, ground.position.y, ship.transform.position.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
            EditorSceneManager.SaveScene(scene); EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
            var catalog = DarkNights.Runtime.Config.GameCatalogJson.Parse(File.ReadAllText("Assets/DarkNights/Res/Config/balance.json"), File.ReadAllText("Assets/DarkNights/Res/Config/pinewatch.json"));
            UnityEngine.Object.FindAnyObjectByType<LevelLayoutAuthoring>().CreateLayout(catalog, DefinitionRuleIndex.RuleKey).Validate(catalog);
            if (!EditorBuildSettings.scenes.Any(s => s.path == RandomLevelEntry.ExpeditionScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(RandomLevelEntry.ExpeditionScenePath, true) }).ToArray();
            return "Dock alignment fixed; saved/reopened formal expedition scene validates.";
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
    public static string AddResupply()
    {
        const string path = "Assets/DarkNights/Res/UI/Expedition/Expedition.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var panel = root.GetComponent<ExpeditionPanel>();
            if (panel.Commands.Contains("resupply")) return "Already installed";
            var button = UnityEngine.Object.Instantiate(panel.Actions.Last(), panel.Actions.Last().transform.parent);
            button.name = "resupply";
            ((RectTransform)button.transform).anchoredPosition = new Vector2(292, -228);
            button.GetComponentInChildren<Text>().text = "补充损失";
            panel.Commands = panel.Commands.Concat(new[] { "resupply" }).ToArray();
            panel.Actions = panel.Actions.Concat(new[] { button }).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "Resupply button saved in native HUD prefab";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
