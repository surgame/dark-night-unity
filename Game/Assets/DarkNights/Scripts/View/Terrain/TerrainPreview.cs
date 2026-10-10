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
        private CaveVisualSource caveSource;
        public CaveVisualSource LightingSource => awaitingBaseline ? null : caveSource;
        public GridBounds LightingLoadedBounds => regionLoader?.Loaded ?? controller?.Descriptor.Bounds ?? default;
        private MineralLayerPresentation minerals;
        private MineralReplicaPresentation mineralReplica;
        public bool UseMineralReplica;
        private ARDMapController controller;
        private CancellationTokenSource lifetime;
        private TerrainReplicaSource replicaSource;
        private TerrainReplicaRegion regionLoader;
        private TerrainDrawReceipt drawReceipt;
        public GridBounds LocalRegion;
        private ITerrainInputSource inputSource;
        private readonly TerrainInputBatchQueue inputQueue = new TerrainInputBatchQueue();
        private GridBounds visible;
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
            replicaSource?.WaitingForBaseline == true || inputQueue.HasPending;
        public Exception LastError { get; private set; }
        private bool IsPresentationStable => LastError == null && controller != null && !RefreshingReplica &&
            (caveSource?.BackgroundReady ?? true) && !controller.HasPendingPresentationWork;
        public bool Ready => IsPresentationStable && PresentedInputGeneration == InstalledInputGeneration &&
            PresentedSourceCommit == InstalledSourceCommit && (minerals?.Ready ?? true) && (!UseMineralReplica || mineralReplica?.Ready == true);
        /// <summary>只读绘制请求；不会推进 Ready 或跳过真实相机回执。</summary>
        public bool NeedsPresentationDraw => IsPresentationStable && !Ready;
        /// <summary>诊断等待阶段，不参与网络授权或调度。</summary>
        public string PresentationWaitReason
        {
            get
            {
                if (LastError != null) return "错误：" + LastError.Message;
                if (controller == null || loading) return "初始地图装载";
                if (regionLoader?.Loading == true || replicaSource?.WaitingForBaseline == true) return "等待局部区域完整基线";
                if (awaitingBaseline) return "等待完整基线";
                if (inputQueue.HasPending) return "待安装变化格";
                if (controller.HasPendingPresentationWork) return "Dual Grid 规则/资源处理中";
                if (!(caveSource?.BackgroundReady ?? true)) return "岩壁/背景烘焙中（" + RefreshPath + "）";
                if (UseMineralReplica && mineralReplica?.Ready != true) return "矿层：" + mineralReplica?.WaitReason;
                return Ready ? "已绘制" : "等待相机完成回执";
            }
        }

        public void NotifyReplicaChanged() => replicaSource?.NotifyChanged();
        public void NotifyReplicaChanged(MapReplicaChange transition)
        { if (!loading && regionLoader?.Loading != true) replicaSource?.NotifyChanged(transition); }
        public void SetReplicaRegion(ChunkReplicaStateMachine replica, GridBounds region, bool ready)
        {
            regionLoader?.Present(replica, region, ready);
            if (regionLoader?.LastError != null) Fail(regionLoader.LastError);
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
                    caveSource = new CaveVisualSource(source, CaveStyle, definition.LoadGameplayCatalog().Tiles, transform, reference, SurfaceSky);
                    mapSource = caveSource;
                }
                var profile = caveSource == null ? null : new RenderProfile(defaultMaterial: caveSource.Material);
                var layers = inputSource == null ? null : new GridLayerConfiguration(GridEditability.ReadOnly,
                    GridRenderPolicy.LiveRules, GridBusinessCapability.TileTypeOnly);
                var options = new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false,
                    maximumInitializationCells: 131072, chunkSource: mapSource, parent: transform, world: world,
                    sourceDrivenInputs: inputSource != null, layers: layers, profile: profile,
                    scheduling: new RenderSchedulingOptions(lagPolicy: RenderLagPolicy.BoundedLag));
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
                        caveSource?.LightTexture, CaveStyle.Background?.BackgroundAmbient ?? .36f, MineralLayerView.DefaultSortingOrder);
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
        public void TickFromEditor() { EnsureCameraHooks(); TickPresentation(); }

        private void TickPresentation()
        {
            minerals?.Tick();
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
            VisualRevision++;
        }
        private void HideForBaseline()
        {
            awaitingBaseline = true; visible = default; drawReceipt?.Invalidate();
            controller.HideRegion(controller.Descriptor.Bounds);
            caveSource?.SetVisible(default);
            VisualRevision++;
        }
        private void UpdateVisible()
        {
            if (ViewCamera == null) return;
            var next = TerrainViewport.Capture(controller.Descriptor, transform, ViewCamera);
            next = TerrainViewport.Limit(next, regionLoader?.Loaded ?? controller.Descriptor.Bounds, controller.Descriptor);
            if (visible.Equals(next)) return;
            TerrainViewport.HideDifference(controller, visible, next);
            if (next.IsValid) controller.ShowRegion(next);
            visible = next; caveSource?.SetVisible(next); VisualRevision++;
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
            var old = controller; controller = null; visible = default;
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
