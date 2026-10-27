using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Unity;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Config.Expedition;
using DarkNights.View.Expedition;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>Map Design Workbench 的地图专用离屏宿主；临时空场景仅容纳相机与正式 TerrainPreview。</summary>
    public sealed class TerrainStylePreviewStage : IDisposable
    {
        private const int PixelWidth = 2560, PixelHeight = 1536;
        private readonly int outputWidth, outputHeight;
        private readonly bool terrainFirst;
        private TerrainDrawReceipt foregroundReceipt;
        private Scene scene;
        private GameObject root;
        private Camera camera;
        private TerrainPreview terrain;
        private CaveTerrainStyle transientStyle;
        private CaveBackgroundStyle transientBackground;
        private RenderTexture target;
        private JourneySurfaceBackdrop environment;
        private Material environmentMaterial;
        private PlanetDefinition planet;
        private bool cameraChanged;
        private IReadOnlyList<DarkNights.Core.ViewData.MineralDepositViewData> pendingMinerals;
        public bool SurfaceCameraActive { get; private set; }
        private int renderedRevision = -1;
        private ulong renderedGeneration, renderedCommit;
        private bool lastDrawCouldConfirm;
        public int LastCameraMilliseconds { get; private set; }
        public string Progress => terrain == null ? "尚未创建预览" :
            terrainFirst && terrain.ForegroundStable && !TerrainReady ? "矿层构建／地形首帧确认" : terrain.PresentationWaitReason;
        public string RefreshPath => terrain == null ? "未选择" : terrain.RefreshPath;
        public Texture Image => target;
        internal Vector3 ImageCameraPosition { get; private set; }
        internal float ImageCameraSize { get; private set; } = 96;
        public TerrainBlueprintSource Source { get; private set; }
        public Exception Error => terrain != null ? terrain.LastError : null;
        public bool Ready => terrain != null && terrain.Ready && pendingMinerals == null;
        public bool TerrainReady => !terrainFirst ? Ready : terrain != null && terrain.ForegroundStable && terrain.MineralsReady &&
            pendingMinerals == null && foregroundReceipt?.IsCurrent == true;
        internal bool CanReplaceCandidate => Source != null && terrain != null &&
            terrain.InstalledInputGeneration != 0 && terrain.LastError == null;
        public int RockBuildCount => terrain != null ? terrain.RockBuildCount : 0;
        public int LastChangedChunkCount => terrain != null ? terrain.LastChangedChunkCount : 0;
        public long RefreshBatchCount => terrain != null ? terrain.RefreshBatchCount : 0;
        public Camera Camera => camera;
        public TerrainPreview Terrain => terrain;
        internal bool MineralInputPending => pendingMinerals != null;
        public Scene PreviewScene => scene;

        public TerrainStylePreviewStage(int width = PixelWidth, int height = PixelHeight, bool terrainFirst = false)
        {
            if (width < 64 || height < 64 || width > PixelWidth || height > PixelHeight) throw new ArgumentOutOfRangeException("预览输出尺寸无效。");
            outputWidth = width; outputHeight = height; this.terrainFirst = terrainFirst;
        }

        public void Open(TerrainMapAsset map, TerrainBlueprint blueprint, CaveTerrainStyle style,
            TerrainStyleDrafts drafts, byte[] materials, byte[] shapes, BackgroundBakeDescriptor reference)
            => Open(map.Definition, blueprint, style, drafts, materials, shapes, reference);

        public void Open(ARDMapDefinition definition, TerrainBlueprint blueprint, CaveTerrainStyle style,
            TerrainStyleDrafts drafts, byte[] materials, byte[] shapes, BackgroundBakeDescriptor reference,
            GridBounds localRegion = default, Vector3? cameraPosition = null, float cameraSize = 96, PlanetDefinition planet = null)
        {
            Dispose();
            if (definition == null || blueprint == null || style == null || drafts == null)
                throw new ArgumentNullException("地图、蓝图、样式和草稿必须完整。");
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Map Design Workbench Runtime Preview") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var cameraObject = new GameObject("Map Design Workbench Camera") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = cameraPosition ?? new Vector3(160, -96, -10);
                camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = cameraSize; camera.aspect = (float)outputWidth / outputHeight;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .16f, .16f);
                // Camera.Render 在 Editor 中默认可能跨已加载 Scene 绘制；限制为本预览场景，避免带入游戏角色。
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.allowMSAA = false; camera.allowHDR = false; camera.enabled = false;
                target = new RenderTexture(outputWidth, outputHeight, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default)
                { name = "Map Design Workbench Runtime Output", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                target.Create(); camera.targetTexture = target;
                terrain = root.AddComponent<TerrainPreview>(); transientStyle = BuildStyle(style, drafts);
                terrain.DeferEditorBackground = terrainFirst;
                transientBackground = transientStyle.Background; terrain.CaveStyle = transientStyle;
                this.planet = planet;
                terrain.SurfaceSky = planet != null;
                if (planet != null)
                {
                    var environmentSettings = transientStyle.SurfaceEnvironment.Capture();
                    environmentSettings.Layouts = Array.ConvertAll(environmentSettings.Layouts, x => drafts.Draft(x));
                    transientStyle.SurfaceEnvironment = environmentSettings;
                    environmentMaterial = new Material(Shader.Find("Sprites/Default"));
                    environmentMaterial.mainTexture = Texture2D.whiteTexture;
                    environment = new JourneySurfaceBackdrop(root.transform, environmentMaterial, environmentSettings, true);
                    ColorUtility.TryParseHtmlString(planet.SkyColorHex, out var sky);
                    camera.backgroundColor = sky;
                }
                terrain.ViewCamera = camera;
                terrain.LocalRegion = localRegion; terrain.LocalPreviewBackground = localRegion.IsValid;
                Source = new TerrainBlueprintSource(blueprint, definition.LoadGameplayCatalog().Tiles);
                if (materials != null && shapes != null) Source.ReplaceCells(materials, shapes);
                IMapChunkSource input = localRegion.IsValid ? (IMapChunkSource)new TerrainPreviewChunkSource(Source) : Source;
                try { terrain.ShowBlueprint(definition, blueprint, input, reference); }
                finally { DestroyTransientStyle(); }
                // 矿层Ready在相机绘制期间为false，只在绘制外组合，避免回执互相等待。
                if (terrainFirst) foregroundReceipt = new TerrainDrawReceipt(() => camera,
                    () => terrain.ForegroundStable && pendingMinerals == null,
                    () => (terrain.InstalledInputGeneration, terrain.InstalledSourceCommit,
                        unchecked((int)(terrain.BuiltPages + terrain.RockBuildCount + terrain.MineralBuiltPages))), (_, __) => { });
                renderedRevision = -1;
            }
            catch { Dispose(); throw; }
        }

        public bool Tick()
        {
            if (terrain == null || camera == null) return false;
            if (pendingMinerals != null && (terrainFirst ? terrain.ForegroundStable : terrain.Ready))
            { terrain.SetMinerals(pendingMinerals); pendingMinerals = null; cameraChanged = true; }
            terrain.TickFromEditor(4);
            if (terrain.LastError != null) return false;
            if (terrainFirst && TerrainReady && terrain.LightingSource?.StartDeferredBackground() == true) cameraChanged = true;
            bool newTicket = renderedGeneration != terrain.InstalledInputGeneration ||
                renderedCommit != terrain.InstalledSourceCommit;
            // 清理作业可能使表现稳定而不改变画面版本，需要补一次真正绘制。
            bool confirmation = terrain.NeedsPresentationDraw && (!lastDrawCouldConfirm || newTicket) ||
                terrainFirst && terrain.ForegroundStable && pendingMinerals == null && !TerrainReady;
            if (renderedRevision == terrain.VisualRevision && !confirmation && !cameraChanged) return false;
            cameraChanged = false;
            lastDrawCouldConfirm = terrain.NeedsPresentationDraw;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Color skyTint = QualitySettings.activeColorSpace == ColorSpace.Linear ? camera.backgroundColor.linear : camera.backgroundColor;
            if (!terrainFirst || TerrainReady) environment?.Render(camera, planet, skyTint);
            camera.Render();
            ImageCameraPosition = camera.transform.localPosition; ImageCameraSize = camera.orthographicSize;
            LastCameraMilliseconds = (int)watch.ElapsedMilliseconds;
            renderedRevision = terrain.VisualRevision;
            renderedGeneration = terrain.InstalledInputGeneration;
            renderedCommit = terrain.InstalledSourceCommit;
            return true;
        }

        /// <summary>同种子作者几何以稀疏事务更新，宿主、相机和矿层输入保持，背景使用新完整摘要。</summary>
        internal void ReplaceCandidate(PlayableTerrain map, CaveTerrainStyle style, TerrainStyleDrafts drafts)
        {
            Source.ReplaceBlueprint(map.Blueprint());
            var working = drafts.Draft(style);
            var environmentSettings = working.SurfaceEnvironment.Capture();
            environmentSettings.Layouts = Array.ConvertAll(environmentSettings.Layouts, x => drafts.Draft(x));
            var copy = UnityEngine.Object.Instantiate(working);
            try
            {
                copy.SurfaceEnvironment = environmentSettings;
                terrain.ReplaceAuthoringReference(map.Background, copy);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            ReplaceMinerals(map); cameraChanged = true;
        }

        /// <summary>环境调参只替换远景与近景装饰，沿用地形、地下背景、矿层和相机，不启动洞穴烘焙。</summary>
        internal void ReplaceEnvironment(CaveTerrainStyle style, TerrainStyleDrafts drafts, BackgroundBakeDescriptor reference)
        {
            var working = drafts.Draft(style);
            var settings = working.SurfaceEnvironment.Capture();
            settings.Layouts = Array.ConvertAll(settings.Layouts, x => drafts.Draft(x));
            var copy = UnityEngine.Object.Instantiate(working);
            try
            {
                copy.SurfaceEnvironment = settings;
                terrain.ReplaceAuthoringReference(reference, copy, replaceBackground: false);
                environment?.Dispose();
                environment = planet == null ? null :
                    new JourneySurfaceBackdrop(root.transform, environmentMaterial, settings, true);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            cameraChanged = true;
        }

        /// <summary>只安装矿层差异；不更换地形输入、背景缓存、相机或预览场景。</summary>
        internal void ReplaceMinerals(PlayableTerrain map)
        {
            int id = 0;
            pendingMinerals = map.Deposits.Select(d => new DarkNights.Core.ViewData.MineralDepositViewData(
                ++id, d.X, d.Y, d.RoomKind, d.Rarity, d.Rarity == "rare" ? "gold" : "iron", 1, 1, 1,
                d.Cells.Select(c => new DarkNights.Core.ViewData.MineralCellViewData(
                    c.U, c.V, c.Capacity, c.Capacity, 1, 1)).ToArray())).ToArray();
        }

        public void Dispose()
        {
            foregroundReceipt?.Dispose(); foregroundReceipt = null;
            environment?.Dispose(); environment = null;
            if (environmentMaterial != null) UnityEngine.Object.DestroyImmediate(environmentMaterial);
            environmentMaterial = null; SurfaceCameraActive = false;
            Source = null; terrain = null; renderedRevision = -1;
            pendingMinerals = null;
            renderedGeneration = renderedCommit = 0; lastDrawCouldConfirm = false;
            if (camera != null) camera.targetTexture = null;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            camera = null; root = null;
            DestroyTransientStyle();
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); target = null; }
        }

        /// <summary>同一宿主切换制作镜头；不运行角色或会话，关闭局部镜头后恢复原全图坐标。</summary>
        public void SetSurfaceCamera(float column, int row)
        {
            if (camera == null) return;
            SurfaceCameraActive = true;
            camera.transform.localPosition = new Vector3(column, -row + 4, -10);
            camera.orthographicSize = 12; cameraChanged = true;
        }

        public void SetFullMapCamera()
        {
            if (camera == null) return;
            SurfaceCameraActive = false;
            camera.transform.localPosition = new Vector3(160, -96, -10);
            camera.orthographicSize = 96; cameraChanged = true;
        }

        /// <summary>自由平移缩放只更改制作相机，不替换地图宿主或重置作者选择。</summary>
        internal void SetCanvasCamera(Vector3 position, float size)
        {
            if (camera == null) return;
            SurfaceCameraActive = false;
            if (camera.transform.localPosition == position &&
                Mathf.Approximately(camera.orthographicSize, size)) return;
            camera.transform.localPosition = position; camera.orthographicSize = size; cameraChanged = true;
        }

        private static CaveTerrainStyle BuildStyle(CaveTerrainStyle source, TerrainStyleDrafts drafts)
        {
            CaveTerrainStyle working = drafts.Draft(source);
            var result = UnityEngine.Object.Instantiate(working); result.hideFlags = HideFlags.DontSave;
            result.Modifiers = DraftArray(working.Modifiers, drafts);
            if (working.Background != null)
            {
                CaveBackgroundStyle background = drafts.Draft(working.Background);
                var backgroundCopy = UnityEngine.Object.Instantiate(background); backgroundCopy.hideFlags = HideFlags.DontSave;
                backgroundCopy.Generator = drafts.Draft(background.Generator);
                backgroundCopy.NearModifiers = DraftArray(background.NearModifiers, drafts);
                backgroundCopy.MiddleModifiers = DraftArray(background.MiddleModifiers, drafts);
                backgroundCopy.DeepModifiers = DraftArray(background.DeepModifiers, drafts);
                result.Background = backgroundCopy;
            }
            return result;
        }

        private void DestroyTransientStyle()
        {
            if (transientStyle != null) UnityEngine.Object.DestroyImmediate(transientStyle);
            if (transientBackground != null) UnityEngine.Object.DestroyImmediate(transientBackground);
            transientStyle = null; transientBackground = null;
        }

        private static CaveModifierAsset[] DraftArray(CaveModifierAsset[] assets, TerrainStyleDrafts drafts)
        {
            if (assets == null) return Array.Empty<CaveModifierAsset>();
            var result = new CaveModifierAsset[assets.Length];
            for (int i = 0; i < assets.Length; i++) result[i] = drafts.Draft(assets[i]);
            return result;
        }
    }
}
