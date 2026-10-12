using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
#endif
        private readonly Vector4[] positions = new Vector4[Capacity], directions = new Vector4[Capacity], colors = new Vector4[Capacity];
        private readonly Vector4[] nearOrigins = new Vector4[Capacity];
        private readonly Vector4[] apertures = new Vector4[Capacity];
        private readonly Vector4[] shadows = new Vector4[Capacity];
        private readonly int resolve, illuminate, illuminateDiffuse;
        private bool retired;
        private Task retirement;
        public ExplorationLightSettings Settings { get; }
        public int SourceCount { get; private set; }
        public bool GeometryReady => geometry.Ready;
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
            illuminateDiffuse = shader.HasKernel("IlluminateDiffuse") ? shader.FindKernel("IlluminateDiffuse") : -1;
            light = Target("Exploration irradiance"); direction = Target("Exploration light direction");
            bounce = new ComputeBuffer(Capacity, sizeof(float) * 4);
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
            geometry.Tick(source, bounds);
            float halfH = camera.orthographicSize / Mathf.Abs(preview.transform.lossyScale.y);
            float halfW = halfH * camera.aspect;
            var area = new Vector4(Mathf.Floor(center.x-halfW)+.5f, Mathf.Floor(-center.y-halfH)+.5f,
                Mathf.Ceil(halfW*2)+2, Mathf.Ceil(halfH*2)+2);
            SourceCount = Math.Min(Capacity, emitters.Count);
            for (int n = 0; n < SourceCount; n++)
            {
                var emitter = emitters[n]; var rule = emitter.Rules;
                Vector3 local = matrix.MultiplyPoint3x4(emitter.Position);
                Vector3 forward = matrix.MultiplyVector(new Vector3(Mathf.Cos(emitter.Angle*Mathf.Deg2Rad), Mathf.Sin(emitter.Angle*Mathf.Deg2Rad),0)).normalized;
                positions[n] = new Vector4(local.x+.5f,-local.y+.5f,rule.Range,
                    emitter.Directional ? Mathf.Cos(rule.Cone*.5f*Mathf.Deg2Rad) : -1);
                Vector3 near = matrix.MultiplyPoint3x4(emitter.NearPosition);
                nearOrigins[n] = new Vector4(near.x+.5f,-near.y+.5f,0,0);
                apertures[n] = new Vector4(emitter.Directional ? rule.ApertureWidth * .5f : 0, 0, 0, 0);
                shadows[n] = new Vector4(rule.SoftShadows ? rule.Softness : 0, rule.ConeFeather, 0, 0);
                directions[n] = new Vector4(forward.x,-forward.y,rule.NearRange,rule.NearIntensity);
                var linear = new Color(rule.Red,rule.Green,rule.Blue,1).linear;
                colors[n] = new Vector4(linear.r,linear.g,linear.b,rule.Intensity);
            }
            var shadow = new Vector4(0, Mathf.Clamp(Settings.WallDepth,0,1),Mathf.Clamp01(Settings.WallStrength),0);
            shader.SetVector("_DNKnownBounds",new Vector4(bounds.x,bounds.y,bounds.width,bounds.height));
            shader.SetVector("_DNShadowSettings",shadow); shader.SetVector("_DNLightRect",area);
            shader.SetVector("_DNTargetSize",new Vector4(Width,Height,0,0));
            bool continuous = Settings.BounceMode == LightBounceMode.Continuous && Settings.DiffuseGain > 0 && Settings.DiffuseMaxReach > 0;
            if (continuous && illuminateDiffuse < 0) throw new InvalidOperationException("指定 Shader 缺少连续漫反射核。");
            int lightingKernel = continuous ? illuminateDiffuse : illuminate;
            shader.SetInt("_DNLightCount",SourceCount);
            shader.SetFloat("_DNBounceStrength",Settings.BounceMode == LightBounceMode.LegacyPoint ? Mathf.Clamp(Settings.Bounce,0,.4f) : 0);
            shader.SetVectorArray("_DNLightPositions",positions); shader.SetVectorArray("_DNLightDirections",directions); shader.SetVectorArray("_DNLightColors",colors);
            shader.SetVectorArray("_DNLightNearOrigins",nearOrigins);
            shader.SetVectorArray("_DNLightApertures",apertures);
            shader.SetVectorArray("_DNLightShadows",shadows);
            shader.SetTexture(resolve,"_DNLightCells",source.LightingGeometry);
            shader.SetTexture(lightingKernel,"_DNLightCells",source.LightingGeometry);
            shader.SetBuffer(resolve,"_DNBounceSources",bounce); shader.SetBuffer(lightingKernel,"_DNBounceSources",bounce);
            shader.SetTexture(lightingKernel,"_DNLightOutput",light); shader.SetTexture(lightingKernel,"_DNDirectionOutput",direction);
            // 两个有序批次使用同一图形队列；不等待 CPU 回读，也不启动异步计算队列。
            commands.Clear();
            commands.BeginSample(totalSampler);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            shader.SetInt("_DNDiffuseBenchmark", DiffuseBenchmarkMode);
#endif
            if (continuous)
            {
                diffuse = diffuse ?? new WallDiffuseResources(shader, boundaryBuilder);
                bool countQueries = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                countQueries = CountDiffuseQueries;
#endif
                diffuse.Prepare(shader, commands, source, bounds, positions, directions, apertures, SourceCount, Settings, countQueries);
                diffuse.Bind(shader, lightingKernel);
            }
            else if (Settings.BounceMode == LightBounceMode.LegacyPoint)
                commands.DispatchCompute(shader, resolve, 1, 1, 1);
            var sampler = continuous ? composeSampler : directSampler;
            commands.BeginSample(sampler);
            commands.DispatchCompute(shader, lightingKernel, (Width+7)/8, (Height+7)/8, 1);
            commands.EndSample(sampler); commands.EndSample(totalSampler);
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
                commands.Dispose(); bounce.Dispose(); light.Release(); direction.Release();
                UnityEngine.Object.Destroy(light); UnityEngine.Object.Destroy(direction); UnityEngine.Object.Destroy(shader);
            }
        }

        private static RenderTexture Target(string name)
        {
            var target = new RenderTexture(Width,Height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
            { name = name,enableRandomWrite = true,filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,useMipMap = false };
            target.Create(); return target;
        }
    }
}
