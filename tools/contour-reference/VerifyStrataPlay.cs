using System;
using System.IO;
using System.Threading.Tasks;
using DarkNights.Entry.Terrain;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>独立样板的真实 GPU 画面和 AnyRuleD 编辑检查；同机位记录比例、挖掘前后与静态背景计数。</summary>
public static class VerifyStrataPlay
{
    public static async Task<string> Run()
    {
        var debug = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        if (!Application.isPlaying || debug == null) throw new Exception("需要在 StrataCave 场景 Play。");
        Application.runInBackground = true;
        UnityEditor.EditorApplication.isPaused = false;
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        await Wait(() => debug.Preview != null && debug.Preview.Ready);
        await Task.Delay(800);
        debug.GetComponent<CaveWorkshopInput>().enabled = false; debug.Flyer.enabled = false;
        for (int i = 0; i < 90; i++) debug.Workshop.Tick(0, false, false, 1f / 60);
        debug.Flyer.Teleport(new Vector2(debug.Workshop.X, debug.Workshop.Y));
        var camera = debug.Flyer.ViewCamera;
        string prefix = debug.FixedMap != null ? "strata" : "random-strata";
        camera.orthographicSize = 11.25f;
        var actor = debug.Flyer.transform.position;
        camera.transform.position = new Vector3(actor.x + 4, actor.y + 3, -10);
        await Task.Delay(200); await Wait(() => debug.Preview.Ready);
        Capture(camera, prefix + "-close.png", 1280, 720);
        var closePosition = camera.transform.position;
        if (debug.FixedMap != null)
        {
            camera.orthographicSize = 19.5f; camera.transform.position = new Vector3(71, -55, -10);
            await Task.Delay(200); await Wait(() => debug.Preview.Ready);
            Capture(camera, "strata-overview.png", 1512, 936);
            debug.Flyer.Art.enabled = false;
            Capture(camera, "strata-editor-preview.png", 504, 312);
            debug.Flyer.Art.enabled = true;
            camera.orthographicSize = 11.25f; camera.transform.position = closePosition;
            await Task.Delay(200); await Wait(() => debug.Preview.Ready);
        }
        int background = debug.Preview.BackgroundBuildCount, rock = debug.Preview.RockBuildCount;
        await Task.Delay(500);
        if (rock != debug.Preview.RockBuildCount) throw new Exception("静止重烘焙岩壁。");
        float before = debug.Workshop.X;
        for (int i = 0; i < 30; i++) debug.Workshop.Tick(1, false, false, 1f / 60);
        float after = debug.Workshop.X;
        if (after - before < .1f) throw new Exception("固定样板角色无法实际行走。");
        debug.Workshop.Teleport(actor.x, actor.y);
        int cut = 0;
        for (int y = (int)-actor.y - 3; y < -actor.y + 5 && cut == 0; y++)
            for (int x = (int)actor.x - 4; x < actor.x + 5 && cut == 0; x++)
                if (!debug.Workshop.Map.Read(new AnyRules.Next.CellCoord(x, -y)).Cell.IsEmpty && debug.Workshop.Edit(x, -y, true)) cut++;
        if (cut == 0) throw new Exception("没有真实地图编辑。");
        debug.Preview.NotifyReplicaChanged();
        await Task.Delay(100);
        await Wait(() => !debug.Preview.RefreshingReplica && debug.Preview.Ready);
        if (debug.Preview.BackgroundBuildCount != background || debug.Preview.RockBuildCount <= rock)
            throw new Exception("地形局部重绘计数：bg=" + background + "->" + debug.Preview.BackgroundBuildCount + ", rock=" + rock + "->" + debug.Preview.RockBuildCount);
        Capture(camera, prefix + "-after-blast.png", 1280, 720);
        var report = new JObject { ["linear"] = QualitySettings.activeColorSpace.ToString(),
            ["position"] = actor.ToString(), ["walkDistance"] = after - before, ["blasts"] = cut,
            ["backgroundBefore"] = background, ["backgroundAfterEdit"] = background,
            ["rockBefore"] = rock, ["rockAfter"] = debug.Preview.RockBuildCount,
            ["pixelsPerCell"] = 8, ["actorSpritePixelsPerCell"] = 8, ["mineralVisuals"] = false, ["outline"] = "HybridB/OUTLINE-0921/3/20/2", ["stonePixels"] = 4 };
        File.WriteAllText(Path.GetFullPath("../artifacts/contour/" + prefix + "-play.json"), report.ToString());
        return report.ToString();
    }
    private static async Task Wait(Func<bool> predicate)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 45;
        while (!predicate()) { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("样板可见页未就绪。"); await Task.Delay(30); }
    }
    private static void Capture(Camera camera, string name, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.GetFullPath("../artifacts/contour/" + name), texture.EncodeToPNG());
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(texture); }
    }
}
