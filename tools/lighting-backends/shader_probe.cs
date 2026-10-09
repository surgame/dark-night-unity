// Unity CLI eval_file：候选Shader的有限离屏探针，不进入正式Play，不保存场景。
var worktree = "C:/Users/Jobscn/.codex/worktrees/aperture-lighting-backends/unity-projects/Game/";
var output = System.IO.Path.GetFullPath("../artifacts/lighting-backends-20261009/shader-probe");
System.IO.Directory.CreateDirectory(output);
var monitor = System.IO.Path.Combine(output, "memory", "breach.json");
var owned = new System.Collections.Generic.List<UnityEngine.Object>();
var shaders = new System.Collections.Generic.List<object>();
try
{
    foreach (string path in new[] {
        "Assets/DarkNights/Res/Shared/Lighting/CampSprite.shader",
        "Assets/DarkNights/Res/Art/Custom/CaveContourStatic/CaveStrata.shader",
        "Assets/DarkNights/Res/Art/Custom/CaveEntranceLayers/CaveBackgroundLayer.shader",
        "Assets/DarkNights/Res/Art/Custom/CaveExploration/CavePixelRock.shader" })
    {
        if (System.IO.File.Exists(monitor)) throw new System.OperationCanceledException("内存门控停止Shader探针。");
        string text = System.IO.File.ReadAllText(worktree + path);
        text = text.Replace("\"Assets/DarkNights/Res/Shared/Lighting/", "\"" + worktree + "Assets/DarkNights/Res/Shared/Lighting/");
        text = text.Replace("Shader \"", "Shader \"LightingProbe/");
        var shader = UnityEditor.ShaderUtil.CreateShaderAsset(text, false); owned.Add(shader);
        var material = new UnityEngine.Material(shader); owned.Add(material);
        foreach (string mode in new[] { "private", "urp" })
        {
            if (System.IO.File.Exists(monitor)) throw new System.OperationCanceledException("内存门控停止Shader探针。");
            if (mode == "urp") material.EnableKeyword("USE_SHAPE_LIGHT_TYPE_0");
            else material.DisableKeyword("USE_SHAPE_LIGHT_TYPE_0");
            for (int pass = 0; pass < material.passCount; pass++) UnityEditor.ShaderUtil.CompilePass(material, pass, true);
        }
        var messages = UnityEditor.ShaderUtil.GetShaderMessages(shader);
        var errors = new System.Collections.Generic.List<string>();
        foreach (var message in messages) if (message.severity.ToString() == "Error") errors.Add(message.message);
        shaders.Add(new { path, passes = material.passCount, errors });
        if (errors.Count != 0) throw new System.Exception(path + ": " + string.Join("; ", errors));
    }
    var report = new { passed = true, scope = "Actual Unity Shader compiler, private/URP variants and normals passes; no game Play", shaders };
    System.IO.File.WriteAllText(System.IO.Path.Combine(output, "result.json"), Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
    return report;
}
catch (System.Exception error)
{
    System.IO.File.WriteAllText(System.IO.Path.Combine(output, "failure.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { error = error.ToString(), shaders }, Newtonsoft.Json.Formatting.Indented));
    throw;
}
finally { foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); }
