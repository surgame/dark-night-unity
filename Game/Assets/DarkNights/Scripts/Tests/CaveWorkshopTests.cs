using System;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Config;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>洞穴坡形的权威运动、格编码、AMP1 副本、破坏及像素素材合同回归；采用实际地图会话，视觉验收另外保存 Play 截图。</summary>
    public sealed class CaveWorkshopTests
    {
        private static CaveWorkshopSession Session(TerrainBlueprint map)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(CaveTerrainAssets.DefinitionPath).LoadGameplayCatalog();
            var game = GameCatalogJson.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/balance.json").text,
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/pinewatch.json").text);
            return new CaveWorkshopSession(map, catalog, game);
        }
        private static TerrainBlueprint Ramp()
        {
            var cells = new byte[320 * 192]; var shapes = new byte[cells.Length];
            for (int y = 100; y < 192; y++) for (int x = 0; x < 320; x++) cells[y * 320 + x] = 2;
            for (int x = 100; x < 160; x++) cells[99 * 320 + x] = 2;
            shapes[99 * 320 + 100] = 3; shapes[99 * 320 + 101] = 4;
            cells[99 * 320 + 160] = 2; shapes[99 * 320 + 160] = 2;
            return new TerrainBlueprint(new TerrainGenerationSettings(), cells, new bool[cells.Length], new int[320],
                new[] { new TerrainRoom("ramp", 97, 95, 20, 10) }, new bool[cells.Length], Array.Empty<TerrainDepositBlueprint>(), shapes: shapes);
        }
        [Test]
        public void GentleRampAscendsDescendsAndJumpLandsWithoutPenetration()
        {
            using var session = Session(Ramp());
            for (int i = 0; i < 90; i++) session.Tick(0, false, false, 1f / 60);
            Assert.That(session.Y, Is.EqualTo(-99.5).Within(.01));
            for (int i = 0; i < 300; i++) session.Tick(1, false, false, 1f / 60);
            Assert.That(session.X, Is.GreaterThan(104)); Assert.That(session.Y, Is.EqualTo(-98.5).Within(.01));
            session.Tick(0, true, true, 1f / 60); Assert.That(session.Y, Is.GreaterThan(-98.5));
            for (int i = 0; i < 240; i++) session.Tick(0, false, false, 1f / 60);
            Assert.That(session.Y, Is.EqualTo(-98.5).Within(.01));
            for (int i = 0; i < 300; i++) session.Tick(-1, false, false, 1f / 60);
            Assert.That(session.X, Is.LessThan(99)); Assert.That(session.Y, Is.EqualTo(-99.5).Within(.01));
        }
        [Test]
        public void ShapesRoundTripAndCeilingsOccupyOppositeHalf()
        {
            for (int s = 0; s <= 12; s++)
            {
                var shape = (TerrainCellShape)s; var cell = new GridCell(2, 0, TerrainShapeGeometry.Encode(shape, false));
                byte[] bytes = new byte[8]; GridCellEncoding.WriteLittleEndian(cell, bytes);
                Assert.That(GridCellEncoding.ReadLittleEndian(bytes), Is.EqualTo(cell));
                Assert.That(TerrainShapeGeometry.Decode(cell.Flags), Is.EqualTo(shape));
                if (s == 0) continue;
                float edge = TerrainShapeGeometry.Edge(shape, .4f);
                Assert.That(TerrainShapeGeometry.Contains(shape, .4f, edge - .01f), Is.EqualTo(s < 7));
                Assert.That(TerrainShapeGeometry.Contains(shape, .4f, edge + .01f), Is.EqualTo(s >= 7));
            }
        }
        [Test]
        public void InitialAndLateReplicasKeepSlopesAndDestroyedState()
        {
            using var session = Session(Ramp()); var map = session.Map;
            var catalog = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(CaveTerrainAssets.DefinitionPath).LoadGameplayCatalog();
            string visual = new string('b', 64);
            var hello = TerrainMapNetworking.Handshake(map, catalog, visual);
            var stream = TerrainMapNetworking.OpenStream(map, hello, 1, _ => true, () => 1);
            var replica = TerrainMapNetworking.CreateReplica(catalog, visual);
            stream.Subscribe(map.Descriptor.Bounds);
            while (stream.QueuedPackets > 0) replica.Receive(stream.Dequeue());
            var slope = new CellCoord(100, -99);
            Assert.That(replica.Read(slope).Cell.Flags, Is.EqualTo(6));
            session.Teleport(100, -97); Assert.That(session.Edit(100, -99, true), Is.True);
            var late = TerrainMapNetworking.CreateReplica(catalog, visual);
            var lateStream = TerrainMapNetworking.OpenStream(map, hello, 2, _ => true, () => 1);
            lateStream.Subscribe(map.Descriptor.Bounds);
            while (lateStream.QueuedPackets > 0) late.Receive(lateStream.Dequeue());
            Assert.That(late.Read(slope).Cell.IsEmpty, Is.True);
            Assert.That(late.Read(new CellCoord(103,-99)).Cell, Is.EqualTo(map.Read(new CellCoord(103,-99)).Cell));
        }
        [Test]
        public void BakedMapRetainsShapesAndRejectsUnknownShape()
        {
            var source = Ramp(); var bytes = new byte[320 * 192 * 2];
            for (int y = 0; y < 192; y++) for (int x = 0; x < 320; x++)
            { int i = (y * 320 + x) * 2; bytes[i] = source.MaterialAt(x,y); bytes[i+1] = (byte)source.CellFlagsAt(x,y); }
            var map = ScriptableObject.CreateInstance<DarkNights.View.Terrain.TerrainMapAsset>();
            var text = new TextAsset(System.Text.Encoding.UTF8.GetString(bytes));
            try
            {
                map.Definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(CaveTerrainAssets.DefinitionPath);
                map.InitialCells = text; map.CellFormat = 2;
                CollectionAssert.AreEqual(source.CopyShapes(), map.ReadBlueprint().CopyShapes());
                map.CellFormat = 1; Assert.Throws<FormatException>(() => map.ReadBlueprint());
            }
            finally { UnityEngine.Object.DestroyImmediate(text); UnityEngine.Object.DestroyImmediate(map); }
        }
        [Test]
        public void CaveSeedsProduceSlopeDiversityAndFrozenShapeArrays()
        {
            for (int n = 0; n < 100; n++)
            {
                var map = CaveExplorationGenerator.Generate(new TerrainGenerationSettings { Seed = "CAVE-V2-" + n });
                var shapes = map.CopyShapes(); Assert.That(shapes.Count(s => s > 0), Is.GreaterThan(100));
                Assert.That(shapes.Any(s => s >= 7), Is.True); Assert.That(shapes.Any(s => s == 3), Is.True);
                Assert.That(map.Deposits.Count, Is.GreaterThanOrEqualTo(10));
                int i = Array.FindIndex(shapes, s => s > 0); byte old = shapes[i]; shapes[i] = 0;
                Assert.That((byte)map.ShapeAt(i % 320, i / 320), Is.EqualTo(old));
            }
        }
        [Test]
        public void CaveStyleKeepsEightPixelTonalAssetContract()
        {
            const string root = "Assets/DarkNights/Res/Art/Custom/CaveExploration/";
            var style = AssetDatabase.LoadAssetAtPath<DarkNights.View.Terrain.CaveTerrainStyle>(CaveWorkshopSetup.StylePath);
            var rock = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "cave-rock.png");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "cave-dualgrid.png");
            Assert.That(style, Is.Not.Null); Assert.That(style.Shader.name, Is.EqualTo("DarkNights/CavePixelRock"));
            Assert.That(style.Rock, Is.SameAs(rock)); Assert.That(rock.width, Is.EqualTo(256)); Assert.That(rock.height, Is.EqualTo(256));
            Assert.That(atlas.width, Is.EqualTo(512)); Assert.That(atlas.height, Is.EqualTo(1024));
            var rockImporter = (TextureImporter)AssetImporter.GetAtPath(root + "cave-rock.png");
            var atlasImporter = (TextureImporter)AssetImporter.GetAtPath(root + "cave-dualgrid.png");
            Assert.That(rockImporter.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(rockImporter.mipmapEnabled, Is.False);
            Assert.That(rockImporter.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(rockImporter.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(atlasImporter.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(atlasImporter.mipmapEnabled, Is.False);
            Assert.That(atlasImporter.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(root + "cave-art-manifest.json");
            StringAssert.Contains("\"nativeTilePixels\": 8", manifest.text);
            StringAssert.Contains("\"fixedEdgeShadingPixels\"", manifest.text);
        }
    }
}
