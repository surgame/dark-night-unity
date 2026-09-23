using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>编辑态全图预览与草稿事务回归；确认参数修改在 Apply 前不写回源资产。</summary>
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

        [Test]
        public void FullPreviewContainsTheSamePixelsAsLocalBake()
        {
            const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            var map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(root + "ReferenceChamber.asset");
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(root + "Style.asset");
            Assert.That(map, Is.Not.Null); Assert.That(style, Is.Not.Null);
            var blueprint = map.ReadBlueprint(); var background = style.Background;
            var materials = blueprint.CopyMaterials(); var shapes = blueprint.CopyShapes();
            var visible = new[] { background.Near, background.Middle, background.Deep };
            var outline = style.CaptureOutline(); var foreground = style.CaptureModifiers();
            var generator = background.CaptureGenerator(); var modifiers = background.CaptureModifiers();
            byte[] full = TerrainStylePreviewBaker.BakeFull(materials, shapes, blueprint.Settings.Seed,
                style.StoneSize, outline, foreground, generator, modifiers, visible, background.MiddleSoftness);
            byte[] local = TerrainStylePreviewBaker.Bake(materials, shapes, blueprint.Settings.Seed,
                316, 284, style.StoneSize, outline, foreground, generator, modifiers, visible, background.MiddleSoftness);
            Assert.That(full.Length, Is.EqualTo(TerrainStylePreviewBaker.WorldWidth * TerrainStylePreviewBaker.WorldHeight * 4));
            for (int y = 0; y < TerrainStylePreviewBaker.Height; y++)
                for (int x = 0; x < TerrainStylePreviewBaker.Width; x++)
                {
                    int source = (y * TerrainStylePreviewBaker.Width + x) * 4;
                    int destination = ((y + 284) * TerrainStylePreviewBaker.WorldWidth + x + 316) * 4;
                    for (int channel = 0; channel < 4; channel++)
                        if (full[destination + channel] != local[source + channel])
                            Assert.Fail("Pixel {0},{1} channel {2} differs", x, y, channel);
                }
        }

        [Test]
        public void ResetAndCancelLeaveOriginalParametersUntouched()
        {
            var original = ScriptableObject.CreateInstance<DownwardEdgeModifierAsset>();
            var style = ScriptableObject.CreateInstance<CaveTerrainStyle>();
            style.Modifiers = new CaveModifierAsset[] { original };
            using (var drafts = new TerrainStyleDrafts())
            {
                try
                {
                    var working = drafts.Draft(original);
                    var workingStyle = drafts.Draft(style);
                    Assert.That(drafts.Draft(original), Is.SameAs(working));
                    Assert.That(working.hideFlags & HideFlags.NotEditable, Is.EqualTo(HideFlags.None));
                    Assert.That(working.hideFlags & HideFlags.DontSaveInEditor, Is.Not.EqualTo(HideFlags.None));
                    working.Length = 12; working.Density = 45;
                    workingStyle.Modifiers = Array.Empty<CaveModifierAsset>();
                    Assert.That(drafts.HasChanges, Is.True);
                    Assert.That(original.Length, Is.EqualTo(7));
                    Assert.That(original.Density, Is.EqualTo(100));
                    Assert.That(drafts.Reset(original, "Length"), Is.True);
                    Assert.That(working.Length, Is.EqualTo(7));
                    Assert.That(working.Density, Is.EqualTo(45));
                    Assert.That(drafts.Reset(style, "Modifiers"), Is.True);
                    Assert.That(workingStyle.Modifiers, Is.EquivalentTo(new[] { original }));
                    drafts.Clear();
                    Assert.That(original.Length, Is.EqualTo(7));
                    Assert.That(original.Density, Is.EqualTo(100));
                    Assert.That(style.Modifiers, Is.EquivalentTo(new[] { original }));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(style);
                    UnityEngine.Object.DestroyImmediate(original);
                }
            }
        }

        [Test]
        public void ApplyWritesOnlyAfterConfirmationAndRejectsExternalConflicts()
        {
            string folder = "Assets/CaveWallTunerTests-" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
            try
            {
                string firstPath = folder + "/first.asset", secondPath = folder + "/second.asset";
                var first = ScriptableObject.CreateInstance<DownwardEdgeModifierAsset>();
                var second = ScriptableObject.CreateInstance<DownwardEdgeModifierAsset>();
                AssetDatabase.CreateAsset(first, firstPath);
                AssetDatabase.CreateAsset(second, secondPath);
                string diskPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), firstPath);
                byte[] before = File.ReadAllBytes(diskPath);
                using (var drafts = new TerrainStyleDrafts())
                {
                    drafts.Draft(first).Length = 11;
                    drafts.Draft(second).Length = 12;
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(diskPath));
                    var external = new SerializedObject(second);
                    external.FindProperty("Length").intValue = 13;
                    external.ApplyModifiedPropertiesWithoutUndo();
                    Assert.Throws<InvalidOperationException>(() => drafts.Apply());
                    Assert.That(first.Length, Is.EqualTo(7));
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(diskPath));
                    external.Update();
                    external.FindProperty("Length").intValue = 7;
                    external.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(drafts.Apply(), Is.EqualTo(2));
                    Assert.That(first.Length, Is.EqualTo(11));
                    Assert.That(second.Length, Is.EqualTo(12));
                    CollectionAssert.AreNotEqual(before, File.ReadAllBytes(diskPath));
                }
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void MapPaintingIsDraftedAndPreservesProtectedCells()
        {
            const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            var source = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(root + "ReferenceChamber.asset");
            Assert.That(source, Is.Not.Null);
            string folder = "Assets/CaveWallMapTests-" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
            try
            {
                string mapPath = folder + "/scratch.asset", cellsPath = folder + "/scratch.cells.bytes";
                Assert.That(AssetDatabase.CopyAsset(root + "ReferenceChamber.cells.bytes", cellsPath), Is.True);
                string diskPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), cellsPath);
                byte[] protectedCopy = File.ReadAllBytes(diskPath);
                protectedCopy[(100 * 320 + 100) * 2 + 1] = 2;
                protectedCopy[(100 * 320 + 101) * 2 + 1] = 1;
                File.WriteAllBytes(diskPath, protectedCopy);
                AssetDatabase.ImportAsset(cellsPath, ImportAssetOptions.ForceSynchronousImport);
                var map = UnityEngine.Object.Instantiate(source);
                map.InitialCells = AssetDatabase.LoadAssetAtPath<TextAsset>(cellsPath);
                AssetDatabase.CreateAsset(map, mapPath);
                byte[] original = File.ReadAllBytes(diskPath);
                var draft = new TerrainMapDraft(); draft.Open(map);
                Assert.That(draft.IsReady, Is.True, draft.Error);
                const int x = 100, y = 100;
                int offset = (y * 320 + x) * 2;
                byte before = original[offset];
                Assert.That(draft.Paint(x, y, false, 1), Is.EqualTo(before != 0 || original[offset + 1] != 0));
                Assert.That(draft.CopyMaterials()[y * 320 + x], Is.Zero);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(diskPath));
                Assert.That(draft.Paint(0, y, true, 1), Is.False);
                Assert.That(draft.Paint(x + 1, y, false, 1), Is.False);
                Assert.That(draft.Paint(x, y, true, 2), Is.True);
                Assert.That(draft.CopyMaterials()[y * 320 + x], Is.EqualTo(2));
                Assert.That(draft.CopyShapes()[y * 320 + x], Is.Zero);
                Assert.That(draft.CopyOriginalShapes()[y * 320 + x], Is.EqualTo(1));
                Assert.That(draft.ChangedCells, Is.EqualTo(1));
                draft.Open(map);
                Assert.That(draft.HasChanges, Is.False);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(diskPath));

                Assert.That(draft.Paint(x, y, true, 1), Is.True);
                byte[] external = (byte[])original.Clone();
                external[50] = external[50] == 1 ? (byte)2 : (byte)1;
                File.WriteAllBytes(diskPath, external);
                Assert.Throws<InvalidOperationException>(() => draft.Apply());
                CollectionAssert.AreEqual(external, File.ReadAllBytes(diskPath));
                File.WriteAllBytes(diskPath, original);
                AssetDatabase.ImportAsset(cellsPath, ImportAssetOptions.ForceSynchronousImport);
                draft.Apply();
                Assert.That(draft.HasChanges, Is.False);
                Assert.That(map.ReadBlueprint().MaterialAt(x, y), Is.EqualTo(1));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }
    }
}
