using System.Linq;
using DarkNights.Editor;
using DarkNights.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Tests
{
    /// <summary>
    /// 重开灰松谷检查原生环境数量、冻结位置和引用，采样不启动会话或推进玩法随机数。
    /// 保存来源仍是场景与 Prefab；测试只操作临时加载副本并关闭，不反向生成布局。
    /// </summary>
    public sealed class NativeEnvironmentTests
    {
        [Test]
        public void NativeEnvironmentReopensWithFrozenLightsAndGeometry()
        {
            Scene scene = EditorSceneManager.OpenScene(PinewatchLayoutSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single();
                var environment = root.GetComponentInChildren<NativeEnvironment>();
                Assert.NotNull(environment);
                Assert.AreEqual(4, environment.GetComponentsInChildren<CampTorch>().Length);
                var lights = environment.GetComponentsInChildren<CampLight>();
                Assert.AreEqual(7, lights.Length);
                CollectionAssert.AreEquivalent(new[] { 94f, 155, 302, 365, 630, 745, 820 },
                    lights.Select(l => Mathf.Round(l.transform.position.x * 100)).ToArray());
                var data = new SerializedObject(environment);
                Assert.AreEqual(15, data.FindProperty("fireflies").arraySize);
                Assert.AreEqual(9, data.FindProperty("staticSprites").arraySize);
                var terrain = AssetDatabase.LoadAssetAtPath<Mesh>(NativeEnvironmentSetup.Root + "/Terrain.asset");
                Assert.AreEqual(190 * 4, terrain.vertexCount);
                environment.Present(0, 1, 500, Color.white);
                var moon = (Transform)data.FindProperty("moon").objectReferenceValue;
                Assert.That(moon.position.x, Is.EqualTo(6.38).Within(0.00001));
                Assert.That(moon.position.y, Is.EqualTo(1.55).Within(0.00001));
                Assert.That(lights[0].ColorAt(1, 0), Is.EqualTo(Vector4.zero));
                Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find("Dark Nights/Camp Sprite")));
                Assert.IsNull(root.GetComponentInChildren<DarkNights.Runtime.Network.SessionNetwork>());
                foreach (string name in new[] { "Worker", "Spearman", "Archer", "Zombie", "Ghoul", "Armored" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DarkNights/Res/Objects/" + name + "/" + name + ".prefab");
                    var visual = new SerializedObject(prefab.GetComponent<NativeVisual>());
                    Assert.NotNull(visual.FindProperty("shadow").objectReferenceValue);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
