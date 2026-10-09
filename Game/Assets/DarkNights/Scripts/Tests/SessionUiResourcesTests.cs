using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Framework;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 通过真实六个UI Prefab验证预加载只取得模板、后续工厂请求可命中同一资源缓存。
    /// 不实例化或修改人工面板；编辑器资源延迟和所有测试租约均在结束时恢复／释放。
    /// </summary>
    public sealed class SessionUiResourcesTests
    {
        [UnityTest]
        public IEnumerator PreloadDoesNotCreateInstancesAndMakesFactoryAssetRequestsReady()
        {
            yield return UniTask.ToCoroutine(async () =>
            {
                using var loading = new EditorAssetLoading();
                int before = LiveInstances();
                using var resources = await SessionUiResources.Prepare(loading.CancellationToken);
                Assert.That(LiveInstances(), Is.EqualTo(before));
                foreach (string name in SessionUiResources.PanelNames)
                {
                    var definition = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("ui." + name.ToLowerInvariant());
                    var handle = Addressables.LoadAssetAsync<GameObject>(definition.PrefabRef.RuntimeKey);
                    try
                    {
                        Assert.That(handle.IsDone, Is.True, name + " asset must be prepared before sequential creation");
                        Assert.That(handle.Result, Is.Not.Null);
                    }
                    finally { if (handle.IsValid()) Addressables.Release(handle); }
                }
                resources.Dispose();
                Assert.That(LiveInstances(), Is.EqualTo(before));
            });
        }

        [UnityTest]
        public IEnumerator CancelledPreloadDoesNotCreateInstances()
        {
            yield return UniTask.ToCoroutine(async () =>
            {
                using var loading = new EditorAssetLoading();
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                int before = LiveInstances();
                bool cancelled = false;
                try { using var ignored = await SessionUiResources.Prepare(cancellation.Token); }
                catch (System.OperationCanceledException) { cancelled = true; }
                Assert.That(cancelled, Is.True);
                Assert.That(LiveInstances(), Is.EqualTo(before));
            });
        }

        private static int LiveInstances() => Resources.FindObjectsOfTypeAll<ObjectInstance>()
            .Count(instance => !EditorUtility.IsPersistent(instance));
    }
}
