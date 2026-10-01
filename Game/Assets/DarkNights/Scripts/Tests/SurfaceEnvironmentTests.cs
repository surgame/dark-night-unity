using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Expedition;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>地表环境的增量与生命周期检查；背景参考固定，当前露天列可局部刷新，远景确定性及配置冻结均不访问业务状态。</summary>
    public sealed class SurfaceEnvironmentTests
    {
        [Test]
        public void DigAndFillOnlyRebuildTouchedColumnAndKeepReferenceDepth()
        {
            var materials = new byte[320 * 192]; var shapes = new byte[materials.Length];
            for (int x = 0; x < 320; x++)
            {
                materials[40 * 320 + x] = 1; materials[44 * 320 + x] = 1;
            }
            var reference = new BackgroundBakeDescriptor("493c74157caf446d902aa82f0a49fdea", "SURFACE-INCREMENTAL", materials, shapes);
            var material = new Material(Shader.Find("DarkNights/CaveStrata"));
            using var skyline = new CaveSurfaceSkyline(reference, material, new SurfaceEnvironmentSettings());
            try
            {
                var cells = new Color32[materials.Length];
                Color32[] before = skyline.Texture.GetPixels32();
                cells[40 * 320 + 20] = new Color32(0, 0, 0, 255);
                skyline.Mark(20); skyline.Mark(20); skyline.Flush(cells);
                var after = skyline.Texture.GetPixels32();
                Assert.That(skyline.RebuiltColumns, Is.EqualTo(1));
                Assert.That(after[20 * 8].r + after[20 * 8].g * 256, Is.EqualTo(44 * 8));
                Assert.That(after[20 * 8].b + after[20 * 8].a * 256, Is.EqualTo(40 * 8));
                for (int x = 0; x < before.Length; x++)
                    if (x / 8 != 20) Assert.That(after[x], Is.EqualTo(before[x]), "未变化列保持原纹理字节");
                skyline.Flush(cells); Assert.That(skyline.RebuiltColumns, Is.EqualTo(1));
                cells[40 * 320 + 20] = new Color32(1, 0, 0, 255);
                skyline.Mark(20); skyline.Flush(cells);
                CollectionAssert.AreEqual(before, skyline.Texture.GetPixels32());
                CollectionAssert.AreEqual(materials, reference.CopyMaterials());
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void SurfaceRockRemovedBeforeFirstBaselineDoesNotRemainInFrozenMask()
        {
            var cells = new byte[320 * 192]; cells[40 * 320 + 20] = 1;
            var reference = new BackgroundBakeDescriptor("493c74157caf446d902aa82f0a49fdea", "REMOVED-BEFORE-JOIN", cells, new byte[cells.Length]);
            var material = new Material(Shader.Find("DarkNights/CaveStrata"));
            using var skyline = new CaveSurfaceSkyline(reference, material, new SurfaceEnvironmentSettings());
            try
            {
                var current = new Color32[cells.Length]; current[40 * 320 + 20] = new Color32(0, 0, 0, 255);
                skyline.Mark(20); skyline.Flush(current);
                Color32 pixel = skyline.Texture.GetPixels32()[20 * 8];
                Assert.That(pixel.r + pixel.g * 256, Is.EqualTo(192 * 8));
                Assert.That(pixel.b + pixel.a * 256, Is.EqualTo(40 * 8));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void SceneryIsDeterministicQuantizedAndIndependentOfGlobalRandom()
        {
            var root = new GameObject("surface scenery fixture");
            var material = new Material(Shader.Find("Sprites/Default"));
            var camera = root.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 2;
            camera.aspect = 16f / 9; camera.transform.position = new Vector3(5, 0, -10);
            using var backdrop = new JourneySurfaceBackdrop(root.transform, material, new SurfaceEnvironmentSettings());
            try
            {
                // 正式宿主没有相机父变换；夹具相机单独放置后也使用相同世界坐标几何。
                var planet = new PlanetDefinition("scenery", "远景");
                var random = UnityEngine.Random.state;
                backdrop.Render(camera, planet, new Color(.04f, .08f, .12f, 1));
                var first = backdrop.Mesh.vertices;
                Assert.That(first.Length, Is.GreaterThan(0));
                backdrop.Render(camera, planet, new Color(.04f, .08f, .12f, 1));
                CollectionAssert.AreEqual(first, backdrop.Mesh.vertices);
                Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
                Assert.That(first.All(v => Math.Abs(v.y / .02f - Mathf.Round(v.y / .02f)) < .001f), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CaptureFreezesSettingsAndDisablingDecorationsKeepsSkylineFix()
        {
            var source = new SurfaceEnvironmentSettings { Enabled = false, WeatheredDepth = float.NaN, EntranceDepth = -1 };
            var frozen = source.Capture(); source.RidgeHeight = 16;
            Assert.That(frozen.RidgeHeight, Is.EqualTo(7)); Assert.That(frozen.WeatheredDepth, Is.EqualTo(1.25f));
            Assert.That(frozen.EntranceDepth, Is.EqualTo(2));
            var reference = new BackgroundBakeDescriptor("493c74157caf446d902aa82f0a49fdea", "DISABLED-DECORATIONS", new byte[320 * 192], new byte[320 * 192]);
            var material = new Material(Shader.Find("DarkNights/CaveStrata"));
            using var skyline = new CaveSurfaceSkyline(reference, material, frozen);
            try
            {
                Assert.That(material.GetFloat("_SurfaceSky"), Is.EqualTo(1));
                Assert.That(material.GetVector("_SurfaceSettings").x, Is.Zero);
                Assert.That(material.GetTexture("_SurfaceSkyline"), Is.SameAs(skyline.Texture));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
