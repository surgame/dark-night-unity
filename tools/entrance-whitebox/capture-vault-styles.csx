var project = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/../..");
var output = System.IO.Path.Combine(project,"artifacts/vault-svg-styles-20261102");
if (UnityEditor.EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
    throw new System.InvalidOperationException("编辑态画面核对需要保存且未运行的场景。");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(output,"design-inputs.json")));
var variants = (Newtonsoft.Json.Linq.JArray)manifest["profiles"];
var checks = new System.Collections.Generic.List<object>();
foreach (Newtonsoft.Json.Linq.JObject variant in variants)
{
    string path = DarkNights.Editor.Terrain.TerrainScenePaths.Workbenches + "/" + (string)variant["scene"] + ".unity";
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
    var entry = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DarkNights.Entry.Terrain.WhiteboxEntry>(true)).Single();
    var module = entry.TerrainModule;
    var data = DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Build(module);
    if (module.BakedFingerprint != DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Fingerprint(module))
        throw new System.InvalidOperationException("已保存轮廓与烘焙网格来源不一致。");
    var camera = entry.ViewCamera;
    var previousTarget = camera.targetTexture;
    var previousActive = UnityEngine.RenderTexture.active;
    var target = UnityEngine.RenderTexture.GetTemporary(1600,900,24,UnityEngine.RenderTextureFormat.ARGB32,UnityEngine.RenderTextureReadWrite.sRGB);
    var texture = new UnityEngine.Texture2D(1600,900,UnityEngine.TextureFormat.RGBA32,false,false);
    try
    {
        camera.targetTexture = target;
        camera.Render();
        UnityEngine.RenderTexture.active = target;
        texture.ReadPixels(new UnityEngine.Rect(0,0,1600,900),0,0);
        texture.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,(string)variant["folder"]+"-overview.png"),UnityEngine.ImageConversion.EncodeToPNG(texture));
        camera.transform.position = new UnityEngine.Vector3(5.4f,.2f,-10);
        camera.orthographicSize = 2.2f;
        camera.Render();
        texture.ReadPixels(new UnityEngine.Rect(0,0,1600,900),0,0);
        texture.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,(string)variant["folder"]+"-near.png"),UnityEngine.ImageConversion.EncodeToPNG(texture));
    }
    finally
    {
        camera.targetTexture = previousTarget;
        UnityEngine.RenderTexture.active = previousActive;
        UnityEngine.RenderTexture.ReleaseTemporary(target);
        UnityEngine.Object.DestroyImmediate(texture);
    }
    camera.transform.position = new UnityEngine.Vector3(module.Width*.5f,.1f,-10);
    camera.orthographicSize = 4.6f;
    var objects = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Transform>(true)).Select(t=>t.gameObject).ToArray();
    checks.Add(new
    {
        id=module.ModuleId,path,fingerprint=module.BakedFingerprint,
        hero=entry.HeroPlacement.PlacementKey,heroX=entry.HeroPlacement.transform.position.x,heroY=entry.HeroPlacement.transform.position.y,
        missingScripts=objects.Sum(go=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)),
        colliders=objects.Sum(go=>go.GetComponents<UnityEngine.Collider2D>().Length+go.GetComponents<UnityEngine.Collider>().Length),
        minerals=data.Deposits.Count,nests=data.Nests.Count,events=data.GeologySites.Count,
        prefab=UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(module.gameObject),
        prefabsConnected=UnityEditor.PrefabUtility.GetPrefabInstanceStatus(module.gameObject).ToString(),
        inBuild=UnityEditor.EditorBuildSettings.scenes.Any(s=>s.path==path)
    });
}
var finalScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.VaultStrata);
var selected = finalScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DarkNights.View.Terrain.WhiteboxTerrainAuthoring>(true)).Single();
UnityEditor.Selection.activeGameObject = selected.gameObject;
if (UnityEditor.SceneView.lastActiveSceneView != null)
{
    UnityEditor.SceneView.lastActiveSceneView.in2DMode = true;
    UnityEditor.SceneView.lastActiveSceneView.LookAt(new UnityEngine.Vector3(7.64f,-.4f,0),UnityEngine.Quaternion.identity,8.5f,true,true);
}
string result = Newtonsoft.Json.JsonConvert.SerializeObject(new {completed=true,scope="Edit-mode native camera and asset binding inspection, no Play or actor traversal test.",checks,playing=UnityEditor.EditorApplication.isPlaying,compileFailed=UnityEditor.EditorUtility.scriptCompilationFailed},Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(System.IO.Path.Combine(output,"display-review.json"),result,new System.Text.UTF8Encoding(false));
return result;
