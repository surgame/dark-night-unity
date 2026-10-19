using System;
using System.Collections;
using System.Reflection;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>独立背景宿主的分页回归；验证基础先就绪、隐藏装饰按需、挖填立即显露与静止复用，不开启玩法会话。</summary>
    public sealed class CaveBackgroundStreamingTests
    {
        private static readonly Assembly ViewAssembly = typeof(CaveVisualSource).Assembly;
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static T Read<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);

        [UnityTest]
        public IEnumerator OpaquePagePreparesOnlyBaseAndDigReusesItBeforeDecorations()
        {
            var root = new GameObject("Background streaming fixture");
            var wall = new Material(Shader.Find("DarkNights/CaveStrata"));
            var style = ScriptableObject.CreateInstance<CaveBackgroundStyle>();
            style.LayerShader = Shader.Find("DarkNights/CaveBackgroundLayer");
            object cache = null;
            try
            {
                Type coverageType = ViewAssembly.GetType("DarkNights.View.Terrain.CaveForegroundCoverage", true);
                object coverage = Activator.CreateInstance(coverageType);
                var opaque = new byte[256 * 256 * 4];
                for (int i = 3; i < opaque.Length; i += 4) opaque[i] = 255;
                Invoke(coverage, "Commit", new RectInt(0, 0, 256, 256), opaque);
                var cells = new byte[320 * 192];
                for (int i = 0; i < cells.Length; i++) cells[i] = 1;
                var reference = new BackgroundBakeDescriptor("cf7a124d6c434c8fa970a397cbe83a21", "OPAQUE-BACKGROUND-PAGE", cells, new byte[cells.Length]);
                Type cacheType = ViewAssembly.GetType("DarkNights.View.Terrain.CaveBackgroundCache", true);
                cache = Activator.CreateInstance(cacheType, new object[] { reference, style, wall, root.transform,
                    null, 43, true, default(GridBounds), coverage, new Func<int, bool>(_ => true) });
                Assert.That((bool)Invoke(cache, "CanPresent", 0), Is.False, "基础未备时不得提交前景页");
                Invoke(cache, "SetVisible", new GridBounds(0, -31, 32, 32));
                yield return Wait(cache, () => Read<bool>(cache, "Ready"));
                Assert.That(Read<int>(cache, "BaseBuildCount"), Is.EqualTo(1));
                Assert.That(Read<int>(cache, "DecorationBuildCount"), Is.Zero, "整页被挡时不生成装饰");
                Assert.That(Read<long>(cache, "ResidentBytes"), Is.EqualTo(256 * 256 * 4));
                Assert.That(Read<int>(cache, "OccludedPages"), Is.EqualTo(1));
                Assert.That(root.transform.GetChild(0).gameObject.activeSelf, Is.False);

                Invoke(coverage, "Commit", new RectInt(1, 1, 1, 1), new byte[4]);
                Invoke(cache, "UpdateOcclusion");
                Assert.That((bool)Invoke(cache, "CanPresent", 0), Is.True, "拆墙不用再准备基础");
                Assert.That(root.transform.GetChild(0).gameObject.activeSelf, Is.True, "拆墙当帧立即打开已备基础");
                Assert.That(Read<int>(cache, "DecorationBuildCount"), Is.Zero);
                yield return Wait(cache, () => Read<bool>(cache, "Ready"));
                Assert.That(Read<int>(cache, "BaseBuildCount"), Is.EqualTo(1));
                Assert.That(Read<int>(cache, "DecorationBuildCount"), Is.EqualTo(1));
                Assert.That(Read<long>(cache, "ResidentBytes"), Is.EqualTo(256 * 256 * 4), "原实心区的全透明装饰不分配纹理");
                long bytes = Read<long>(cache, "UploadedBytes");
                Invoke(coverage, "Commit", new RectInt(1, 1, 1, 1), new byte[] { 0, 0, 0, 255 });
                Invoke(cache, "UpdateOcclusion");
                Assert.That(root.transform.GetChild(0).gameObject.activeSelf, Is.False, "填岩后整页停止绘制");
                Invoke(coverage, "Commit", new RectInt(1, 1, 1, 1), new byte[4]);
                Invoke(cache, "UpdateOcclusion");
                for (int i = 0; i < 5; i++) { Invoke(cache, "Tick"); yield return null; }
                Assert.That(Read<int>(cache, "DecorationBuildCount"), Is.EqualTo(1));
                Assert.That(Read<long>(cache, "UploadedBytes"), Is.EqualTo(bytes), "重复挖填及静止不重传背景");
            }
            finally
            {
                (cache as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(style);
            }
        }

        private static IEnumerator Wait(object cache, Func<bool> completed)
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            do
            {
                Invoke(cache, "Tick");
                if (completed()) yield break;
                yield return null;
            } while (EditorApplication.timeSinceStartup < deadline);
            Assert.Fail("背景页未在有界等待内就绪");
        }
    }
}
