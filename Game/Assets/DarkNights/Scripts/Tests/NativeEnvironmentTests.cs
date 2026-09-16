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
        public void PlatformVisibleTopMatchesAuthoritativeHeightAfterReopen()
        {
            Scene scene = EditorSceneManager.OpenScene(PinewatchLayoutSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var authoring = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelLayoutAuthoring>()).Single();
                var layout = PinewatchLayoutSetup.Validate();
                var platforms = authoring.GetComponentsInChildren<HeroPlatform>();
                Assert.That(platforms.Length, Is.EqualTo(3));
                foreach (var platform in platforms)
                {
                    var definition = layout.Platforms.Single(p => p.Id == platform.Id);
                    var bounds = platform.GetComponentsInChildren<SpriteRenderer>().Single().bounds;
                    // 验证实际可见顶面与运行中角色采用的 Unity 世界坐标，避免数据自洽但图形反向。
                    Assert.That((bounds.max.y - authoring.GroundPoint.y) * EntityView.PixelsPerUnit,
                        Is.EqualTo(definition.Height).Within(0.001), "visible top of platform " + platform.Id);
                    Assert.That(bounds.min.x * EntityView.PixelsPerUnit, Is.EqualTo(definition.MinX).Within(0.001));
                    Assert.That(bounds.max.x * EntityView.PixelsPerUnit, Is.EqualTo(definition.MaxX).Within(0.001));
                    Assert.That(definition.Height, Is.EqualTo(platform.Id * 12).Within(0.001));
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(platform), Is.Not.Null);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void NativeEnvironmentReopensWithFrozenLightsAndGeometry()
        {
            Scene scene = EditorSceneManager.OpenScene(PinewatchLayoutSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var environment = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NativeEnvironment>()).Single();
                var root = environment.transform.root.gameObject;
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
                Shader campSprite = Shader.Find("Dark Nights/Camp Sprite");
                Assert.IsFalse(ShaderUtil.ShaderHasError(campSprite));
                var material = new Material(campSprite);
                try { Assert.That(material.FindPass("CampSpriteUniversal2D"), Is.GreaterThanOrEqualTo(0)); }
                finally { Object.DestroyImmediate(material); }
                int mainTexture = campSprite.FindPropertyIndex("_MainTex");
                Assert.That(campSprite.GetPropertyFlags(mainTexture) &
                    UnityEngine.Rendering.ShaderPropertyFlags.PerRendererData,
                    Is.EqualTo(UnityEngine.Rendering.ShaderPropertyFlags.None));
                Assert.IsNull(root.GetComponentInChildren<DarkNights.Runtime.Network.SessionNetwork>());
                foreach (string name in new[] { "Worker", "Spearman", "Archer", "Zombie", "Ghoul", "Armored" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DarkNights/Res/Objects/" + name + "/" + name + ".prefab");
                    var visual = new SerializedObject(prefab.GetComponent<ActorView>());
                    Assert.NotNull(visual.FindProperty("shadow").objectReferenceValue);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
