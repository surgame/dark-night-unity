using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>现有三层背景的入口合同；原 RGBA 不变、窄井及宽谷覆盖、跨页一致性和冻结参考均不启动玩法。</summary>
    public sealed class CaveEntranceArtTests
    {
        internal static BackgroundBakeDescriptor Reference()
        {
            var cells = new byte[320 * 192];
            for (int x = 0; x < 320; x++)
                cells[(x >= 154 && x < 166 ? 120 : x >= 40 && x < 110 ? 44 : 40) * 320 + x] = 1;
            return new BackgroundBakeDescriptor("cf7a124d6c434c8fa970a397cbe83a21", "SURFACE-ENTRANCE-EXISTING", cells, new byte[cells.Length]);
        }
        /// <summary>只读测试布局；三槽仍由原 BackgroundPageBaker 着色，不参与正式背景生成或碰撞。</summary>
        internal sealed class Layout : ICaveBackgroundLayout
        {
            public int Width => 2560;
            public int Height => 1536;
            public int Top => 320;
            public uint LayoutSeed => 7;
            public bool Solid(int layer, int x, int y) => y > 380 && (x / 16 + layer) % 3 == 0;
        }
        [Test]
        public void ExistingMasksAndThreeLayerConfigurationRemainBoundToFormalStyles()
        {
            var first = AssetDatabase.LoadAssetAtPath<CaveBackgroundStyle>("Assets/DarkNights/Res/Terrain/StrataCave/Background.asset");
            var second = AssetDatabase.LoadAssetAtPath<CaveBackgroundStyle>("Assets/DarkNights/Res/Terrain/CaveContourStatic/Background.asset");
            Assert.That(first != null && second != null, Is.True, "两套正式背景配置必须存在");
            AssertSharedAsset(first.LayerShader, second.LayerShader,
                "Assets/DarkNights/Res/Art/Custom/CaveEntranceLayers/CaveBackgroundLayer.shader");
            AssertSharedAsset(first.SourceMaskAtlas, second.SourceMaskAtlas,
                "Assets/DarkNights/Res/Art/Custom/CaveContourStatic/background-mask-v16.png");
            Assert.That(first.Near && first.Middle && first.Deep, Is.True);
            Assert.That(first.FarOrder, Is.LessThan(first.DeepOrder)); Assert.That(first.NearOrder, Is.LessThan(-1));
        }

        private static void AssertSharedAsset(UnityEngine.Object first, UnityEngine.Object second, string expectedPath)
        {
            // Unity 可为同一原生资产返回不同托管包装；绑定合同按持久化 GUID 和子资产身份验证。
            Assert.That(first != null && second != null, Is.True, expectedPath);
            Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo(expectedPath));
            Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(expectedPath));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string firstGuid, out long firstId), Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(second, out string secondGuid, out long secondId), Is.True);
            Assert.That(firstGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(expectedPath)));
            Assert.That(secondGuid, Is.EqualTo(firstGuid));
            Assert.That(secondId, Is.EqualTo(firstId));
        }
        [Test]
        public void OriginalThreeLayersAreByteIdenticalAndShaftHasAnOpaqueRearWall()
        {
            var reference = Reference(); var original = reference.CopyMaterials(); var layout = new Layout();
            var page = new CaveEntranceBackdrop(reference, layout).Bake(1280, 256, 256, 512);
            var previous = BackgroundPageBaker.Bake(layout, 1280, 256, 256, 512);
            for (int i = 0; i < 3; i++) CollectionAssert.AreEqual(previous[i], page[i], "原三层形状、RGB、Alpha 均保持");
            byte Alpha(int x, int y) => page[3][((y - 256) * 256 + x - 1280) * 4 + 3];
            Assert.That(Alpha(1280, 38 * 8), Is.Zero); Assert.That(Alpha(1280, 65 * 8), Is.EqualTo(255));
            Assert.That(page[3].Where((v, i) => i % 4 == 3).All(a => a == 0 || a == 255), Is.True,
                "基础墙只有原掩码轮廓覆盖，没有入口深度渐隐");
            CollectionAssert.AreEqual(original, reference.CopyMaterials());
        }
        [Test]
        public void WideValleyKeepsSkyAndEmptyMapsDoNotInventUndergroundWalls()
        {
            var page = new CaveEntranceBackdrop(Reference(), new Layout()).Bake(512, 256, 256, 256);
            Assert.That(page[3][((42 * 8 - 256) * 256 + 80 * 8 - 512) * 4 + 3], Is.Zero);
            var empty = new BackgroundBakeDescriptor("cf7a124d6c434c8fa970a397cbe83a21", "EMPTY-EXISTING", new byte[320 * 192], new byte[320 * 192]);
            Assert.That(new CaveEntranceBackdrop(empty, new Layout()).Bake(1280, 512, 256, 256).SelectMany(p => p).All(v => v == 0), Is.True);
        }
        [Test]
        public void PagingDoesNotChangeExistingLayersAtEitherAxisOrWorldBoundary()
        {
            var background = new CaveEntranceBackdrop(Reference(), new Layout());
            byte[][] whole = background.Bake(1024, 256, 512, 512);
            for (int py = 0; py < 2; py++) for (int px = 0; px < 2; px++)
            {
                byte[][] page = background.Bake(1024 + px * 256, 256 + py * 256, 256, 256);
                for (int layer = 0; layer < 4; layer++) for (int y = 0; y < 256; y++)
                    CollectionAssert.AreEqual(new ArraySegment<byte>(whole[layer], ((py * 256 + y) * 512 + px * 256) * 4, 1024),
                        new ArraySegment<byte>(page[layer], y * 1024, 1024));
            }
            Assert.That(background.Bake(2304, 1280, 256, 256)[3].Where((v, i) => i % 4 == 3).All(a => a == 255), Is.True);
        }
        [Test]
        public void BackdropIsFrozenWhenCurrentTerrainChanges()
        {
            var reference = Reference(); var mutable = reference.CopyMaterials();
            var background = new CaveEntranceBackdrop(reference, new Layout());
            byte[][] before = background.Bake(1280, 256, 256, 512); Array.Clear(mutable, 0, mutable.Length);
            byte[][] after = background.Bake(1280, 256, 256, 512);
            for (int i = 0; i < 4; i++) CollectionAssert.AreEqual(before[i], after[i]);
            Assert.Throws<ArgumentException>(() => background.Bake(2500, 0, 256, 256));
        }
    }
}
