using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

namespace DarkNights.View.Lighting
{
    /// <summary>连续漫反射的固定容量 GPU 资源；GPU 选两个相邻受光组，每组最多四段，节点只描述密度，关闭后资源可退休。</summary>
    public sealed class WallDiffuseResources
    {
        public const int Lights = 16, CandidatesPerLight = 64, Nodes = 33, SegmentsPerLight = 8;
        private readonly LightBoundaryCache cache;
        private readonly WallDiffusePreparationCache preparation = new WallDiffusePreparationCache();
        private readonly Candidate[] candidates = new Candidate[Lights * CandidatesPerLight];
        private readonly Vector4[] counts = new Vector4[Lights];
        private readonly float[] scores = new float[CandidatesPerLight];
        private int[] visited = Array.Empty<int>();
        private int stamp;
        private Texture boundGeometry;
        private int boundKernel = -1;
        private bool queryFlagReady, queriesEnabled;
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
        public double BoundaryTickMilliseconds { get; private set; }
        public double CandidateMilliseconds { get; private set; }
        public long BufferBytes => Lights * CandidatesPerLight * (32 + 48 + Nodes * 4) + Lights * SegmentsPerLight * (48 + Nodes * 4) + 12;
        public long UploadedBytes { get; private set; }
        public int PreparationCount { get; private set; }
        public bool Settled => cache.Settled;

        public WallDiffuseResources(ComputeShader shader, Func<LightGeometrySnapshot, Func<bool>, LightBoundarySnapshot> builder)
        {
            cache = new LightBoundaryCache(builder);
            candidateBuffer = new ComputeBuffer(Lights * CandidatesPerLight, 32);
            candidateProfiles = new ComputeBuffer(Lights * CandidatesPerLight, 48);
            candidateNodes = new ComputeBuffer(Lights * CandidatesPerLight * Nodes, 4);
            profiles = new ComputeBuffer(Lights * SegmentsPerLight, 48);
            nodes = new ComputeBuffer(Lights * SegmentsPerLight * Nodes, 4);
            prepare = shader.FindKernel("PrepareWallProfiles"); select = shader.FindKernel("SelectWallProfiles");
            shader.SetBuffer(prepare, "_DNWallCandidates", candidateBuffer);
            shader.SetBuffer(prepare, "_DNCandidateProfiles", candidateProfiles); shader.SetBuffer(prepare, "_DNCandidateNodes", candidateNodes);
            shader.SetBuffer(prepare, "_DNDiffuseQueries", queryCounters);
            shader.SetBuffer(select, "_DNCandidateProfiles", candidateProfiles); shader.SetBuffer(select, "_DNCandidateNodes", candidateNodes);
            shader.SetBuffer(select, "_DNWallCandidates", candidateBuffer); shader.SetBuffer(select, "_DNDiffuseQueries", queryCounters);
            shader.SetBuffer(select, "_DNWallProfiles", profiles); shader.SetBuffer(select, "_DNWallNodes", nodes);
        }

        public void Prepare(ComputeShader shader, CommandBuffer commands, CaveVisualSource source, RectInt bounds,
            Vector4[] positions, Vector4[] directions, Vector4[] apertures, Vector4[] colors, Vector4[] shadows,
            int count, LightBounceRules[] rules, bool countQueries = false, int benchmark = 0)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            cache.Tick(source, bounds);
            BoundaryTickMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            var snapshot = cache.Snapshot;
            if (!queryFlagReady || queriesEnabled != countQueries)
            { shader.SetInt("_DNDiffuseCountQueries", countQueries ? 1 : 0); queriesEnabled = countQueries; queryFlagReady = true; }
            UploadedBytes = 0; UploadMilliseconds = 0; CandidateMilliseconds = 0;
            if (!countQueries && preparation.Matches(source, snapshot, bounds, positions, directions, apertures, colors, shadows, rules, count, benchmark))
            {
                PrepareMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                return;
            }
            if (indexed != snapshot)
            { indexed = snapshot; visited = snapshot == null ? Array.Empty<int>() : new int[snapshot.Count]; }
            CandidateCount = 0; DroppedCount = 0;
            Profiler.BeginSample("DN.Diffuse.Candidates");
            long candidateStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                for (int n = 0; n < Lights; n++)
                {
                    int found = snapshot != null && n < count && WallDiffusePreparationCache.Enabled(positions[n], colors[n], rules[n]) ?
                        Query(snapshot, cache.Bounds, positions[n], directions[n], apertures[n].x, n, rules[n].DiffuseFarDistance) : 0;
                    counts[n] = new Vector4(found, 0, 0, 0); CandidateCount += found;
                }
            }
            finally { Profiler.EndSample(); }
            CandidateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - candidateStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Profiler.BeginSample("DN.Diffuse.Upload");
            long uploadStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try { candidateBuffer.SetData(candidates, 0, 0, count * CandidatesPerLight); UploadedBytes = count * CandidatesPerLight * 32; }
            finally { Profiler.EndSample(); }
            UploadMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - uploadStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (countQueries) queryCounters.SetData(emptyCounters);
            shader.SetVectorArray("_DNWallCandidateCounts", counts);
            if (boundGeometry != source.LightingGeometry)
            {
                shader.SetTexture(prepare, "_DNLightCells", source.LightingGeometry);
                shader.SetTexture(select, "_DNLightCells", source.LightingGeometry); boundGeometry = source.LightingGeometry;
            }
            commands.SetComputeIntParam(shader, "_DNDiffuseQueryStage", 0);
            commands.BeginSample(profileSampler); commands.DispatchCompute(shader, prepare, 1, count, 1); commands.EndSample(profileSampler);
            commands.BeginSample(selectSampler); commands.DispatchCompute(shader, select, 1, 1, 1); commands.EndSample(selectSampler);
            commands.SetComputeIntParam(shader, "_DNDiffuseQueryStage", 1);
            preparation.Remember(source, snapshot, bounds, positions, directions, apertures, colors, shadows, rules, count, benchmark);
            PreparationCount++;
            PrepareMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public void Bind(ComputeShader shader, int kernel)
        {
            if (boundKernel == kernel) return;
            shader.SetBuffer(kernel, "_DNWallProfiles", profiles); shader.SetBuffer(kernel, "_DNWallNodes", nodes);
            shader.SetBuffer(kernel, "_DNDiffuseQueries", queryCounters); boundKernel = kernel;
        }
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
