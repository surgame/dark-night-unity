using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YY.Features.Players.View;

namespace DarkNights.Tests
{
    /// <summary>通过真实 Addressables 和 Worker Prefab 验证预加载、同步装配、取消和未激活对象释放。</summary>
    public sealed class UnifiedObjectFactoryTests
    {
        [Test]
        public void SceneLoaderBindsTheOriginalPrefabInstanceAcrossSessions()
        {
            const string path = "Assets/DarkNights/Res/Objects/Worker/Worker";
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(path + ".asset");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab"));
            var container = new DIContainer();
            container.Initialize();
            using var first = ObjectSessionContext.CreateReplica(container);
            using var next = ObjectSessionContext.CreateReplica(container);
            var owner = root.GetComponent<ObjectInstance>();
            try
            {
                root.transform.position = new Vector3(17, 3, 0);
                var loader = root.AddComponent<ObjectDefinitionLoader>();
                loader.EditorConfigure(definition);
                Assert.That(loader.BindSession(first), Is.SameAs(owner));
                Assert.That(owner.IsAssembled, Is.True);
                Assert.That(owner.IsActive, Is.False);
                first.Dispose();
                Assert.That(loader.BindSession(next), Is.SameAs(owner));
                Assert.That(owner.SessionContext, Is.SameAs(next));
                Assert.That(root.transform.position, Is.EqualTo(new Vector3(17, 3, 0)));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(root), Is.Not.Null);
            }
            finally
            {
                owner.Release();
                UnityEngine.Object.DestroyImmediate(root);
                first.Dispose();
                next.Dispose();
                container.OnReturnToPool();
            }
        }

        [UnityTest]
        public IEnumerator PreparedWorkerCreatesSynchronouslyAndCancellationCannotActivate()
        {
            yield return UniTask.ToCoroutine(async () =>
            {
                using var loading = new EditorAssetLoading();
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/Worker/Worker.asset");
                var container = new DIContainer();
                container.Initialize();
                using var session = ObjectSessionContext.CreateReplica(container);
                using var preparation = CancellationTokenSource.CreateLinkedTokenSource(session.Lifetime, loading.CancellationToken);
                ObjectView view = null;
                try
                {
                    using var prepared = await ObjectInstanceFactory.PrepareAsync(definition, preparation.Token);
                    view = prepared.Create(new Vector3(1, 2, 0), Quaternion.identity, session);
                    Assert.That(view.Owner.Definition, Is.SameAs(definition));
                    Assert.That(view.Owner.IsAssembled, Is.True);
                    Assert.That(view.Owner.IsActive, Is.False);
                    Assert.That(view.gameObject.activeSelf, Is.False);
                    Assert.That(view.transform.position, Is.EqualTo(new Vector3(1, 2, 0)));
                    session.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => prepared.Create(Vector3.zero, Quaternion.identity, session));
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    bool cancelled = false;
                    try { using var ignored = await ObjectInstanceFactory.PrepareAsync(definition, cancellation.Token); }
                    catch (OperationCanceledException) { cancelled = true; }
                    Assert.That(cancelled, Is.True);
                    view.Owner.Release();
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
                    view = null;
                }
                finally
                {
                    if (view != null) { view.Owner.Release(); UnityEngine.Object.DestroyImmediate(view.gameObject); }
                    container.OnReturnToPool();
                }
            });
        }
    }
}
