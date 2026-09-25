using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AnyRules.Next;
using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

namespace DarkNights.Tests
{
    /// <summary>实际 Tuner、GPU、帧确认和局部工作量的验收；不以 CPU 模型替代实际渲染，不改写人工资源。</summary>
    public sealed class TerrainVisualAcceptanceTests
    {
        [UnityTest]
        public IEnumerator HotDigFillAnd64CellsUseSparseRulesAndGpuPatches()
        {
            using var scope = new TerrainVisualTestScope();
            yield return scope.Settle();
            Assert.That(scope.Controller.LoadedChunkCount, Is.EqualTo(70));
            Assert.That(scope.Preview.RefreshPath, Does.StartWith("LocalV2"));
            scope.Observe();
            int background = scope.Preview.BackgroundBuildCount;
            long backgroundBytes = scope.Preview.BackgroundUploadedBytes;
            scope.SaveImage("tuner-before.png");
            foreach (var point in new[] { new Vector2Int(31, 60), new Vector2Int(32, 64), new Vector2Int(159, 96) })
            {
                scope.Apply(point.x, point.y, 1); yield return scope.Settle();
                long solved = scope.SolvedCells; scope.Changes.Clear();
                scope.Apply(point.x, point.y, 0); scope.Stage.Tick();
                Assert.That(scope.Controller.Read(new CellCoord(point.x, -point.y)).Cell.IsEmpty, Is.True);
                yield return scope.Settle();
                Assert.That(scope.SolvedCells - solved, Is.EqualTo(4), "Single-cell Dual Grid dependency");
                Assert.That(scope.Changes.Single().Changes.Count, Is.EqualTo(1));
                var rect = new RectInt(point.x * 8 - 8, point.y * 8 - 8, 24, 24);
                scope.AssertRockMatchesCpu(rect);
                solved = scope.SolvedCells;
                scope.Apply(point.x, point.y, 1); yield return scope.Settle();
                Assert.That(scope.SolvedCells - solved, Is.EqualTo(4));
                scope.AssertRockMatchesCpu(rect);
            }
            var cells = new List<TerrainBlueprintCellChange>();
            for (int y = 92; y < 100; y++) for (int x = 156; x < 164; x++) cells.Add(new TerrainBlueprintCellChange(x, y, 0, 0));
            scope.Stage.Source.ApplyChanges(cells); yield return scope.Settle();
            long before = scope.SolvedCells; scope.Changes.Clear();
            scope.Stage.Source.ApplyChanges(cells.Select(c => new TerrainBlueprintCellChange(c.X, c.Row, 1, 0)).ToArray());
            yield return scope.Settle();
            Assert.That(scope.Changes.Count, Is.EqualTo(1));
            Assert.That(scope.Changes[0].Changes.Count, Is.EqualTo(64));
            Assert.That(scope.SolvedCells - before, Is.EqualTo(81));
            Assert.That(scope.Changes.All(c => c.Kind == GridChangeKind.SourceInputChanged), Is.True);
            Assert.That(scope.Preview.BackgroundBuildCount, Is.EqualTo(background));
            Assert.That(scope.Preview.BackgroundUploadedBytes, Is.EqualTo(backgroundBytes));
            Assert.That(scope.Controller.UnsavedChunkCount, Is.Zero);
            scope.AssertRockMatchesCpu(new RectInt(1224, 728, 112, 96));
            scope.SaveImage("tuner-after.png");
            long builds = scope.Preview.BuiltPages, batches = scope.Preview.RefreshBatchCount;
            int rocks = scope.Preview.RockBuildCount;
            scope.Stage.Source.ApplyChanges(cells.Select(c => new TerrainBlueprintCellChange(c.X, c.Row, 1, 0)).ToArray());
            for (int i = 0; i < 40; i++) { Assert.That(scope.Stage.Tick(), Is.False); yield return null; }
            Assert.That(scope.Preview.BuiltPages, Is.EqualTo(builds));
            Assert.That(scope.Preview.RockBuildCount, Is.EqualTo(rocks));
            Assert.That(scope.Preview.RefreshBatchCount, Is.EqualTo(batches));
        }

        [UnityTest]
        public IEnumerator ContinuousStrokeProgressesBeforeMouseUpAndUndoRedoCancel()
        {
            using var scope = new TerrainVisualTestScope();
            yield return scope.Settle();
            var draft = new TerrainMapDraft(); draft.Open(scope.Map); draft.BeginStroke();
            int before = scope.Preview.RockBuildCount;
            var watch = Stopwatch.StartNew(); int samples = 0, nextCell = 0;
            while (watch.Elapsed.TotalSeconds < 3)
            {
                int x = 90 + nextCell % 16, row = 72 + nextCell / 16 % 3;
                byte material = draft.CopyMaterials()[row * 320 + x];
                draft.Paint(x, row, material == 0, 1);
                scope.Stage.Source.ApplyChanges(draft.DrainChangedCells()); scope.Stage.Tick();
                if (scope.Preview.RockBuildCount > before) samples++;
                nextCell++; yield return null;
            }
            Assert.That(samples, Is.GreaterThan(0), "No output advanced while the stroke was still open.");
            draft.EndStroke(); yield return scope.Settle();
            byte[] edited = draft.CopyMaterials();
            Assert.That(draft.Undo(), Is.True); scope.Stage.Source.ApplyChanges(draft.DrainChangedCells()); yield return scope.Settle();
            CollectionAssert.AreEqual(scope.Blueprint.CopyMaterials(), draft.CopyMaterials());
            Assert.That(draft.Redo(), Is.True); scope.Stage.Source.ApplyChanges(draft.DrainChangedCells()); yield return scope.Settle();
            CollectionAssert.AreEqual(edited, draft.CopyMaterials());
            draft.Cancel(); scope.Stage.Source.ApplyChanges(draft.DrainChangedCells()); yield return scope.Settle();
            CollectionAssert.AreEqual(scope.Blueprint.CopyMaterials(), draft.CopyMaterials());
            Assert.That(scope.Preview.PresentedSourceCommit, Is.EqualTo(scope.Preview.InstalledSourceCommit));
        }

