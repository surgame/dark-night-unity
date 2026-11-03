if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling ||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
    throw new System.InvalidOperationException("只在已保存的空闲编辑态核对资源。");
var project = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/../..");
var output = System.IO.Path.Combine(project, "artifacts/vault-svg-styles-20261102");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(output, "design-inputs.json")));
var checks = new System.Collections.Generic.List<object>();
foreach (Newtonsoft.Json.Linq.JObject variant in (Newtonsoft.Json.Linq.JArray)manifest["profiles"])
{
    string path = DarkNights.Editor.Terrain.TerrainScenePaths.Workbenches + "/" + (string)variant["scene"] + ".unity";
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
    var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Transform>(true)).Select(t => t.gameObject).ToArray();
    var entry = all.SelectMany(go => go.GetComponents<DarkNights.Entry.Terrain.WhiteboxEntry>()).Single();
    var source = entry.TerrainModule;
    string fingerprint = DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Fingerprint(source);
    var data = DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Build(source);
    var blueprint = data.Blueprint();
    var profile = entry.SessionDefinition.SharedConfigs.OfType<DarkNights.Runtime.Terrain.TerrainProfileConfig>().Single();
    var rules = profile.Freeze();
    var spriteChecks = new System.Collections.Generic.List<object>();
    foreach (var renderer in new[] { source.TerrainArtwork, source.BackgroundArtwork })
    {
        if (renderer == null || renderer.sprite == null || !renderer.enabled)
            throw new System.InvalidOperationException("图层未显式绑定。");
        var sprite = renderer.sprite;
        string spritePath = UnityEditor.AssetDatabase.GetAssetPath(sprite);
        var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(spritePath);
        var settings = new UnityEditor.TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (sprite.rect.width != 1920 || sprite.rect.height != 1200 ||
            UnityEngine.Mathf.Abs(sprite.pixelsPerUnit - 125) > .001f ||
            UnityEngine.Mathf.Abs(sprite.bounds.size.x - 15.36f) > .001f || UnityEngine.Mathf.Abs(sprite.bounds.size.y - 9.6f) > .001f ||
            importer.textureType != UnityEditor.TextureImporterType.Sprite ||
            settings.spriteMeshType != UnityEngine.SpriteMeshType.FullRect || !importer.sRGBTexture ||
            importer.mipmapEnabled || importer.filterMode != UnityEngine.FilterMode.Point ||
            importer.textureCompression != UnityEditor.TextureImporterCompression.Uncompressed)
            throw new System.InvalidOperationException("图层导入合同不一致：" + spritePath);
        spriteChecks.Add(new
        {
            path = spritePath, sprite.pixelsPerUnit, width = sprite.rect.width, height = sprite.rect.height,
            worldWidth = sprite.bounds.size.x, worldHeight = sprite.bounds.size.y,
            importer.sRGBTexture, importer.mipmapEnabled, filter = importer.filterMode.ToString(),
            compression = importer.textureCompression.ToString(), meshType = settings.spriteMeshType.ToString(),
            renderer.sortingOrder, shader = renderer.sharedMaterial.shader.name
        });
    }
    var heroSprite = entry.HeroPlacement.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true).Single(r => r.name == "Author Hero");
    if (scene.isDirty || rules == null || entry.Ground != null || fingerprint != source.BakedFingerprint ||
        fingerprint != source.ArtworkFingerprint || source.TerrainMesh.GetComponent<UnityEngine.MeshRenderer>().enabled ||
        source.BackgroundMesh.GetComponent<UnityEngine.MeshRenderer>().enabled || source.FloorLine.enabled || source.CeilingLine.enabled ||
        all.Sum(go => UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)) != 0 ||
        UnityEditor.PrefabUtility.GetPrefabInstanceStatus(source.gameObject) != UnityEditor.PrefabInstanceStatus.Connected)
        throw new System.InvalidOperationException("原生模块引用、轮廓摘要或保存状态不一致。");
    checks.Add(new
    {
        id = source.ModuleId, path, scene.isDirty, fingerprint, artworkFingerprint = source.ArtworkFingerprint,
        prefab = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source.gameObject),
        heroSprite = UnityEditor.AssetDatabase.GetAssetPath(heroSprite.sprite),
        clips = ((DarkNights.View.ActorView)entry.HeroPlacement.View).Clips.Select(c => c.Name).ToArray(),
        shapes = data.CopyShapes().GroupBy(s => s).ToDictionary(g => g.Key.ToString(), g => g.Count()),
        blueprintWidth = blueprint.Width, blueprintHeight = blueprint.Height, spriteChecks
    });
}
var oldChecks = new System.Collections.Generic.List<object>();
var previous = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(
    System.IO.Path.Combine(project, "artifacts/entrance-whitebox-20261102/binding-review.json")));
foreach (Newtonsoft.Json.Linq.JObject item in (Newtonsoft.Json.Linq.JArray)previous["items"])
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene((string)item["path"]);
    var source = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DarkNights.View.Terrain.WhiteboxTerrainAuthoring>(true)).Single();
    var data = DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Build(source);
    var shapes = data.CopyShapes().GroupBy(s => s).ToDictionary(g => g.Key.ToString(), g => g.Count());
    bool same = Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JObject.FromObject(shapes), item["shapes"]);
    if (!same || source.BakedFingerprint != (string)item["fingerprint"] || scene.isDirty)
        throw new System.InvalidOperationException("原四形态编码发生变化：" + source.ModuleId);
    oldChecks.Add(new { id = source.ModuleId, unchangedShapeCounts = same });
}
var finalScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.VaultStrata);
var module = finalScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DarkNights.View.Terrain.WhiteboxTerrainAuthoring>(true)).Single();
UnityEditor.Selection.activeGameObject = module.gameObject;
if (UnityEditor.SceneView.lastActiveSceneView != null)
{
    var view = UnityEditor.SceneView.lastActiveSceneView;
    view.in2DMode = true;
    view.LookAt(new UnityEngine.Vector3(7.64f, -.4f, 0), UnityEngine.Quaternion.identity, 8.5f, true, true);
    view.Repaint();
}
string result = Newtonsoft.Json.JsonConvert.SerializeObject(new
{
    completed = true, scope = "Native saved assets and shape encoding only; no Play, actor traversal or game tests.",
    checks, oldChecks, finalScene = finalScene.path, finalSceneDirty = finalScene.isDirty,
    editor = UnityEngine.Application.unityVersion, playing = UnityEditor.EditorApplication.isPlaying,
    compileFailed = UnityEditor.EditorUtility.scriptCompilationFailed
}, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(System.IO.Path.Combine(output, "binding-review.json"), result, new System.Text.UTF8Encoding(false));
return result;
