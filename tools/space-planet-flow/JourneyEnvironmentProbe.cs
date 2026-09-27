using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkNights.Core.ViewData;
using DarkNights.View.Expedition;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>隔离本地航程表现探针；复用已捕获冻结投影创建预览场景，不启动会话或改作者资产，核对策略、颜色与实际相机回执。</summary>
public static class JourneyEnvironmentProbe
{
    public static string Run(string snapshot)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Editor occupied.");
        string folder = Path.GetFullPath("../artifacts/space-planet-flow/environment-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(folder);
        var source = JObject.Parse(File.ReadAllText(Path.GetFullPath(snapshot)));
        var checks = new JArray();
        var report = new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["sourceSnapshot"] = snapshot,
            ["scope"] = "isolated local presentation fixture, actual camera render; not a foreground gameplay screenshot", ["checks"] = checks };
        var preview = new PreviewRenderUtility();
        var target = new RenderTexture(640, 360, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        var root = new GameObject("航程本地表现验收");
        try
        {
            preview.AddSingleGO(root);
            var camera = preview.camera; camera.orthographic = true; camera.orthographicSize = 2;
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.transform.position = new Vector3(0, 0, -10); camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.targetTexture = target; target.Create();
            var environment = root.AddComponent<JourneyEnvironment>(); environment.Initialize(camera);
            Mesh stars = (Mesh)typeof(JourneyEnvironment).GetField("starMesh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(environment);
            void Check(string name, bool passed)
            {
                checks.Add(new JObject { ["name"] = name, ["passed"] = passed });
                if (!passed) throw new InvalidOperationException(name);
            }
            JObject Frame(string kind, double elapsed, int count, string color)
            {
                var frame = (JObject)source["frame"].DeepClone();
                var journey = (JObject)frame["World"]["Expedition"]["Journey"];
                var planet = (JObject)journey["Planets"][0];
                planet["TransitionKind"] = kind; planet["TransitSeconds"] = 2;
                planet["StarCount"] = count; planet["StarSpeed"] = 110; planet["SpaceColorHex"] = color;
                journey["PlanetId"] = planet["Id"]; journey["Phase"] = 2; journey["PhaseElapsed"] = elapsed;
                return frame;
            }
            Vector3[] Draw(string kind, double elapsed, int count, string color, string label)
            {
                environment.Present(Frame(kind, elapsed, count, color).ToObject<SessionViewData>());
                camera.Render();
                Check(label + " camera receipt", environment.SpaceReady);
                Check(label + " configured star count", stars.vertexCount == count * 4);
                RenderTexture.active = target;
                var pixels = new Texture2D(640, 360, TextureFormat.RGBA32, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(folder, label + ".png"), pixels.EncodeToPNG());
                    var dominant = pixels.GetPixels32().GroupBy(c => ((int)c.r << 16) | ((int)c.g << 8) | c.b)
                        .OrderByDescending(g => g.Count()).First().Key;
                    report[label + "-dominantColor"] = "#" + dominant.ToString("X6");
                    Check(label + " author RGB preserved", dominant == Convert.ToInt32(color.TrimStart('#'), 16));
                }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                return stars.vertices;
            }
            Check("Linear project", QualitySettings.activeColorSpace == ColorSpace.Linear);
            var shift0 = Draw("star-shift", 0, 24, "#060C20", "shift-0");
            var shift1 = Draw("star-shift", .75, 24, "#060C20", "shift-1");
            Check("star-shift changes positions", !shift0.SequenceEqual(shift1));
            var still0 = Draw("none", 0, 80, "#142040", "none-0");
            var still1 = Draw("none", .75, 80, "#142040", "none-1");
            Check("none keeps positions", still0.SequenceEqual(still1));
            Draw("fade", .1, 24, "#060C20", "fade-start"); float alpha0 = stars.colors[0].a;
            Draw("fade", 1, 24, "#060C20", "fade-middle"); float alpha1 = stars.colors[0].a;
            Check("fade changes opacity", Math.Abs(alpha0 - alpha1) > .1f);
            report["success"] = true;
        }
        catch (Exception error) { report["success"] = false; report["error"] = error.ToString(); }
        finally
        {
            RenderTexture.active = previous; preview.camera.targetTexture = null;
            // 运行组件只在 Play 销毁资源；本探针的 Editor 预览资源使用即时回收后清空引用。
            var owner = root.GetComponent<JourneyEnvironment>();
            if (owner != null) foreach (string name in new[] { "sky", "stars", "skyMesh", "starMesh", "material" })
            {
                var field = typeof(JourneyEnvironment).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                var asset = (UnityEngine.Object)field.GetValue(owner);
                field.SetValue(owner, null); if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
            preview.Cleanup(); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            report["finishedUtc"] = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.Combine(folder, "result.json"), report.ToString());
        }
        report["folder"] = folder; return report.ToString();
    }
}
