using System;
using System.Linq;
using System.Collections;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.TestTools;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>初始背景封套与连接基线的破损、重复、乱序、陈旧代次和冻结保护；不替代真实多进程收敛验收。</summary>
    public sealed class BackgroundReferenceTests
    {
        private const string Id = "00000000000000000000000000000001";
        private static BackgroundBakeDescriptor Source() => ExpeditionTerrainGenerator.Generate("CONTOUR-0922", Id).Background;
        [Test]
        public void CodecRoundtripAndDefensiveCopies()
        {
            var source = Source(); byte[] bytes = BackgroundReferenceCodec.Encode(source);
            var restored = BackgroundReferenceCodec.Decode(bytes);
            Assert.That(restored.ReferenceHash, Is.EqualTo(source.ReferenceHash));
            CollectionAssert.AreEqual(source.CopyShapes(), restored.CopyShapes());
            byte[] changed = source.CopyMaterials(); changed[0] ^= 1;
            Assert.That(source.Material(0, 0), Is.Not.EqualTo(changed[0]));
            Assert.Throws<ArgumentException>(() => new BackgroundBakeDescriptor(Id, source.LayoutSeed, changed, source.CopyShapes(), source.ReferenceHash));
            Assert.That(bytes.Length, Is.LessThan(BackgroundReferenceCodec.MaximumBytes));
        }
        [Test]
        public void CorruptVersionHashRunAndTrailingDataAreRejected()
        {
            byte[] original = BackgroundReferenceCodec.Encode(Source());
            byte[] version = (byte[])original.Clone(); version[0]++;
            Assert.Throws<FormatException>(() => BackgroundReferenceCodec.Decode(version));
            Assert.Throws<FormatException>(() => BackgroundReferenceCodec.Decode(original.Take(original.Length - 1).ToArray()));
            Assert.Throws<FormatException>(() => BackgroundReferenceCodec.Decode(original.Concat(new byte[1]).ToArray()));
            byte[] damaged = (byte[])original.Clone(); damaged[damaged.Length - 2] = 254;
            Assert.Throws<FormatException>(() => BackgroundReferenceCodec.Decode(damaged));
            byte[] wrongStyle = (byte[])original.Clone(); wrongStyle[60] ^= 1;
            Assert.Throws<FormatException>(() => BackgroundReferenceCodec.Decode(wrongStyle));
        }
        [Test]
        public void ReliableBaselineIsAtomicAndRejectsStaleWorld()
        {
            var source = Source(); byte[] bytes = BackgroundReferenceCodec.Encode(source);
            var receiver = new TerrainBackgroundBaseline();
            var signal = new TerrainEpochSignal { Epoch = 2, MapEpoch = 7, WorldId = Id, Seed = source.LayoutSeed, BackgroundBytes = bytes.Length };
            receiver.Begin(signal);
            int count = (bytes.Length + TerrainBackgroundBaseline.ChunkBytes - 1) / TerrainBackgroundBaseline.ChunkBytes;
            for (int i = count - 1; i >= 0; i--)
            {
                int offset = i * TerrainBackgroundBaseline.ChunkBytes;
                var chunk = new TerrainBackgroundChunk { Epoch = 1, MapEpoch = 7, WorldId = Id, Index = i,
                    Bytes = bytes.Skip(offset).Take(TerrainBackgroundBaseline.ChunkBytes).ToArray() };
                Assert.That(receiver.Accept(chunk), Is.False); chunk.Epoch = 2;
                receiver.Accept(chunk); Assert.That(receiver.Accept(chunk), Is.False);
                Assert.That(receiver.Ready, Is.EqualTo(i == 0));
            }
            Assert.That(receiver.Reference.ReferenceHash, Is.EqualTo(source.ReferenceHash));
            signal.Epoch = 3; signal.MapEpoch = 8; receiver.Begin(signal);
            Assert.That(receiver.Reference, Is.Null); Assert.That(receiver.Ready, Is.False);
            Assert.Throws<FormatException>(() => receiver.Accept(new TerrainBackgroundChunk { Epoch = 3, MapEpoch = 8,
                WorldId = Id, Index = 0, Bytes = new byte[1] }));
            receiver.Reset(); Assert.That(receiver.Ready, Is.False);
        }
        [Test]
        public void AlphaSoftnessNeverChangesRgbOrExpandsMask()
        {
            var raw = new byte[9 * 9 * 4];
            for (int i = 0; i < 81; i++) { raw[i * 4] = 92; raw[i * 4 + 1] = 70; raw[i * 4 + 2] = 50; raw[i * 4 + 3] = 242; }
            foreach (int width in new[] { 0, 2, 4 })
            {
                var soft = (byte[])raw.Clone(); BackgroundPixelMath.SoftenAlpha(soft, 9, 9, width);
                for (int k = 0; k < raw.Length; k++)
                    if (k % 4 == 3) Assert.That(soft[k], Is.LessThanOrEqualTo(raw[k])); else Assert.That(soft[k], Is.EqualTo(raw[k]));
                if (width == 0) CollectionAssert.AreEqual(raw, soft);
                if (width == 2) { Assert.That(soft[3], Is.EqualTo(63)); Assert.That(soft[(4 * 9 + 4) * 4 + 3], Is.EqualTo(242)); }
            }
        }
        [UnityTest]
        public IEnumerator DestructionSaveRestoreKeepsInitialReferenceAndRejectsBrokenCandidate() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var catalog = RuleScenario.Catalog();
            var layout = new LevelLayout(5120, RuleScenario.Layout().GroundY, 380, 770, 900, 568,
                new[] { new PlacementDefinition("ship", 568) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                randomTerrain: true, expedition: true);
            var selected = ExpeditionTerrainGenerator.Generate("CONTOUR-0922", Id);
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(Editor.Terrain.CaveTerrainAssets.DefinitionPath);
            var world = scope.NewWorld(catalog, layout, false, terrain: w => new SessionTerrain(w.Context, definition.LoadGameplayCatalog(), selected));
            using var authority = new SessionAuthority(world);
            var host = authority.Connect(0); authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
            var map = world.Terrain.Map;
            var center = Enumerable.Range(44 * 320, 100 * 320).Select(i => new CellCoord(i % 320, -(i / 320)))
                .First(p => !map.Read(p).Cell.IsEmpty && (map.Read(p).Cell.Flags & 1) == 0);
            var targets = map.BuildTargets(TerrainEditAction.Explosive, center);
            map.DestroyTrusted(1, "background-blast", TerrainEditAction.Explosive, map.World, center, targets, _ => true);
            Assert.That(map.Read(center).Cell.IsEmpty, Is.True);
            Assert.That(world.Terrain.Background.ReferenceHash, Is.EqualTo(selected.Background.ReferenceHash));
            Assert.That(world.Terrain.Background.Material(center.U, -center.V), Is.Not.Zero);
            string json = world.SaveCodec.Serialize(world.CaptureWorld());
            world.Restore(json);
            Assert.That(world.Terrain.Background.ReferenceHash, Is.EqualTo(selected.Background.ReferenceHash));
            Assert.That(world.Terrain.Map.Read(center).Cell.IsEmpty, Is.True);
            var damaged = JObject.Parse(json);
            var background = damaged.Descendants().OfType<JProperty>().Single(p => p.Name == "background");
            background.Value = "AAAA";
            var previous = world.Terrain.Map;
            Assert.Throws<FormatException>(() => world.Restore(damaged.ToString()));
            Assert.That(world.Terrain.Map, Is.SameAs(previous));
            Assert.That(world.Terrain.Background.ReferenceHash, Is.EqualTo(selected.Background.ReferenceHash));
        });
    }
}
