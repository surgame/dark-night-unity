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
namespace DarkNights.View.Terrain
{
    /// <summary>Editor 与运行时共用的只读地形宿主；先安装冻结输入，再求解表现，相机绘制后确认当前游标。</summary>
    public sealed class TerrainPreview : MonoBehaviour
    {
        public TerrainMapAsset Map;
        public CaveTerrainStyle CaveStyle;
        public Camera ViewCamera;
        /// <summary>航程地表启用天际线透明背景；独立洞穴工作台保留其完整背景预览。</summary>
        public bool SurfaceSky;
        public bool DeferEditorBackground { get; set; }
        private CaveVisualSource caveSource;
        private Expedition.SurfaceScenery scenery;
        public CaveVisualSource LightingSource => awaitingBaseline ? null : caveSource;
        public GridBounds LightingLoadedBounds => regionLoader?.Loaded ?? controller?.Descriptor.Bounds ?? default;
        private MineralLayerPresentation minerals;
        private long observedMineralPages;
        private MineralReplicaPresentation mineralReplica;
        public bool UseMineralReplica;
        private ARDMapController controller;
        private CancellationTokenSource lifetime;
        private TerrainReplicaSource replicaSource;
        private TerrainReplicaRegion regionLoader;
        private TerrainDrawReceipt drawReceipt;
        public GridBounds LocalRegion;
        /// <summary>局部 Editor 预览限定静态背景求解范围；正式世界与整图地形工作台默认关闭。</summary>
        public bool LocalPreviewBackground;
        private ITerrainInputSource inputSource;
        private readonly TerrainInputBatchQueue inputQueue = new TerrainInputBatchQueue();
        private GridBounds visible, requested;
        private bool awaitingBaseline, loading;
        public int VisualRevision { get; private set; }
        public long BuiltPages => controller?.Renderer?.CommittedBuilds ?? 0;
        public int LoadedTerrainChunks => controller?.LoadedChunkCount ?? 0;
        public int BackgroundBuildCount => caveSource?.BackgroundBuildCount ?? 0;
        public long MineralInputBatches => mineralReplica?.InputBatches ?? minerals?.InputBatches ?? 0;
        public long MineralBuiltPages => mineralReplica?.BuiltPages ?? minerals?.BuiltPages ?? 0;
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
        public bool RefreshingReplica => loading || regionLoader?.Loading == true || awaitingBaseline ||
            replicaSource?.WaitingForBaseline == true || replicaSource?.ProjectionRefreshPending == true || inputQueue.HasPending;
        public Exception LastError { get; private set; }
        private bool IsInputStable => LastError == null && controller != null && !RefreshingReplica &&
            !controller.HasPendingPresentationWork;
        public bool MineralsReady => (minerals?.Ready ?? true) && (!UseMineralReplica || mineralReplica?.Ready == true);
        private bool IsPresentationStable => IsInputStable && (caveSource?.BackgroundReady ?? true);
        public bool ForegroundStable => IsInputStable && (caveSource?.ForegroundReady ?? true);
        public bool Ready => IsPresentationStable && drawReceipt?.IsCurrent == true && MineralsReady;
        /// <summary>只读绘制请求；不会推进 Ready 或跳过真实相机回执。</summary>
        public bool NeedsPresentationDraw => IsPresentationStable && !Ready;
        public TerrainPreviewProgress Progress => new TerrainPreviewProgress(LastError, controller, regionLoader, replicaSource,
            inputQueue, caveSource, minerals, mineralReplica, visible, loading, awaitingBaseline, UseMineralReplica, Ready);
        public string PresentationWaitReason => Progress.Message;

        /// <summary>显式作者预览更新；环境单独调参可跳过背景替换，运行中的挖掘与恢复不使用此入口。</summary>
        public void ReplaceAuthoringReference(BackgroundBakeDescriptor reference, CaveTerrainStyle style, bool replaceBackground = true)
        {
            if (replaceBackground) caveSource?.ReplaceAuthoringReference(reference);
            scenery?.Dispose();
            scenery = SurfaceSky ? new Expedition.SurfaceScenery(transform, style.SurfaceEnvironment, reference) : null;
            VisualRevision++;
        }

