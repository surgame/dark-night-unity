using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DarkNights.Tests
{
    /// <summary>
    /// 光场退出的独占资源回归；模拟 Unity 先销毁纹理以及后台任务迟到完成。
    /// 仅使用自建光场与克隆 Shader，验证重复退休及全部原生资源释放，不改作者资源或存档。
    /// </summary>
    public sealed class LightingRetirementTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ExplorationLightField field;
        private ComputeShader shader;
        private RenderTexture light, direction;
        private ComputeBuffer bounce, apertures;

        [SetUp]
        public void Create()
        {
            Assume.That(SystemInfo.supportsComputeShaders);
            var template = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute");
            field = new ExplorationLightField(template, (snapshot, canceled) => Array.Empty<float>());
            shader = Read<ComputeShader>(field, "shader");
            light = Read<RenderTexture>(field, "light");
            direction = Read<RenderTexture>(field, "direction");
            bounce = Read<ComputeBuffer>(field, "bounce");
            apertures = Read<ComputeBuffer>(field, "apertureVisibility");
        }

        [TestCase("light")]
        [TestCase("direction")]
        [TestCase("shader")]
        public void AlreadyDestroyedObjectDoesNotPreventOtherResourcesRetiring(string name)
        {
            Object.DestroyImmediate(Read<Object>(field, name));
            Task retirement = field.RetireAsync();
            Assert.That(retirement.IsCompletedSuccessfully, Is.True, retirement.Exception?.ToString());
            Assert.That(field.RetireAsync(), Is.SameAs(retirement));
            AssertReleased();
        }

        [UnityTest]
        public IEnumerator DelayedWorkerAndDestroyedTexturesStillReleaseBuffersAndShader()
        {
            var worker = new TaskCompletionSource<float[]>();
            var geometry = Read<LightGeometryCache>(field, "geometry");
            typeof(LightGeometryCache).GetField("pending", Private).SetValue(geometry, worker.Task);
            Task retirement = field.RetireAsync();
            Assert.That(retirement.IsCompleted, Is.False);
            Object.DestroyImmediate(light);
            Object.DestroyImmediate(direction);
            worker.SetResult(Array.Empty<float>());
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (!retirement.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(retirement.IsCompletedSuccessfully, Is.True, retirement.Exception?.ToString());
            Assert.That(field.RetireAsync(), Is.SameAs(retirement));
            AssertReleased();
        }

        private void AssertReleased()
        {
            Assert.That(light == null, Is.True);
            Assert.That(direction == null, Is.True);
            Assert.That(shader == null, Is.True);
            Assert.That(bounce.IsValid(), Is.False);
            Assert.That(apertures.IsValid(), Is.False);
        }

        [TearDown]
        public void ReleaseProbeResources()
        {
            if (bounce != null && bounce.IsValid()) bounce.Dispose();
            if (apertures != null && apertures.IsValid()) apertures.Dispose();
            if (light != null) Object.DestroyImmediate(light);
            if (direction != null) Object.DestroyImmediate(direction);
            if (shader != null) Object.DestroyImmediate(shader);
            field = null;
        }

        private static T Read<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, Private).GetValue(owner);
    }
}
