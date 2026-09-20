using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>调试角色原生素材及脚底锚点回归；连续换帧、转身和停止必须保留同一个根锚点，不改动权威运动。</summary>
    public sealed class TerrainDebugCharacterTests
    {
        [Test]
        public void AuthoredFramesAnimateAndKeepFeetCenteredInBothDirections()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainDebugSetup.Root + "/DebugFlyer.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var flyer = instance.GetComponent<TerrainDebugFlyer>();
                Assert.That(flyer.IdleFrame, Is.Not.Null);
                Assert.That(flyer.WalkFrames.Length, Is.EqualTo(12));
                instance.transform.position = new Vector3(100, -80, 0);
                flyer.Art.transform.localScale = Vector3.one * 6.25f;
                Sprite previous = null;
                for (int frame = 0; frame < 36; frame++)
                {
                    float direction = frame % 2 == 0 ? 1 : -1;
                    flyer.PresentMovement(direction, true, .09f);
                    Assert.That(flyer.Art.sprite, Is.Not.Null);
                    Assert.That(flyer.Art.sprite, Is.Not.SameAs(previous), "转向不能重置动画");
                    Assert.That(flyer.Art.flipX, Is.EqualTo(direction < 0));
                    AssertFeet(flyer);
                    previous = flyer.Art.sprite;
                }
                flyer.PresentMovement(0, false, .1f);
                Assert.That(flyer.Art.sprite, Is.SameAs(flyer.IdleFrame));
                Assert.That(flyer.Art.flipX, Is.True, "松键保留朝向");
                AssertFeet(flyer);
                Assert.That(instance.transform.position, Is.EqualTo(new Vector3(100, -80, 0)));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void FlightUsesTheSameAnchorAtOriginalPreviewScale()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainDebugSetup.Root + "/DebugFlyer.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var flyer = instance.GetComponent<TerrainDebugFlyer>();
                flyer.Teleport(new Vector2(100, -80));
                flyer.Move(Vector2.left, .1f, false); AssertFeet(flyer);
                flyer.Move(Vector2.right, .1f, false); AssertFeet(flyer);
                Assert.That(flyer.transform.position.x, Is.EqualTo(100).Within(.001f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void AssertFeet(TerrainDebugFlyer flyer)
        {
            Bounds bounds = flyer.Art.bounds;
            Assert.That(bounds.center.x, Is.EqualTo(flyer.transform.position.x).Within(.001f));
            Assert.That(bounds.min.y, Is.EqualTo(flyer.transform.position.y).Within(.001f));
        }
    }
}
