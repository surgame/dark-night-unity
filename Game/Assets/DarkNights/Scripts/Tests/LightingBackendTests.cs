using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Lighting;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkNights.Tests
{
    /// <summary>有限灯口与后端回退合同；核对截面、未知区遮挡、原生模板与纹理释放，不代替正式场景和性能验收。</summary>
    public sealed class LightingBackendTests
    {
        [Test]
        public void ApertureLightsNearEdgeWithoutIlluminatingBehindMouth()
        {
            Assert.That(LightBeamProfile.Evaluate(.02f, .15f, 14, 90, .375f, .045f, true), Is.GreaterThan(.9f));
            Assert.That(LightBeamProfile.Evaluate(.02f, .15f, 14, 90, 0, .045f, true), Is.Zero);
            Assert.That(LightBeamProfile.Evaluate(-.01f, 0, 14, 90, .375f, .045f, true), Is.Zero);
            Assert.That(LightBeamProfile.Evaluate(14, 0, 14, 90, .375f, .045f, true), Is.Zero);
        }

        [Test]
        public void AperturePreservesSymmetryAndCircularRange()
        {
            Assert.That(LightBeamProfile.Evaluate(2, 1, 14, 90, .375f, .045f, true),
                Is.EqualTo(LightBeamProfile.Evaluate(2, -1, 14, 90, .375f, .045f, true)));
            Assert.That(LightBeamProfile.Evaluate(13, 6, 14, 90, 2, .045f, true), Is.Zero);
            Assert.That(LightBeamProfile.Evaluate(-2, 0, 14, 90, .375f, .045f, false), Is.GreaterThan(0));
            Assert.Throws<ArgumentException>(() => new LightEmissionRules(14, 90, 1, 2, 0, 1, 1, 1, float.NaN));
            Assert.Throws<ArgumentException>(() => new LightEmissionRules(14, 90, 1, 2, 0, 1, 1, 1, -.1f));
            Assert.That(EnvironmentLighting.InitialBackend(new[] { "--dn-lighting-urp" }), Is.EqualTo(LightingBackendKind.Urp2D));
            Assert.That(EnvironmentLighting.InitialBackend(new[] { "--dn-lighting-urp", "--dn-lighting-private" }), Is.EqualTo(LightingBackendKind.PrivateField));
        }

        [Test]
        public void ShadowRowsMergeSolidRunsAndKeepUnknownOpaque()
        {
            var merged = UrpTerrainShadowPaths.Build(new RectInt(0, 0, 16, 1), (u, row) => 192);
            Assert.That(merged.Count, Is.EqualTo(1), "整行不能生成每格一个遮挡对象或路径。");
            Assert.That(merged[0][1].x - merged[0][0].x, Is.EqualTo(16));
            Assert.That(UrpTerrainShadowPaths.Build(new RectInt(0, 0, 1, 1), (u, row) => 0).Count, Is.EqualTo(1));
            Assert.That(UrpTerrainShadowPaths.Build(new RectInt(0, 0, 1, 1), (u, row) => 128), Is.Empty);
        }

        [Test]
        public void NativeTemplatesAreBoundAndUseNormalMapsWithoutGameplayCollisions()
        {
            Editor.Lighting.LightingBackendContentSetup.Validate();
            var preset = AssetDatabase.LoadAssetAtPath<LightProfile>(Editor.Lighting.LightProfileMigration.FlashlightPath);
            var scene = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>(Editor.Lighting.LightProfileMigration.ScenePath);
            Assert.That(preset.Freeze().Rules.ApertureWidth, Is.EqualTo(.375f));
            Assert.That(scene.UrpLightTemplate.normalMapQuality, Is.EqualTo(Light2D.NormalMapQuality.Accurate));
            Assert.That(scene.UrpLightTemplate.normalMapDistance, Is.EqualTo(3));
            var data = new SerializedObject(scene.UrpShadowTemplate);
            Assert.That(data.FindProperty("m_ShadowShape2DProvider").managedReferenceValue, Is.Not.Null,
                "Player 不会自动选择 provider，模板必须已经持久化 Collider 来源。");
        }

        [Test]
        public void NativeCookieUpdatesOnlyForShapeChangesAndReleasesItsSprite()
        {
            var cookie = new UrpLightCookie(LightBeamProfile.Evaluate); var settings = new ExplorationLightSettings();
            var rules = new LightEmissionRules(14, 90, 1, 2, 0, 1, 1, 1, .375f);
            try
            {
                cookie.Update(new LightEmitterData(Vector3.zero, 0, rules), settings);
                var first = cookie.Sprite;
                cookie.Update(new LightEmitterData(Vector3.one, 70, rules), settings);
                Assert.That(cookie.Sprite, Is.SameAs(first), "移动和转向不能重建形状纹理。");
                Assert.That(first.texture.GetPixel(120, 128).r, Is.Zero, "灯口背后不能发直射光。");
                Assert.That(first.texture.GetPixel(130, 128).r, Is.GreaterThan(0));
                var wider = new LightEmissionRules(14, 90, 1, 2, 0, 1, 1, 1, .75f);
                cookie.Update(new LightEmitterData(Vector3.zero, 0, wider), settings);
                Assert.That(cookie.Sprite, Is.Not.SameAs(first));
            }
            finally { cookie.Dispose(); }
            Assert.That(cookie.Sprite, Is.Null);
        }
    }
}
