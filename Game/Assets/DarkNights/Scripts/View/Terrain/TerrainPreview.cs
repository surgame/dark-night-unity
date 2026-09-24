using System;
using System.Collections.Generic;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Networking;
using AnyRules.Next.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>Editor 与正式会话共用的只读地形表现宿主；冻结输入在主线程原位安装，隐藏页休眠并随宿主释放。</summary>
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
        private bool localCoordinates, awaitingBaseline, loading, recoverableOverflowError;
        public int VisualRevision { get; private set; }
        public long BuiltPages => controller?.Renderer.CommittedBuilds ?? 0;
        public int BackgroundBuildCount => caveSource?.BackgroundBuildCount ?? 0;
        public int RockBuildCount => caveSource?.RockBuildCount ?? 0;
        public long BackgroundUploadedBytes => caveSource?.BackgroundUploadedBytes ?? 0;
        public int BackgroundResidentPages => caveSource?.BackgroundResidentPages ?? 0;
        public int LastChangedChunkCount { get; private set; }
        public int LastRefreshRegionCount { get; private set; }
        public long RefreshBatchCount { get; private set; }
        public ulong ReceivedInputGeneration => inputQueue.ReceivedGeneration;
        public ulong ReceivedSourceCommit => inputQueue.ReceivedCommit;
        public ulong InstalledInputGeneration { get; private set; }
        public ulong InstalledSourceCommit { get; private set; }
        public ulong PresentedInputGeneration { get; private set; }
        public ulong PresentedSourceCommit { get; private set; }
        public bool RefreshingReplica
        {
            get { return loading || awaitingBaseline || inputQueue.HasPending; }
        }
        public Exception LastError { get; private set; }
        public bool Ready => IsPresentationStable && PresentedInputGeneration == InstalledInputGeneration &&
            PresentedSourceCommit == InstalledSourceCommit;
        private bool IsPresentationStable => LastError == null && controller != null && !RefreshingReplica &&
            (caveSource?.BackgroundReady ?? true) && controller.Renderer.CommittedBuilds > 0 && !controller.HasPendingPresentationWork;

        public void NotifyReplicaChanged() => replicaSource?.NotifyChanged();
        public void NotifyReplicaChanged(MapReplicaChange transition) => replicaSource?.NotifyChanged(transition);

        public async void ShowReplica(AnyRules.Next.Authoring.ARDMapDefinition definition, IMapChunkSource source, WorldIdentity world,
            DarkNights.Core.Config.Terrain.BackgroundBakeDescriptor reference = null)
        {
            if (lifetime != null) throw new InvalidOperationException("每个预览只接收一份世界。");
            replicaSource = source as TerrainReplicaSource; inputSource = source as ITerrainInputSource;
            localCoordinates = true; loading = true;
            var own = lifetime = new CancellationTokenSource();
            if (inputSource != null) inputSource.InputChanged += OnInputChanged;
            try
            {
                var catalog = definition.LoadGameplayCatalog();
                IMapChunkSource mapSource = source;
                if (CaveStyle != null)
                { caveSource = new CaveVisualSource(source, CaveStyle, catalog.Tiles, transform, reference); mapSource = caveSource; }
                var result = await ARDMapController.CreateAsync(definition, PreviewOptions(mapSource, world, CaveProfile()), own.Token);
                if (own.IsCancellationRequested) { await result.DisposeAsync(); return; }
                controller = result;
                await result.LoadRegionAsync(result.Descriptor.Bounds, own.Token);
                if (own.IsCancellationRequested) return;
                inputSource?.PublishInitialBaseline();
                caveSource?.Flush();
                loading = false; UpdateVisible(); VisualRevision++;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { loading = false; LastError = error; Debug.LogException(error, this); }
        }

        public void SetMinerals(IReadOnlyList<DarkNights.Core.ViewData.WorksiteViewData> deposits)
        { caveSource?.SetMinerals(deposits); caveSource?.Flush(); }
        public void SetDevices(DarkNights.Core.ViewData.WorldViewData world)
        { caveSource?.SetDevices(world); caveSource?.Flush(); }

        private void OnEnable()
        {
            Camera.onPostRender += OnCameraPostRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            if (Map == null || ViewCamera == null) return;
            if (CaveStyle == null) CaveStyle = Map.CaveStyle;
            ShowBlueprint(Map.Definition, Map.ReadBlueprint());
        }

        public async void ShowBlueprint(AnyRules.Next.Authoring.ARDMapDefinition definition,
            DarkNights.Core.Config.Terrain.TerrainBlueprint blueprint, IMapChunkSource input = null,
            DarkNights.Core.Config.Terrain.BackgroundBakeDescriptor reference = null)
        {
            if (lifetime != null) throw new InvalidOperationException("每个预览只接收一份蓝图；重新生成须替换预览实例。");
            localCoordinates = true; loading = true;
            var own = lifetime = new CancellationTokenSource();
            try
            {
                var catalog = definition.LoadGameplayCatalog();
                input = input ?? new TerrainBlueprintSource(blueprint, catalog.Tiles);
                inputSource = input as ITerrainInputSource;
                if (inputSource != null) inputSource.InputChanged += OnInputChanged;
                IMapChunkSource mapSource = input;
                if (CaveStyle != null)
                {
                    reference = reference ?? new DarkNights.Core.Config.Terrain.BackgroundBakeDescriptor(Guid.NewGuid().ToString("N"),
                        blueprint.Settings.Seed, blueprint.CopyMaterials(), blueprint.CopyShapes());
                    caveSource = new CaveVisualSource(input, CaveStyle, catalog.Tiles, transform, reference); mapSource = caveSource;
                }
                var result = await ARDMapController.CreateAsync(definition, PreviewOptions(mapSource, null, CaveProfile()), own.Token);
                if (own.IsCancellationRequested) { await result.DisposeAsync(); return; }
                controller = result;
                await result.LoadRegionAsync(result.Descriptor.Bounds, own.Token);
                if (own.IsCancellationRequested) return;
                inputSource?.PublishInitialBaseline();
                caveSource?.Flush();
                loading = false; UpdateVisible(); VisualRevision++;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { loading = false; LastError = error; Debug.LogException(error, this); }
        }

        private MapOptions PreviewOptions(IMapChunkSource source, WorldIdentity? world, RenderProfile profile)
        {
            bool sourceDriven = source is CaveVisualSource cave ? cave.IsSourceDriven : source is ITerrainInputSource;
            var layers = sourceDriven ? new GridLayerConfiguration(GridEditability.ReadOnly, GridRenderPolicy.LiveRules,
                GridBusinessCapability.TileTypeOnly) : null;
            return new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false, maximumInitializationCells: 131072,
                chunkSource: source, parent: null, world: world, layers: layers, sourceDrivenInputs: sourceDriven, profile: profile);
        }

        private RenderProfile CaveProfile() => caveSource == null ? null : new RenderProfile(defaultMaterial: caveSource.Material);
        private void Update() => TickPresentation();
        /// <summary>供离屏 Editor 宿主驱动与运行时 Update 相同的输入安装、规则求解和动态岩壁链。</summary>
        public void TickFromEditor() => TickPresentation();

        private void TickPresentation()
        {
            int rockBefore = RockBuildCount, backgroundBefore = BackgroundBuildCount;
            long pagesBefore = BuiltPages;
            try { caveSource?.TickBackground(); }
            catch (Exception error) { LastError = error; Debug.LogException(error, this); }
            if (rockBefore != RockBuildCount || backgroundBefore != BackgroundBuildCount) VisualRevision++;
            if (controller == null || LastError != null || loading) return;
            HandleInputOverflow(); ProcessOneInputBatch();
            if (!awaitingBaseline) UpdateVisible();
            if (controller.HasPendingPresentationWork) controller.Tick();
            if (pagesBefore != BuiltPages) VisualRevision++;
        }

        private void OnCameraPostRender(Camera camera)
        { if (camera == ViewCamera) MarkPresented(); }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        { if (camera == ViewCamera) MarkPresented(); }

        private void MarkPresented()
        {
            if (!IsPresentationStable) return;
            PresentedInputGeneration = InstalledInputGeneration; PresentedSourceCommit = InstalledSourceCommit;
        }

        private void OnInputChanged(MapInputBatch batch)
        {
            if (batch == null || lifetime == null || lifetime.IsCancellationRequested) return;
            if (batch.Kind == MapInputBatchKind.Baseline && recoverableOverflowError)
            { LastError = null; recoverableOverflowError = false; }
            inputQueue.Enqueue(batch);
        }

        private void HandleInputOverflow()
        {
            if (!inputQueue.TakeOverflow()) return;
            awaitingBaseline = true; visible = default;
            controller.HideRegion(controller.Descriptor.Bounds);
            caveSource?.SetVisible(default);
            VisualRevision++;
            LastError = new InvalidOperationException("地形输入队列超限，已撤销旧画面的显示资格；等待完整基线恢复。");
            recoverableOverflowError = true;
            try { inputSource?.PublishInitialBaseline(); }
            catch (Exception error) { LastError = error; recoverableOverflowError = false; }
        }

        private void ProcessOneInputBatch()
        {
            if (LastError != null) return;
            if (!inputQueue.TryDequeue(out var batch)) return;
            try
            {
                if (awaitingBaseline && batch.Kind != MapInputBatchKind.Baseline)
                    throw new InvalidOperationException("地形源正在等待完整基线，普通增量不能恢复显示资格。");
                var result = controller.InstallSourceInput(batch);
                InstalledInputGeneration = result.InputGeneration; InstalledSourceCommit = result.SourceCommit;
                if (IsLifecycle(batch.Kind))
                {
                    awaitingBaseline = true; visible = default; PresentedInputGeneration = 0; PresentedSourceCommit = 0;
                    controller.HideRegion(controller.Descriptor.Bounds); caveSource?.SetVisible(default);
                    VisualRevision++;
                }
                else if (result.Status == MapInputInstallStatus.NoChange)
                {
                    LastChangedChunkCount = 0; LastRefreshRegionCount = 0;
                    if (batch.Kind == MapInputBatchKind.Baseline)
                    { awaitingBaseline = false; visible = default; VisualRevision++; }
                    else if (IsPresentationStable)
                    { PresentedInputGeneration = InstalledInputGeneration; PresentedSourceCommit = InstalledSourceCommit; }
                }
                else
                {
                    caveSource?.ApplyInput(batch); caveSource?.Flush();
                    awaitingBaseline = false; visible = default;
                    LastChangedChunkCount = CountChangedChunks(batch, controller.Descriptor.ChunkSize);
                    LastRefreshRegionCount = LastChangedChunkCount;
                    RefreshBatchCount++;
                    VisualRevision++;
                    if (batch.Kind == MapInputBatchKind.Baseline) awaitingBaseline = false;
                }
            }
            catch (Exception error) { LastError = error; Debug.LogException(error, this); }
        }

        private static int CountChangedChunks(MapInputBatch batch, int chunkSize)
        {
            var chunks = new HashSet<ChunkCoord>();
            foreach (var snapshot in batch.SnapshotChunks) chunks.Add(snapshot.Coordinate);
            foreach (var cell in batch.Cells) chunks.Add(GridMath.ChunkOf(cell.Position, chunkSize));
            return chunks.Count;
        }

        private static bool IsLifecycle(MapInputBatchKind kind) => kind == MapInputBatchKind.Reset ||
            kind == MapInputBatchKind.VisibilityRevoked || kind == MapInputBatchKind.Disconnected;

        private void UpdateVisible()
        {
            var bounds = controller.Descriptor.Bounds;
            float halfH = ViewCamera.orthographicSize / (localCoordinates ? transform.lossyScale.y : 1), halfW = halfH * ViewCamera.aspect;
            Vector3 p = localCoordinates ? transform.InverseTransformPoint(ViewCamera.transform.position) : ViewCamera.transform.position;
            int page = controller.Descriptor.PageSize;
            int minU = Math.Max(bounds.MinU, Mathf.FloorToInt((p.x - halfW - 2) / page) * page);
            int minV = Math.Max(bounds.MinV, Mathf.FloorToInt((p.y - halfH - 2) / page) * page);
            int maxU = Math.Min((int)bounds.MaxUExclusive, Mathf.CeilToInt((p.x + halfW + 2) / page) * page);
            int maxV = Math.Min((int)bounds.MaxVExclusive, Mathf.CeilToInt((p.y + halfH + 2) / page) * page);
            if (maxU <= minU || maxV <= minV) return;
            var next = new GridBounds(minU, minV, maxU - minU, maxV - minV);
            if (visible.Equals(next)) return;
            if (visible.IsValid) HideDifference(visible, next);
            controller.ShowRegion(next); visible = next; caveSource?.SetVisible(next);
        }

        private void HideDifference(GridBounds area, GridBounds overlap)
        {
            int left = Math.Max(area.MinU, overlap.MinU), bottom = Math.Max(area.MinV, overlap.MinV);
            int right = (int)Math.Min(area.MaxUExclusive, overlap.MaxUExclusive);
            int top = (int)Math.Min(area.MaxVExclusive, overlap.MaxVExclusive);
            if (!overlap.IsValid || left >= right || bottom >= top) { controller.HideRegion(area); return; }
            HideStrip(area.MinU, area.MinV, left - area.MinU, area.Height);
            HideStrip(right, area.MinV, (int)area.MaxUExclusive - right, area.Height);
            HideStrip(left, area.MinV, right - left, bottom - area.MinV);
            HideStrip(left, top, right - left, (int)area.MaxVExclusive - top);
        }

        private void HideStrip(int u, int v, int width, int height)
        { if (width > 0 && height > 0) controller.HideRegion(new GridBounds(u, v, width, height)); }

        private async void OnDisable()
        {
            Camera.onPostRender -= OnCameraPostRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            if (inputSource != null) { inputSource.InputChanged -= OnInputChanged; inputSource = null; }
            replicaSource = null;
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            inputQueue.Clear();
            loading = false; awaitingBaseline = false;
            var old = controller; controller = null; visible = default;
            if (old != null) await old.DisposeAsync();
            caveSource?.Dispose(); caveSource = null;
        }
    }
}
