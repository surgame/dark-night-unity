if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling)
    throw new System.InvalidOperationException("只在空闲编辑态读取模块来源。");
var project = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/../..");
var output = System.IO.Path.Combine(project,"artifacts/entrance-whitebox-20261102");
var variants = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(output,"variants.json")));
var items = new System.Collections.Generic.List<object>();
foreach (Newtonsoft.Json.Linq.JObject variant in variants)
{
    string path = DarkNights.Editor.Terrain.TerrainScenePaths.Workbenches + "/" + (string)variant["scene"] + ".unity";
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
    var all = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Transform>(true)).Select(t=>t.gameObject).ToArray();
    var entry = all.SelectMany(go=>go.GetComponents<DarkNights.Entry.Terrain.WhiteboxEntry>()).Single();
    var source = entry.TerrainModule;
    var data = DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Build(source);
    var blueprint = data.Blueprint();
    var profile = entry.SessionDefinition.SharedConfigs.OfType<DarkNights.Runtime.Terrain.TerrainProfileConfig>().Single();
    var rules = profile.Freeze();
    var shapeCounts = data.CopyShapes().GroupBy(s=>s).ToDictionary(g=>g.Key.ToString(),g=>g.Count());
    var authorSprite = entry.HeroPlacement.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true).Single(r=>r.name=="Author Hero");
    if (scene.isDirty || entry.Ground != null || rules == null || data.Deposits.Count != 0 ||
        source.BakedFingerprint != DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Fingerprint(source))
        throw new System.InvalidOperationException("入口白模有未保存输入、失效来源或多余内容。");
    items.Add(new
    {
        id=source.ModuleId,path,scene.isDirty,fingerprint=source.BakedFingerprint,
        width=source.Width,shapes=shapeCounts,blueprintWidth=blueprint.Width,blueprintHeight=blueprint.Height,
        heroSprite=UnityEditor.AssetDatabase.GetAssetPath(authorSprite.sprite),
        clips=((DarkNights.View.ActorView)entry.HeroPlacement.View).Clips.Select(c=>new {c.Name,c.Duration}).ToArray(),
        missing=all.Sum(go=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)),
        colliderCount=all.Sum(go=>go.GetComponents<UnityEngine.Collider2D>().Length+go.GetComponents<UnityEngine.Rigidbody2D>().Length),
        objects=all.Length,profile=UnityEditor.AssetDatabase.GetAssetPath(profile.Definition)
    });
}
var baseScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.CaveWhitebox);
var baseline = baseScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DarkNights.Entry.Terrain.WhiteboxEntry>(true)).Single();
if (baseline.Ground == null || baseline.TerrainModule != null || baseScene.isDirty)
    throw new System.InvalidOperationException("原平地白模来源已改变。");
var finalScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.EntranceGentle);
var module = finalScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DarkNights.View.Terrain.WhiteboxTerrainAuthoring>(true)).Single();
UnityEditor.Selection.activeGameObject = module.gameObject;
if (UnityEditor.SceneView.lastActiveSceneView != null)
{
    var view = UnityEditor.SceneView.lastActiveSceneView;
    view.in2DMode = true;
    view.LookAt(new UnityEngine.Vector3(7.64f,-.4f,0),UnityEngine.Quaternion.identity,8.5f,true,true);
    view.Repaint();
}
UnityEditor.EditorApplication.RepaintHierarchyWindow();
string result = Newtonsoft.Json.JsonConvert.SerializeObject(new
{
    completed=true,scope="Native author assets, shape encoding and references only; no runtime world, Play or actor traversal test.",
    items,baselineRetained=true,finalScene=finalScene.path,finalSceneDirty=finalScene.isDirty,
    editor=UnityEngine.Application.unityVersion,compileFailed=UnityEditor.EditorUtility.scriptCompilationFailed,
    playStart=UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene),
    playing=UnityEditor.EditorApplication.isPlaying
},Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(System.IO.Path.Combine(output,"binding-review.json"),result,new System.Text.UTF8Encoding(false));
return result;
