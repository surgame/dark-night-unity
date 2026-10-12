using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

namespace DarkNights.View.Lighting
{
    /// <summary>连续漫反射的固定容量 GPU 资源；主线程裁剪空间候选，GPU 选两物理段，节点不作为点光源，关闭后资源可退休。</summary>
    public sealed class WallDiffuseResources
    {
        public const int Lights = 16, CandidatesPerLight = 64, Nodes = 33, SegmentsPerLight = 2;
        private readonly LightBoundaryCache cache;
        private readonly Candidate[] candidates = new Candidate[Lights * CandidatesPerLight];
        private readonly Vector4[] counts = new Vector4[Lights];
        private readonly float[] scores = new float[CandidatesPerLight];
        private int[] visited = Array.Empty<int>();
        private int stamp;
        private LightBoundarySnapshot indexed;
        private readonly ComputeBuffer candidateBuffer, candidateProfiles, candidateNodes, profiles, nodes;
        private readonly int prepare, select;
        private readonly ComputeBuffer queryCounters = new ComputeBuffer(3, 4);
        private readonly uint[] emptyCounters = new uint[3];
        private static readonly CustomSampler profileSampler = CustomSampler.Create("DN.Diffuse.Profile", true);
        private static readonly CustomSampler selectSampler = CustomSampler.Create("DN.Diffuse.Select", true);
        public int CandidateCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int BuildCount => cache.BuildCount;
        public int DiscardCount => cache.DiscardCount;
        public double WorkerMilliseconds => cache.WorkerMilliseconds;
        public double FreezeMilliseconds => cache.FreezeMilliseconds;
        public double UploadMilliseconds { get; private set; }
        public double PrepareMilliseconds { get; private set; }
        public long BufferBytes => Lights * CandidatesPerLight * (32 + 48 + Nodes * 4) + Lights * SegmentsPerLight * (48 + Nodes * 4) + 12;
        public long UploadedBytes { get; private set; }

        public WallDiffuseResources(ComputeShader shader, Func<LightGeometrySnapshot, Func<bool>, LightBoundarySnapshot> builder)
        {
            cache = new LightBoundaryCache(builder);
            candidateBuffer = new ComputeBuffer(Lights * CandidatesPerLight, 32);
            candidateProfiles = new ComputeBuffer(Lights * CandidatesPerLight, 48);
            candidateNodes = new ComputeBuffer(Lights * CandidatesPerLight * Nodes, 4);
            profiles = new ComputeBuffer(Lights * SegmentsPerLight, 48);
            nodes = new ComputeBuffer(Lights * SegmentsPerLight * Nodes, 4);
            prepare = shader.FindKernel("PrepareWallProfiles"); select = shader.FindKernel("SelectWallProfiles");
        }

        public void Prepare(ComputeShader shader, CommandBuffer commands, CaveVisualSource source, RectInt bounds,
            Vector4[] positions, Vector4[] directions, Vector4[] apertures, int count, ExplorationLightSettings settings, bool countQueries = false)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            cache.Tick(source, bounds);
            var snapshot = cache.Snapshot;
            if (indexed != snapshot)
            { indexed = snapshot; visited = snapshot == null ? Array.Empty<int>() : new int[snapshot.Count]; }
            CandidateCount = 0; DroppedCount = 0;
            Profiler.BeginSample("DN.Diffuse.Candidates");
            try
            {
                for (int n = 0; n < Lights; n++)
                {
                    int found = snapshot != null && n < count && positions[n].w > -.9f && settings.DiffuseGain > 0 ?
                        Query(snapshot, cache.Bounds, positions[n], directions[n], apertures[n].x, n, settings.DiffuseFarDistance) : 0;
                    counts[n] = new Vector4(found, 0, 0, 0); CandidateCount += found;
                }
            }
            finally { Profiler.EndSample(); }
            Profiler.BeginSample("DN.Diffuse.Upload");
            long uploadStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try { candidateBuffer.SetData(candidates); UploadedBytes = candidates.Length * 32; }
            finally { Profiler.EndSample(); }
            UploadMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - uploadStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            shader.SetInt("_DNDiffuseCountQueries", countQueries ? 1 : 0);
            if (countQueries) queryCounters.SetData(emptyCounters);
            shader.SetVectorArray("_DNWallCandidateCounts", counts);
            shader.SetVector("_DNDiffuseSettings", new Vector4(settings.DiffuseGain, settings.DiffuseNearDistance, settings.DiffuseFarDistance, settings.DiffuseMaxReach));
            shader.SetFloat("_DNDiffuseSpread", settings.DiffuseLateralSpread);
            shader.SetInt("_DNDiffuseProbes", settings.DiffuseVisibilityProbes);
            shader.SetBuffer(prepare, "_DNWallCandidates", candidateBuffer);
            shader.SetBuffer(prepare, "_DNCandidateProfiles", candidateProfiles); shader.SetBuffer(prepare, "_DNCandidateNodes", candidateNodes);
            shader.SetTexture(prepare, "_DNLightCells", source.LightingGeometry);
            shader.SetBuffer(prepare, "_DNDiffuseQueries", queryCounters);
            shader.SetBuffer(select, "_DNCandidateProfiles", candidateProfiles); shader.SetBuffer(select, "_DNCandidateNodes", candidateNodes);
            shader.SetBuffer(select, "_DNWallProfiles", profiles); shader.SetBuffer(select, "_DNWallNodes", nodes);
            commands.SetComputeIntParam(shader, "_DNDiffuseQueryStage", 0);
            commands.BeginSample(profileSampler); commands.DispatchCompute(shader, prepare, 1, Lights, 1); commands.EndSample(profileSampler);
            commands.BeginSample(selectSampler); commands.DispatchCompute(shader, select, 1, 1, 1); commands.EndSample(selectSampler);
            commands.SetComputeIntParam(shader, "_DNDiffuseQueryStage", 1);
            PrepareMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public void Bind(ComputeShader shader, int kernel)
        { shader.SetBuffer(kernel, "_DNWallProfiles", profiles); shader.SetBuffer(kernel, "_DNWallNodes", nodes); shader.SetBuffer(kernel, "_DNDiffuseQueries", queryCounters); }
        public uint[] ReadQueryCounts() { var result = new uint[3]; queryCounters.GetData(result); return result; }

