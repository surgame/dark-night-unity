using System;
using System.Diagnostics;
using System.IO;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using Newtonsoft.Json.Linq;
/// <summary>在当前 Unity Mono 运行时量取纯 CPU 烘焙，不以此替代前台帧率和 GPU 提交验收。</summary>
public static class MeasureOutlineBake
{
    public static string Run()
    {
        var raw = File.ReadAllBytes("../tools/contour-reference/profile-raw.bin");
        var settings = CaveOutlineSettings.Current;
        bool Solid(int x, int y) => raw[y * 504 + x] != 0;
        CaveRockBaker.Bake(Solid, 504, 312, "DN-MATERIAL-0921", 117, 32, 256, 256, null, 4, settings);
        var samples = new double[7]; var watch = new Stopwatch();
        for (int i = 0; i < samples.Length; i++)
        {
            watch.Restart();
            CaveRockBaker.Bake(Solid, 504, 312, "DN-MATERIAL-0921", 117, 32, 256, 256, null, 4, settings);
            samples[i] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        var reference = ExpeditionTerrainGenerator.Generate("EXPEDITION-QUICK-01", Guid.NewGuid().ToString("N")).Background;
        watch.Restart(); var layout = BackgroundContourBaker.Build(reference, null, settings);
        double layoutMs = watch.Elapsed.TotalMilliseconds;
        var backgrounds = new double[7];
        for (int i = 0; i < backgrounds.Length; i++)
        {
            watch.Restart(); BackgroundPageBaker.Bake(layout, 256, 256, 256, 256, 2);
            backgrounds[i] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(backgrounds);
        var cells = new UnityEngine.Color32[320 * 192];
        for (int y = 0; y < 192; y++) for (int x = 0; x < 320; x++)
            cells[y * 320 + x] = new UnityEngine.Color32(reference.Material(x, y), reference.Shape(x, y), 0, 255);
        var method = typeof(DarkNights.View.Terrain.CaveTerrainStyle).Assembly.GetType("DarkNights.View.Terrain.CaveSoftLightField").GetMethod("Build");
        watch.Restart(); method.Invoke(null, new object[] { cells, 320, 192, null });
        double lightMs = watch.Elapsed.TotalMilliseconds; int lights = 0;
        foreach (var cell in cells) if (cell.b != 0) lights++;
        var output = new JObject { ["runtime"] = "Unity Editor Mono 6000.4.9f1", ["pagePixels"] = 256,
            ["cpuMedianMs"] = samples[3], ["cpuMaxMs"] = samples[6], ["samplesMs"] = new JArray(samples),
            ["initialBackgroundLayoutMs"] = layoutMs, ["backgroundThreeLayersPageMedianMs"] = backgrounds[3],
            ["backgroundThreeLayersPageMaxMs"] = backgrounds[6], ["debugLightFieldMs"] = lightMs, ["debugLights"] = lights,
            ["foregroundGpuBytes"] = 2560 * 1536 * 4, ["backgroundGpuMaxBytes"] = 60 * 256 * 256 * 4 * 3,
            ["scope"] = "CPU bake only; excludes GPU upload, draw, foreground frame budget" };
        File.WriteAllText("../artifacts/contour/outline-unity-benchmark.json", output.ToString()); return output.ToString();
    }
}
