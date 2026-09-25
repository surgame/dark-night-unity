using System;
using System.Collections.Generic;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>Cave Wall Tuner 的地图专用离屏宿主；临时空场景仅容纳相机与正式 TerrainPreview。</summary>
    public sealed class TerrainStylePreviewStage : IDisposable
    {
        private const int PixelWidth = 2560, PixelHeight = 1536;
        private Scene scene;
        private GameObject root;
        private Camera camera;
        private TerrainPreview terrain;
        private CaveTerrainStyle transientStyle;
        private CaveBackgroundStyle transientBackground;
        private RenderTexture target;
        private int renderedRevision = -1;
        private ulong renderedGeneration, renderedCommit;
        private bool lastDrawCouldConfirm;
        public int LastCameraMilliseconds { get; private set; }
        public string Progress => terrain == null ? "尚未创建预览" : terrain.PresentationWaitReason;
        public string RefreshPath => terrain == null ? "未选择" : terrain.RefreshPath;
        public Texture Image => target;
        public TerrainBlueprintSource Source { get; private set; }
        public Exception Error => terrain != null ? terrain.LastError : null;
        public bool Ready => terrain != null && terrain.Ready;
        public int RockBuildCount => terrain != null ? terrain.RockBuildCount : 0;
        public int LastChangedChunkCount => terrain != null ? terrain.LastChangedChunkCount : 0;
        public long RefreshBatchCount => terrain != null ? terrain.RefreshBatchCount : 0;

        public void Open(TerrainMapAsset map, TerrainBlueprint blueprint, CaveTerrainStyle style,
            TerrainStyleDrafts drafts, byte[] materials, byte[] shapes, BackgroundBakeDescriptor reference)
        {
            Dispose();
            if (map == null || blueprint == null || style == null || drafts == null)
                throw new ArgumentNullException("地图、蓝图、样式和草稿必须完整。");
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Cave Wall Tuner Runtime Preview") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var cameraObject = new GameObject("Cave Wall Tuner Camera") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = new Vector3(160, -96, -10);
                camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 96; camera.aspect = 5f / 3f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .16f, .16f);
                // Camera.Render 在 Editor 中默认可能跨已加载 Scene 绘制；限制为本预览场景，避免带入游戏角色。
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.allowMSAA = false; camera.allowHDR = false; camera.enabled = false;
                target = new RenderTexture(PixelWidth, PixelHeight, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default)
                { name = "Cave Wall Tuner Runtime Output", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                target.Create(); camera.targetTexture = target;
                terrain = root.AddComponent<TerrainPreview>(); transientStyle = BuildStyle(style, drafts);
                transientBackground = transientStyle.Background; terrain.CaveStyle = transientStyle;
                terrain.ViewCamera = camera;
                Source = new TerrainBlueprintSource(blueprint, map.Definition.LoadGameplayCatalog().Tiles);
                if (materials != null && shapes != null) Source.ReplaceCells(materials, shapes);
                try { terrain.ShowBlueprint(map.Definition, blueprint, Source, reference); }
                finally { DestroyTransientStyle(); }
                renderedRevision = -1;
            }
            catch { Dispose(); throw; }
        }

        public bool Tick()
        {
            if (terrain == null || camera == null) return false;
            terrain.TickFromEditor();
            if (terrain.LastError != null) return false;
            bool newTicket = renderedGeneration != terrain.InstalledInputGeneration ||
                renderedCommit != terrain.InstalledSourceCommit;
            // 清理作业可能使表现稳定而不改变画面版本，需要补一次真正绘制。
            bool confirmation = terrain.NeedsPresentationDraw && (!lastDrawCouldConfirm || newTicket);
            if (renderedRevision == terrain.VisualRevision && !confirmation) return false;
            lastDrawCouldConfirm = terrain.NeedsPresentationDraw;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            camera.Render();
            LastCameraMilliseconds = (int)watch.ElapsedMilliseconds;
            renderedRevision = terrain.VisualRevision;
            renderedGeneration = terrain.InstalledInputGeneration;
            renderedCommit = terrain.InstalledSourceCommit;
            return true;
        }

        public void Dispose()
        {
            Source = null; terrain = null; renderedRevision = -1;
            renderedGeneration = renderedCommit = 0; lastDrawCouldConfirm = false;
            if (camera != null) camera.targetTexture = null;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            camera = null; root = null;
            DestroyTransientStyle();
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
            scene = default;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); target = null; }
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
