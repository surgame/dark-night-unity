using System;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.ViewData;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>真实私有 Compute Shader 的隔离 GPU 夹具；明确格与灯姿态可重放，输出浮点读数，释放全部临时资源。</summary>
    public sealed class WallDiffuseGpuFixture : IDisposable
    {
        public const int Size = 96, Grid = 24;
        public readonly ComputeShader Shader;
        public readonly Texture2D Cells;
        public readonly RenderTexture Light, Direction;
        private readonly Texture2D read;
        private readonly ComputeBuffer bounce, candidates, candidateProfiles, candidateNodes, profiles, nodes;
        private readonly ComputeBuffer queryCounters = new ComputeBuffer(3, 4);
        private readonly Vector4[] positions = new Vector4[16], directions = new Vector4[16], colors = new Vector4[16];
        private readonly Vector4[] counts = new Vector4[16], mouths = new Vector4[16], shadows = new Vector4[16];
        private readonly Candidate[] candidateData = new Candidate[16 * 64];
        private readonly byte[] geometry = new byte[Grid * Grid];
        public int Count = 1, Probes = 2, Benchmark;
        public float Gain = 1, OriginX = 7, OriginY = 12, Intensity = 1.35f;
        public Vector2 Forward = Vector2.right;
        public float Cone = .7071068f, NearRange, Range = 14;
        public bool ReverseOrder, ReverseCandidates;
        public View.Lighting.WallDiffuseShape Shape;
        public bool MatchIncident;
        public float CornerRadius = .8f;

        public WallDiffuseGpuFixture(Func<int, int, byte> cell)
        {
            Shader = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"));
            Cells = new Texture2D(Grid, Grid, TextureFormat.RGBA32, false, true);
            for (int y = 0; y < Grid; y++) for (int x = 0; x < Grid; x++)
            {
                byte value = cell(x, y); geometry[y * Grid + x] = value;
                Cells.SetPixel(x, y, new Color((value & 64) != 0 ? 1 : 0, (value & 15) / 255f, 0, (value & 128) != 0 ? 1 : 0));
            }
            Cells.Apply(); Light = Target(); Direction = Target(); read = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true);
            bounce = new ComputeBuffer(16, 16); candidates = new ComputeBuffer(16 * 64, 32);
            candidateProfiles = new ComputeBuffer(16 * 64, 48); candidateNodes = new ComputeBuffer(16 * 64 * 33, 4);
            profiles = new ComputeBuffer(128, 48); nodes = new ComputeBuffer(128 * 33, 4);
        }

        public void Dispatch(bool continuous, bool legacy = false)
        {
            var boundary = LightBoundaryBuilder.Build(new LightGeometrySnapshot(Grid, Grid, geometry), null);
            for (int n = 0; n < 16; n++)
            {
                positions[n] = new Vector4(OriginX, OriginY + (ReverseOrder ? Count - 1 - n : n) * .3f, Range, Cone);
                directions[n] = new Vector4(Forward.x, Forward.y, NearRange, NearRange > 0 ? .5f : 0);
                colors[n] = new Vector4(1, .75f, .45f, Intensity);
                mouths[n] = new Vector4(.1875f, 0, 0, 0); shadows[n] = new Vector4(0, .045f, 0, 0);
                int found = 0;
                if (n < Count) for (int j = 0; j < boundary.Count && found < 64; j++)
                {
                    var wall = boundary[ReverseCandidates ? boundary.Count - 1 - j : j]; var a = new Vector2(wall.Ax, wall.Ay); var b = new Vector2(wall.Bx, wall.By);
                    if (Vector2.Dot(new Vector2(OriginX - a.x, positions[n].y - a.y), new Vector2(wall.Nx, wall.Ny)) <= 0 ||
                        !View.Lighting.WallDiffuseResources.Clip(ref a, ref b, new Vector2(OriginX, positions[n].y), Mathf.Min(6, Range), directions[n], Cone, .1875f)) continue;
                    candidateData[n * 64 + found] = new Candidate { Ends = new Vector4(a.x, a.y, b.x, b.y), Normal = new Vector4(wall.Nx, wall.Ny, 0, 0) }; found++;
                }
                counts[n] = new Vector4(found, 0, 0, 0);
            }
            Shader.SetVector("_DNKnownBounds", new Vector4(0, 0, Grid, Grid)); Shader.SetVector("_DNLightRect", new Vector4(0, 0, Grid, Grid));
            Shader.SetVector("_DNTargetSize", new Vector4(Size, Size, 0, 0)); Shader.SetVector("_DNShadowSettings", new Vector4(0, .375f, .65f, 0));
            Shader.SetInt("_DNLightCount", Count); Shader.SetFloat("_DNBounceStrength", legacy ? .12f : 0);
            Shader.SetVectorArray("_DNLightPositions", positions); Shader.SetVectorArray("_DNLightDirections", directions);
            Shader.SetVectorArray("_DNLightNearOrigins", positions); Shader.SetVectorArray("_DNLightColors", colors);
            Shader.SetVectorArray("_DNLightApertures", mouths); Shader.SetVectorArray("_DNLightShadows", shadows);
            Shader.SetVectorArray("_DNWallCandidateCounts", counts);
            Shader.SetVector("_DNDiffuseSettings", new Vector4(Gain, 1.5f, 6, 3.2f)); Shader.SetFloat("_DNDiffuseSpread", .65f);
            Shader.SetInt("_DNDiffuseProbes", Probes); Shader.SetInt("_DNDiffuseBenchmark", Benchmark);
            Shader.SetInt("_DNDiffuseShape", (int)Shape); Shader.SetInt("_DNDiffuseMatchIncident", MatchIncident ? 1 : 0);
            Shader.SetFloat("_DNDiffuseCornerRadius", CornerRadius);
            Shader.SetInt("_DNDiffuseCountQueries", 0);
            if (continuous)
            {
                candidates.SetData(candidateData);
                int prepare = Shader.FindKernel("PrepareWallProfiles"), select = Shader.FindKernel("SelectWallProfiles");
                Shader.SetTexture(prepare, "_DNLightCells", Cells); Shader.SetBuffer(prepare, "_DNWallCandidates", candidates);
                Shader.SetBuffer(prepare, "_DNDiffuseQueries", queryCounters);
                Shader.SetBuffer(prepare, "_DNCandidateProfiles", candidateProfiles); Shader.SetBuffer(prepare, "_DNCandidateNodes", candidateNodes);
                Shader.SetBuffer(select, "_DNCandidateProfiles", candidateProfiles); Shader.SetBuffer(select, "_DNCandidateNodes", candidateNodes);
                Shader.SetBuffer(select, "_DNWallCandidates", candidates); Shader.SetTexture(select, "_DNLightCells", Cells);
                Shader.SetBuffer(select, "_DNDiffuseQueries", queryCounters);
                Shader.SetBuffer(select, "_DNWallProfiles", profiles); Shader.SetBuffer(select, "_DNWallNodes", nodes);
                Shader.Dispatch(prepare, 1, 16, 1); Shader.Dispatch(select, 1, 1, 1);
            }
            else if (legacy)
            {
                int resolve = Shader.FindKernel("ResolveSources"); Shader.SetTexture(resolve, "_DNLightCells", Cells);
                Shader.SetBuffer(resolve, "_DNBounceSources", bounce); Shader.Dispatch(resolve, 1, 1, 1);
            }
            int kernel = Shader.FindKernel(continuous ? "IlluminateDiffuse" : "Illuminate");
            Shader.SetTexture(kernel, "_DNLightCells", Cells); Shader.SetTexture(kernel, "_DNLightOutput", Light);
            Shader.SetTexture(kernel, "_DNDirectionOutput", Direction); Shader.SetBuffer(kernel, "_DNBounceSources", bounce);
            if (continuous) { Shader.SetBuffer(kernel, "_DNWallProfiles", profiles); Shader.SetBuffer(kernel, "_DNWallNodes", nodes); Shader.SetBuffer(kernel, "_DNDiffuseQueries", queryCounters); }
            Shader.Dispatch(kernel, Size / 8, Size / 8, 1);
        }

        public Color[] Pixels(bool direction = false)
        {
            var previous = RenderTexture.active;
            try { RenderTexture.active = direction ? Direction : Light; read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); read.Apply(); return read.GetPixels(); }
            finally { RenderTexture.active = previous; }
        }
        public void SetCurrentCell(int x, int y, byte value)
        {
            Cells.SetPixel(x, y, new Color((value & 64) != 0 ? 1 : 0, (value & 15) / 255f, 0, (value & 128) != 0 ? 1 : 0));
            Cells.Apply(); // 冻结候选几何故意保持旧版，验证当前 GPU 格能立即屏蔽旧墙贡献。
        }
        public Color At(Color[] pixels, float x, float y) => pixels[Mathf.FloorToInt(y / Grid * Size) * Size + Mathf.FloorToInt(x / Grid * Size)];
        public Vector4[] Profiles()
        {
            var data = new Profile[128]; profiles.GetData(data); var flat = new Vector4[384];
            for (int i = 0; i < data.Length; i++) { flat[i * 3] = data[i].Frame; flat[i * 3 + 1] = data[i].Shape; flat[i * 3 + 2] = data[i].Stats; }
            return flat;
        }
        public float[] NodesData() { var data = new float[128 * 33]; nodes.GetData(data); return data; }
        public void UploadProfiles(Vector4[] data, float[] values)
        {
            var packed = new Profile[128];
            for (int i = 0; i < packed.Length; i++) packed[i] = new Profile { Frame = data[i * 3], Shape = data[i * 3 + 1], Stats = data[i * 3 + 2] };
            profiles.SetData(packed); nodes.SetData(values);
        }
        private static RenderTexture Target()
        { var value = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGBFloat) { enableRandomWrite = true }; value.Create(); return value; }
        public void Dispose()
        {
            foreach (var buffer in new[] { bounce, candidates, candidateProfiles, candidateNodes, profiles, nodes, queryCounters }) buffer.Dispose();
            Light.Release(); Direction.Release();
            foreach (var obj in new UnityEngine.Object[] { Shader, Cells, Light, Direction, read }) UnityEngine.Object.DestroyImmediate(obj);
        }
        /// <summary>与候选缓冲的 32 字节结构配对，避免 float4 数量与 GPU 元素数量混淆。</summary>
        private struct Candidate { public Vector4 Ends, Normal; }
        /// <summary>与 GPU 的 48 字节墙段合同配对，读回后才展开为浮点向量。</summary>
        private struct Profile { public Vector4 Frame, Shape, Stats; }
    }
}
