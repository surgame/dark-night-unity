using DarkNights.Editor.Lighting;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>资产 Undo 的原生合同回归；验证整次拖动终值、资产独立历史和窗口退出后的恢复，不创建会话或访问存档。</summary>
    public sealed class FlashlightDebugUndoTests
    {
        private LightProfile first;
        private LightProfile second;
        private FlashlightDebugUndo history;

        [SetUp]
        public void SetUp()
        {
            first = ScriptableObject.CreateInstance<LightProfile>();
            second = ScriptableObject.CreateInstance<LightProfile>();
            history = new FlashlightDebugUndo();
        }

        [TearDown]
        public void TearDown()
        {
            history.EndGesture(); Undo.ClearUndo(first); Undo.ClearUndo(second);
            Object.DestroyImmediate(first); Object.DestroyImmediate(second);
        }

        [Test]
        public void WholeDragUndoesOnceAndRedoesToFinalValue()
        {
            history.BeginGesture("Intensity");
            history.Change(first, "Intensity", "拖动", () => first.Intensity = 2);
            history.Change(first, "Intensity", "拖动", () => first.Intensity = 3);
            history.EndGesture();
            Undo.PerformUndo(); Assert.That(first.Intensity, Is.EqualTo(1.35f));
            Undo.PerformRedo(); Assert.That(first.Intensity, Is.EqualTo(3));
        }

        [Test]
        public void DragCollapsesAdditionalNativeRecordsWithoutMergingEarlierEdits()
        {
            history.Change(first, "Range", "拖动前独立修改", () => first.Range = 20);
            history.BeginGesture("Intensity");
            history.Change(first, "Intensity", "拖动", () => first.Intensity = 1);
            Undo.IncrementCurrentGroup(); Undo.RecordObject(first, "属性树中间记录");
            history.Change(first, "Intensity", "拖动", () => first.Intensity = .5f);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup(); Undo.RecordObject(first, "属性树终值记录");
            history.Change(first, "Intensity", "拖动", () => first.Intensity = .1f);
            history.EndGesture();
            Undo.PerformUndo();
            Assert.That(first.Intensity, Is.EqualTo(1.35f)); Assert.That(first.Range, Is.EqualTo(20));
            Undo.PerformUndo(); Assert.That(first.Range, Is.EqualTo(14));
            Undo.PerformRedo(); Undo.PerformRedo();
            Assert.That(first.Intensity, Is.EqualTo(.1f)); Assert.That(first.Range, Is.EqualTo(20));
        }

        [Test]
        public void SeparateDragsKeepTheirOwnReleaseValues()
        {
            history.BeginGesture("Intensity");
            history.Change(first, "Intensity", "第一次拖动", () => first.Intensity = .5f);
            history.EndGesture();
            history.BeginGesture("Intensity");
            history.Change(first, "Intensity", "第二次拖动", () => first.Intensity = .1f);
            history.EndGesture();
            Undo.PerformUndo(); Assert.That(first.Intensity, Is.EqualTo(.5f));
            Undo.PerformUndo(); Assert.That(first.Intensity, Is.EqualTo(1.35f));
            Undo.PerformRedo(); Assert.That(first.Intensity, Is.EqualTo(.5f));
            Undo.PerformRedo(); Assert.That(first.Intensity, Is.EqualTo(.1f));
        }

        [Test]
        public void ChangingAssetDuringGestureClosesThePreviousAssetsGroup()
        {
            history.BeginGesture("Intensity");
            history.Change(first, "Intensity", "第一个资产拖动", () => first.Intensity = .5f);
            history.Change(second, "Intensity", "切换后的独立修改", () => second.Intensity = .1f);
            history.EndGesture();
            Undo.PerformUndo(); Assert.That(second.Intensity, Is.EqualTo(1.35f));
            Assert.That(first.Intensity, Is.EqualTo(.5f));
            Undo.PerformUndo(); Assert.That(first.Intensity, Is.EqualTo(1.35f));
        }

        [Test]
        public void SelectingAnotherAssetPreservesBothAssetsHistory()
        {
            history.Change(first, "Range", "第一个预设", () => first.Range = 20);
            history.Change(second, "Range", "第二个预设", () => second.Range = 8);
            Undo.PerformUndo();
            Assert.That(second.Range, Is.EqualTo(14)); Assert.That(first.Range, Is.EqualTo(20));
            Undo.PerformUndo(); Assert.That(first.Range, Is.EqualTo(14));
            Undo.PerformRedo(); Undo.PerformRedo();
            Assert.That(first.Range, Is.EqualTo(20)); Assert.That(second.Range, Is.EqualTo(8));
        }

        [Test]
        public void EndingWindowGestureDoesNotClearNativeAssetHistory()
        {
            history.BeginGesture("Intensity");
            history.Change(first, "Intensity", "窗口修改", () => first.Intensity = 2);
            history.EndGesture();
            Undo.PerformUndo(); Assert.That(first.Intensity, Is.EqualTo(1.35f));
            Undo.PerformRedo(); Assert.That(first.Intensity, Is.EqualTo(2));
        }

        [Test]
        public void NewEditAfterUndoStartsANewBranch()
        {
            history.Change(first, "Range", "旧分支", () => first.Range = 20);
            Undo.PerformUndo();
            history.Change(first, "Range", "新分支", () => first.Range = 8);
            Undo.PerformRedo(); Assert.That(first.Range, Is.EqualTo(8));
            Undo.PerformUndo(); Assert.That(first.Range, Is.EqualTo(14));
        }
    }
}
