var project = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/../..");
var output = System.IO.Path.Combine(project, "artifacts/entrance-whitebox-20261102");
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling ||
    active.isDirty || active.path != DarkNights.Editor.Terrain.TerrainScenePaths.CaveWhitebox)
    throw new System.InvalidOperationException("需要已保存的原平地白模及空闲Editor，不改动未保存场景。");
var variants = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(output, "variants.json")));
string assetRoot = DarkNights.Editor.Terrain.WhiteboxModuleBaker.AssetRoot;
if (UnityEditor.AssetDatabase.IsValidFolder(assetRoot))
    throw new System.InvalidOperationException("入口资源目录已存在，初始化只允许空目标，不覆盖已有模块。");
UnityEditor.AssetDatabase.CreateFolder(DarkNights.Editor.Terrain.TerrainScenePaths.Workbenches, "EntranceWhiteboxAssets");
var shader = UnityEngine.Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
if (shader == null) throw new System.InvalidOperationException("缺少已锁定URP2D非受光Shader。");
var bodyMaterial = new UnityEngine.Material(shader) { name = "WhiteboxRock" };
bodyMaterial.SetColor("_Color", new UnityEngine.Color(.24f,.29f,.33f,1));
UnityEditor.AssetDatabase.CreateAsset(bodyMaterial, assetRoot + "/WhiteboxRock.mat");
var airMaterial = new UnityEngine.Material(shader) { name = "WhiteboxCavity" };
airMaterial.SetColor("_Color", new UnityEngine.Color(.40f,.46f,.49f,1));
UnityEditor.AssetDatabase.CreateAsset(airMaterial, assetRoot + "/WhiteboxCavity.mat");
var edgeMaterial = new UnityEngine.Material(shader) { name = "WhiteboxEdge" };
edgeMaterial.SetColor("_Color", new UnityEngine.Color(.58f,.63f,.64f,1));
UnityEditor.AssetDatabase.CreateAsset(edgeMaterial, assetRoot + "/WhiteboxEdge.mat");
var results = new System.Collections.Generic.List<object>();
foreach (Newtonsoft.Json.Linq.JObject variant in variants)
{
    string id = (string)variant["id"], title = (string)variant["name"], folderName = (string)variant["folder"];
    string directory = assetRoot + "/" + folderName;
    string scenePath = DarkNights.Editor.Terrain.TerrainScenePaths.Workbenches + "/" + (string)variant["scene"] + ".unity";
    if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(scenePath) != null)
        throw new System.InvalidOperationException("目标场景已存在，不覆盖：" + scenePath);
    UnityEditor.AssetDatabase.CreateFolder(assetRoot, folderName);
    if (!UnityEditor.AssetDatabase.CopyAsset(DarkNights.Editor.Terrain.TerrainScenePaths.CaveWhitebox, scenePath))
        throw new System.InvalidOperationException("复制原生作者场景失败：" + scenePath);
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
    var roots = scene.GetRootGameObjects();
    var entry = roots.SelectMany(r => r.GetComponentsInChildren<DarkNights.Entry.Terrain.WhiteboxEntry>(true)).Single();
    var terrain = roots.Single(r => r.name == "01_Terrain");
    while (terrain.transform.childCount > 0)
        UnityEngine.Object.DestroyImmediate(terrain.transform.GetChild(0).gameObject);
    var moduleRoot = new UnityEngine.GameObject(id + " · " + title);
    moduleRoot.transform.SetParent(terrain.transform, false);
    var module = moduleRoot.AddComponent<DarkNights.View.Terrain.WhiteboxTerrainAuthoring>();
    module.ModuleId = id; module.DisplayName = title;
    module.Floor = ((Newtonsoft.Json.Linq.JArray)variant["floor"]).Select(p => new UnityEngine.Vector2((float)p[0],(float)p[1])).ToArray();
    module.Ceiling = ((Newtonsoft.Json.Linq.JArray)variant["ceiling"]).Select(p => new UnityEngine.Vector2((float)p[0],(float)p[1])).ToArray();
    var body = new UnityEngine.GameObject("地形截面",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));
    body.transform.SetParent(moduleRoot.transform,false);
    module.TerrainMesh = body.GetComponent<UnityEngine.MeshFilter>();
    var bodyRenderer = body.GetComponent<UnityEngine.MeshRenderer>();
    bodyRenderer.sharedMaterial = bodyMaterial;
    bodyRenderer.sortingOrder = 0;
    var air = new UnityEngine.GameObject("空腔底色",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));
    air.transform.SetParent(moduleRoot.transform,false);
    module.BackgroundMesh = air.GetComponent<UnityEngine.MeshFilter>();
    var airRenderer = air.GetComponent<UnityEngine.MeshRenderer>();
    airRenderer.sharedMaterial = airMaterial; airRenderer.sortingOrder = -40;
    foreach (string kind in new[] { "地面轮廓", "洞顶轮廓" })
    {
        var go = new UnityEngine.GameObject(kind,typeof(UnityEngine.LineRenderer));
        go.transform.SetParent(moduleRoot.transform,false);
        var line = go.GetComponent<UnityEngine.LineRenderer>();
        line.sharedMaterial = edgeMaterial; line.startColor = line.endColor = UnityEngine.Color.white;
        line.widthMultiplier = .018f; line.numCornerVertices = 3; line.numCapVertices = 3;
        line.sortingOrder = 1;
        if (kind == "地面轮廓") module.FloorLine = line; else module.CeilingLine = line;
    }
    DarkNights.Editor.Terrain.WhiteboxModuleBaker.Bake(module,directory);
    string prefabPath = directory + "/" + id + ".prefab";
    UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect(moduleRoot,prefabPath,UnityEditor.InteractionMode.AutomatedAction);
    entry.Ground = null; entry.TerrainModule = module;
    entry.HeroPlacement.transform.position = new UnityEngine.Vector3(2,0,0);
    entry.TerrainCameraHalfHeight = 2.2f;
    entry.ViewCamera.transform.position = new UnityEngine.Vector3(module.Width*.5f,.1f,-10);
    entry.ViewCamera.orthographicSize = 4.6f;
    entry.ViewCamera.backgroundColor = new UnityEngine.Color(.68f,.73f,.76f,1);
    UnityEditor.EditorUtility.SetDirty(entry);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
        throw new System.InvalidOperationException("原生场景保存失败：" + scenePath);
    var data = DarkNights.Entry.Terrain.WhiteboxTerrainBuilder.Build(module);
    byte[] shapeData = data.CopyShapes();
    results.Add(new
    {
        id,title,scene=scenePath,prefab=prefabPath,guid=UnityEditor.AssetDatabase.AssetPathToGUID(scenePath),
        fingerprint=module.BakedFingerprint,vertices=module.TerrainMesh.sharedMesh.vertexCount,
        slopeCells=shapeData.Count(v=>v!=0),objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Transform>(true)).Count(),
        missingScripts=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Transform>(true)).Sum(t=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
        role=entry.HeroPlacement.PlacementKey
    });
}
UnityEditor.AssetDatabase.SaveAssets();
var finalScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.EntranceGentle);
var finalEntry = finalScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DarkNights.Entry.Terrain.WhiteboxEntry>(true)).Single();
if (UnityEditor.SceneView.lastActiveSceneView != null)
{
    var view = UnityEditor.SceneView.lastActiveSceneView;
    view.in2DMode = true;
    view.LookAt(new UnityEngine.Vector3(7.64f,-.4f,0),UnityEngine.Quaternion.identity,8.5f,true,true);
}
UnityEditor.Selection.activeGameObject = finalEntry.TerrainModule.gameObject;
string result = Newtonsoft.Json.JsonConvert.SerializeObject(new {completed=true,variants=results,playing=UnityEditor.EditorApplication.isPlaying},Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(System.IO.Path.Combine(output,"creation-result.json"),result,new System.Text.UTF8Encoding(false));
return result;
