using System;
using System.Linq;
using DarkNights.Editor;
using DarkNights.View;
using GameCore.Objects.Types;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Tests
{
    /// <summary>
    /// 核验正式 Pinewatch 对象直接位于三个制作分组，并由 sibling 顺序形成冻结布局。
    /// 测试只读已保存场景，不重建 Prefab、不生成身份，也不改变人工制作内容。
    /// </summary>
    public sealed class ScenePlacementTests
    {
        [Test]
        public void PinewatchUsesDirectFormalInstancesAndAutomaticIdentities()
        {
            Scene scene = SceneManager.GetSceneByPath(PinewatchLayoutSetup.ScenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(PinewatchLayoutSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                LevelLayoutAuthoring layout = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LevelLayoutAuthoring>(true)).Single();
                AssertGroup(layout.PlacementGroup(ObjectType.Placeable_CompositeStructure), 4);
                AssertGroup(layout.PlacementGroup(ObjectType.Scenery_ResourceNode), 5);
                AssertGroup(layout.PlacementGroup(ObjectType.Unit), 7);
                ScenePlacement[] placements = layout.GetComponentsInChildren<ScenePlacement>(true);
                Assert.That(placements.Select(value => value.PlacementKey).Distinct().Count(), Is.EqualTo(16));
                Assert.That(placements.All(value => Guid.TryParseExact(value.PlacementKey, "N", out _)), Is.True);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AssertGroup(Transform group, int expected)
        {
            Assert.That(group.childCount, Is.EqualTo(expected));
            for (int index = 0; index < group.childCount; index++)
            {
                GameObject child = group.GetChild(index).gameObject;
                ScenePlacement placement = child.GetComponent<ScenePlacement>();
                Assert.That(placement, Is.Not.Null, child.name);
                Assert.That(PrefabUtility.GetNearestPrefabInstanceRoot(child), Is.EqualTo(child), child.name);
                Assert.That(child.name, Is.Not.EqualTo("VisualPreview"));
                SceneDefinitionAuthoring.ValidatePlacement(placement);
            }
        }
    }
}
