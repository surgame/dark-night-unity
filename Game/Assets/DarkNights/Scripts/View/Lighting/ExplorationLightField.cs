using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Lighting
{
    /// <summary>一台游戏相机拥有的局部 GPU 照明；只使用冻结光源和已确认格，资源退休可等待，不参与模拟。</summary>
    public sealed class ExplorationLightField
    {
        private const int Capacity = 16, Width = 384, Height = 216;
        private readonly ComputeShader shader;
        private readonly RenderTexture light, direction;
        private readonly ComputeBuffer bounce;
        private readonly LightGeometryCache geometry;
        private readonly Vector4[] positions = new Vector4[Capacity], directions = new Vector4[Capacity], colors = new Vector4[Capacity];
        private readonly Vector4[] nearOrigins = new Vector4[Capacity];
        private readonly int resolve, illuminate;
        private bool retired;
        private Task retirement;
        public ExplorationLightSettings Settings { get; } = new ExplorationLightSettings();
        public int SourceCount { get; private set; }
        public bool GeometryReady => geometry.Ready;

        public ExplorationLightField(ComputeShader template, Func<Core.ViewData.LightGeometrySnapshot,Func<bool>,float[]> calculator)
        {
            if (template == null || !SystemInfo.supportsComputeShaders)
                throw new InvalidOperationException("当前 GPU 或手电 Prefab 缺少计算光照能力。");
            geometry = new LightGeometryCache(calculator);
            shader = UnityEngine.Object.Instantiate(template);
            resolve = shader.FindKernel("ResolveSources"); illuminate = shader.FindKernel("Illuminate");
            light = Target("Exploration irradiance"); direction = Target("Exploration light direction");
            bounce = new ComputeBuffer(Capacity, sizeof(float) * 4);
        }

        public void Render(Camera camera, TerrainPreview preview, IReadOnlyList<FlashlightEmitterData> emitters)
        {
            if (retired || camera == null || preview?.LightingSource == null) { Suspend(); return; }
            var known = preview.LightingLoadedBounds;
            if (!known.IsValid) { Suspend(); return; }
            var bounds = new RectInt(known.MinU, 1-(int)known.MaxVExclusive, known.Width, known.Height);
            var source = preview.LightingSource;
            geometry.Tick(source, bounds);
            Matrix4x4 matrix = preview.transform.worldToLocalMatrix;
            Vector3 center = matrix.MultiplyPoint3x4(camera.transform.position);
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
                positions[n] = new Vector4(local.x+.5f,-local.y+.5f,Mathf.Clamp(rule.Range*Settings.RangeScale,2,24),
                    emitter.Directional ? Mathf.Cos(Mathf.Clamp(rule.Cone+Settings.ConeOffset,20,150)*.5f*Mathf.Deg2Rad) : -1);
                Vector3 near = matrix.MultiplyPoint3x4(emitter.NearPosition);
                nearOrigins[n] = new Vector4(near.x+.5f,-near.y+.5f,0,0);
                directions[n] = new Vector4(forward.x,-forward.y,rule.NearRange,rule.NearIntensity*Mathf.Clamp(Settings.NearStrength,0,2));
                var linear = new Color(rule.Red,rule.Green,rule.Blue,1).linear;
                colors[n] = new Vector4(linear.r,linear.g,linear.b,rule.Intensity*Mathf.Clamp(Settings.IntensityScale,0,2));
            }
            var shadow = new Vector4(Settings.SoftShadows ? Mathf.Clamp(Settings.Softness,0,.75f) : 0,
                Mathf.Clamp(Settings.WallDepth,0,1),Mathf.Clamp01(Settings.WallStrength),Mathf.Clamp(Settings.ConeFeather,0,.25f));
            shader.SetVector("_DNKnownBounds",new Vector4(bounds.x,bounds.y,bounds.width,bounds.height));
            shader.SetVector("_DNShadowSettings",shadow); shader.SetVector("_DNLightRect",area);
            shader.SetVector("_DNTargetSize",new Vector4(Width,Height,0,0));
            shader.SetInt("_DNLightCount",SourceCount); shader.SetFloat("_DNBounceStrength",Mathf.Clamp(Settings.Bounce,0,.4f));
            shader.SetVectorArray("_DNLightPositions",positions); shader.SetVectorArray("_DNLightDirections",directions); shader.SetVectorArray("_DNLightColors",colors);
            shader.SetVectorArray("_DNLightNearOrigins",nearOrigins);
            shader.SetTexture(resolve,"_DNLightCells",source.LightingGeometry);
            shader.SetTexture(illuminate,"_DNLightCells",source.LightingGeometry);
            shader.SetBuffer(resolve,"_DNBounceSources",bounce); shader.SetBuffer(illuminate,"_DNBounceSources",bounce);
            shader.SetTexture(illuminate,"_DNLightOutput",light); shader.SetTexture(illuminate,"_DNDirectionOutput",direction);
            // 两个有序批次使用同一图形队列；不等待 CPU 回读，也不启动异步计算队列。
            shader.Dispatch(resolve,1,1,1); shader.Dispatch(illuminate,(Width+7)/8,(Height+7)/8,1);
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
        }

        public static void Suspend()
        { Shader.SetGlobalFloat("_DNLightingActive",0); Shader.SetGlobalFloat("_DNLightingReady",0); }

        public Task RetireAsync()
        {
            if (retirement != null) return retirement;
            retired = true; Suspend();
            return retirement = RetireCoreAsync();
        }

        private async Task RetireCoreAsync()
        {
            try { await geometry.RetireAsync(); }
            finally
            {
                bounce.Dispose(); light.Release(); direction.Release();
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
