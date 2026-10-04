using System;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>独立矿层的薄原生宿主；复用 AnyRuleD 页面与资源服务，只有当前输入实际绘制后才 Ready，退场取消旧工作且不释放共享光场。</summary>
    public sealed class MineralLayerView : MonoBehaviour
    {
        public const int DefaultSortingOrder = -88;
        private readonly TerrainInputBatchQueue queue = new TerrainInputBatchQueue();
        private ARDMapController controller;
        private DirectAssetService assets;
        private Material material;
        private MineralLayerInputSource source;
        private Camera sceneCamera;
        private CancellationTokenSource lifetime;
        private GridBounds visible;
        private ulong installed, presented, drawingCommit;
        private bool loading, baseline, drawing, hooked, hasPresented;
        private bool retiring;
        private Task opening = Task.CompletedTask;
        private int revision, drawingRevision;
        public Exception LastError { get; private set; }
        public long BuiltPages => controller?.Renderer?.CommittedBuilds ?? 0;
        public long InputBatches { get; private set; }
        public ulong InstalledCommit => installed;
        public ulong PresentedCommit => presented;
        public Task Retirement { get; private set; } = Task.CompletedTask;
        private bool Stable => LastError == null && controller != null && !loading && baseline &&
            !queue.HasPending && BuiltPages > 0 && !controller.HasPendingPresentationWork;
        public bool Ready => Stable && hasPresented && presented == installed && !drawing;
        public bool NeedsPresentationDraw => Stable && !Ready;

        public Task OpenAsync(ARDMapDefinition definition, MineralLayerInputSource input, Camera viewCamera,
            WorldIdentity world, ulong seed = 42, Texture lights = null, float ambient = .36f, int sortingOrder = DefaultSortingOrder)
        {
            if (lifetime != null || retiring) throw new InvalidOperationException("矿层宿主已初始化或已退场。");
            return opening = OpenCoreAsync(definition, input, viewCamera, world, seed, lights, ambient, sortingOrder);
        }

        private async Task OpenCoreAsync(ARDMapDefinition definition, MineralLayerInputSource input, Camera viewCamera,
            WorldIdentity world, ulong seed, Texture lights, float ambient, int sortingOrder)
        {
            if (lifetime != null || definition == null || input == null || viewCamera == null)
                throw new InvalidOperationException("矿层宿主只能初始化一次，且必须有定义、冻结输入和相机。");
            var own = lifetime = new CancellationTokenSource();
            var token = own.Token;
            LastError = null; installed = presented = 0; visible = default; baseline = false; hasPresented = false; InputBatches = 0;
            loading = true; source = input; sceneCamera = viewCamera;
            Hook(); source.InputChanged += Changed;
            try
            {
                var shader = Shader.Find("DarkNights/CaveBackgroundLayer");
                if (shader == null) throw new InvalidOperationException("缺少矿层兼容的原生 Sprite 材质。");
                material = new Material(shader) { name = "Embedded mineral layer" };
                material.SetTexture("_CaveLight", lights ?? Texture2D.blackTexture);
                material.SetFloat("_Ambient", ambient);
                material.SetMatrix("_MapWorldToLocal", transform.worldToLocalMatrix);
                assets = DirectAssetService.FromBindings(definition.VisualAssets);
                var descriptor = definition.Describe(world, seed, layer: 1);
                var options = new MapOptions(initialize: false, showOnCreate: false, autoUpdate: false,
                    maximumInitializationCells: 131072,
                    sourceDrivenInputs: true, layers: new GridLayerConfiguration(GridEditability.ReadOnly,
                        GridRenderPolicy.LiveRules, GridBusinessCapability.TileTypeOnly),
                    profile: new RenderProfile(definition.CellSize, sortingOrder: sortingOrder, defaultMaterial: material),
                    parent: transform, chunkSource: source, assets: assets);
                var next = await ARDMapController.CreateAsync(descriptor, definition.LoadRuntimeCatalog(), options, token);
                if (own != lifetime || token.IsCancellationRequested) { await next.DisposeAsync(); return; }
                controller = next; queue.Configure(descriptor);
                await next.LoadRegionAsync(descriptor.Bounds, token);
                if (own != lifetime || token.IsCancellationRequested) return;
                source.PublishInitialBaseline(); loading = false;
                Tick();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (own == lifetime) Fail(error); }
        }

        private void Changed(MapInputBatch batch)
        {
            if (lifetime == null || lifetime.IsCancellationRequested) return;
            try { queue.Enqueue(batch); }
            catch (Exception error) { Fail(error); }
        }

        private void Update() => Tick();
        public void Tick()
        {
            if (controller == null || loading || LastError != null) return;
            try
            {
                if (queue.TryDequeue(out var batch))
                {
                    if (!baseline && batch.Kind != MapInputBatchKind.Baseline) throw new InvalidOperationException("矿层必须先安装完整基线。");
                    var result = controller.InstallSourceInput(batch);
                    installed = result.SourceCommit; baseline |= batch.Kind == MapInputBatchKind.Baseline;
                    InputBatches++; revision++; hasPresented = false;
                }
                Visibility(); if (controller.HasPendingPresentationWork) controller.Tick();
            }
            catch (Exception error) { Fail(error); }
        }

        private void Visibility()
        {
            if (sceneCamera == null || !baseline) return;
            var next = TerrainViewport.Capture(controller.Descriptor, transform, sceneCamera);
            if (next.Equals(visible)) return;
            TerrainViewport.HideDifference(controller, visible, next);
            if (next.IsValid) controller.ShowRegion(next);
            visible = next; revision++; hasPresented = false;
        }

        private void Hook()
        {
            hooked = true;
            Camera.onPreCull += Begin; Camera.onPostRender += End;
            RenderPipelineManager.beginCameraRendering += BeginPipeline;
            RenderPipelineManager.endCameraRendering += EndPipeline;
        }
        private void BeginPipeline(ScriptableRenderContext context, Camera value) => Begin(value);
        private void EndPipeline(ScriptableRenderContext context, Camera value) => End(value);
        private void Begin(Camera value)
        {
            if (value != sceneCamera) return;
            drawing = Stable; drawingCommit = installed; drawingRevision = revision;
        }
        private void End(Camera value)
        {
            if (value != sceneCamera || !drawing) return;
            drawing = false;
            if (Stable && drawingCommit == installed && drawingRevision == revision) { presented = drawingCommit; hasPresented = true; }
        }
        private void Fail(Exception error) { LastError = error; loading = false; Debug.LogException(error, this); }

        private void OnDisable() => Retirement = RetireAsync();
        public Task RetireAsync()
        {
            if (retiring) return Retirement;
            retiring = true;
            return Retirement = RetireCoreAsync();
        }

        private async Task RetireCoreAsync()
        {
            if (hooked)
            {
                Camera.onPreCull -= Begin; Camera.onPostRender -= End;
                RenderPipelineManager.beginCameraRendering -= BeginPipeline;
                RenderPipelineManager.endCameraRendering -= EndPipeline; hooked = false;
            }
            if (source != null) source.InputChanged -= Changed;
            source = null; sceneCamera = null; drawing = false; baseline = false; hasPresented = false; visible = default; loading = false;
            lifetime?.Cancel(); queue.Clear();
            await opening;
            lifetime?.Dispose(); lifetime = null;
            var old = controller; controller = null;
            var ownedAssets = assets; assets = null;
            var ownedMaterial = material; material = null;
            try { if (old != null) await old.DisposeAsync(); }
            finally
            {
                ownedAssets?.Dispose();
                if (ownedMaterial != null)
                {
                    if (Application.isPlaying) Destroy(ownedMaterial);
                    else DestroyImmediate(ownedMaterial);
                }
            }
        }
    }
}
