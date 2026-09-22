using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>实际工作台 Play 的同机位软硬边、旧风格截图与冻结检查；生成真实 GPU 渲染，不把离线 H5 图当 Unity 图。</summary>
public static class VerifyContourPlay
{
    private static string Dir => Path.GetFullPath("../artifacts/contour");
    public static async Task<string> Run()
    {
        var debug = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        if (!Application.isPlaying || debug == null) throw new Exception("请先在候选工作台进入 Play。");
        await Wait(() => debug.Preview != null && debug.Preview.Ready);
        debug.Flyer.enabled = false;
        Camera camera = debug.Flyer.ViewCamera; camera.orthographicSize = 12;
        var room = debug.Blueprint.Rooms[0]; camera.transform.position = new Vector3(room.X, -room.Y, -10);
        await Task.Delay(1000); await Wait(() => debug.Preview.Ready);
        Capture(camera, "soft.png");
        int before = debug.Preview.BackgroundBuildCount;
        await Task.Delay(1200);
        if (debug.Preview.BackgroundBuildCount != before) throw new Exception("静止重建背景。");
        var reference = new BackgroundBakeDescriptor(debug.Workshop.Map.World.WorldId.ToString().Replace("-", ""),
            debug.Settings.Seed, debug.Blueprint.CopyMaterials(), debug.Blueprint.CopyShapes());
        var pixels = BackgroundPageBaker.Bake(BackgroundContourBaker.Build(reference), Math.Max(0, (room.X - 16) * 8), (room.Y - 12) * 8, 256, 192);
        int cut = 0;
        for (int y = room.Y - 8; y <= room.Y + 8 && cut < 6; y++) for (int x = room.X - 12; x <= room.X + 12 && cut < 6; x++)
        {
            var p = new AnyRules.Next.CellCoord(x, -y);
            var cell = debug.Workshop.Map.Read(p).Cell;
            if (cell.IsEmpty || (cell.Flags & 1) != 0) continue;
            try
            {
                var targets = debug.Workshop.Map.BuildTargets(TerrainEditAction.Explosive, p);
                debug.Workshop.Map.DestroyTrusted(1, "contour-play-" + cut, TerrainEditAction.Explosive, debug.Workshop.Map.World, p, targets, _ => true);
                if (++cut == 6) break;
            }
            catch (InvalidOperationException) { }
            if (cut == 6) break;
        }
        debug.Preview.NotifyReplicaChanged();
        await Wait(() => !debug.Preview.RefreshingReplica && debug.Preview.Ready);
        if (debug.Preview.BackgroundBuildCount != before) throw new Exception("爆破触发背景重建。");
        Capture(camera, "soft-after-blast.png");
        var report = new JObject { ["playing"] = true, ["staticBuildCount"] = before,
            ["afterBlastBuildCount"] = debug.Preview.BackgroundBuildCount, ["blasts"] = cut,
            ["uploadedBytes"] = debug.Preview.BackgroundUploadedBytes, ["residentPages"] = debug.Preview.BackgroundResidentPages,
            ["referenceHash"] = reference.ReferenceHash, ["linear"] = QualitySettings.activeColorSpace.ToString() };
        if (cut == 0) throw new Exception("No actual blast targets were tested");
        // Hide the changed preview and create comparison previews from the same initial blueprint and fixed camera.
        debug.Preview.gameObject.SetActive(false);
        await Task.Delay(200);
        foreach (string mode in new[] { "hard", "legacy" })
        {
            var root = new GameObject("Contour QA " + mode);
            var style = UnityEngine.Object.Instantiate(debug.CaveStyle);
            var bg = UnityEngine.Object.Instantiate(style.Background); style.Background = bg;
            if (mode == "legacy") bg.ContourStatic = false; else bg.MiddleSoftness = 0;
            var preview = root.AddComponent<TerrainPreview>(); preview.ViewCamera = camera; preview.CaveStyle = style;
            preview.ShowBlueprint(debug.Definition, debug.Blueprint);
            await Wait(() => preview.Ready);
            preview.SetMinerals(debug.Blueprint.Deposits.Select((d, i) => new DarkNights.Core.ViewData.WorksiteViewData(
                i + 1, "mineral-deposit", (d.X + .5f) * 16, d.Y, 0, d.Capacity, 0, 0, 0, true, d.RoomKind, d.Rarity, d.Capacity, "Active")).ToArray());
            Capture(camera, mode + ".png");
            root.SetActive(false); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(style); UnityEngine.Object.Destroy(bg);
        }
        File.WriteAllText(Path.Combine(Dir, "play.json"), report.ToString());
        return report.ToString();
    }
    private static async Task Wait(Func<bool> ready)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 45;
        while (!ready())
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Contour visible page timeout");
            await Task.Delay(30);
        }
    }
    private static void Capture(Camera camera, string name)
    {
        var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Dir, name), texture.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(texture); }
    }
}
