using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;
using UnityEditor;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 窗口独占的串行文件准备作业；主线程捕获资源，后台只解码冻结文本及构造纯区域计划。
    /// 请求换代或取消时撤销旧回写，校验失败保留原编辑上下文；关闭时取消并观察后台异常。
    /// </summary>
    internal sealed class MapWorkbenchFileLoad : IDisposable
    {
        private readonly MapWorkbenchWindow owner;
        private Func<CancellationToken, Task<Action>> requested;
        private Task<Action> running;
        private CancellationTokenSource cancellation;
        private long revision, runningRevision;
        private bool yieldFrame;
        private string editBaseline, styleBaseline, planetBaseline;
        internal bool Busy => requested != null || running != null;
        internal UnityEngine.Object Target { get; private set; }

        internal MapWorkbenchFileLoad(MapWorkbenchWindow owner) { this.owner = owner; }

        internal void Run(string title, Action action)
        { Request(title, null, _ => Task.FromResult(action)); }

        internal void Open(TerrainMapDocument document, bool asCopy = false)
        {
            Request("打开 " + document.name, document, token =>
            {
                var input = MapWorkbenchImport.Read(document);
                var settings = input.FreezeCaveMap();
                var style = document.Style; string planet = document.PlanetId;
                string baseline = EditorJsonUtility.ToJson(document);
                string path = AssetDatabase.GetAssetPath(document);
                var dependency = AssetDatabase.GetAssetDependencyHash(path);
                string content = document.Map.CaptureContent(), recipe = document.Map.Recipe;
                string metadata = document.GenerationMetadata, mapHash = document.Map.ContentSha256;
                return Task.Run<Action>(() =>
                {
                    var map = ReviewedMineMap.Decode(content, recipe, TerrainGenerationPreview.WorldId, token);
                    var generated = TerrainGenerationMetadata.Decode(metadata, map, mapHash);
                    var plan = generated?.Plan;
                    token.ThrowIfCancellationRequested();
                    return () =>
                    {
                        VerifySource(document, path, dependency);
                        if (EditorJsonUtility.ToJson(document) != baseline)
                            throw new InvalidOperationException("读取期间配置已变化，请重新选择。");
                        TerrainDocumentStore.OpenPrepared(owner, document, input, style, planet, baseline, map, plan, asCopy, generated);
                    };
                }, token);
            });
        }

        internal void ViewFrozen(ReviewedMineMap asset)
        {
            Request("查看 " + asset.name, asset, token =>
            {
                string path = AssetDatabase.GetAssetPath(asset);
                var dependency = AssetDatabase.GetAssetDependencyHash(path);
                string content = asset.CaptureContent(), recipe = asset.Recipe;
                return Task.Run<Action>(() =>
                {
                    var map = ReviewedMineMap.Decode(content, recipe, TerrainGenerationPreview.WorldId, token);
                    return () =>
                    {
                        VerifySource(asset, path, dependency);
                        owner.Preview.UsePan(); owner.State.ViewedMap = asset; owner.State.Pick = 0;
                        owner.State.ShowJourney = owner.State.ShowStudy = false;
                        owner.Preview.SetFrozen(map); owner.RebuildPage();
                        owner.Notice = "正在只读查看地图结果；原编辑配置及未保存输入保留。";
                    };
                }, token);
            });
        }

        private static void VerifySource(UnityEngine.Object asset, string path, UnityEngine.Hash128 dependency)
        {
            if (asset == null || AssetDatabase.GetAssetPath(asset) != path ||
                AssetDatabase.GetAssetDependencyHash(path) != dependency)
                throw new InvalidOperationException("读取期间地图来源已变化，请重新选择；原编辑内容保留。");
        }

        private void Request(string title, UnityEngine.Object target, Func<CancellationToken, Task<Action>> capture)
        {
            revision++; cancellation?.Cancel();
            requested = capture; Target = target; yieldFrame = true;
            editBaseline = owner.Draft.Config.CanonicalIdentity();
            styleBaseline = owner.Styles.VisualIdentity; planetBaseline = owner.Generation.PlanetId;
            owner.Progress.Begin(title); owner.Refresh();
        }

        internal void Tick()
        {
            if (running != null)
            {
                if (!running.IsCompleted) return;
                var completed = running; running = null;
                cancellation.Dispose(); cancellation = null;
                try
                {
                    var apply = completed.GetAwaiter().GetResult();
                    if (runningRevision == revision)
                    {
                        if (owner.Draft.Config.CanonicalIdentity() != editBaseline ||
                            owner.Styles.VisualIdentity != styleBaseline || owner.Generation.PlanetId != planetBaseline)
                            throw new InvalidOperationException("读取期间原编辑内容已变化，已拒绝替换；请重新选择地图。");
                        Target = null; owner.Progress.Phase("准备画面");
                        var watch = Stopwatch.StartNew(); apply();
                        owner.Progress.MainStep("安装编辑上下文", watch.Elapsed.TotalMilliseconds);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception error) { if (runningRevision == revision) Fail(error); }
            }
            if (requested == null) return;
            if (yieldFrame) { yieldFrame = false; return; }
            if (owner.Generation.IsGenerating) { owner.Generation.AdvanceGeneration(); return; }
            var capture = requested; requested = null; runningRevision = revision;
            cancellation = new CancellationTokenSource();
            try
            {
                var watch = Stopwatch.StartNew();
                running = capture(cancellation.Token);
                owner.Progress.MainStep("捕获及校验资源", watch.Elapsed.TotalMilliseconds);
                owner.Progress.Phase("后台读取地图");
            }
            catch (Exception error)
            {
                cancellation.Dispose(); cancellation = null; Fail(error);
            }
        }

        internal void Cancel()
        {
            revision++; requested = null; Target = null; cancellation?.Cancel();
            owner.Progress.Finish("已取消，原编辑内容保留");
        }

        private void Fail(Exception error)
        {
            Target = null; owner.Progress.Finish("读取失败");
            owner.Notice = "地图读取失败，原编辑内容保留：" + error.Message;
            owner.Refresh();
        }

        public void Dispose()
        {
            Cancel();
            if (running != null) _ = running.ContinueWith(t => { var observed = t.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            cancellation?.Dispose();
        }
    }
}
