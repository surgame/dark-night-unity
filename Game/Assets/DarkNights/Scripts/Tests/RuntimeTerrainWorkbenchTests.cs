using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Config;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>运行工作台的草稿、权威笔触和缩放合同；验证取消与保存冲突不会改写源素材，屏幕命中和可见布局采用同一比例。</summary>
    public sealed class RuntimeTerrainWorkbenchTests
    {
        private const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        [TestCase(640, 360, 1.75f, 520)]
        [TestCase(800, 600, 1f, 280)]
        [TestCase(1280, 720, 1f, 380)]
        [TestCase(1920, 1080, 1.75f, 520)]
        [TestCase(2560, 1440, .75f, 380)]
        [TestCase(3840, 2160, 1.75f, 520)]
        public void ScaledPanelFitsAndPointerUsesTheSameCoordinates(int width, int height, float scale, float panelWidth)
        {
            var layout = new TerrainPanelLayout(width, height, scale, panelWidth);
            Assert.That(layout.Panel.xMax * layout.Scale, Is.LessThanOrEqualTo(width));
            Assert.That(layout.Panel.yMax * layout.Scale, Is.LessThanOrEqualTo(height));
            var center = layout.Panel.center * layout.Scale;
            Assert.That(layout.ContainsScreenPoint(new Vector2(center.x, height - center.y), height), Is.True);
            Assert.That(layout.ContainsScreenPoint(new Vector2(width - 1, 1), height), Is.False);
            Assert.That(layout.TabColumns, Is.EqualTo(panelWidth < 360 ? 3 : 5));
        }
        [Test]
        public void SharedModifiersStaySharedAndCancelDoesNotTouchAssets()
        {
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            string before = style.VisualIdentity;
            using var draft = new CaveStyleDraft(style);
            draft.Style.OutlineAmplitude += 1;
            var source = style.Background.NearModifiers[0];
            Assert.That(draft.Get(source), Is.SameAs(draft.Get(source)));
            var modifier = (RoundedClusterModifierAsset)draft.Get(source); modifier.Density = 12;
            Assert.That(draft.HasChanges, Is.True); Assert.That(style.VisualIdentity, Is.EqualTo(before));
            var captured = draft.Capture(out var background);
            try { Assert.That(background.NearModifiers[0], Is.SameAs(modifier)); Assert.That(captured.OutlineAmplitude, Is.EqualTo(draft.Style.OutlineAmplitude)); }
            finally { CaveStyleDraft.Release(captured); CaveStyleDraft.Release(background); }
            draft.Cancel(); Assert.That(draft.HasChanges, Is.False); Assert.That(style.VisualIdentity, Is.EqualTo(before));
            draft.Style.StoneSize = 6; draft.Apply(); draft.Style.StoneSize = 8; draft.Cancel();
            Assert.That(draft.Style.StoneSize, Is.EqualTo(6)); Assert.That(style.VisualIdentity, Is.EqualTo(before));
        }
        [Test]
        public void RuntimeApplyCannotBypassOriginalAssetConflict()
        {
            var style = ScriptableObject.CreateInstance<CaveTerrainStyle>();
            try
            {
                using var draft = new CaveStyleDraft(style);
                draft.Style.StoneSize = 6; draft.Apply(); style.StoneSize = 8;
                using var editor = new TerrainStyleDrafts(); editor.Import(draft);
                Assert.Throws<InvalidOperationException>(() => editor.Apply());
                Assert.That(style.StoneSize, Is.EqualTo(8));
            }
            finally { UnityEngine.Object.DestroyImmediate(style); }
        }
        [Test]
        public void BrushUndoRedoAndCancelUseAuthorityAndRetainSourceShapes()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            var blueprint = asset.ReadBlueprint();
            using var session = Session(blueprint, asset);
            var before = session.Edits.CaptureCells();
            var cell = Editable(blueprint); var point = new CellCoord(cell.x, -cell.y);
            var old = session.Map.Read(point).Cell; byte material = blueprint.MaterialAt(cell.x, cell.y) == 2 ? (byte)3 : (byte)2;
            Assert.That(session.Edits.Paint(cell.x, cell.y, true, material), Is.True); session.Edits.EndStroke();
            var after = session.Map.Read(point).Cell; Assert.That(after, Is.Not.EqualTo(old));
            session.Edits.Undo(); Assert.That(session.Map.Read(point).Cell, Is.EqualTo(old));
            session.Edits.Redo(); Assert.That(session.Map.Read(point).Cell, Is.EqualTo(after));
            session.Edits.Cancel(); CollectionAssert.AreEqual(before, session.Edits.CaptureCells());
            CollectionAssert.AreEqual(asset.InitialCells.bytes, before);
            Assert.That(session.Edits.Paint(0, 10, true, 1), Is.False);
        }
        [Test]
        public void OneDragSegmentCommitsOnceAndExternalEditsCannotBeOverwrittenByUndo()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            var blueprint = asset.ReadBlueprint(); using var session = Session(blueprint, asset);
            var cell = Editable(blueprint); ulong commit = session.Map.CommitId;
            byte material = blueprint.MaterialAt(cell.x, cell.y) == 2 ? (byte)3 : (byte)2;
            session.Edits.PaintLine(cell.x, cell.y, Math.Min(318, cell.x + 20), cell.y, true, material); session.Edits.EndStroke();
            Assert.That(session.Map.CommitId, Is.EqualTo(commit + 1));
            session.Teleport(cell.x, -cell.y + 1); Assert.That(session.Edit(cell.x, -cell.y, true), Is.True);
            Assert.Throws<InvalidOperationException>(() => session.Edits.Undo());
            Assert.That(session.Map.Read(new CellCoord(cell.x, -cell.y)).Cell.IsEmpty, Is.True);
        }
        [Test]
        public void FixedMapImportRejectsProtectedCellsAndExternalBaseline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            var draft = new TerrainMapDraft(); draft.Open(asset); var original = asset.InitialCells.bytes.ToArray();
            var changed = (byte[])original.Clone(); changed[0] = original[0] == 0 ? (byte)1 : (byte)0;
            Assert.Throws<InvalidOperationException>(() => draft.ImportCells(original, changed));
            Assert.That(draft.HasChanges, Is.False);
            changed = (byte[])original.Clone(); changed[500] ^= 1;
            Assert.Throws<InvalidOperationException>(() => draft.ImportCells(changed, original));
            CollectionAssert.AreEqual(original, asset.InitialCells.bytes);
        }
        private static CaveWorkshopSession Session(TerrainBlueprint blueprint, TerrainMapAsset asset)
        {
            var game = GameCatalogJson.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/balance.json").text,
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/pinewatch.json").text);
            return new CaveWorkshopSession(blueprint, asset.Definition.LoadGameplayCatalog(), game);
        }
        private static Vector2Int Editable(TerrainBlueprint blueprint)
        {
            for (int y = 10; y < 180; y++) for (int x = 10; x < 300; x++)
                if (!blueprint.IsProtected(x, y) && blueprint.MaterialAt(x, y) != 8) return new Vector2Int(x, y);
            throw new InvalidOperationException("No editable fixture cell.");
        }
    }
}
