using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Networking;
using AnyRules.Next.Unity;
using DarkNights.Core.Config.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>Editor 与运行时共用的只读地形宿主；先安装冻结输入，再求解表现，相机绘制后确认当前游标。</summary>
    public sealed class TerrainPreview : MonoBehaviour
    {
        public TerrainMapAsset Map;
        public CaveTerrainStyle CaveStyle;
        public Camera ViewCamera;
        private CaveVisualSource caveSource;
        private ARDMapController controller;
        private CancellationTokenSource lifetime;
        private TerrainReplicaSource replicaSource;
        private ITerrainInputSource inputSource;
        private readonly TerrainInputBatchQueue inputQueue = new TerrainInputBatchQueue();
        private GridBounds visible;
        private bool awaitingBaseline, loading, cameraHooks, drawing;
        private int drawingVisualRevision;
        private ulong drawingGeneration, drawingCommit;
        public int VisualRevision { get; private set; }
        public long BuiltPages => controller?.Renderer?.CommittedBuilds ?? 0;
        public int BackgroundBuildCount => caveSource?.BackgroundBuildCount ?? 0;
        public int RockBuildCount => caveSource?.RockBuildCount ?? 0;
        public long BackgroundUploadedBytes => caveSource?.BackgroundUploadedBytes ?? 0;
        public int BackgroundResidentPages => caveSource?.BackgroundResidentPages ?? 0;
        public string RefreshPath => caveSource?.RefreshPath ?? "DualGrid";
        public int LastChangedChunkCount { get; private set; }
        public int LastRefreshRegionCount { get; private set; }
        public long RefreshBatchCount { get; private set; }
        public ulong ReceivedInputGeneration => inputQueue.ReceivedGeneration;
        public ulong ReceivedSourceCommit => inputQueue.ReceivedCommit;
        public ulong InstalledInputGeneration { get; private set; }
        public ulong InstalledSourceCommit { get; private set; }
        public ulong PresentedInputGeneration { get; private set; }
        public ulong PresentedSourceCommit { get; private set; }
        public bool RefreshingReplica => loading || awaitingBaseline || inputQueue.HasPending;
        public Exception LastError { get; private set; }
        private bool IsPresentationStable => LastError == null && controller != null && !RefreshingReplica &&
            (caveSource?.BackgroundReady ?? true) && BuiltPages > 0 && !controller.HasPendingPresentationWork;
        public bool Ready => IsPresentationStable && PresentedInputGeneration == InstalledInputGeneration &&
            PresentedSourceCommit == InstalledSourceCommit;

        public void NotifyReplicaChanged() => replicaSource?.NotifyChanged();
        public void NotifyReplicaChanged(MapReplicaChange transition) => replicaSource?.NotifyChanged(transition);

        public async void ShowReplica(ARDMapDefinition definition, IMapChunkSource source, WorldIdentity world,
            BackgroundBakeDescriptor reference = null)
        {
            replicaSource = source as TerrainReplicaSource;
            await OpenAsync(definition, source, world, reference);
        }

        public async void ShowBlueprint(ARDMapDefinition definition, TerrainBlueprint blueprint,
            IMapChunkSource input = null, BackgroundBakeDescriptor reference = null)
        {
            try
            {
                input = input ?? new TerrainBlueprintSource(blueprint, definition.LoadGameplayCatalog().Tiles);
                if (CaveStyle != null && reference == null)
                    reference = new BackgroundBakeDescriptor(Guid.NewGuid().ToString("N"), blueprint.Settings.Seed,
                        blueprint.CopyMaterials(), blueprint.CopyShapes());
                await OpenAsync(definition, input, null, reference);
            }
            catch (Exception error) { Fail(error); }
        }

        private async Task OpenAsync(ARDMapDefinition definition, IMapChunkSource source,
            WorldIdentity? world, BackgroundBakeDescriptor reference)
        {
            if (lifetime != null) throw new InvalidOperationException("每个表现宿主只接收一个世界；换图须替换宿主。");
            var own = lifetime = new CancellationTokenSource();
            loading = true; awaitingBaseline = source is ITerrainInputSource;
            EnsureCameraHooks();
            inputSource = source as ITerrainInputSource;
            if (inputSource != null) inputSource.InputChanged += OnInputChanged;
            try
            {
                IMapChunkSource mapSource = source;
                if (CaveStyle != null)
                {
                    caveSource = new CaveVisualSource(source, CaveStyle, definition.LoadGameplayCatalog().Tiles, transform, reference);
                    mapSource = caveSource;
                }
                var profile = caveSource == null ? null : new RenderProfile(defaultMaterial: caveSource.Material);
                var options = new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false,
                    maximumInitializationCells: 131072, chunkSource: mapSource, parent: transform, world: world,
                    sourceDrivenInputs: inputSource != null, profile: profile,
                    scheduling: new RenderSchedulingOptions(lagPolicy: RenderLagPolicy.LatestOnly));
                var result = await ARDMapController.CreateAsync(definition, options, own.Token);
                if (own.IsCancellationRequested) { await result.DisposeAsync(); return; }
                controller = result;
                inputQueue.Configure(result.Descriptor);
                await result.LoadRegionAsync(result.Descriptor.Bounds, own.Token);
                if (own.IsCancellationRequested) return;
                inputSource?.PublishInitialBaseline();
                caveSource?.Flush();
                loading = false;
                TickPresentation();
                VisualRevision++;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { loading = false; Fail(error); }
        }

        private void OnEnable()
        {
            EnsureCameraHooks();
            if (Map == null || ViewCamera == null) return;
            if (CaveStyle == null) CaveStyle = Map.CaveStyle;
            ShowBlueprint(Map.Definition, Map.ReadBlueprint());
        }

        public void SetMinerals(IReadOnlyList<DarkNights.Core.ViewData.WorksiteViewData> deposits)
        { caveSource?.SetMinerals(deposits); caveSource?.Flush(); }
        public void SetDevices(DarkNights.Core.ViewData.WorldViewData world)
        { caveSource?.SetDevices(world); caveSource?.Flush(); }
        private void Update() => TickPresentation();
        /// <summary>离屏窗口显式驱动同一条链；不依赖编辑态 MonoBehaviour.Update 自动运行。</summary>
        public void TickFromEditor() { EnsureCameraHooks(); TickPresentation(); }

        private void TickPresentation()
        {
            if (controller == null || loading || LastError != null) return;
            try
            {
                // 新输入先使旧作业失效，不能先提交旧岩壁再处理已收到的新数据。
                if (inputQueue.TakeOverflow())
                {
                    HideForBaseline();
                    inputSource?.PublishInitialBaseline();
                }
                ProcessOneInputBatch();
                if (!awaitingBaseline) UpdateVisible();
                long pagesBefore = BuiltPages;
                int rockBefore = RockBuildCount, backgroundBefore = BackgroundBuildCount;
                if (controller.HasPendingPresentationWork) controller.Tick();
                caveSource?.TickBackground();
                if (pagesBefore != BuiltPages || rockBefore != RockBuildCount || backgroundBefore != BackgroundBuildCount)
                    VisualRevision++;
            }
            catch (Exception error) { Fail(error); }
        }

        private void OnInputChanged(MapInputBatch batch)
        {
            if (batch == null || lifetime == null || lifetime.IsCancellationRequested) return;
            try { inputQueue.Enqueue(batch); }
            catch (Exception error) { Fail(error); }
        }

        private void ProcessOneInputBatch()
        {
            if (!inputQueue.TryDequeue(out var batch)) return;
            bool lifecycle = batch.Kind == MapInputBatchKind.Reset || batch.Kind == MapInputBatchKind.VisibilityRevoked ||
                batch.Kind == MapInputBatchKind.Disconnected;
            if (awaitingBaseline && batch.Kind != MapInputBatchKind.Baseline && !lifecycle)
                throw new InvalidOperationException("等待完整基线时不能安装普通增量。");
            var result = controller.InstallSourceInput(batch);
            InstalledInputGeneration = result.InputGeneration; InstalledSourceCommit = result.SourceCommit;
            if (lifecycle)
            {
                HideForBaseline();
                PresentedInputGeneration = PresentedSourceCommit = 0;
                return;
            }
            if (result.Status == MapInputInstallStatus.Applied)
            {
                caveSource?.ApplyInput(batch); caveSource?.Flush();
                LastChangedChunkCount = result.Receipt.ChangedChunks.Count;
                LastRefreshRegionCount = result.Receipt.PageTargets.Count;
                RefreshBatchCount++;
            }
            else { LastChangedChunkCount = 0; LastRefreshRegionCount = 0; }
            if (batch.Kind == MapInputBatchKind.Baseline) awaitingBaseline = false;
            // 保留已有可见区域，普通 Delta 不重复 ShowRegion，也不覆盖 Interactive 优先级。
            VisualRevision++;
        }

        private void HideForBaseline()
        {
            awaitingBaseline = true; visible = default; drawing = false;
            controller.HideRegion(controller.Descriptor.Bounds);
            caveSource?.SetVisible(default);
            VisualRevision++;
        }

        private void UpdateVisible()
        {
            if (ViewCamera == null) return;
            var bounds = controller.Descriptor.Bounds;
            float halfH = ViewCamera.orthographicSize / Mathf.Abs(transform.lossyScale.y), halfW = halfH * ViewCamera.aspect;
            Vector3 p = transform.InverseTransformPoint(ViewCamera.transform.position);
            int page = controller.Descriptor.PageSize;
            int minU = Math.Max(bounds.MinU, Mathf.FloorToInt((p.x - halfW - 2) / page) * page);
            int minV = Math.Max(bounds.MinV, Mathf.FloorToInt((p.y - halfH - 2) / page) * page);
            int maxU = Math.Min((int)bounds.MaxUExclusive, Mathf.CeilToInt((p.x + halfW + 2) / page) * page);
            int maxV = Math.Min((int)bounds.MaxVExclusive, Mathf.CeilToInt((p.y + halfH + 2) / page) * page);
            if (maxU <= minU || maxV <= minV) return;
            var next = new GridBounds(minU, minV, maxU - minU, maxV - minV);
            if (visible.Equals(next)) return;
            if (visible.IsValid) HideDifference(visible, next);
            controller.ShowRegion(next); visible = next; caveSource?.SetVisible(next); VisualRevision++;
        }

        private void HideDifference(GridBounds area, GridBounds overlap)
        {
            int left = Math.Max(area.MinU, overlap.MinU), bottom = Math.Max(area.MinV, overlap.MinV);
            int right = (int)Math.Min(area.MaxUExclusive, overlap.MaxUExclusive), top = (int)Math.Min(area.MaxVExclusive, overlap.MaxVExclusive);
            if (left >= right || bottom >= top) { controller.HideRegion(area); return; }
            HideStrip(area.MinU, area.MinV, left - area.MinU, area.Height);
            HideStrip(right, area.MinV, (int)area.MaxUExclusive - right, area.Height);
            HideStrip(left, area.MinV, right - left, bottom - area.MinV);
            HideStrip(left, top, right - left, (int)area.MaxVExclusive - top);
        }
        private void HideStrip(int u, int v, int width, int height)
        { if (width > 0 && height > 0) controller.HideRegion(new GridBounds(u, v, width, height)); }

        private void EnsureCameraHooks()
        {
            if (cameraHooks) return;
            cameraHooks = true;
            Camera.onPreCull += BeginCamera; Camera.onPostRender += EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginPipelineCamera;
            RenderPipelineManager.endCameraRendering += EndPipelineCamera;
        }
        private void BeginPipelineCamera(ScriptableRenderContext context, Camera camera) => BeginCamera(camera);
        private void EndPipelineCamera(ScriptableRenderContext context, Camera camera) => EndCamera(camera);
        private void BeginCamera(Camera camera)
        {
            if (camera != ViewCamera) return;
            drawing = IsPresentationStable; drawingGeneration = InstalledInputGeneration;
            drawingCommit = InstalledSourceCommit; drawingVisualRevision = VisualRevision;
        }
        private void EndCamera(Camera camera)
        {
            if (camera != ViewCamera || !drawing) return;
            drawing = false;
            if (!IsPresentationStable || drawingVisualRevision != VisualRevision || drawingGeneration != InstalledInputGeneration ||
                drawingCommit != InstalledSourceCommit) return;
            PresentedInputGeneration = drawingGeneration; PresentedSourceCommit = drawingCommit;
        }
        private void Fail(Exception error) { LastError = error; Debug.LogException(error, this); }

        private async void OnDisable()
        {
            if (cameraHooks)
            {
                Camera.onPreCull -= BeginCamera; Camera.onPostRender -= EndCamera;
                RenderPipelineManager.beginCameraRendering -= BeginPipelineCamera;
                RenderPipelineManager.endCameraRendering -= EndPipelineCamera;
                cameraHooks = false;
            }
            if (inputSource != null) inputSource.InputChanged -= OnInputChanged;
            inputSource = null; replicaSource = null; drawing = false;
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            inputQueue.Clear(); loading = awaitingBaseline = false;
            var old = controller; controller = null; visible = default;
            caveSource?.Dispose(); caveSource = null;
            if (old != null) await old.DisposeAsync();
        }
    }
}