        private int Query(LightBoundarySnapshot snapshot, RectInt bounds, Vector4 origin, Vector4 forward, float aperture, int light, float far)
        {
            if (++stamp == int.MaxValue) { Array.Clear(visited, 0, visited.Length); stamp = 1; }
            float radius = Mathf.Min(origin.z, far);
            Vector2 center = new Vector2(origin.x - bounds.x, origin.y - bounds.y);
            int left = Mathf.Max(0, Mathf.FloorToInt((center.x - radius) / 8)), top = Mathf.Max(0, Mathf.FloorToInt((center.y - radius) / 8));
            int right = Mathf.Min(snapshot.Columns - 1, Mathf.FloorToInt((center.x + radius) / 8));
            int bottom = Mathf.Min((snapshot.Height - 1) / 8, Mathf.FloorToInt((center.y + radius) / 8));
            int count = 0;
            for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
                for (int i = 0; i < snapshot.PageCount(x, y); i++)
                {
                    int id = snapshot.PageSegment(x, y, i);
                    if (visited[id] == stamp) continue;
                    visited[id] = stamp; var edge = snapshot[id];
                    var a = new Vector2(edge.Ax, edge.Ay); var b = new Vector2(edge.Bx, edge.By);
                    var normal = new Vector2(edge.Nx, edge.Ny);
                    if (Vector2.Dot(center - a, normal) <= .002f || !Clip(ref a, ref b, center, radius, forward, origin.w, aperture)) continue;
                    float length = (b - a).magnitude;
                    if (length < .01f) continue;
                    float quality = length / Mathf.Max(.01f, Vector2.Distance(center, (a + b) * .5f));
                    int slot = count;
                    if (count == CandidatesPerLight)
                    {
                        DroppedCount++; slot = 0;
                        for (int j = 1; j < count; j++) if (scores[j] < scores[slot]) slot = j;
                        if (quality <= scores[slot]) continue;
                    }
                    else count++;
                    scores[slot] = quality;
                    a += new Vector2(bounds.x, bounds.y); b += new Vector2(bounds.x, bounds.y);
                    candidates[light * CandidatesPerLight + slot] = new Candidate
                    { Ends = new Vector4(a.x, a.y, b.x, b.y), Normal = new Vector4(normal.x, normal.y, 0, 0) };
                }
            return count;
        }

        /// <summary>先裁圆形照距及有限灯口锥束，再采样长物理墙段；投影阴影留给 GPU 的零照度节点。</summary>
        public static bool Clip(ref Vector2 a, ref Vector2 b, Vector2 origin, float radius, Vector4 forward, float cutoff, float aperture)
        {
            Vector2 delta = b - a, offset = a - origin;
            float squared = delta.sqrMagnitude, projection = Vector2.Dot(offset, delta), discriminant = projection * projection - squared * (offset.sqrMagnitude - radius * radius);
            if (squared < .000001f || discriminant <= 0) return false;
            float root = Mathf.Sqrt(discriminant), lo = Mathf.Max(0, (-projection - root) / squared), hi = Mathf.Min(1, (-projection + root) / squared);
            Vector2 f = new Vector2(forward.x, forward.y), side = new Vector2(-f.y, f.x);
            float tangent = Mathf.Sqrt(Mathf.Max(0, 1 - cutoff * cutoff)) / Mathf.Max(cutoff, .001f);
            if (!Half(Vector2.Dot(offset, f), Vector2.Dot(delta, f), ref lo, ref hi) ||
                !Half(aperture + Vector2.Dot(offset, f) * tangent - Vector2.Dot(offset, side), Vector2.Dot(delta, f) * tangent - Vector2.Dot(delta, side), ref lo, ref hi) ||
                !Half(aperture + Vector2.Dot(offset, f) * tangent + Vector2.Dot(offset, side), Vector2.Dot(delta, f) * tangent + Vector2.Dot(delta, side), ref lo, ref hi) || hi <= lo) return false;
            b = a + delta * hi; a += delta * lo; return true;
        }
        private static bool Half(float value, float change, ref float lo, ref float hi)
        {
            if (Mathf.Abs(change) < .000001f) return value >= 0;
            float t = -value / change;
            if (change > 0) lo = Mathf.Max(lo, t); else hi = Mathf.Min(hi, t);
            return hi > lo;
        }

        public async Task RetireAsync()
        {
            try { await cache.RetireAsync(); }
            finally { candidateBuffer.Dispose(); candidateProfiles.Dispose(); candidateNodes.Dispose(); profiles.Dispose(); nodes.Dispose(); queryCounters.Dispose(); }
        }

        /// <summary>与 HLSL 两个 float4 严格配对的上传合同；包含裁剪端点和已知空气侧法线。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Candidate
        { public Vector4 Ends; public Vector4 Normal; }
    }
}
