using System;
using System.IO;
using System.Threading.Tasks;
using DarkNights.Entry.Terrain;
using UnityEngine;

/// <summary>由 Editor run_script 显式运行的 8×8 洞穴材质近景与全景捕获；只读取 Play 中的展示，不修改场景或权威地图。</summary>
public static class CaveStyleReview
{
    public static async Task<string> Run()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/cave-8x8-style"));
        Directory.CreateDirectory(output);
        var bootstrap = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        var input = UnityEngine.Object.FindAnyObjectByType<CaveWorkshopInput>();
        if (bootstrap == null || input == null) throw new InvalidOperationException("Open and play cave scene first.");
        input.Walking = false;
        Application.runInBackground = true;
        await WaitForPreview(bootstrap);

        bootstrap.Flyer.CameraDistance = 100;
        bootstrap.Flyer.Teleport(new Vector2(160, -96));
        await Task.Delay(100);
        await WaitForPreview(bootstrap);
        Capture(bootstrap.Flyer.ViewCamera, Path.Combine(output, "overview.png"), 1600, 1000);

        bootstrap.VisitRoom(0);
        bootstrap.Flyer.CameraDistance = 10;
        for (int frame = 0; frame < 600; frame++) bootstrap.Workshop.Tick(0, false, false, 1f / 60);
        bootstrap.Flyer.Teleport(new Vector2(bootstrap.Workshop.X, bootstrap.Workshop.Y));
        await Task.Delay(100);
        await WaitForPreview(bootstrap);
        Capture(bootstrap.Flyer.ViewCamera, Path.Combine(output, "near.png"), 1600, 900);
        input.Walking = true;
        File.WriteAllText(Path.Combine(output, "visual-status.json"),
            "{\"seed\":\"" + bootstrap.Settings.Seed + "\",\"overview\":true,\"near\":true}");
        return "Captured 8x8 cave overview and near view for " + bootstrap.Settings.Seed;
    }

    private static async Task WaitForPreview(TerrainDebugBootstrap bootstrap)
    {
        float deadline = Time.realtimeSinceStartup + 45;
        while (bootstrap.Preview == null || !bootstrap.Preview.Ready || bootstrap.Preview.RefreshingReplica)
        {
            if (bootstrap.LastError != null || Time.realtimeSinceStartup > deadline)
                throw new InvalidOperationException(bootstrap.LastError ?? "Cave preview timeout.");
            await Task.Delay(40);
        }
    }

    private static void Capture(Camera camera, string path, int width, int height)
    {
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(image);
        }
    }
}
