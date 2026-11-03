var project = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath + "/../..");
var output = System.IO.Path.Combine(project, "artifacts/natural-contour-blockout-20261102");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(output, "design-inputs.json")));
var activeBefore = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
bool playingBefore = UnityEditor.EditorApplication.isPlaying;
int scenesBefore = UnityEngine.SceneManagement.SceneManager.sceneCount;
var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(
    "Assets/DarkNights/Res/Objects/Hero/AnimationFrames/idle/idle-000.png");
string assetRoot = DarkNights.Editor.Terrain.WhiteboxModuleBaker.AssetRoot + "/NaturalContours";
var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(assetRoot + "/NaturalContourUnlit.mat");
var checks = new System.Collections.Generic.List<object>();
foreach (Newtonsoft.Json.Linq.JObject profile in (Newtonsoft.Json.Linq.JArray)manifest["profiles"])
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
    UnityEngine.RenderTexture target = null;
    UnityEngine.Texture2D texture = null;
    var previous = UnityEngine.RenderTexture.active;
    try
    {
        string prefabPath = assetRoot + "/" + (string)profile["folder"] + "/" + (string)profile["id"] + ".prefab";
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
        if (prefab == null) throw new System.IO.FileNotFoundException(prefabPath);
        UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
        var hero = new UnityEngine.GameObject("指定主角 · 原场景比例", typeof(UnityEngine.SpriteRenderer));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hero, scene);
        hero.transform.position = new UnityEngine.Vector3(.72f, 0, -.03f);
        // CaveWhitebox已保存的Worker作者根缩放为4；独立Sprite预览必须继承这一层。
        hero.transform.localScale = UnityEngine.Vector3.one * 4;
        var heroRenderer = hero.GetComponent<UnityEngine.SpriteRenderer>();
        heroRenderer.sprite = sprite; heroRenderer.sharedMaterial = material; heroRenderer.sortingOrder = 5;
        var cameraObject = new UnityEngine.GameObject("Isolated Contour Preview", typeof(UnityEngine.Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>();
        camera.enabled = false; camera.orthographic = true; camera.aspect = 1.6f;
        camera.orthographicSize = 3.2f; camera.transform.position = new UnityEngine.Vector3(4.8f, .08f, -10);
        camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
        UnityEngine.Color sky; UnityEngine.ColorUtility.TryParseHtmlString((string)manifest["palette"]["sky"], out sky);
        camera.backgroundColor = sky.linear; camera.allowHDR = false; camera.allowMSAA = false;
        camera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);
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
            camera.Render(); UnityEngine.RenderTexture.active = target;
            texture.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, (string)profile["folder"] + "-" + view + "-final.png"),
                UnityEngine.ImageConversion.EncodeToPNG(texture));
        }
        checks.Add(new { id = (string)profile["id"], prefab = prefabPath, heroScale = 4,
            heroHeight = heroRenderer.bounds.size.y, heroWidth = heroRenderer.bounds.size.x,
            cameraCullingIsolated = true, meshes = prefab.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Length });
        camera.targetTexture = null;
    }
    finally
    {
        UnityEngine.RenderTexture.active = previous;
        if (target != null) UnityEngine.RenderTexture.ReleaseTemporary(target);
        if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
    }
}
var activeAfter = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (activeBefore.handle != activeAfter.handle || playingBefore != UnityEditor.EditorApplication.isPlaying ||
    scenesBefore != UnityEngine.SceneManagement.SceneManager.sceneCount)
    throw new System.InvalidOperationException("原活动Scene或Play状态改变，需核对。");
string result = Newtonsoft.Json.JsonConvert.SerializeObject(new { completed = true, checks,
    scope = "Native visual contours with corrected existing author hero scale, no movement or collision acceptance.",
    activeScene = activeAfter.path, playingBefore, playingAfter = UnityEditor.EditorApplication.isPlaying,
    activeAfter.isDirty, scenesBefore, scenesAfter = UnityEngine.SceneManagement.SceneManager.sceneCount,
    compileFailed = UnityEditor.EditorUtility.scriptCompilationFailed }, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(System.IO.Path.Combine(output, "final-display-review.json"), result, new System.Text.UTF8Encoding(false));
return result;
