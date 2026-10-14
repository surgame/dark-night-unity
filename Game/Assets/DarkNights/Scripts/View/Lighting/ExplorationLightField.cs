using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Lighting
{
    /// <summary>一台游戏相机拥有的局部 GPU 照明；只使用冻结光源和已确认格，资源退休可等待，不参与模拟。</summary>
    public sealed class ExplorationLightField : IEnvironmentLightBackend
    {
        private const int Capacity = 16, Width = 384, Height = 216;
        private readonly ComputeShader shader;
        private readonly RenderTexture light, direction;
        private readonly ComputeBuffer bounce;
        private readonly ComputeBuffer apertureVisibility;
        private readonly LightGeometryCache geometry;
        private readonly Func<Core.ViewData.LightGeometrySnapshot, Func<bool>, Core.ViewData.LightBoundarySnapshot> boundaryBuilder;
        private WallDiffuseResources diffuse;
        private readonly CommandBuffer commands = new CommandBuffer { name = "Dark Nights private lighting" };
        private static readonly UnityEngine.Profiling.CustomSampler composeSampler = UnityEngine.Profiling.CustomSampler.Create("DN.Diffuse.IntegrateCompose", true);
        private static readonly UnityEngine.Profiling.CustomSampler directSampler = UnityEngine.Profiling.CustomSampler.Create("DN.Direct.Legacy", true);
        private static readonly UnityEngine.Profiling.CustomSampler totalSampler = UnityEngine.Profiling.CustomSampler.Create("DN.Light.Total", true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int DiffuseBenchmarkMode { get; set; }
        public bool CountDiffuseQueries { get; set; }
        public UnityEngine.Profiling.CustomSampler MeasurementSampler { get; set; }
#endif
        private readonly Vector4[] positions = new Vector4[Capacity], directions = new Vector4[Capacity], colors = new Vector4[Capacity];
        private readonly Vector4[] nearOrigins = new Vector4[Capacity];
        private readonly Vector4[] apertures = new Vector4[Capacity];
        private readonly Vector4[] shadows = new Vector4[Capacity];
        private readonly Vector4[] bounceOptions = new Vector4[Capacity], diffuseSettings = new Vector4[Capacity], diffuseOptions = new Vector4[Capacity];
        private readonly LightBounceRules[] bounceRules = new LightBounceRules[Capacity];
        private readonly int resolve, resolveApertures, prepareProfiles, illuminate, illuminateDiffuse;
        private bool retired;
        private bool uniformsReady;
        private CaveVisualSource apertureSource;
        private Texture apertureGeometry;
        private RectInt apertureBounds;
        private long apertureRevision;
        private Task retirement;
        public ExplorationLightSettings Settings { get; }
        public int SourceCount { get; private set; }
        public bool GeometryReady => geometry.Ready;
        public bool GeometrySettled => geometry.Settled;
        public double GeometryTickMilliseconds { get; private set; }
        public WallDiffuseResources DiffuseResources => diffuse;
        public double SubmitMilliseconds { get; private set; }

        public ExplorationLightField(ComputeShader template, Func<Core.ViewData.LightGeometrySnapshot,Func<bool>,float[]> calculator,
            ExplorationLightSettings settings = null,
            Func<Core.ViewData.LightGeometrySnapshot, Func<bool>, Core.ViewData.LightBoundarySnapshot> boundaries = null)
        {
            Settings = settings ?? new ExplorationLightSettings();
            if (template == null || !SystemInfo.supportsComputeShaders)
                throw new InvalidOperationException("当前 GPU 或手电 Prefab 缺少计算光照能力。");
            geometry = new LightGeometryCache(calculator);
            boundaryBuilder = boundaries;
            shader = UnityEngine.Object.Instantiate(template);
            resolve = shader.FindKernel("ResolveSources"); illuminate = shader.FindKernel("Illuminate");
            resolveApertures = shader.HasKernel("ResolveApertures") ? shader.FindKernel("ResolveApertures") : -1;
            prepareProfiles = shader.HasKernel("PrepareWallProfiles") ? shader.FindKernel("PrepareWallProfiles") : -1;
            illuminateDiffuse = shader.HasKernel("IlluminateDiffuse") ? shader.FindKernel("IlluminateDiffuse") : -1;
            light = Target("Exploration irradiance"); direction = Target("Exploration light direction");
            bounce = new ComputeBuffer(Capacity, sizeof(float) * 4);
            apertureVisibility = new ComputeBuffer(Capacity * 10, sizeof(float));
            shader.SetVector("_DNTargetSize",new Vector4(Width,Height,0,0));
            shader.SetBuffer(resolve,"_DNBounceSources",bounce);
            foreach (int kernel in new[] { illuminate, illuminateDiffuse })
            {
                if (kernel < 0) continue;
                shader.SetBuffer(kernel,"_DNBounceSources",bounce);
                shader.SetTexture(kernel,"_DNLightOutput",light); shader.SetTexture(kernel,"_DNDirectionOutput",direction);
                if (resolveApertures >= 0) shader.SetBuffer(kernel,"_DNApertureVisibility",apertureVisibility);
            }
            if (resolveApertures >= 0) shader.SetBuffer(resolveApertures,"_DNApertureVisibility",apertureVisibility);
            if (prepareProfiles >= 0) shader.SetBuffer(prepareProfiles,"_DNApertureVisibility",apertureVisibility);
        }

        public void Render(Camera camera, TerrainPreview preview, IReadOnlyList<LightEmitterData> emitters)
        {
            if (retired || camera == null || preview?.LightingSource == null || preview.LightingSource.IsRetired) { Suspend(); return; }
            var known = preview.LightingLoadedBounds;
            if (!known.IsValid) { Suspend(); return; }
            var bounds = new RectInt(known.MinU, 1-(int)known.MaxVExclusive, known.Width, known.Height);
            var source = preview.LightingSource;
            Matrix4x4 matrix = preview.transform.worldToLocalMatrix;
            Vector3 center = matrix.MultiplyPoint3x4(camera.transform.position);
            // 工作台可装载整张 320 格地图；照明仍只冻结相机附近至多 128×128 格。
            if (bounds.width > 192 || bounds.height > 192)
            {
                int x = Mathf.Max(bounds.xMin, Mathf.FloorToInt(center.x) - 64), y = Mathf.Max(bounds.yMin, Mathf.FloorToInt(-center.y) - 64);
                bounds = new RectInt(x, y, Mathf.Min(128, bounds.xMax - x), Mathf.Min(128, bounds.yMax - y));
            }
            long geometryStart = System.Diagnostics.Stopwatch.GetTimestamp();
            geometry.Tick(source, bounds);
            GeometryTickMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - geometryStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            float halfH = camera.orthographicSize / Mathf.Abs(preview.transform.lossyScale.y);
            float halfW = halfH * camera.aspect;
            var area = new Vector4(Mathf.Floor(center.x-halfW)+.5f, Mathf.Floor(-center.y-halfH)+.5f,
                Mathf.Ceil(halfW*2)+2, Mathf.Ceil(halfH*2)+2);
            int nextCount = Math.Min(Capacity, emitters.Count);
            bool uploadLightData = !uniformsReady || SourceCount != nextCount;
            SourceCount = nextCount;
            bool continuous = false, legacy = false;
            for (int n = 0; n < SourceCount; n++)
            {
                var emitter = emitters[n]; var rule = emitter.Rules; var returned = rule.Bounce;
                bounceRules[n] = returned;
                continuous |= returned.Continuous && emitter.Directional && rule.Intensity > 0;
                legacy |= returned.BounceMode == LightBounceMode.LegacyPoint && returned.Bounce > 0;
                uploadLightData |= Replace(bounceOptions, n, new Vector4((int)returned.BounceMode, returned.Bounce, returned.DiffuseCornerRadius, (int)returned.DiffuseOcclusionMode));
                uploadLightData |= Replace(diffuseSettings, n, new Vector4(returned.DiffuseGain, returned.DiffuseNearDistance, returned.DiffuseFarDistance, returned.DiffuseMaxReach));
                uploadLightData |= Replace(diffuseOptions, n, new Vector4(returned.DiffuseLateralSpread, returned.DiffuseVisibilityProbes, (int)returned.DiffuseShape, returned.DiffuseMatchIncident ? 1 : 0));
                Vector3 local = matrix.MultiplyPoint3x4(emitter.Position);
                Vector3 forward = matrix.MultiplyVector(new Vector3(Mathf.Cos(emitter.Angle*Mathf.Deg2Rad), Mathf.Sin(emitter.Angle*Mathf.Deg2Rad),0)).normalized;
                uploadLightData |= Replace(positions, n, new Vector4(local.x+.5f,-local.y+.5f,rule.Range,
                    emitter.Directional ? Mathf.Cos(rule.Cone*.5f*Mathf.Deg2Rad) : -1));
                Vector3 near = matrix.MultiplyPoint3x4(emitter.NearPosition);
                uploadLightData |= Replace(nearOrigins, n, new Vector4(near.x+.5f,-near.y+.5f,0,0));
                uploadLightData |= Replace(apertures, n, new Vector4(emitter.Directional ? rule.ApertureWidth * .5f : 0, 0, 0, 0));
                uploadLightData |= Replace(shadows, n, new Vector4(rule.SoftShadows ? rule.Softness : 0, rule.ConeFeather, returned.DiffuseFeatherWidth, 0));
                uploadLightData |= Replace(directions, n, new Vector4(forward.x,-forward.y,rule.NearRange,rule.NearIntensity));
                var linear = new Color(rule.Red,rule.Green,rule.Blue,1).linear;
                uploadLightData |= Replace(colors, n, new Vector4(linear.r,linear.g,linear.b,rule.Intensity));
            }
            var shadow = new Vector4(0, Mathf.Clamp(Settings.WallDepth,0,1),Mathf.Clamp01(Settings.WallStrength),0);
            shader.SetVector("_DNKnownBounds",new Vector4(bounds.x,bounds.y,bounds.width,bounds.height));
            shader.SetVector("_DNShadowSettings",shadow); shader.SetVector("_DNLightRect",area);
            if (continuous && illuminateDiffuse < 0) throw new InvalidOperationException("指定 Shader 缺少连续漫反射核。");
            int lightingKernel = continuous ? illuminateDiffuse : illuminate;
            shader.SetInt("_DNLightCount",SourceCount);
            if (uploadLightData)
            {
                shader.SetVectorArray("_DNLightPositions",positions); shader.SetVectorArray("_DNLightDirections",directions); shader.SetVectorArray("_DNLightColors",colors);
                shader.SetVectorArray("_DNLightNearOrigins",nearOrigins);
                shader.SetVectorArray("_DNLightApertures",apertures);
                shader.SetVectorArray("_DNLightShadows",shadows);
                shader.SetVectorArray("_DNLightBounce", bounceOptions);
                shader.SetVectorArray("_DNDiffuseSettings", diffuseSettings);
                shader.SetVectorArray("_DNDiffuseOptions", diffuseOptions);
                uniformsReady = true;
            }
            if (apertureGeometry != source.LightingGeometry)
            {
                shader.SetTexture(resolve,"_DNLightCells",source.LightingGeometry);
                shader.SetTexture(illuminate,"_DNLightCells",source.LightingGeometry);
                if (illuminateDiffuse >= 0) shader.SetTexture(illuminateDiffuse,"_DNLightCells",source.LightingGeometry);
                if (resolveApertures >= 0) shader.SetTexture(resolveApertures,"_DNLightCells",source.LightingGeometry);
            }
            // 两个有序批次使用同一图形队列；不等待 CPU 回读，也不启动异步计算队列。
            commands.Clear();
            var measuredTotal = totalSampler;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            measuredTotal = MeasurementSampler ?? totalSampler;
#endif
            commands.BeginSample(measuredTotal);
            if (resolveApertures >= 0 && (uploadLightData || apertureSource != source || apertureGeometry != source.LightingGeometry ||
                apertureBounds != bounds || apertureRevision != source.GeometryRevision))
            {
                commands.DispatchCompute(shader, resolveApertures, 1, 1, 1);
                apertureSource = source; apertureGeometry = source.LightingGeometry;
                apertureBounds = bounds; apertureRevision = source.GeometryRevision;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            shader.SetInt("_DNDiffuseBenchmark", DiffuseBenchmarkMode);
#endif
            if (continuous)
            {
                diffuse = diffuse ?? new WallDiffuseResources(shader, boundaryBuilder);
                bool countQueries = false;
                int benchmark = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                countQueries = CountDiffuseQueries;
                benchmark = DiffuseBenchmarkMode;
#endif
                diffuse.Prepare(shader, commands, source, bounds, positions, directions, apertures, colors, shadows, SourceCount, bounceRules, countQueries, benchmark);
                diffuse.Bind(shader, lightingKernel);
            }
            if (legacy)
                commands.DispatchCompute(shader, resolve, 1, 1, 1);
            var sampler = continuous ? composeSampler : directSampler;
            commands.BeginSample(sampler);
            commands.DispatchCompute(shader, lightingKernel, (Width+7)/8, (Height+7)/8, 1);
            commands.EndSample(sampler); commands.EndSample(measuredTotal);
            long submitStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Graphics.ExecuteCommandBuffer(commands);
            SubmitMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - submitStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Shader.SetGlobalMatrix("_DNMapWorldToLocal",matrix);
            Shader.SetGlobalVector("_DNKnownBounds",new Vector4(bounds.x,bounds.y,bounds.width,bounds.height));
            Shader.SetGlobalVector("_DNShadowSettings",shadow); Shader.SetGlobalVector("_DNLightRect",area);
            var distanceBounds = geometry.Bounds;
            Shader.SetGlobalVector("_DNDistanceRect",new Vector4(distanceBounds.x,distanceBounds.y,distanceBounds.width,distanceBounds.height));
            Shader.SetGlobalTexture("_DNLightCells",source.LightingGeometry);
            Shader.SetGlobalTexture("_DNLightField",light); Shader.SetGlobalTexture("_DNLightDirection",direction);
            Shader.SetGlobalTexture("_DNWallDistance",geometry.Texture != null ? geometry.Texture : Texture2D.whiteTexture);
            Shader.SetGlobalFloat("_DNLightingAmbient",Mathf.Clamp01(Settings.Ambient));
            Shader.SetGlobalFloat("_DNRelief",Mathf.Clamp01(Settings.Relief)); Shader.SetGlobalFloat("_DNLightingActive",1);
            Shader.SetGlobalFloat("_DNLightingReady",1);
            Shader.SetGlobalFloat("_DNLightingBackend",1);
        }

        public static void Suspend()
        { Shader.SetGlobalFloat("_DNLightingActive",0); Shader.SetGlobalFloat("_DNLightingReady",0); }
        void IEnvironmentLightBackend.Suspend() => Suspend();

        private static bool Replace(Vector4[] values, int index, Vector4 current)
        {
            bool changed = !values[index].Equals(current);
            values[index] = current; return changed;
        }

        public Task RetireAsync()
        {
            if (retirement != null) return retirement;
            retired = true; Suspend();
            return retirement = RetireCoreAsync();
        }

        private async Task RetireCoreAsync()
        {
            try
            {
                try { await geometry.RetireAsync(); }
                finally { if (diffuse != null) await diffuse.RetireAsync(); }
            }
            finally
            {
                commands.Dispose(); bounce.Dispose(); apertureVisibility.Dispose(); light.Release(); direction.Release();
                Release(light); Release(direction); Release(shader);
            }
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static RenderTexture Target(string name)
        {
            var target = new RenderTexture(Width,Height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
            { name = name,enableRandomWrite = true,filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,useMipMap = false };
            target.Create(); return target;
        }
    }
}
