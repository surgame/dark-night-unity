using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.View;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YY.Features.Players.View;

namespace DarkNights.Tests
{
    /// <summary>
    /// 通过正式定义和真实绑定验证本地圈池的几何、容量、对象及网格复用，不伪造网络或工厂。
    /// 显示时间由测试显式推进，清理必须释放全部临时对象；不改写 Prefab 或场景资源。
    /// </summary>
    public sealed class LocalCommandRingsTests
    {
        [UnityTest]
        public IEnumerator NeverShownPoolReleasesEveryPrewarmedMesh()
        {
            var root = new GameObject("Unused local command ring test");
            var pool = new LocalCommandRings();
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                    "Assets/DarkNights/Res/Effects/Command/Command.asset");
                yield return UniTask.ToCoroutine(() => pool.Initialize(definition, root.transform, 320));
                var meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).ToArray();
                Assert.That(meshes.Length, Is.EqualTo(LocalCommandRings.Capacity));
                Assert.That(meshes.All(m => m != null), Is.True);
                Assert.That(pool.PresentationCount, Is.Zero);
                pool.Dispose();
                pool.Dispose();
                Assert.That(root.GetComponentsInChildren<ObjectView>(true), Is.Empty);
                Assert.That(meshes.All(m => m == null), Is.True);
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator BurstsReuseNativeObjectsAndMeshesAndResetLifetime()
        {
            var root = new GameObject("Local command ring test");
            var pool = new LocalCommandRings();
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                    "Assets/DarkNights/Res/Effects/Command/Command.asset");
                yield return UniTask.ToCoroutine(() => pool.Initialize(definition, root.transform, 320));
                var owners = root.GetComponentsInChildren<ObjectView>(true);
                var meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).ToArray();
                Assert.That(pool.InstanceCount, Is.EqualTo(LocalCommandRings.Capacity));
                Assert.That(owners.Length, Is.EqualTo(LocalCommandRings.Capacity));
                Assert.That(pool.Count, Is.Zero);
                Assert.That(owners.All(o => !o.gameObject.activeSelf && !o.Owner.Definition.isNetwork), Is.True);

                pool.Show(200, 10);
                pool.Tick(10.4);
                var active = owners.Single(o => o.gameObject.activeSelf);
                Assert.That(active.transform.position, Is.EqualTo(new Vector3(2, 0, 0)));
                var mesh = active.Get<NativeEffect>("effect").GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.colors[0].a, Is.EqualTo(.5f).Within(.001f));
                Assert.That(mesh.bounds.size.x, Is.InRange(.187f, .191f));
                for (int i = 0; i < 64; i++) pool.Show(400 + i, 10.5);
                Assert.That(pool.Count, Is.EqualTo(LocalCommandRings.Capacity));
                CollectionAssert.AreEquivalent(owners, root.GetComponentsInChildren<ObjectView>(true));
                CollectionAssert.AreEquivalent(meshes, root.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh));

                pool.Tick(11.31);
                Assert.That(pool.Count, Is.Zero);
                Assert.That(owners.All(o => !o.gameObject.activeSelf), Is.True);
                pool.Show(600, 12);
                Assert.That(pool.Count, Is.EqualTo(1));
                pool.Clear();
                Assert.That(pool.Count, Is.Zero);
                Assert.That(owners.All(o => !o.gameObject.activeSelf), Is.True);
                pool.Show(700, 13);
                active = owners.Single(o => o.gameObject.activeSelf);
                Assert.That(active.transform.position.x, Is.EqualTo(7));
                Assert.That(active.Get<NativeEffect>("effect").GetComponentInChildren<MeshFilter>().sharedMesh.colors[0].a, Is.EqualTo(1));
                pool.Dispose();
                Assert.That(pool.InstanceCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<ObjectView>(true), Is.Empty);
                Assert.That(meshes.All(m => m == null), Is.True);
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
