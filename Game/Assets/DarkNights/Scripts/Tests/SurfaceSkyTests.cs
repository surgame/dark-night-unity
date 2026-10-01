using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.View.Expedition;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Tests
{
    /// <summary>地表天空专项回归；纯天际线逐像素对照坡形，真实相机核对地表透明与地下遮挡，不修改人工资产或运行会话。</summary>
    public sealed class SurfaceSkyTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static BackgroundBakeDescriptor Reference(byte[] materials, byte[] shapes) =>
            new BackgroundBakeDescriptor("cf7a124d6c434c8fa970a397cbe83a21", "SKY-REGRESSION", materials, shapes);

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        public void SkylineMatchesFirstOccupiedPixelForEveryShape(int shape)
        {
            var cells = new byte[320 * 192]; var shapes = new byte[cells.Length];
            cells[40 * 320 + 20] = 1; shapes[40 * 320 + 20] = (byte)shape;
            cells[45 * 320 + 21] = 1;
            var source = Reference(cells, shapes);
            byte[] raster = TerrainVisualCoordinates.Rasterize(source);
            ushort[] skyline = TerrainVisualCoordinates.SurfaceSkyline(source);
            for (int x = 20 * 8; x < 22 * 8; x++)
            {
                int expected = 1536;
                for (int y = 0; y < 1536; y++) if (raster[y * 2560 + x] != 0) { expected = y; break; }
                Assert.That(skyline[x], Is.EqualTo(expected));
            }
            Assert.That(skyline[0], Is.EqualTo(1536), "空列不能人为裁在泊位高度");
            CollectionAssert.AreEqual(cells, source.CopyMaterials());
        }

        [TestCase(false, 1f)] [TestCase(true, 1f)] [TestCase(true, .16f)]
        public void ActualShaderShowsValleySkyAndPreservesUnderground(bool surfaceSky, float scale)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Surface sky GPU fixture"); SceneManager.MoveGameObjectToScene(root, scene);
            var cameraRoot = new GameObject("Surface sky fixture camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.allowHDR = false; camera.allowMSAA = false;
            root.transform.position = new Vector3(3, 7, 0); root.transform.localScale = Vector3.one * scale;
            var material = new Material(Shader.Find("DarkNights/CaveStrata"));
            var skyline = new Texture2D(2560, 1, TextureFormat.RGBA32, false, true)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var mesh = new Mesh();
            var target = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var heights = new Color32[2560];
                for (int x = 0; x < heights.Length; x++)
                {
                    int height = x < 4 * 8 ? 40 * 8 : 44 * 8;
                    heights[x] = new Color32((byte)(height & 255), (byte)(height >> 8), (byte)(height & 255), (byte)(height >> 8));
                }
                skyline.SetPixels32(heights); skyline.Apply(); clear.SetPixel(0, 0, Color.clear); clear.Apply();
                material.SetTexture("_SurfaceSkyline", skyline); material.SetFloat("_SurfaceSky", surfaceSky ? 1 : 0);
                material.SetTexture("_RockSurface", clear); material.SetTexture("_CaveLight", clear);
                material.SetFloat("_Background", 1); material.SetMatrix("_MapWorldToLocal", root.transform.worldToLocalMatrix);
                mesh.vertices = new[] { new Vector3(-1, -60, 1), new Vector3(9, -60, 1), new Vector3(9, 0, 1), new Vector3(-1, 0, 1) };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -100;
                camera.orthographic = true; camera.orthographicSize = 4 * scale;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.scene);
                camera.transform.position = root.transform.TransformPoint(new Vector3(3.5f, -41.5f, -10 / scale));
                camera.transform.rotation = Quaternion.identity; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta; camera.targetTexture = target; target.Create();
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                string folder = Path.GetFullPath("../artifacts/surface-environment-20261001/images"); Directory.CreateDirectory(folder);
                string imagePath = Path.Combine(folder, "shader-" + surfaceSky + "-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png");
                File.WriteAllBytes(imagePath, pixels.EncodeToPNG());
                bool IsSky(float x, float row)
                {
                    var point = camera.WorldToScreenPoint(root.transform.TransformPoint(new Vector3(x, .5f - row, 0)));
                    Color32 color = pixels.GetPixel((int)point.x, (int)point.y);
                    return color.r > 240 && color.g < 10 && color.b > 240;
                }
                Assert.That(IsSky(2, 39), Is.EqualTo(surfaceSky), "地表上方天空");
                Assert.That(IsSky(6, 42), Is.EqualTo(surfaceSky), "低于泊位但高于凹地表的位置仍应是天空");
                Assert.That(IsSky(2, 42), Is.False, "同高度平台下仍是洞穴背景");
                Assert.That(IsSky(6, 45), Is.False, "凹地下方仍是洞穴背景");
                // 背景分页复制材质后也必须保留同一遮罩，不能重新覆盖天空。
                material.SetVector("_StrataEnabled", new Vector4(1, 1, 1, 1));
                material.SetTexture("_StrataNear", clear); material.SetTexture("_StrataMiddle", clear); material.SetTexture("_StrataDeep", clear);
                camera.Render(); pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                Assert.That(IsSky(6, 42), Is.EqualTo(surfaceSky));
                File.WriteAllBytes(imagePath, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                foreach (var asset in new UnityEngine.Object[] { material, skyline, clear, mesh, target, pixels }) UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [TestCase(JourneyPhase.Orbit)] [TestCase(JourneyPhase.Transit)] [TestCase(JourneyPhase.Descent)] [TestCase(JourneyPhase.Landed)]
        public void JourneySkyCoversViewportBelowDockAndKeepsSpaceReceipt(JourneyPhase phase)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Journey full viewport fixture"); SceneManager.MoveGameObjectToScene(root, scene);
            var cameraRoot = new GameObject("Journey fixture camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.allowHDR = false; camera.allowMSAA = false;
            var target = new RenderTexture(128, 128, 16);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            JourneyEnvironment environment = null;
            try
            {
                camera.orthographic = true; camera.orthographicSize = 1;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
                camera.transform.position = new Vector3(1, -20, -10); camera.transform.rotation = Quaternion.identity;
                camera.targetTexture = target; target.Create();
                environment = root.AddComponent<JourneyEnvironment>(); environment.Initialize(camera);
                var planet = new PlanetDefinition("sky", "天空", starCount: 0);
                typeof(JourneyEnvironment).GetField("planet", Private).SetValue(environment, planet);
                typeof(JourneyEnvironment).GetField("journey", Private).SetValue(environment,
                    new JourneyViewData(true, "fixture", 1, phase, planet.Id, "", "", "", 0, "", new[] { planet }));
                typeof(JourneyEnvironment).GetField("identity", Private).SetValue(environment, "fixture");
                camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                Color32 drawn = pixels.GetPixel(64, 100);
                Assert.That(drawn.r > 240 && drawn.g < 10 && drawn.b > 240, Is.False, "相机实际绘制天空，不能仅以网格顶点确认");
                var mesh = (Mesh)typeof(JourneyEnvironment).GetField("skyMesh", Private).GetValue(environment);
                Assert.That(mesh.vertices.Min(v => v.y), Is.EqualTo(-21));
                Assert.That(mesh.vertices.Max(v => v.y), Is.EqualTo(-19));
                var sky = (GameObject)typeof(JourneyEnvironment).GetField("sky", Private).GetValue(environment);
                Assert.That(sky.GetComponent<MeshRenderer>().sortingOrder, Is.LessThan(-100));
                Assert.That(environment.SpaceReady, Is.EqualTo(phase == JourneyPhase.Orbit || phase == JourneyPhase.Transit));
            }
            finally
            {
                if (environment != null) foreach (string name in new[] { "sky", "stars", "skyMesh", "starMesh", "material" })
                {
                    var field = typeof(JourneyEnvironment).GetField(name, Private);
                    var asset = (UnityEngine.Object)field.GetValue(environment); field.SetValue(environment, null);
                    UnityEngine.Object.DestroyImmediate(asset);
                }
                RenderTexture.active = previous; camera.targetTexture = null; EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
