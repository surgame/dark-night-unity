using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Lighting;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调参台独占的小尺寸洞穴离屏宿主；只读规则预制地图、正式地形和双光照后端，不启动会话、地图生成或Play。</summary>
    public sealed class FlashlightPreviewStage : IDisposable
    {
        private readonly TerrainStylePreviewStage terrain = new TerrainStylePreviewStage(1024, 640);
        private readonly TerrainStyleDrafts styles = new TerrainStyleDrafts();
        private readonly FlashlightPreviewActor actor = new FlashlightPreviewActor();
        private readonly List<LightEmitterData> emitters = new List<LightEmitterData>(1);
        private EnvironmentLighting lighting;
        private SceneLightingProfile settings;
        private SceneLightingProfile sceneSource;
        private TerrainBlueprint blueprint;
        private string mapId;
        private GameCore.Objects.Definition.ObjectDefinition definition;
        private double previous;
        private float angle;
        private Exception drawError;
        private GridBounds loaded;
        private Vector3 lastCamera;
        private float lastSize;
        private double movedAt;
        public Texture Image => terrain.Image;
        public float Angle => angle;
        public string Status => terrain.Error != null ? terrain.Error.Message : terrain.Ready ?
            "局部地形 " + loaded.Width + "×" + loaded.Height + " 格 · 区块 " + terrain.Terrain.LoadedTerrainChunks +
            " · 背景页 " + terrain.Terrain.BackgroundResidentPages : terrain.Progress;
        public IReadOnlyList<string> Rooms => blueprint?.Rooms.Select((room, index) =>
            (index + 1) + " · " + room.Kind).ToArray() ?? Array.Empty<string>();

        public void Tick(FlashlightDebugDraft draft, SceneLightingProfile source, SceneLightingProfile scene, FlashlightPreviewSettings preview)
        {
            if (draft?.Definition == null || source == null || scene == null || !preview.Enabled || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { previous = 0; return; }
            preview.Validate();
            if (mapId != preview.MapId || definition != draft.Definition || sceneSource != source) Open(draft, source, preview);
            using (new FlashlightPreviewGlobals())
            {
                ExplorationLightField.Suspend(); Shader.SetGlobalFloat("_DNLightingBackend", 0);
                Vector2 position = Position(preview);
                Vector3 cameraPosition = FlashlightPreviewRegion.CameraPosition(position, ClampOffset(preview, preview.CameraOffset));
                double now = EditorApplication.timeSinceStartup;
                if (lastCamera != cameraPosition || lastSize != preview.OrthographicSize)
                { lastCamera = cameraPosition; lastSize = preview.OrthographicSize; movedAt = now; }
                if (!FlashlightPreviewRegion.ContainsView(loaded, cameraPosition, preview.OrthographicSize) && now - movedAt >= .15)
                    OpenRegion(draft, preview);
                var camera = terrain.Camera; camera.orthographicSize = preview.OrthographicSize;
                camera.transform.position = cameraPosition;
                terrain.Tick(); if (terrain.Error != null) throw terrain.Error;
                if (!terrain.Ready) return;
                settings.Settings = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(preview.CompareSaved ? source.Settings : scene.Settings));
                if (!preview.AutoRotate) angle = preview.Angle;
                else angle = Mathf.Repeat(angle + (float)Math.Min(.1, previous == 0 ? 0 : now - previous) * preview.DegreesPerSecond, 360);
                previous = now;
                terrain.Terrain.TickFromEditor();
                LightProfileSnapshot profile = preview.CompareSaved ? draft.PersistentSnapshot() : draft.Snapshot();
                actor.Present(draft, profile, position, angle, preview.CompareSaved);
                emitters.Clear();
                if (profile.EnvironmentEnabled) emitters.Add(actor.Effect.Environment.Sample(terrain.Terrain));
                drawError = null;
                RenderPipelineManager.beginCameraRendering += BeginPreviewCamera;
                try
                {
                    if (GraphicsSettings.currentRenderPipeline == null) lighting.Render(camera, terrain.Terrain, emitters);
                    camera.Render();
                    if (drawError != null) throw drawError;
                }
                finally { RenderPipelineManager.beginCameraRendering -= BeginPreviewCamera; lighting.Suspend(); }
            }
        }
        private void BeginPreviewCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != terrain.Camera) return;
            try { lighting.Render(camera, terrain.Terrain, emitters); }
            catch (Exception error) { drawError = error; lighting.Suspend(); }
        }
        private void Open(FlashlightDebugDraft draft, SceneLightingProfile source, FlashlightPreviewSettings preview)
        {
            Dispose();
            try
            {
                blueprint = LightingPreviewMap.Load(preview.MapId);
                settings = UnityEngine.Object.Instantiate(source); settings.hideFlags = HideFlags.HideAndDontSave;
                OpenRegion(draft, preview);
                mapId = preview.MapId; definition = draft.Definition; sceneSource = source; angle = preview.Angle;
            }
            catch { Dispose(); throw; }
        }
        private void OpenRegion(FlashlightDebugDraft draft, FlashlightPreviewSettings preview)
        {
            actor.Dispose(); lighting?.RetireAsync(); lighting = null;
            Vector3 center = FlashlightPreviewRegion.CameraPosition(Position(preview), ClampOffset(preview, preview.CameraOffset));
            loaded = FlashlightPreviewRegion.Capture(center, preview.OrthographicSize);
            const string path = "Assets/DarkNights/Res/Terrain/StrataCave/";
            terrain.Open(AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(path + "MapDefinition.asset"), blueprint,
                AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(path + "Style.asset"), styles, null, null,
                new BackgroundBakeDescriptor(Guid.NewGuid().ToString("N"), blueprint.Settings.Seed, blueprint.CopyMaterials(), blueprint.CopyShapes()),
                loaded, center, preview.OrthographicSize);
            lighting = new EnvironmentLighting(settings, LightWallDistance.Build, LightBeamProfile.Evaluate, null, LightBoundaryBuilder.Build);
            actor.Open(terrain.PreviewScene, draft);
            lastCamera = center; lastSize = preview.OrthographicSize; movedAt = EditorApplication.timeSinceStartup;
        }
        public Vector2 ClampOffset(FlashlightPreviewSettings preview, Vector2 offset)
        {
            Vector2 position = Position(preview);
            return new Vector2(Mathf.Clamp(position.x + offset.x, .5f, 319.5f) - position.x,
                Mathf.Clamp(position.y + offset.y, 2.5f, 194.5f) - position.y);
        }
        public Vector2 Position(FlashlightPreviewSettings preview)
        {
            if (!preview.UseRoomStart || blueprint == null) return preview.Position;
            var rooms = blueprint.Rooms.ToArray();
            if (rooms.Length == 0) throw new InvalidOperationException("预制地图没有矿室。");
            var room = rooms[Mathf.Clamp(preview.RoomIndex, 0, rooms.Length - 1)];
            int x = room.X;
            for (int y = Math.Min(191, room.Top + room.Height - 1); y >= Math.Max(0, room.Top); y--)
                if (blueprint.MaterialAt(x, y) == 0) return new Vector2(x + .5f, y + 1);
            throw new InvalidOperationException("当前矿室中心没有可预览的空格。");
        }
        public Vector2 Pick(Vector2 normalized)
        {
            if (terrain.Camera == null) throw new InvalidOperationException("预览相机尚未就绪。");
            Vector3 world = terrain.Camera.ViewportToWorldPoint(new Vector3(normalized.x, normalized.y, 10));
            return new Vector2(world.x + .5f, -world.y + .5f);
        }
        public void Dispose()
        {
            using (new FlashlightPreviewGlobals())
            {
                if (lighting != null) lighting.RetireAsync(); lighting = null;
                actor.Dispose(); terrain.Dispose(); styles.Dispose();
            }
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            settings = null; sceneSource = null; blueprint = null; mapId = null; definition = null; previous = 0;
        }
    }
}
