using System;
using System.IO;
using System.Text.Json;
using DarkNights.Core.Logic.Terrain;

/// <summary>
/// 独立H5后壁的只读采样工具，调用当前磁盘Core程序集的三层背景算法。
/// 输入仅为演示洞室的初始实心矩形，不启动Unity、不访问正式地图或存档，
/// 输出RGBA与后续PNG均由实验构建拥有，不写回Game或来源程序集。
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(args[0]));
        string output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        int count = 0;
        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            string id = entry.GetProperty("id").GetString();
            const int width = 768;
            const int height = 336;
            var mask = new byte[width * height];
            foreach (JsonElement rect in entry.GetProperty("solids").EnumerateArray())
            {
                int x0 = rect[0].GetInt32() / 2;
                int y0 = rect[1].GetInt32() / 2;
                int x1 = (rect[0].GetInt32() + rect[2].GetInt32() + 1) / 2;
                int y1 = (rect[1].GetInt32() + rect[3].GetInt32() + 1) / 2;
                for (int y = Math.Max(0, y0); y < Math.Min(height, y1); y++)
                    for (int x = Math.Max(0, x0); x < Math.Min(width, x1); x++) mask[y * width + x] = 1;
            }
            var layout = new BackgroundContourBaker(mask, width, height, "DN-H5-" + id, 0);
            byte[][] layers = BackgroundPageBaker.Bake(layout, 0, 0, width, height);
            var rgba = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                int k = i * 4;
                rgba[k] = 27; rgba[k + 1] = 21; rgba[k + 2] = 15; rgba[k + 3] = 255;
                for (int layer = 2; layer >= 0; layer--)
                {
                    double alpha = layers[layer][k + 3] / 255.0;
                    for (int c = 0; c < 3; c++)
                        rgba[k + c] = (byte)Math.Floor(rgba[k + c] * (1 - alpha) + layers[layer][k + c] * alpha + .5);
                }
            }
            File.WriteAllBytes(Path.Combine(output, id + ".rgba"), rgba);
            count++;
        }
        string calibration = Path.Combine(output, "calibration");
        Directory.CreateDirectory(calibration);
        bool Solid(int x, int y) => y >= 30 || x < 7 || y < 6 || x > 22 && x < 27 && y < 20;
        File.WriteAllBytes(Path.Combine(calibration, "rock.rgba"),
            CaveRockBaker.Bake(Solid, 32, 32, "", 0, 0, 32, 32, stoneSize: 4));
        Console.WriteLine("Sampled " + count + " independent H5 backgrounds; no Unity process.");
    }
}
