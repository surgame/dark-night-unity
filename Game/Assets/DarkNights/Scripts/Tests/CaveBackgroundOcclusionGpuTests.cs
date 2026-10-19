using System;
using System.IO;
using DarkNights.Editor;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Tests
{
    /// <summary>背景专用剔除的真实相机检查；独立场景验证Alpha阈值、Unknown及共享Shader默认行为，释放全部自有GPU资源。</summary>
    public sealed class CaveBackgroundOcclusionGpuTests
    {
        [Test]
        public void ActualBackgroundShaderRejectsCoveredAndUnknownPixelsOnlyWhenEnabled()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Background occlusion fixture"); SceneManager.MoveGameObjectToScene(root, scene);
            var cameraRoot = new GameObject("Background occlusion camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 16; camera.allowHDR = false; camera.allowMSAA = false;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(15.5f, -47.5f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            var wall = new Material(Shader.Find("DarkNights/CaveStrata"));
            var style = ScriptableObject.CreateInstance<CaveBackgroundStyle>(); style.LayerShader = Shader.Find("DarkNights/CaveBackgroundLayer");
            var known = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var clear = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var foreground = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var target = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32);
            var image = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            IDisposable page = null;
            try
            {
                foreground.Create(); target.Create(); camera.targetTexture = target;
                known.SetPixel(0, 0, Color.white); known.Apply(); clear.SetPixel(0, 0, Color.clear); clear.Apply();
                wall.SetTexture("_RockSurface", foreground); wall.SetTexture("_CaveMap", known); wall.SetTexture("_CaveLight", clear);
                var pixels = new byte[256 * 256 * 4];
                for (int i = 0; i < pixels.Length; i += 4)
                { pixels[i] = 80; pixels[i + 1] = 60; pixels[i + 2] = 40; pixels[i + 3] = 255; }
                Type pageType = typeof(CaveVisualSource).Assembly.GetType("DarkNights.View.Terrain.CaveBackgroundPage", true);
                page = (IDisposable)Activator.CreateInstance(pageType, new object[] { 0, 1, new[] { pixels }, wall, root.transform, style, 3 });
                Material background = root.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.That(background.GetFloat("_BackgroundOcclusion"), Is.EqualTo(1));
                Color32 Render(float alpha)
                {
                    RenderTexture.active = foreground; GL.Clear(false, true, new Color(0, 0, 0, alpha));
                    camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                    return image.GetPixel(64, 64);
                }
                bool Sky(Color32 color) => color.r > 240 && color.g < 10 && color.b > 240;
                Color32 revealed = Render(0);
                Assert.That(Sky(revealed), Is.False, "已知且无前景时画出基础后壁");
                Directory.CreateDirectory(CaveEntranceArtValidation.Root);
                File.WriteAllBytes(Path.Combine(CaveEntranceArtValidation.Root, "occlusion-revealed.png"), image.EncodeToPNG());
                Assert.That(Render(127 / 255f), Is.EqualTo(revealed), "阈值以下保持背景");
                Assert.That(Sky(Render(128 / 255f)), Is.True, "阈值以上裁掉被挡背景");
                known.SetPixel(0, 0, Color.clear); known.Apply();
                Assert.That(Sky(Render(0)), Is.True, "未知格不能显示成已揭露背景");
                File.WriteAllBytes(Path.Combine(CaveEntranceArtValidation.Root, "occlusion-unknown.png"), image.EncodeToPNG());
                background.SetFloat("_BackgroundOcclusion", 0);
                Assert.That(Render(0), Is.EqualTo(revealed), "未启用剔除的共享Shader使用者保持原行为");
            }
            finally
            {
                page?.Dispose(); RenderTexture.active = previous; camera.targetTexture = null;
                EditorSceneManager.ClosePreviewScene(scene); foreground.Release(); target.Release();
                foreach (var value in new UnityEngine.Object[] { wall, style, known, clear, foreground, target, image })
                    UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
