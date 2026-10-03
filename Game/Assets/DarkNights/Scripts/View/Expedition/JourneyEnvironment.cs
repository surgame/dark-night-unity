using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Expedition
{
    /// <summary>当前航程专属的本地太空和地表天空；确定性星点不消耗玩法 RNG，相机回执仅证明太空表现已绘制。</summary>
    public sealed class JourneyEnvironment : MonoBehaviour
    {
        private readonly Dictionary<string, ITravelTransitionPresentation> transitions =
            new Dictionary<string, ITravelTransitionPresentation>();
        private Camera viewCamera;
        private Material material;
        private Mesh skyMesh, starMesh;
        private GameObject sky, stars;
        private JourneySurfaceBackdrop surface;
        private Vector2[] origins = Array.Empty<Vector2>();
        private Vector3[] vertices = Array.Empty<Vector3>();
        private Color[] colors = Array.Empty<Color>();
        private JourneyViewData journey;
        private PlanetDefinition planet;
        private string identity = "", drawingIdentity = "";
        private double receivedAt;
        private readonly JourneyArrivalPresentation presentation = new JourneyArrivalPresentation();
        private float arrivalStarOpacity = 1, lastStarOpacity = 1;
        private float drawingSurface;
        private bool rendered, paused;
        public bool SpaceReady => rendered && JourneyPresentationRules.InSpace(journey);
        public bool DestinationDrawn { get; set; }
        public float SurfaceAmount => presentation.SurfaceAmount;
        public bool SurfaceReady => rendered && !JourneyPresentationRules.InSpace(journey) && presentation.Complete && drawingSurface >= 1;

        public void Initialize(Camera camera, SurfaceEnvironmentSettings surfaceSettings = null)
        {
            viewCamera = camera;
            material = new Material(Shader.Find("Sprites/Default")) { name = "Journey local environment" };
            material.mainTexture = Texture2D.whiteTexture;
            sky = Layer("Journey sky", -110, out skyMesh);
            stars = Layer("Journey stars", -109, out starMesh);
            surface = new JourneySurfaceBackdrop(transform, material, surfaceSettings ?? new SurfaceEnvironmentSettings());
            RegisterTransition("star-shift", new ConfiguredTravelTransition("star-shift"));
            RegisterTransition("fade", new ConfiguredTravelTransition("fade"));
            RegisterTransition("none", new ConfiguredTravelTransition("none"));
            Camera.onPreCull += BeginCamera; Camera.onPostRender += EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginPipeline;
            RenderPipelineManager.endCameraRendering += EndPipeline;
            sky.SetActive(false); stars.SetActive(false);
        }

        public void RegisterTransition(string key, ITravelTransitionPresentation presentation)
        {
            if (string.IsNullOrEmpty(key) || presentation == null) throw new ArgumentException("过场策略无效。");
            transitions[key] = presentation;
        }

        public void Present(SessionViewData frame)
        {
            var next = frame?.World.Expedition?.Journey;
            if (presentation.Observe(frame)) arrivalStarOpacity = lastStarOpacity;
            string nextIdentity = next?.Enabled == true ? frame.Epoch + ":" + next.JourneyId + ":" + next.Revision : "";
            if (identity != nextIdentity) { identity = nextIdentity; rendered = false; }
            if (!ReferenceEquals(journey, next)) receivedAt = Time.unscaledTimeAsDouble;
            journey = next; paused = frame?.Paused == true;
            planet = next?.ActivePlanet ?? next?.Planets.FirstOrDefault(p => p.Enabled);
            bool active = next?.Enabled == true;
            double speed = next?.Phase == JourneyPhase.Transit ? planet?.StarSpeed ?? 110 :
                !JourneyPresentationRules.InSpace(next) && !presentation.Complete ? (planet?.StarSpeed ?? 110) * (1 - SurfaceAmount) : 1.5;
            presentation.Advance(Time.unscaledDeltaTime, DestinationDrawn, paused, speed);
            sky.SetActive(active); stars.SetActive(active && (JourneyPresentationRules.InSpace(next) || !presentation.Complete));
            surface.Show(active && SurfaceAmount > 0);
            if (!active) return;
            if (origins.Length != (planet?.StarCount ?? 120)) BuildStars(planet?.StarCount ?? 120);
        }

        private void RenderEnvironment()
        {
            if (journey?.Enabled != true || viewCamera == null) return;
            bool space = JourneyPresentationRules.InSpace(journey);
            var center = viewCamera.transform.position;
            float halfHeight = viewCamera.orthographicSize, halfWidth = halfHeight * viewCamera.aspect;
            float left = center.x - halfWidth, right = center.x + halfWidth, top = center.y + halfHeight;
            float bottom = center.y - halfHeight;
            // 天空铺满视口，地表由只读天际线和洞穴背景遮挡，不能按泊位高度截成水平矩形。
            bool visible = top > bottom;
            sky.SetActive(visible);
            if (!visible) return;
            ColorUtility.TryParseHtmlString(planet?.SpaceColorHex ?? "#060C20", out Color spaceTint);
            ColorUtility.TryParseHtmlString(planet?.SkyColorHex ?? "#243B55", out Color surfaceTint);
            // Mesh 顶点颜色不会像材质 Color 属性那样自动解码，作者十六进制颜色先进入线性空间。
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) { spaceTint = spaceTint.linear; surfaceTint = surfaceTint.linear; }
            float blend = space ? 0 : SurfaceAmount;
            Color tint = Color.Lerp(spaceTint, surfaceTint, blend);
            var upperTint = Color.Lerp(spaceTint, new Color(surfaceTint.r * .7f, surfaceTint.g * .7f, surfaceTint.b * .7f, 1), blend);
            skyMesh.Clear();
            skyMesh.vertices = new[] { new Vector3(left, bottom, 1), new Vector3(right, bottom, 1),
                new Vector3(right, top, 1), new Vector3(left, top, 1) };
            skyMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            skyMesh.colors = new[] { tint, tint, upperTint, upperTint };
            skyMesh.RecalculateBounds();
            if (!space && blend > 0) surface.Render(viewCamera, planet, tint, blend);
            if (!space && presentation.Complete) return;
            string kind = planet?.TransitionKind ?? "star-shift";
            if (!transitions.TryGetValue(kind, out var transition)) transition = transitions["none"];
            bool transit = journey.Phase == JourneyPhase.Transit;
            float elapsed = (float)(journey.PhaseElapsed + (paused ? 0 : Time.unscaledTimeAsDouble - receivedAt));
            float opacity = !space ? arrivalStarOpacity * (1 - blend) :
                transit ? transition.StarOpacity(elapsed, (float)(planet?.TransitSeconds ?? 1)) : 1;
            lastStarOpacity = opacity;
            for (int i = 0; i < origins.Length; i++)
            {
                var position = transition.StarPosition(origins[i], (float)presentation.StarTravel, 1000 * (.6f + (i % 7) * .1f));
                float x = Mathf.Lerp(left, right, position.x), y = Mathf.Lerp(bottom, top, position.y);
                float radius = halfHeight / Mathf.Max(1, viewCamera.pixelHeight) * (i % 5 == 0 ? 2 : 1);
                int k = i * 4;
                vertices[k] = new Vector3(x - radius, y - radius, 0);
                vertices[k + 1] = new Vector3(x + radius, y - radius, 0);
                vertices[k + 2] = new Vector3(x + radius, y + radius, 0);
                vertices[k + 3] = new Vector3(x - radius, y + radius, 0);
                var color = new Color(.65f + (i % 3) * .1f, .8f, 1, opacity);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
                for (int v = 0; v < 4; v++) colors[k + v] = color;
            }
            starMesh.vertices = vertices; starMesh.colors = colors; starMesh.RecalculateBounds();
        }

        private GameObject Layer(string name, int order, out Mesh mesh)
        {
            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(transform, false);
            mesh = new Mesh { name = name }; mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order;
            return root;
        }

        private void BuildStars(int count)
        {
            origins = new Vector2[count]; vertices = new Vector3[count * 4]; colors = new Color[count * 4];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                origins[i] = new Vector2(Mathf.Repeat((i + 1) * .61803399f, 1), Mathf.Repeat((i + 1) * .41421356f, 1));
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            starMesh.Clear(); starMesh.vertices = vertices; starMesh.triangles = triangles;
        }

        private void BeginPipeline(ScriptableRenderContext context, Camera camera) => BeginCamera(camera);
        private void EndPipeline(ScriptableRenderContext context, Camera camera) => EndCamera(camera);
        private void BeginCamera(Camera camera)
        {
            if (camera != viewCamera) return;
            RenderEnvironment(); drawingIdentity = identity; drawingSurface = SurfaceAmount;
        }
        private void EndCamera(Camera camera)
        {
            if (camera == viewCamera && identity.Length != 0 && drawingIdentity == identity && sky.activeInHierarchy)
                rendered = true;
        }

        private void OnDestroy()
        {
            surface?.Dispose();
            Camera.onPreCull -= BeginCamera; Camera.onPostRender -= EndCamera;
            RenderPipelineManager.beginCameraRendering -= BeginPipeline;
            RenderPipelineManager.endCameraRendering -= EndPipeline;
            foreach (var asset in new UnityEngine.Object[] { sky, stars, skyMesh, starMesh, material })
                if (asset != null) Destroy(asset);
        }
    }
}
