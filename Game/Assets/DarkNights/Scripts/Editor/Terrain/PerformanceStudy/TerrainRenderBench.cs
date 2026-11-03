using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace DarkNights.Editor.Terrain.PerformanceStudy
{
    /// <summary>显式执行三种铺设的离屏微测量；不进入Play、不切场景，计时包括同步读回而不代表前台帧率。</summary>
    public static class TerrainRenderBench
    {
        public const string SourceVersion = "terrain-render-bench-20261101-v4";
        /// <summary>一组原始样本及分位数，保留异常值供复核，不过滤较慢结果。</summary>
        [Serializable]
        public sealed class Samples
        {
            public double median, p95, max;
            public double[] values;
            public Samples(List<double> source)
            {
                values = source.ToArray(); var sorted = source.ToArray(); Array.Sort(sorted);
                if (sorted.Length == 0) return;
                median = sorted[sorted.Length / 2]; p95 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * .95))];
                max = sorted[sorted.Length - 1];
            }
        }
        /// <summary>一种策略的冻结结果；资源数字仅覆盖本实验拥有的地形GPU纹理和网格有效载荷。</summary>
        [Serializable]
        public sealed class Row
        {
            public string mode;
            public Samples initial, stable, enqueue, single, blast, singleUpload, blastUpload;
            public long textureBytes, vertexBytes;
            public int draws, vertices;
        }
        /// <summary>当前Editor及设备身份和三策略结果，不能替代正式游戏光照、碰撞或联机验收。</summary>
        [Serializable]
        public sealed class Report
        {
            public string unity, device, graphics, scope, inputPath, inputSha256, sourceVersion;
            public int rounds = 3, width = 960, height = 640;
            public bool passed;
            public List<Row> rows = new List<Row>();
        }
        public static string Run(string inputPath, string outputDirectory)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("离屏实验要求Editor空闲且不处于Play。");
            Directory.CreateDirectory(outputDirectory);
            var inputBytes = File.ReadAllBytes(inputPath);
            var input = JsonUtility.FromJson<TerrainRenderBenchInput>(System.Text.Encoding.UTF8.GetString(inputBytes));
            if (input.version != 1 || input.frames.Length < 3) throw new InvalidDataException("冻结输入身份无效。");
            var shader = Shader.Find("Hidden/DarkNights/TerrainRenderBench");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("实验Shader不可用。");
            var textures = new Texture2D[4];
            var target = new RenderTexture(input.width, input.height, 0, RenderTextureFormat.ARGB32);
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            var report = new Report { unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName,
                width = input.width, height = input.height, sourceVersion = SourceVersion,
                graphics = SystemInfo.graphicsDeviceType.ToString(), inputPath = inputPath,
                scope = "Editor offscreen; frozen browser geometry; mesh packing/upload + rendering/readback. No contour extraction, collision, light, ecology or FPS." };
            using (var sha = System.Security.Cryptography.SHA256.Create())
                report.inputSha256 = BitConverter.ToString(sha.ComputeHash(inputBytes)).Replace("-", "").ToLowerInvariant();
            target.Create();
            try
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    textures[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(textures[i], Convert.FromBase64String(input.textures[i]), false))
                        throw new InvalidDataException("纹理解码失败。");
                    textures[i].filterMode = FilterMode.Point;
                    textures[i].wrapModeU = i == 3 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                    textures[i].wrapModeV = i == 0 ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                }
                var modes = new[] { "baked", "strip", "atlas" };
                var samples = new Dictionary<string, List<double>[]>();
                foreach (string mode in modes)
                {
                    var lists = new List<double>[7]; for (int i = 0; i < lists.Length; i++) lists[i] = new List<double>();
                    samples.Add(mode, lists); report.rows.Add(new Row { mode = mode });
                }
                for (int round = 0; round < 3; round++) for (int item = 0; item < 3; item++)
                {
                    string mode = modes[(round + item) % 3]; var s = samples[mode];
                    using (var surface = new TerrainRenderBenchSurface(mode, textures, shader))
                    {
                        var clock = Stopwatch.StartNew(); surface.Apply(input.frames[0]);
                        surface.Draw(target, input.view); Drain(target, pixel); s[0].Add(clock.Elapsed.TotalMilliseconds);
                        for (int warm = 0; warm < 5; warm++) { surface.Draw(target, input.view); Drain(target, pixel); }
                        for (int frame = 0; frame < 15; frame++)
                        {
                            clock.Restart(); surface.Draw(target, input.view); s[2].Add(clock.Elapsed.TotalMilliseconds);
                            Drain(target, pixel); s[1].Add(clock.Elapsed.TotalMilliseconds);
                        }
                        if (round == 0) Snapshot(target, Path.Combine(outputDirectory, mode + "-initial.png"));
                        for (int frame = 1; frame < input.frames.Length; frame++)
                        {
                            var update = input.frames[frame];
                            if (update.kind == "reset")
                            { surface.Apply(input.frames[0]); surface.Draw(target, input.view); Drain(target, pixel); continue; }
                            clock.Restart(); surface.Apply(update); surface.Draw(target, input.view); Drain(target, pixel);
                            bool single = update.kind == "single";
                            s[single ? 3 : 4].Add(clock.Elapsed.TotalMilliseconds);
                            s[single ? 5 : 6].Add(surface.UploadedBytes);
                        }
                        if (round == 0) Snapshot(target, Path.Combine(outputDirectory, mode + "-broken.png"));
                        var row = report.rows.Find(r => r.mode == mode); row.textureBytes = surface.TextureBytes;
                        row.vertexBytes = surface.VertexBytes; row.draws = surface.Draws; row.vertices = surface.Vertices;
                    }
                }
                foreach (var row in report.rows)
                {
                    var s = samples[row.mode]; row.initial = new Samples(s[0]); row.stable = new Samples(s[1]);
                    row.enqueue = new Samples(s[2]); row.single = new Samples(s[3]); row.blast = new Samples(s[4]);
                    row.singleUpload = new Samples(s[5]); row.blastUpload = new Samples(s[6]);
                }
                report.passed = true;
                string output = Path.Combine(outputDirectory, "report.json");
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                return output;
            }
            finally
            {
                RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixel);
                foreach (var texture in textures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        private static void Drain(RenderTexture target, Texture2D pixel)
        {
            RenderTexture.active = target;
            pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
        }
        private static void Snapshot(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
