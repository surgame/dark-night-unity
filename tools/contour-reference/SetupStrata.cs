using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Editor;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>只在空目录生成独立岩层样板、AnyRuleD 规则、原生角色 Prefab 和场景；不复制旧地图场景或旧岩石资源。</summary>
public static class SetupStrata
{
    private const string Root = "Assets/DarkNights/Res/Terrain/StrataCave";
    public static string Run()
    {
        if (Application.isPlaying || Directory.Exists(Root)) throw new InvalidOperationException("只允许在停止状态向新目录输出。");
        Directory.CreateDirectory(Root); AssetDatabase.ImportAsset(Root);
        var definition = CreateRules();
        var style = ScriptableObject.CreateInstance<CaveTerrainStyle>();
        style.Shader = Shader.Find("DarkNights/CaveStrata"); style.ProceduralRock = true;
        style.Background = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CaveBackgroundStyle>(
            "Assets/DarkNights/Res/Terrain/CaveContourStatic/Background.asset"));
        AssetDatabase.CreateAsset(style.Background, Root + "/Background.asset");
        AssetDatabase.CreateAsset(style, Root + "/Style.asset");
        var blueprint = CreateSample();
        var map = TerrainMapExporter.Export(blueprint, definition, Root + "/ReferenceChamber.asset");
        map.CaveStyle = style; map.HasSpawn = true; map.SpawnCell = new Vector2Int(69, 51); EditorUtility.SetDirty(map);
        Directory.CreateDirectory(TerrainScenePaths.Workbenches); AssetDatabase.ImportAsset(TerrainScenePaths.Workbenches);
        CreateScene(definition, style, map, "ReferenceChamber");
        CreateScene(definition, style, null, "RandomCave");
        var expedition = EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
        var template = expedition.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RandomLevelTemplate>(true)).Single();
        template.StaticBackgroundStyle = style; template.ContourDefinition = definition;
        EditorSceneManager.MarkSceneDirty(expedition); EditorSceneManager.SaveScene(expedition);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(TerrainScenePaths.ReferenceChamber);
        var boot = UnityEngine.Object.FindFirstObjectByType<TerrainDebugBootstrap>();
        if (boot.Definition != definition || boot.CaveStyle != style || boot.FixedMap != map) throw new Exception("独立样板重开失败。");
        var dependencies = AssetDatabase.GetDependencies(TerrainScenePaths.ReferenceChamber, true);
        var obsolete = dependencies.Where(p => p.Contains("/CaveExploration/") || p.Contains("/DebugBootstrap/") || p.Contains("/Pinewatch/")).ToArray();
        if (obsolete.Length != 0) throw new Exception("仍依赖旧场景或地形：" + string.Join(",", obsolete));
        return "Created independent StrataCave: fixed reference / random scenes, native actor, AnyRuleD rules; old terrain dependencies=0";
    }
    private static ARDMapDefinition CreateRules()
    {
        string png = Root + "/RuleGeometry.png";
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false); texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray()); texture.Apply();
        File.WriteAllBytes(png, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(png); importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 8; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        var db = ScriptableObject.CreateInstance<EditorAnyRuleDDatabase>(); db.CatalogGuid = Id("catalog");
        string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
        db.Terrains = new TerrainDefinition[8]; db.RuleSets = new AnyRuleD[8];
        for (int i = 0; i < 8; i++)
        {
            string key = keys[i]; var terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
            var identity = new StringBuilder(); for (int n = 0; n < 4; n++) identity.Append(TerrainRandom.Hash("dark-nights-h5:" + key + ":" + n).ToString("x8"));
            terrain.PersistentGuid = identity.ToString(); terrain.Key = key; terrain.DisplayName = key;
            terrain.MaximumDurability = i == 7 ? int.MaxValue : new[] {20,40,55,55,65,75,30}[i];
            var visual = ScriptableObject.CreateInstance<TileVisualSet>(); visual.PersistentGuid = Id(key + "/surface"); visual.TileableFill = true;
            visual.Variants = new[] { new VisualVariant { VariantGuid = Id(key + "/variant"), ResourceId = Id(key + "/resource"), Sprite = sprite } };
            terrain.FillVisualSet = visual;
            var rules = ScriptableObject.CreateInstance<AnyRuleD>(); rules.PersistentGuid = Id(key + "/rules"); rules.TargetTerrain = terrain;
            rules.Rules = new ARuleD[15];
            for (int mask = 1; mask < 16; mask++)
            {
                var rule = new ARuleD { RuleGuid = Id(key + "/rule/" + mask), DisplayName = "Coverage " + mask };
                for (int c = 0; c < 4; c++) rule.Corners[c].Condition = (mask & (1 << c)) != 0 ? CornerPredicateKind.This : CornerPredicateKind.Empty;
                rule.Outputs = new[] { new RuleOutputSlot { SlotGuid = Id(key + "/slot/" + mask), Channel = "cell", VisualSet = visual,
                    Coverage = RuleSlotCoverage.ExactCell, BoundaryContract = CellBoundaryContract.CanonicalMaterialEdgesV1 } };
                rules.Rules[mask - 1] = rule;
            }
            AssetDatabase.CreateAsset(visual, Root + "/" + key + "-surface.asset");
            AssetDatabase.CreateAsset(terrain, Root + "/" + key + "-terrain.asset");
            AssetDatabase.CreateAsset(rules, Root + "/" + key + "-rules.asset");
            db.Terrains[i] = terrain; db.RuleSets[i] = rules;
        }
        AssetDatabase.CreateAsset(db, Root + "/TerrainCatalog.asset"); AssetDatabase.SaveAssets();
        var definition = RuleCatalogBuild.CompileToNewAssets(db, Root + "/MapDefinition.asset");
        definition.Width = 320; definition.Height = 192; definition.MinV = -191;
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets(); return definition;
    }
    private static TerrainBlueprint CreateSample()
    {
        var source = File.ReadAllBytes("../tools/contour-reference/profile-raw.bin");
        var cells = new byte[320 * 192]; var shapes = new byte[cells.Length]; var protection = new bool[cells.Length];
        for (int y = 43; y < 192; y++) for (int x = 0; x < 320; x++) cells[y * 320 + x] = 2;
        for (int cy = 0; cy < 39; cy++) for (int cx = 0; cx < 63; cx++)
        {
            int coverage = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) coverage += source[(cy * 8 + y) * 504 + cx * 8 + x];
            int index = (cy + 36) * 320 + cx + 40; cells[index] = coverage >= 32 ? (byte)2 : (byte)0;
        }
        shapes = TerrainShapeGeometry.Build(cells, protection, 320, 192);
        return new TerrainBlueprint(new TerrainGenerationSettings { Seed = "DN-MATERIAL-0921", ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile },
            cells, protection, new int[320], new[] { new TerrainRoom("gallery", 59, 49, 18, 10) }, new bool[cells.Length], Array.Empty<TerrainDepositBlueprint>(), shapes: shapes);
    }
    private static void CreateScene(ARDMapDefinition definition, CaveTerrainStyle style, TerrainMapAsset map, string name)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Cave camera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .015f, .018f); camera.transform.position = new Vector3(59, -48, -10);
        var actor = new GameObject("Cave explorer"); var flyer = actor.AddComponent<TerrainDebugFlyer>();
        var art = new GameObject("Explorer art").AddComponent<SpriteRenderer>(); art.transform.SetParent(actor.transform, false); art.sortingOrder = 20000;
        var idle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DarkNights/Res/Art/Original/sprites/spr_worker_idle/spr_worker_idle_0.png");
        art.sprite = idle; art.transform.localScale = Vector3.one * idle.pixelsPerUnit / 8;
        flyer.Art = art; flyer.IdleFrame = idle; flyer.CameraDistance = 10;
        flyer.WalkFrames = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/DarkNights/Res/Art/Original/sprites/spr_worker_walking" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        flyer.PresentMovement(0, false, 0);
        string prefabPath = Root + "/Explorer.prefab";
        if (!File.Exists(prefabPath)) PrefabUtility.SaveAsPrefabAsset(actor, prefabPath);
        UnityEngine.Object.DestroyImmediate(actor);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);
        flyer = instance.GetComponent<TerrainDebugFlyer>(); flyer.ViewCamera = camera; flyer.Teleport(new Vector2(69, -51.5f));
        var boot = new GameObject("Strata cave workshop").AddComponent<TerrainDebugBootstrap>();
        boot.Definition = definition; boot.CaveStyle = style; boot.FixedMap = map; boot.Flyer = flyer;
        boot.Settings = new TerrainGenerationSettings { Seed = map == null ? "STRATA-0922" : "DN-MATERIAL-0921", ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile };
        boot.BalanceJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/balance.json");
        boot.LevelJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/pinewatch.json");
        boot.gameObject.AddComponent<CaveWorkshopInput>().Bootstrap = boot;
        var panel = boot.gameObject.AddComponent<TerrainDebugPanel>(); panel.Bootstrap = boot;
        panel.Font = AssetDatabase.LoadAssetAtPath<Font>("Assets/DarkNights/Res/UI/Shared/UIFont.fontsettings");
        EditorSceneManager.SaveScene(scene, TerrainScenePaths.Workbenches + "/" + name + ".unity");
    }
    private static string Id(string key)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("dark-nights-strata:" + key)), 0, 16).Replace("-", "").ToLowerInvariant();
    }
}
