var project = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/../..");
var output = System.IO.Path.Combine(project, "artifacts/natural-contour-blockout-20261102");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(output, "design-inputs.json")));
var activeBefore = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
bool playingBefore = UnityEditor.EditorApplication.isPlaying;
int scenesBefore = UnityEngine.SceneManagement.SceneManager.sceneCount;
if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
    throw new System.InvalidOperationException("等待现有导入／编译就绪，不打断当前Play。");
string assetRoot = DarkNights.Editor.Terrain.WhiteboxModuleBaker.AssetRoot + "/NaturalContours";
if (UnityEditor.AssetDatabase.IsValidFolder(assetRoot))
    throw new System.InvalidOperationException("原生候选目标已存在，拒绝初始化覆盖。");
UnityEditor.AssetDatabase.CreateFolder(DarkNights.Editor.Terrain.WhiteboxModuleBaker.AssetRoot, "NaturalContours");
System.Func<string, UnityEngine.Color> parse = hex =>
{
    UnityEngine.Color value;
    if (!UnityEngine.ColorUtility.TryParseHtmlString(hex, out value))
        throw new System.FormatException("无效作者颜色：" + hex);
    return value.linear;
};
var shader = UnityEngine.Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
if (shader == null) throw new System.InvalidOperationException("缺少现有URP2D Shader。");
var material = new UnityEngine.Material(shader) { name = "NaturalContourUnlit" };
material.SetColor("_Color", UnityEngine.Color.white);
UnityEditor.AssetDatabase.CreateAsset(material, assetRoot + "/NaturalContourUnlit.mat");
UnityEditor.AssetDatabase.SaveAssetIfDirty(material);
var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(
    "Assets/DarkNights/Res/Objects/Hero/AnimationFrames/idle/idle-000.png");
if (sprite == null) throw new System.InvalidOperationException("缺少已导入的指定主角。");
var results = new System.Collections.Generic.List<object>();
foreach (Newtonsoft.Json.Linq.JObject profile in (Newtonsoft.Json.Linq.JArray)manifest["profiles"])
{
    string id = (string)profile["id"], folder = (string)profile["folder"];
    string directory = assetRoot + "/" + folder;
    UnityEditor.AssetDatabase.CreateFolder(assetRoot, folder);
    var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
    UnityEngine.RenderTexture target = null;
    UnityEngine.Texture2D texture = null;
    var previousActive = UnityEngine.RenderTexture.active;
    try
    {
        var root = new UnityEngine.GameObject(id + " · " + (string)profile["name"]);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
        foreach (Newtonsoft.Json.Linq.JObject layer in (Newtonsoft.Json.Linq.JArray)profile["meshes"])
        {
            string name = (string)layer["name"];
            var mesh = new UnityEngine.Mesh { name = name };
            mesh.vertices = ((Newtonsoft.Json.Linq.JArray)layer["vertices"]).Select(
                p => new UnityEngine.Vector3((float)p[0], (float)p[1], (float)p[2])).ToArray();
            mesh.triangles = ((Newtonsoft.Json.Linq.JArray)layer["triangles"]).Select(p => (int)p).ToArray();
            mesh.colors = ((Newtonsoft.Json.Linq.JArray)layer["colors"]).Select(p => parse((string)p)).ToArray();
            mesh.uv = new UnityEngine.Vector2[mesh.vertexCount];
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            UnityEditor.AssetDatabase.CreateAsset(mesh, directory + "/" + name + ".asset");
            UnityEditor.AssetDatabase.SaveAssetIfDirty(mesh);
            var child = new UnityEngine.GameObject(name, typeof(UnityEngine.MeshFilter), typeof(UnityEngine.MeshRenderer));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child, preview);
            child.transform.SetParent(root.transform, false);
            child.GetComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<UnityEngine.MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = (int)layer["sorting"];
        }
        string prefabPath = directory + "/" + id + ".prefab";
        var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        if (prefab == null) throw new System.InvalidOperationException("原生Prefab保存失败：" + id);
        var hero = new UnityEngine.GameObject("指定主角 · 仅比例参考", typeof(UnityEngine.SpriteRenderer));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hero, preview);
        hero.transform.position = new UnityEngine.Vector3(.72f, 0, -.03f);
        hero.transform.localScale = UnityEngine.Vector3.one * 4;
        var heroRenderer = hero.GetComponent<UnityEngine.SpriteRenderer>();
        heroRenderer.sprite = sprite;
        heroRenderer.sharedMaterial = material;
        heroRenderer.sortingOrder = 5;
        var cameraObject = new UnityEngine.GameObject("Isolated Contour Preview", typeof(UnityEngine.Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, preview);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.aspect = 1.6f;
        camera.orthographicSize = 3.2f;
        camera.transform.position = new UnityEngine.Vector3(4.8f, .08f, -10);
        camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
        camera.backgroundColor = parse((string)manifest["palette"]["sky"]);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(preview);
        target = UnityEngine.RenderTexture.GetTemporary(1600, 1000, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
        texture = new UnityEngine.Texture2D(1600, 1000, UnityEngine.TextureFormat.RGBA32, false, false);
        camera.targetTexture = target;
        foreach (string view in new[] { "overview", "near" })
        {
            if (view == "near")
            {
                camera.orthographicSize = 2.35f;
                camera.transform.position = new UnityEngine.Vector3(3.6f, .05f, -10);
            }
            camera.Render();
            UnityEngine.RenderTexture.active = target;
            texture.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 1000), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, folder + "-" + view + ".png"),
                UnityEngine.ImageConversion.EncodeToPNG(texture));
        }
        results.Add(new
        {
            id, name = (string)profile["name"], prefab = prefabPath,
            meshCount = prefab.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Length,
            vertices = prefab.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Sum(m => m.sharedMesh.vertexCount),
            texturesInTerrain = 0, colliders = prefab.GetComponentsInChildren<UnityEngine.Collider2D>(true).Length,
            missingScripts = prefab.GetComponentsInChildren<UnityEngine.Transform>(true).Sum(t =>
                UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
            heroSprite = UnityEditor.AssetDatabase.GetAssetPath(sprite), heroScale = hero.transform.localScale.x,
            cullingIsolated = camera.overrideSceneCullingMask == UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(preview)
        });
        camera.targetTexture = null;
    }
    finally
    {
        UnityEngine.RenderTexture.active = previousActive;
        if (target != null) UnityEngine.RenderTexture.ReleaseTemporary(target);
        if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
    }
}
var activeAfter = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (activeBefore.handle != activeAfter.handle || scenesBefore != UnityEngine.SceneManagement.SceneManager.sceneCount ||
    playingBefore != UnityEditor.EditorApplication.isPlaying)
    throw new System.InvalidOperationException("当前用户Scene或Play状态在批次期间变化，需核对原始状态。");
string result = Newtonsoft.Json.JsonConvert.SerializeObject(new
{
    completed = true, scope = "Isolated native visual contour prefab creation and camera capture only. No gameplay, collision or Play tests.",
    results, activeSceneBefore = activeBefore.path, activeSceneAfter = activeAfter.path,
    playingBefore, playingAfter = UnityEditor.EditorApplication.isPlaying, activeAfter.isDirty,
    scenesBefore, scenesAfter = UnityEngine.SceneManagement.SceneManager.sceneCount,
    compileFailed = UnityEditor.EditorUtility.scriptCompilationFailed, editor = UnityEngine.Application.unityVersion
}, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(System.IO.Path.Combine(output, "native-preview-review.json"), result, new System.Text.UTF8Encoding(false));
return result;
