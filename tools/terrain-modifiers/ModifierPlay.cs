using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

/// <summary>真实 Unity 场景中的两模式切换、独立作用域、快速连续破坏及页面缓存检查；只用运行期克隆配置，不写人工资产。</summary>
public static class ModifierPlay
{
    public static async Task<string> Run()
    {
        var debug = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        if (!Application.isPlaying || debug == null) throw new InvalidOperationException("需要 StrataCave Play。");
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        string folder = Path.GetFullPath("../artifacts/terrain-modifiers"); Directory.CreateDirectory(folder);
        string prefix = debug.FixedMap != null ? "fixed" : "random";
        var report = new JObject { ["scene"] = prefix, ["linear"] = QualitySettings.activeColorSpace.ToString(), ["modes"] = new JArray() };
        var original = debug.CaveStyle; var style = UnityEngine.Object.Instantiate(original);
        var background = UnityEngine.Object.Instantiate(original.Background); style.Background = background;
        var down = AssetDatabase.LoadAssetAtPath<DownwardEdgeModifierAsset>("Assets/DarkNights/Res/Terrain/StrataCave/Modifiers/DownwardRock.asset");
        try
        {
            await Wait(() => debug.Preview != null && debug.Preview.Ready, debug);
            debug.GetComponent<CaveWorkshopInput>().enabled = false; debug.Flyer.enabled = false;
            string initial = Hash(debug.Blueprint.CopyMaterials());
            foreach (string mode in prefix == "fixed" ? new[] { "none", "downward", "foreground-only", "background-only", "rounded" } : new[] { "downward", "rounded" })
            {
                int generation = debug.Generation; double started = Time.realtimeSinceStartupAsDouble;
                style.Modifiers = mode == "none" || mode == "background-only" ? Array.Empty<CaveModifierAsset>() :
                    mode == "downward" ? new CaveModifierAsset[] { down } : original.Modifiers;
                var modifiers = mode == "none" || mode == "foreground-only" ? Array.Empty<CaveModifierAsset>() :
                    mode == "downward" ? new CaveModifierAsset[] { down } : original.Background.NearModifiers;
                background.NearModifiers = modifiers; background.MiddleModifiers = modifiers; background.DeepModifiers = modifiers;
                debug.CaveStyle = style; debug.RequestRegenerate();
                await Wait(() => debug.Generation > generation && debug.Preview.Ready, debug);
                var camera = debug.Flyer.ViewCamera;
                camera.orthographicSize = prefix == "fixed" ? 19.5f : 13.5f;
                camera.transform.position = prefix == "fixed" ? new Vector3(71, -55, -10) :
                    new Vector3(debug.Workshop.X + 4, debug.Workshop.Y + 3, -10);
                await Task.Delay(200); await Wait(() => debug.Preview.Ready, debug);
                Capture(camera, Path.Combine(folder, prefix + "-" + mode + ".png"), 1512, 936);
                if (prefix == "fixed" && mode == "rounded")
                {
                    debug.Flyer.Art.enabled = false;
                    Capture(camera, Path.Combine(folder, "fixed-rounded-native.png"), 504, 312); debug.Flyer.Art.enabled = true;
                }
                if (initial != Hash(debug.Blueprint.CopyMaterials())) throw new Exception("Modifier 改写了权威布局。");
                ((JArray)report["modes"]).Add(new JObject { ["mode"] = mode, ["readySeconds"] = Time.realtimeSinceStartupAsDouble - started,
                    ["backgroundPages"] = debug.Preview.BackgroundBuildCount, ["rockPages"] = debug.Preview.RockBuildCount });
            }
            int bg = debug.Preview.BackgroundBuildCount, rock = debug.Preview.RockBuildCount;
            await Task.Delay(500);
            if (rock != debug.Preview.RockBuildCount || bg != debug.Preview.BackgroundBuildCount) throw new Exception("静止页面重复生成。");
            for (int i = 0; i < 90; i++) debug.Workshop.Tick(0, false, false, 1f / 60);
            float before = debug.Workshop.X;
            for (int i = 0; i < 30; i++) debug.Workshop.Tick(1, false, false, 1f / 60);
            if (debug.Workshop.X - before < .1f) throw new Exception("角色实际行走失败。");
            report["walkDistance"] = debug.Workshop.X - before;
            int cut = 0;
            for (int y = (int)-debug.Workshop.Y - 3; y < -debug.Workshop.Y + 5 && cut < 3; y++)
                for (int x = (int)debug.Workshop.X - 4; x < debug.Workshop.X + 5 && cut < 3; x++)
                    if (!debug.Workshop.Map.Read(new AnyRules.Next.CellCoord(x, -y)).Cell.IsEmpty && debug.Workshop.Edit(x, -y, true))
                    { cut++; debug.Preview.NotifyReplicaChanged(); await Task.Delay(50); }
            if (cut == 0) throw new Exception("未触发真实地图破坏。");
            await Task.Delay(100); await Wait(() => !debug.Preview.RefreshingReplica && debug.Preview.Ready, debug);
            if (bg != debug.Preview.BackgroundBuildCount || debug.Preview.RockBuildCount <= rock) throw new Exception("破坏缓存失效合同失败。");
            report["edits"] = cut; report["backgroundBefore"] = bg; report["backgroundAfter"] = debug.Preview.BackgroundBuildCount;
            report["rockBefore"] = rock; report["rockAfter"] = debug.Preview.RockBuildCount;
            Capture(debug.Flyer.ViewCamera, Path.Combine(folder, prefix + "-after-edit.png"), 1512, 936);
            report["success"] = true;
        }
        catch (Exception error) { report["success"] = false; report["error"] = error.ToString(); }
        finally
        {
            debug.CaveStyle = original; UnityEngine.Object.Destroy(style); UnityEngine.Object.Destroy(background);
            File.WriteAllText(Path.Combine(folder, prefix + "-play.json"), report.ToString());
        }
        return report.ToString();
    }
    private static async Task Wait(Func<bool> predicate, TerrainDebugBootstrap debug)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 60;
        while (!predicate())
        {
            if (debug.LastError != null) throw new Exception(debug.LastError);
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("页面未就绪。");
            await Task.Delay(50);
        }
    }
    private static string Hash(byte[] value)
    { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(value)); }
    private static void Capture(Camera camera, string path, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(texture); }
    }
}