        public void NotifyReplicaChanged() => replicaSource?.NotifyChanged();
        /// <summary>正式联机投影变更保留到局部基线就绪；不改变工作台即时刷新合同。</summary>
        public void RequestReplicaProjectionRefresh() => replicaSource?.RequestProjectionRefresh();
        public void NotifyReplicaChanged(MapReplicaChange transition)
        { if (!loading && regionLoader?.Loading != true) replicaSource?.NotifyChanged(transition); }
        public void SetReplicaRegion(ChunkReplicaStateMachine replica, GridBounds region, bool ready)
        {
            regionLoader?.Present(replica, region, ready);
            if (regionLoader?.LastError != null) Fail(regionLoader.LastError);
            if (ready && !loading && regionLoader?.IsCurrent(replica, region) == true)
                replicaSource?.TryRefreshProjection();
        }
        /// <summary>正式场景与地图工作台共用的完整洞穴表现入口；样式决定前景、背景及各装饰层。</summary>
        public void ShowCaveReplica(ARDMapDefinition definition, CaveTerrainStyle style, IMapChunkSource source,
            WorldIdentity world, BackgroundBakeDescriptor reference)
        {
            if (style == null || reference == null)
                throw new InvalidOperationException("完整洞穴表现缺少样式或冻结背景参考。");
            CaveStyle = style;
            ShowReplica(definition, source, world, reference);
        }
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
                    caveSource = new CaveVisualSource(source, CaveStyle, definition.LoadGameplayCatalog().Tiles, transform, reference, SurfaceSky,
                        LocalPreviewBackground ? LocalRegion : default, DeferEditorBackground && !Application.isPlaying);
                    mapSource = caveSource;
                    if (SurfaceSky) scenery = new Expedition.SurfaceScenery(transform, CaveStyle.SurfaceEnvironment, reference);
                }
                var profile = caveSource == null ? null : new RenderProfile(defaultMaterial: caveSource.Material);
                var layers = inputSource == null && !LocalRegion.IsValid ? null : new GridLayerConfiguration(GridEditability.ReadOnly,
                    GridRenderPolicy.LiveRules, GridBusinessCapability.TileTypeOnly);
                var options = new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false,
                    maximumInitializationCells: 131072, chunkSource: mapSource, parent: transform, world: world,
                    sourceDrivenInputs: inputSource != null, layers: layers, profile: profile,
                    scheduling: new RenderSchedulingOptions(maximumSnapshotAgeMilliseconds: LocalPreviewBackground ? 10000 : 100, lagPolicy: RenderLagPolicy.BoundedLag));
                var result = await ARDMapController.CreateAsync(definition, options, own.Token);
                if (own.IsCancellationRequested) { await result.DisposeAsync(); return; }
                controller = result;
#if UNITY_EDITOR
                result.Root.name = "前景地形 · " + result.World.WorldId;
                result.Root.AddComponent<GridDebugView>().Bind(result.Debugger);
