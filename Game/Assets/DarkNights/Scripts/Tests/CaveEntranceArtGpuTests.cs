using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Tests
{
    /// <summary>真实相机检查现有三层背景；覆盖正式缩放、分页、背景开关、遮挡及不随天空颜色变化的完整地下像素。</summary>
    public sealed class CaveEntranceArtGpuTests
    {
        [TestCase(1f)] [TestCase(.16f)]
        public void IndependentLayersPreserveSkyAndOpaqueShaftAcrossPages(float scale)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Authored entrance GPU fixture"); SceneManager.MoveGameObjectToScene(root, scene);
            var cameraRoot = new GameObject("Authored entrance camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.allowHDR = false; camera.allowMSAA = false;
            root.transform.position = new Vector3(3, 7, 0); root.transform.localScale = Vector3.one * scale;
            var clear = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var wall = new Material(Shader.Find("DarkNights/CaveStrata"));
            var style = ScriptableObject.CreateInstance<CaveBackgroundStyle>(); style.LayerShader = Shader.Find("DarkNights/CaveBackgroundLayer");
            var target = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var pages = new List<IDisposable>(); var previous = RenderTexture.active;
            try
            {
                clear.SetPixel(0, 0, Color.clear); clear.Apply(); wall.SetTexture("_CaveLight", clear);
                var backdrop = new CaveEntranceBackdrop(CaveEntranceArtTests.Reference(), new CaveEntranceArtTests.Layout());
                Type type = typeof(CaveVisualSource).Assembly.GetType("DarkNights.View.Terrain.CaveBackgroundPage", true);
                for (int row = 1; row <= 2; row++) for (int x = 4; x <= 5; x++)
                    pages.Add((IDisposable)Activator.CreateInstance(type, new object[] { x, row,
                        backdrop.Bake(x * 256, row * 256, 256, 256), wall, root.transform, style }));
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers.Length, Is.EqualTo(16));
                Assert.That(renderers.Select(r => r.sortingOrder).Distinct().OrderBy(v => v), Is.EqualTo(new[] { -100, -99, -98, -97 }));
                camera.orthographic = true; camera.orthographicSize = 21 * scale;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.transform.position = root.transform.TransformPoint(new Vector3(160, .5f - 53, -10 / scale));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
                camera.targetTexture = target; target.Create();
                void Render()
                { camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply(); }
                Color32 Pixel(float x, float row)
                {
                    Vector3 point = camera.WorldToScreenPoint(root.transform.TransformPoint(new Vector3(x, .5f - row, 0)));
                    return image.GetPixel((int)point.x, (int)point.y);
                }
                Render(); var sky = Pixel(160, 38); var wallPixel = Pixel(160, 65);
                Assert.That(sky.r > 240 && sky.g < 10 && sky.b > 240, Is.True);
                Assert.That(wallPixel.g, Is.GreaterThan(10));
                Directory.CreateDirectory(CaveEntranceArtValidation.Root);
                File.WriteAllBytes(Path.Combine(CaveEntranceArtValidation.Root, "shaft-existing-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"), image.EncodeToPNG());
                camera.backgroundColor = Color.blue; Render();
                Assert.That(Pixel(160, 65), Is.EqualTo(wallPixel), "地下不混入后方天空颜色");
                Assert.That(Pixel(160, 38).b, Is.GreaterThan(240)); Assert.That(Pixel(160, 38).r, Is.LessThan(10));
                foreach (var renderer in renderers.Where(r => r.sortingOrder != -100)) renderer.gameObject.SetActive(false);
                Render(); Assert.That(Pixel(160, 65).g, Is.GreaterThan(10), "关闭所有点缀层仍有完整地下后壁");
            }
            finally
            {
                foreach (var page in pages) page.Dispose(); RenderTexture.active = previous; camera.targetTexture = null;
                EditorSceneManager.ClosePreviewScene(scene); target.Release();
                foreach (var value in new UnityEngine.Object[] { clear, wall, style, target, image }) UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