        [UnityTest]
        public IEnumerator FinalConfirmationIsRealAndMissingCallbacksDoNotSpin()
        {
            using var scope = new TerrainVisualTestScope();
            yield return scope.Settle();
            var type = typeof(TerrainPreview); var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Action<Camera> pre = (Action<Camera>)Delegate.CreateDelegate(typeof(Action<Camera>), scope.Preview, type.GetMethod("BeginCamera", flags));
            Action<Camera> post = (Action<Camera>)Delegate.CreateDelegate(typeof(Action<Camera>), scope.Preview, type.GetMethod("EndCamera", flags));
            var beginPipeline = (Action<ScriptableRenderContext, Camera>)Delegate.CreateDelegate(typeof(Action<ScriptableRenderContext, Camera>), scope.Preview, type.GetMethod("BeginPipelineCamera", flags));
            var endPipeline = (Action<ScriptableRenderContext, Camera>)Delegate.CreateDelegate(typeof(Action<ScriptableRenderContext, Camera>), scope.Preview, type.GetMethod("EndPipelineCamera", flags));
            Camera.onPreCull -= pre; Camera.onPostRender -= post;
            RenderPipelineManager.beginCameraRendering -= beginPipeline; RenderPipelineManager.endCameraRendering -= endPipeline;
            try
            {
                byte next = scope.Controller.Read(new CellCoord(40, -72)).Cell.IsEmpty ? (byte)1 : (byte)0;
                scope.Apply(40, 72, next); scope.Stage.Tick();
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
                while (!scope.Preview.NeedsPresentationDraw && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                { scope.Stage.Tick(); yield return null; }
                Assert.That(scope.Preview.NeedsPresentationDraw, Is.True);
                Assert.That(scope.Stage.Ready, Is.False, "A draw callback must not be fabricated.");
                for (int i = 0; i < 20; i++) { Assert.That(scope.Stage.Tick(), Is.False); yield return null; }
            }
            finally
            {
                Camera.onPreCull += pre; Camera.onPostRender += post;
                RenderPipelineManager.beginCameraRendering += beginPipeline; RenderPipelineManager.endCameraRendering += endPipeline;
            }
            var camera = TerrainVisualTestScope.Read<Camera>(scope.Stage, "camera");
            camera.Render(); Assert.That(scope.Stage.Ready, Is.True);
        }

        [UnityTest]
        public IEnumerator MeasureActualHotInputToFinalCameraLatency()
        {
            using var scope = new TerrainVisualTestScope();
            yield return scope.Settle();
            var single = new List<double>(); var rect = new List<double>();
            for (int i = 0; i < 34; i++)
            {
                var timer = Stopwatch.StartNew(); scope.Apply(144, 88, (byte)(i % 2));
                scope.Stage.Tick(); yield return scope.Settle();
                if (i >= 4) single.Add(timer.Elapsed.TotalMilliseconds);
            }
            for (int i = 0; i < 24; i++)
            {
                var changes = new List<TerrainBlueprintCellChange>();
                for (int y = 92; y < 100; y++) for (int x = 156; x < 164; x++) changes.Add(new TerrainBlueprintCellChange(x, y, (byte)(i % 2), 0));
                var timer = Stopwatch.StartNew(); scope.Stage.Source.ApplyChanges(changes); scope.Stage.Tick(); yield return scope.Settle();
                if (i >= 4) rect.Add(timer.Elapsed.TotalMilliseconds);
            }
            var result = new { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                path = scope.Preview.RefreshPath, single = Stats(single), batch64 = Stats(rect),
                singleP95TargetMs = 33.3, batch64P95TargetMs = 66.7,
                singleTargetPassed = Percentile(single, .95) <= 33.3, batch64TargetPassed = Percentile(rect, .95) <= 66.7,
                note = "EditorApplication update + real offscreen camera. Performance target results are reported separately from harness execution." };
            File.WriteAllText(Path.Combine(TerrainVisualTestScope.Output, "actual-tuner-performance.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
        }
        private static object Stats(List<double> values) => new { samples = values, p50 = Percentile(values, .5), p95 = Percentile(values, .95), p99 = Percentile(values, .99) };
        private static double Percentile(List<double> values, double p) => values.OrderBy(v => v).ElementAt(Math.Max(0, (int)Math.Ceiling(values.Count * p) - 1));
    }
}
