using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>只读格到洞穴贴图的显示适配；输入格原位变化按局部页更新，岩壁纹理与背景资源独立释放。</summary>
    public sealed class CaveVisualSource : IMapChunkSource, IDisposable
    {
        private const int W = 320, H = 192, Page = 32;
        private readonly IMapChunkSource source;
        private readonly Color32[] cells = new Color32[W * H];
        private readonly Dictionary<uint, byte> materials = new Dictionary<uint, byte>();
        private readonly Color32[] ores = new Color32[W * H];
        private readonly Color32[] lightPixels = new Color32[W * H];
        private readonly HashSet<int> mapPages = new HashSet<int>(), lightPages = new HashSet<int>();
        private readonly HashSet<int> lightMarkers = new HashSet<int>(), terrainLightChanges = new HashSet<int>();
        private readonly Dictionary<int, int> lightMarkerAnchors = new Dictionary<int, int>();
        private readonly Color32[] uploadPixels = new Color32[Page * Page];
        private readonly Texture2D oreMap, mapUpload, lightUpload;
        private readonly Texture2D light;
        private readonly RenderTexture map;
        private readonly Material background;
        private readonly GameObject backdrop;
        private readonly Mesh mesh;
        private readonly CaveBackgroundCache staticBackground;
        private readonly CaveRockSurface rockSurface;
        private readonly CaveLocalRockSurface localRockSurface;
        private int mineralHash, deviceHash;
        private int chunkSize = 32;
        private byte[] deviceLights;
        private bool oreDirty = true, lightFullDirty = true, lightInitialized, rockInitialized;
        public bool IsSourceDriven => source is ITerrainInputSource;
        public bool BackgroundReady => (staticBackground == null || staticBackground.Ready) &&
            (rockSurface == null || rockSurface.Ready) && (localRockSurface == null || localRockSurface.Ready);
        public int RockBuildCount => (rockSurface?.BuildCount ?? 0) + (localRockSurface?.BuildCount ?? 0);
        public int BackgroundBuildCount => staticBackground?.BuildCount ?? 0;
        public long BackgroundUploadedBytes => staticBackground?.UploadedBytes ?? 0;
        public int BackgroundResidentPages => staticBackground?.ResidentPages ?? 0;
        public Material Material { get; }

        public CaveVisualSource(IMapChunkSource source, CaveTerrainStyle style, TileCatalog catalog, Transform parent,
            DarkNights.Core.Config.Terrain.BackgroundBakeDescriptor reference = null)
        {
            this.source = source;
            string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
            for (int i = 0; i < keys.Length; i++) materials[catalog.ByKey(keys[i])] = (byte)(i + 1);
            map = CreateTarget("Cave logical cells", W, H, GraphicsFormat.R8G8B8A8_UNorm);
            light = CreateTarget("Cave soft light", W, H, GraphicsFormat.R8G8B8A8_UNorm);
            mapUpload = new Texture2D(Page, Page, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            lightUpload = new Texture2D(Page, Page, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear };
            Material = new Material(style.Shader) { name = "Cave per-map rock" };
            Material.SetTexture("_CaveMap", map); Material.SetTexture("_CaveLight", light);
            Material.SetMatrix("_MapWorldToLocal", parent.worldToLocalMatrix);
            var modifiers = style.CaptureModifiers();
            if (style.ProceduralRock && modifiers.SupportsLocalRoundedCluster)
                localRockSurface = new CaveLocalRockSurface(Material, reference?.LayoutSeed ?? "DN-MATERIAL-0921", style);
            else if (style.ProceduralRock)
                rockSurface = new CaveRockSurface(Material, reference?.LayoutSeed ?? "DN-MATERIAL-0921", style);
            else Material.SetTexture("_RockTex", style.Rock);
            oreMap = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Material.SetTexture("_OreMap", oreMap);
            background = new Material(Material); background.SetFloat("_Background", 1);
            background.SetMatrix("_MapWorldToLocal", parent.worldToLocalMatrix);
            backdrop = new GameObject("Cave distant wall"); backdrop.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Cave backdrop quad" };
            mesh.vertices = new[] { new Vector3(-1, -193, 1), new Vector3(321, -193, 1), new Vector3(321, 12, 1), new Vector3(-1, 12, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
            backdrop.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = backdrop.AddComponent<MeshRenderer>(); renderer.sharedMaterial = background; renderer.sortingOrder = -100;
            if (style.Background != null && style.Background.ContourStatic)
                staticBackground = new CaveBackgroundCache(reference, style.Background, background, parent, style.ProceduralRock ? style.CaptureOutline() : null);
        }

        public async Task<MapChunkData> LoadAsync(WorldDescriptor descriptor, ChunkCoord coordinate, CancellationToken cancellation)
        {
            MapChunkData data = await source.LoadAsync(descriptor, coordinate, cancellation);
            int size = descriptor.ChunkSize;
            chunkSize = size;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int u = coordinate.U * size + x, row = -(coordinate.V * size + y);
                if (u < 0 || u >= W || row < 0 || row >= H) continue;
                cells[row * W + u] = ToColor(data.Cells[y * size + x]);
                MarkPage(mapPages, u, row);
            }
            return data;
        }

        /// <summary>把已经原子安装的输入批次映射到显示缓存，仅把真实变化格交给岩壁失效队列。</summary>
        public void ApplyInput(MapInputBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            var changed = new List<int>(batch.Cells.Count);
            foreach (var cell in batch.Cells) ApplyCell(cell.Position, cell.Value, changed);
            foreach (var snapshot in batch.SnapshotChunks)
            {
                int size = chunkSize;
                for (int i = 0; i < snapshot.Cells.Count; i++)
                {
                    int u = snapshot.Coordinate.U * size + i % size, v = snapshot.Coordinate.V * size + i / size;
                    if (u < 0 || u >= W || -v < 0 || -v >= H) continue;
                    ApplyCell(new CellCoord(u, v), snapshot.Cells[i], changed);
                }
            }
            if (changed.Count == 0) return;
            if (rockInitialized)
            {
                localRockSurface?.ApplyChanges(cells, changed);
                rockSurface?.Replace(cells);
            }
            if (localRockSurface != null) foreach (int index in changed) terrainLightChanges.Add(index);
            else lightFullDirty = true;
        }

        public void SetMinerals(IReadOnlyList<DarkNights.Core.ViewData.WorksiteViewData> deposits)
        {
            if (rockSurface != null || localRockSurface != null) return;
            int hash = 17;
            foreach (var d in deposits) if (d.IsMineralDeposit) hash = unchecked(hash * 31 + d.Id * 17 + d.Amount);
            if (hash == mineralHash) return;
            mineralHash = hash; Array.Clear(ores, 0, ores.Length);
            foreach (var d in deposits)
            {
                if (!d.IsMineralDeposit || d.Amount <= 0) continue;
                int x = Mathf.FloorToInt(d.X / 16 + .5f), y = Mathf.FloorToInt((float)d.Y + 1);
                if (x < 0 || x >= W || y < 0 || y >= H) continue;
                ores[y * W + x] = new Color32(d.Rarity == "rare" ? (byte)255 : (byte)0, 255, 0, 255);
            }
            oreDirty = lightFullDirty = true;
        }

        public void Flush()
        {
            if (!rockInitialized && (rockSurface != null || localRockSurface != null))
            {
                rockSurface?.Replace(cells); localRockSurface?.Replace(cells); rockInitialized = true;
            }
            if (oreDirty) { oreMap.SetPixels32(ores); oreMap.Apply(false, false); oreDirty = false; }
            if (lightFullDirty)
            {
                if (localRockSurface != null)
                    CaveSoftLightField.SyncMarkers(cells, W, H, deviceLights, lightMarkers, lightMarkerAnchors, null, mapPages);
                var next = localRockSurface != null || rockSurface != null ? CaveSoftLightField.Build(cells, W, H, deviceLights) : CaveLightField.Build(cells, W, H, ores, deviceLights);
                for (int i = 0; i < next.Length; i++)
                    if (!lightInitialized || !next[i].Equals(lightPixels[i])) MarkPage(lightPages, i % W, i / W);
                Array.Copy(next, lightPixels, next.Length); lightInitialized = true; lightFullDirty = false;
                terrainLightChanges.Clear();
            }
            else if (localRockSurface != null && terrainLightChanges.Count != 0) RebuildChangedLightPages();
            UploadPages(map, mapUpload, cells, mapPages); mapPages.Clear();
            UploadPages(light, lightUpload, lightPixels, lightPages); lightPages.Clear();
        }

        public void SetDevices(DarkNights.Core.ViewData.WorldViewData world)
        {
            if (world.Expedition == null) return;
            int hash = 17;
            foreach (var b in world.Expedition.Devices) if (b.Powered) hash = unchecked(hash * 31 + b.Id * 7 + b.Height.GetHashCode());
            foreach (var b in world.Buildings) hash = unchecked(hash * 31 + b.X.GetHashCode());
            foreach (var a in world.Actors)
                if (a.Kind == "scout-drone") hash = unchecked(hash * 31 + Mathf.RoundToInt(a.X / 16) * 7 + Mathf.RoundToInt(a.Height / 16));
            if (deviceLights != null && hash == deviceHash) return;
            deviceHash = hash; deviceLights = new byte[W * H];
            foreach (var b in world.Buildings)
            {
                if (b.Kind != "lamp" && b.Kind != "ship") continue;
                foreach (var d in world.Expedition.Devices)
                {
                    if (d.Id != b.Id || !d.Powered) continue;
                    int x = Mathf.RoundToInt(b.X / 16), y = Mathf.RoundToInt((632 - d.Height - 16) / 16);
                    if (x >= 0 && x < W && y >= 0 && y < H) deviceLights[y * W + x] = 255;
                }
            }
            foreach (var a in world.Actors)
            {
                if (a.Kind != "scout-drone" || a.Hp <= 0) continue;
                int x = Mathf.RoundToInt(a.X / 16), y = Mathf.RoundToInt((632 - a.Height) / 16);
                if (x >= 0 && x < W && y >= 0 && y < H) deviceLights[y * W + x] = 255;
            }
            lightFullDirty = true;
        }

        public void SetVisible(GridBounds bounds)
        {
            backdrop.SetActive(bounds.IsValid);
            staticBackground?.SetVisible(bounds); rockSurface?.SetVisible(bounds); localRockSurface?.SetVisible(bounds);
        }
        public void TickBackground() { staticBackground?.Tick(); rockSurface?.Tick(); localRockSurface?.Tick(); }

        public void Dispose()
        {
            staticBackground?.Dispose(); rockSurface?.Dispose(); localRockSurface?.Dispose();
            UnityEngine.Object.Destroy(backdrop); UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(Material); UnityEngine.Object.Destroy(background);
            UnityEngine.Object.Destroy(map); UnityEngine.Object.Destroy(light); UnityEngine.Object.Destroy(oreMap);
            UnityEngine.Object.Destroy(mapUpload); UnityEngine.Object.Destroy(lightUpload);
        }

        private void ApplyCell(CellCoord position, GridCell cell, List<int> changed)
        {
            int u = position.U, row = -position.V;
            if (u < 0 || u >= W || row < 0 || row >= H) return;
            int index = row * W + u; Color32 next = ToColor(cell);
            if (cells[index].r == next.r && cells[index].g == next.g) return;
            next.b = cells[index].b;
            cells[index] = next; changed.Add(index); MarkPage(mapPages, u, row);
        }

        private Color32 ToColor(GridCell cell) => new Color32(cell.IsEmpty ? (byte)0 : materials[cell.TileId],
            (byte)((cell.Flags >> 1) & 15), 0, 255);

        private static RenderTexture CreateTarget(string name, int width, int height, GraphicsFormat format)
        {
            var descriptor = new RenderTextureDescriptor(width, height, format, 0)
            { msaaSamples = 1, useMipMap = false, autoGenerateMips = false };
            var texture = new RenderTexture(descriptor) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.Create(); var active = RenderTexture.active; RenderTexture.active = texture;
            GL.Clear(false, true, Color.clear); RenderTexture.active = active; return texture;
        }

        private static void MarkPage(HashSet<int> pages, int x, int y) => pages.Add(y / Page * (W / Page) + x / Page);

        private void RebuildChangedLightPages()
        {
            CaveSoftLightField.SyncMarkers(cells, W, H, deviceLights, lightMarkers, lightMarkerAnchors,
                terrainLightChanges, mapPages);
            var affected = new HashSet<int>();
            foreach (int index in terrainLightChanges)
            {
                int x = index % W, y = index / W;
                for (int py = Math.Max(0, (y - 18) / Page); py <= Math.Min(H / Page - 1, (y + 18) / Page); py++)
                    for (int px = Math.Max(0, (x - 18) / Page); px <= Math.Min(W / Page - 1, (x + 18) / Page); px++) affected.Add(py * (W / Page) + px);
            }
            var sources = new List<int>(lightMarkers); sources.Sort();
            foreach (int page in affected)
            {
                int left = page % (W / Page) * Page, top = page / (W / Page) * Page;
                var region = CaveSoftLightField.BuildRegion(cells, W, H, deviceLights, left, top, Page, Page, sources);
                bool changed = false;
                for (int y = 0; y < Page; y++)
                {
                    int target = (top + y) * W + left, source = y * Page;
                    for (int x = 0; x < Page; x++)
                        if (!lightPixels[target + x].Equals(region[source + x])) { lightPixels[target + x] = region[source + x]; changed = true; }
                }
                if (changed) lightPages.Add(page);
            }
            terrainLightChanges.Clear();
        }

        private void UploadPages(RenderTexture target, Texture2D staging, Color32[] sourcePixels, HashSet<int> pages)
        {
            foreach (int page in pages)
            {
                int pageX = page % (W / Page), pageY = page / (W / Page), left = pageX * Page, top = pageY * Page;
                for (int y = 0; y < Page; y++) Array.Copy(sourcePixels, (top + y) * W + left, uploadPixels, y * Page, Page);
                staging.SetPixels32(uploadPixels); staging.Apply(false, false);
                Graphics.CopyTexture(staging, 0, 0, 0, 0, Page, Page, target, 0, 0, left, top);
            }
        }

    }
}