#endif
                inputQueue.Configure(result.Descriptor);
                regionLoader = new TerrainReplicaRegion(result, replicaSource, own.Token);
                await regionLoader.Initialize(LocalRegion.IsValid ? LocalRegion : result.Descriptor.Bounds, replicaSource?.Replica);
                if (own.IsCancellationRequested) return;
                if (!UseMineralReplica && CaveStyle?.MineralDefinition != null)
                {
                    minerals = new MineralLayerPresentation(CaveStyle.MineralDefinition, transform);
                    await minerals.OpenAsync(CaveStyle.MineralDefinition, ViewCamera, result.Descriptor.World,
                        caveSource?.LightTexture, CaveStyle.Background?.BackgroundAmbient ?? .36f, MineralLayerView.DefaultSortingOrder,
                        LocalRegion.IsValid ? (GridBounds?)LocalRegion : null);
                    if (own.IsCancellationRequested) return;
                }
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
        public void SetMinerals(IReadOnlyList<DarkNights.Core.ViewData.MineralDepositViewData> deposits)
        { minerals?.Replace(deposits); }
        public void SetMineralReplica(ChunkReplicaStateMachine replica, GridBounds region, bool dataReady)
        {
            if (controller == null || CaveStyle?.MineralDefinition == null) return;
            mineralReplica = mineralReplica ?? new MineralReplicaPresentation(transform, CaveStyle.MineralDefinition, ViewCamera,
                caveSource?.LightTexture, CaveStyle.Background?.BackgroundAmbient ?? .36f);
            mineralReplica.Present(replica, region, dataReady);
            if (mineralReplica.LastError != null) Fail(mineralReplica.LastError);
        }
        public void SetDevices(DarkNights.Core.ViewData.WorldViewData world)
        { caveSource?.SetDevices(world); caveSource?.Flush(); }
        private void Update() => TickPresentation();
        /// <summary>离屏窗口显式驱动同一条链；不依赖编辑态 MonoBehaviour.Update 自动运行。</summary>
        public void TickFromEditor(int maximumBackgroundPages = 1) { EnsureCameraHooks(); TickPresentation(maximumBackgroundPages); }
        private void TickPresentation(int maximumBackgroundPages = 1)
        {
            minerals?.Tick();
            long mineralPages = MineralBuiltPages;
            if (mineralPages != observedMineralPages) { observedMineralPages = mineralPages; VisualRevision++; }
            if (minerals?.LastError != null) { Fail(minerals.LastError); return; }
            if (controller == null || loading || regionLoader?.Loading == true || LastError != null) return;
            try
            {
                // 新输入先更新依赖；工作结果不允许覆盖已经显示过的更高版本。
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
                caveSource?.TickBackground(maximumBackgroundPages);
                if (scenery?.Render(ViewCamera, caveSource, !RefreshingReplica, LightingLoadedBounds) == true) VisualRevision++;
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
            VisualRevision++;
        }
        private void HideForBaseline()
        {
            awaitingBaseline = true; visible = requested = default; drawReceipt?.Invalidate();
            controller.HideRegion(controller.Descriptor.Bounds);
            caveSource?.SetVisible(default);
            VisualRevision++;
        }
        private void UpdateVisible()
        {
            if (ViewCamera == null) return;
            if (TerrainViewport.Present(controller, transform, ViewCamera,
                regionLoader?.Loaded ?? controller.Descriptor.Bounds, caveSource, ref visible, ref requested, DeferEditorBackground)) VisualRevision++;
        }
        private void EnsureCameraHooks()
        {
            drawReceipt = drawReceipt ?? new TerrainDrawReceipt(() => ViewCamera, () => IsPresentationStable,
                () => (InstalledInputGeneration, InstalledSourceCommit, VisualRevision),
                (generation, commit) => { PresentedInputGeneration = generation; PresentedSourceCommit = commit; });
        }
        private void Fail(Exception error) { LastError = error; Debug.LogException(error, this); }
        public Task Retirement { get; private set; } = Task.CompletedTask;
        private void OnDisable() => Retirement = RetireAsync();
        private async Task RetireAsync()
        {
            drawReceipt?.Dispose(); drawReceipt = null;
            if (inputSource != null) inputSource.InputChanged -= OnInputChanged;
            inputSource = null; replicaSource = null; regionLoader = null;
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            inputQueue.Clear(); loading = awaitingBaseline = false;
            scenery?.Dispose(); scenery = null;
            var old = controller; controller = null; visible = requested = default;
            var oldMinerals = minerals; minerals = null;
            var oldReplica = mineralReplica; mineralReplica = null;
            var oldSource = caveSource; caveSource = null;
            if (oldMinerals != null) await oldMinerals.RetireAsync();
            if (oldReplica != null) await oldReplica.RetireAsync();
            oldSource?.Dispose();
            if (old != null) await old.DisposeAsync();
        }
    }
}
