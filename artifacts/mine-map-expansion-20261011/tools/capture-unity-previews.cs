// 在同一 Editor 的有限 update 回调内逐图生成真正的 Tuner 渲染图；不启动会话或网络，不改作者资源。
var output = "D:/Developer/MiniGames/Dark Nights/unity-projects/artifacts/mine-map-expansion-20261011/visual-02";
if (System.IO.Directory.Exists(output)) throw new System.InvalidOperationException("Use a fresh visual output");
System.IO.Directory.CreateDirectory(output);
var windows = UnityEngine.Resources.FindObjectsOfTypeAll<DarkNights.Editor.Terrain.TerrainStylePreviewWindow>();
var window = System.Linq.Enumerable.FirstOrDefault(windows, w => !w.hasUnsavedChanges);
if (window == null) window = UnityEngine.ScriptableObject.CreateInstance<DarkNights.Editor.Terrain.TerrainStylePreviewWindow>();
window.Show(); window.Focus();
var original = window.Draft.Config.MineMap;
var ids = new[] { "rift-expanded", "karst-expanded", "shaft-loop-expanded", "vein-maze-expanded", "folded-expanded" };
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var refresh = typeof(DarkNights.Editor.Terrain.TerrainStylePreviewWindow).GetMethod("RefreshChanges", flags);
var field = typeof(DarkNights.Editor.Terrain.TerrainStylePreviewWindow).GetField("stage", flags);
var generationField = typeof(DarkNights.Editor.Terrain.TerrainStylePreviewWindow).GetField("generation", flags);
var identityField = typeof(DarkNights.Editor.Terrain.TerrainStylePreviewWindow).GetField("generatedIdentity", flags);
var stage = (DarkNights.Editor.Terrain.TerrainStylePreviewStage)field.GetValue(window);
var rows = new System.Collections.Generic.List<object>();
int index = 0; double readyAfter = 0, started = UnityEditor.EditorApplication.timeSinceStartup;
bool waiting = false;
UnityEditor.EditorApplication.CallbackFunction callback = null;
callback = () =>
{
    try
    {
        if (UnityEditor.EditorApplication.timeSinceStartup - started > 180)
            throw new System.TimeoutException("Tuner preview timeout");
        if (!waiting)
        {
            window.Draft.Config.MineMap = UnityEditor.AssetDatabase.LoadAssetAtPath<DarkNights.Runtime.Terrain.MineMapAsset>(
                "Assets/DarkNights/Res/Terrain/MineMaps/" + ids[index] + ".minemap");
            refresh.Invoke(window, null); readyAfter = UnityEditor.EditorApplication.timeSinceStartup + 1; waiting = true;
            return;
        }
        if (UnityEditor.EditorApplication.timeSinceStartup < readyAfter) return;
        var generation = (DarkNights.Editor.Terrain.TerrainGenerationPreview)generationField.GetValue(window);
        if ((string)identityField.GetValue(window) != generation.Identity) return;
        if (stage.Error != null) throw stage.Error;
        if (!stage.Ready || stage.Image == null) return;
        var target = (UnityEngine.RenderTexture)stage.Image;
        var previous = UnityEngine.RenderTexture.active;
        var texture = new UnityEngine.Texture2D(target.width, target.height, UnityEngine.TextureFormat.RGBA32, false);
        try
        {
            UnityEngine.RenderTexture.active = target;
            texture.ReadPixels(new UnityEngine.Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, ids[index] + ".png"), UnityEngine.ImageConversion.EncodeToPNG(texture));
        }
        finally { UnityEngine.RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        rows.Add(new { id = ids[index], width = target.width, height = target.height, ready = stage.Ready,
            rocks = stage.RockBuildCount, refresh = stage.RefreshPath });
        index++; waiting = false;
        if (index < ids.Length) return;
        UnityEditor.EditorApplication.update -= callback;
        window.Draft.Config.MineMap = original; refresh.Invoke(window, null);
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, "result.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(new { completed = true, unity = UnityEngine.Application.unityVersion,
                scope = "Real Tuner camera pixels; no Play, input, ladder or Player acceptance", maps = rows }, Newtonsoft.Json.Formatting.Indented));
    }
    catch (System.Exception error)
    {
        UnityEditor.EditorApplication.update -= callback;
        window.Draft.Config.MineMap = original; refresh.Invoke(window, null);
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, "failure.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(new { error = error.ToString(), maps = rows }, Newtonsoft.Json.Formatting.Indented));
    }
};
UnityEditor.EditorApplication.update += callback;
return new { scheduled = true, output, maps = ids.Length };
