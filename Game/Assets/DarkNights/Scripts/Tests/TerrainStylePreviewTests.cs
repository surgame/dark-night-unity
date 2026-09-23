using System.Linq;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>编辑态预览只读回归；直接从固定地图和共享配置捕获参数，确认不同背景选择改变画面而不写回源资产。</summary>
    public sealed class TerrainStylePreviewTests
    {
        [Test]
        public void FixedMapPreviewUsesCapturedAssetsWithoutChangingSource()
        {
            const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            var map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(root + "ReferenceChamber.asset");
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(root + "Style.asset");
            Assert.That(map, Is.Not.Null); Assert.That(style, Is.Not.Null);
            var original = map.InitialCells.bytes.ToArray(); string identity = style.VisualIdentity;
            var blueprint = map.ReadBlueprint(); var background = style.Background;
            byte[] materials = blueprint.CopyMaterials(), shapes = blueprint.CopyShapes();
            byte[] Bake(bool show) => TerrainStylePreviewBaker.Bake(materials, shapes, blueprint.Settings.Seed,
                316, 284, style.StoneSize, style.CaptureOutline(), style.CaptureModifiers(),
                background.CaptureGenerator(), background.CaptureModifiers(),
                new[] { show && background.Near, show && background.Middle, show && background.Deep },
                background.MiddleSoftness);
            byte[] withBackground = Bake(true), withoutBackground = Bake(false);
            Assert.That(withBackground.Length, Is.EqualTo(TerrainStylePreviewBaker.Width * TerrainStylePreviewBaker.Height * 4));
            Assert.That(withBackground.Where((value, index) => index % 4 == 3).All(a => a == 255), Is.True);
            Assert.That(withBackground.SequenceEqual(withoutBackground), Is.False);
            CollectionAssert.AreEqual(original, map.InitialCells.bytes);
            Assert.That(style.VisualIdentity, Is.EqualTo(identity));
        }
    }
}
