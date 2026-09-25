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
    /// <summary>只读格到洞穴贴图的适配；前景、局部柔光和冻结背景分开失效，资源归当前表现宿主。</summary>
    public sealed class CaveVisualSource : IMapChunkSource, IDisposable
    {
        private const int W = 320, H = 192, Page = 32;
        private readonly IMapChunkSource source;
        private readonly Color32[] cells = new Color32[W * H], ores = new Color32[W * H], lightPixels = new Color32[W * H];
        private readonly Dictionary<uint, byte> materials = new Dictionary<uint, byte>();
        private readonly HashSet<int> mapPages = new HashSet<int>(), lightPages = new HashSet<int>();
        private readonly HashSet<int> lightMarkers = new HashSet<int>(), terrainLightChanges = new HashSet<int>();
        private readonly Dictionary<int, int> lightMarkerAnchors = new Dictionary<int, int>();
        private HashSet<int> deviceCells = new HashSet<int>();
        private readonly Color32[] uploadPixels = new Color32[Page * Page];
        private readonly Texture2D oreMap, mapUpload, lightUpload;
        private readonly RenderTexture map, light;
        private readonly Material background;
        private readonly GameObject backdrop;
        private readonly Mesh mesh;
        private readonly CaveBackgroundCache staticBackground;
        private readonly CaveRockSurface rockSurface;
        private readonly CaveLocalRockSurface localRockSurface;
        private int mineralHash, chunkSize = 32;
        private byte[] deviceLights;
        private bool oreDirty = true, lightFullDirty = true, lightInitialized, rockInitialized;
        public bool IsSourceDriven => source is ITerrainInputSource;
        public bool BackgroundReady => (staticBackground == null || staticBackground.Ready) &&
            (rockSurface == null || rockSurface.Ready) && (localRockSurface == null || localRockSurface.Ready);
        public string RefreshPath => localRockSurface != null ? "LocalV2 / bounded patches" :
            rockSurface != null ? "LegacyV1 / full foreground bake" : "DualGrid / texture";
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
            light.filterMode = FilterMode.Bilinear;
            mapUpload = new Texture2D(Page, Page, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            lightUpload = new Texture2D(Page, Page, TextureFormat.RGBA32, false, true);
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
            chunkSize = descriptor.ChunkSize;
            for (int y = 0; y < chunkSize; y++) for (int x = 0; x < chunkSize; x++)
            {
                int u = coordinate.U * chunkSize + x, row = -(coordinate.V * chunkSize + y);
                if (u < 0 || u >= W || row < 0 || row >= H) continue;
                cells[row * W + u] = ToColor(data.Cells[y * chunkSize + x]); MarkPage(mapPages, u, row);
            }
            return data;
        }
        public void ApplyInput(MapInputBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            var changed = new List<int>(batch.Cells.Count);
            foreach (var cell in batch.Cells) ApplyCell(cell.Position, cell.Value, changed);
            foreach (var snapshot in batch.SnapshotChunks)
                for (int i = 0; i < snapshot.Cells.Count; i++)
                    ApplyCell(new CellCoord(snapshot.Coordinate.U * chunkSize + i % chunkSize,
                        snapshot.Coordinate.V * chunkSize + i / chunkSize), snapshot.Cells[i], changed);
            if (changed.Count == 0) return;
            if (rockInitialized) { localRockSurface?.ApplyChanges(cells, changed); rockSurface?.Replace(cells); }
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
            { rockSurface?.Replace(cells); localRockSurface?.Replace(cells); rockInitialized = true; }
            if (oreDirty) { oreMap.SetPixels32(ores); oreMap.Apply(false, false); oreDirty = false; }
            if (lightFullDirty)
            {
                if (localRockSurface != null)
                    CaveSoftLightField.SyncMarkers(cells, W, H, deviceLights, lightMarkers, lightMarkerAnchors, null, mapPages);
                var next = localRockSurface != null || rockSurface != null ? CaveSoftLightField.Build(cells, W, H, deviceLights) :
                    CaveLightField.Build(cells, W, H, ores, deviceLights);
                for (int i = 0; i < next.Length; i++)
                    if (!lightInitialized || !next[i].Equals(lightPixels[i])) MarkPage(lightPages, i % W, i / W);
                Array.Copy(next, lightPixels, next.Length); lightInitialized = true; lightFullDirty = false; terrainLightChanges.Clear();
            }
            else if (localRockSurface != null && terrainLightChanges.Count != 0) RebuildChangedLightPages();
            UploadPages(map, mapUpload, cells, mapPages); mapPages.Clear();
            UploadPages(light, lightUpload, lightPixels, lightPages); lightPages.Clear();
        }
        /// <summary>设备和移动光源只失效旧、新位置附近；不因每次移动分配并重烘整张光照图。</summary>
        public void SetDevices(DarkNights.Core.ViewData.WorldViewData world)
        {
            if (world.Expedition == null) return;
            var next = new HashSet<int>();
            foreach (var building in world.Buildings)
            {
                if (building.Kind != "lamp" && building.Kind != "ship") continue;
                foreach (var device in world.Expedition.Devices)
                    if (device.Id == building.Id && device.Powered)
                        Add(Mathf.RoundToInt(building.X / 16), Mathf.RoundToInt((632 - device.Height - 16) / 16));
            }
            foreach (var actor in world.Actors)
                if (actor.Kind == "scout-drone" && actor.Hp > 0)
                    Add(Mathf.RoundToInt(actor.X / 16), Mathf.RoundToInt((632 - actor.Height) / 16));
            bool first = deviceLights == null;
            if (!first && next.SetEquals(deviceCells)) return;
            if (first) { deviceLights = new byte[W * H]; lightFullDirty = true; }
            foreach (int index in deviceCells)
                if (!next.Contains(index)) { deviceLights[index] = 0; terrainLightChanges.Add(index); }
            foreach (int index in next)
                if (!deviceCells.Contains(index)) { deviceLights[index] = 255; terrainLightChanges.Add(index); }
            deviceCells = next;
            if (localRockSurface == null) lightFullDirty = true;
            void Add(int x, int y) { if (x >= 0 && x < W && y >= 0 && y < H) next.Add(y * W + x); }
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
            map.Release(); light.Release();
            foreach (var value in new UnityEngine.Object[] { backdrop, mesh, Material, background, map, light, oreMap, mapUpload, lightUpload })
            { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        }
        private void ApplyCell(CellCoord position, GridCell cell, List<int> changed)
        {
            int u = position.U, row = -position.V;
            if (u < 0 || u >= W || row < 0 || row >= H) return;
            int index = row * W + u; Color32 next = ToColor(cell);
            if (cells[index].r == next.r && cells[index].g == next.g) return;
            next.b = cells[index].b; cells[index] = next; changed.Add(index); MarkPage(mapPages, u, row);
        }
        private Color32 ToColor(GridCell cell) => new Color32(cell.IsEmpty ? (byte)0 : materials[cell.TileId], (byte)((cell.Flags >> 1) & 15), 0, 255);
        private static RenderTexture CreateTarget(string name, int width, int height, GraphicsFormat format)
        {
            var descriptor = new RenderTextureDescriptor(width, height, format, 0)
            { msaaSamples = 1, useMipMap = false, autoGenerateMips = false };
            var texture = new RenderTexture(descriptor) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.Create(); var previous = RenderTexture.active;
            try { RenderTexture.active = texture; GL.Clear(false, true, Color.clear); }
            finally { RenderTexture.active = previous; }
            return texture;
        }
        private static void MarkPage(HashSet<int> pages, int x, int y) => pages.Add(y / Page * (W / Page) + x / Page);
        private void RebuildChangedLightPages()
        {
            var previousMarkers = new HashSet<int>(lightMarkers);
            CaveSoftLightField.SyncMarkers(cells, W, H, deviceLights, lightMarkers, lightMarkerAnchors, terrainLightChanges, mapPages);
            var affected = new HashSet<int>();
            foreach (int index in terrainLightChanges) AddHalo(index, 18);
            foreach (int index in previousMarkers) if (!lightMarkers.Contains(index)) AddHalo(index, 9);
            foreach (int index in lightMarkers) if (!previousMarkers.Contains(index)) AddHalo(index, 9);
            var sources = new List<int>(lightMarkers); sources.Sort();
            foreach (int page in affected)
            {
                int left = page % (W / Page) * Page, top = page / (W / Page) * Page;
                var region = CaveSoftLightField.BuildRegion(cells, W, H, deviceLights, left, top, Page, Page, sources);
                bool changed = false;
                for (int y = 0; y < Page; y++) for (int x = 0; x < Page; x++)
                {
                    int target = (top + y) * W + left + x, sourceIndex = y * Page + x;
                    if (!lightPixels[target].Equals(region[sourceIndex])) { lightPixels[target] = region[sourceIndex]; changed = true; }
                }
                if (changed) lightPages.Add(page);
            }
            terrainLightChanges.Clear();
            void AddHalo(int index, int radius)
            {
                int x = index % W, y = index / W;
                for (int py = Math.Max(0, (y - radius) / Page); py <= Math.Min(H / Page - 1, (y + radius) / Page); py++)
                    for (int px = Math.Max(0, (x - radius) / Page); px <= Math.Min(W / Page - 1, (x + radius) / Page); px++) affected.Add(py * (W / Page) + px);
            }
        }
        private void UploadPages(RenderTexture target, Texture2D staging, Color32[] pixels, HashSet<int> pages)
        {
            foreach (int page in pages)
            {
                int left = page % (W / Page) * Page, top = page / (W / Page) * Page;
                for (int y = 0; y < Page; y++) Array.Copy(pixels, (top + y) * W + left, uploadPixels, y * Page, Page);
                staging.SetPixels32(uploadPixels); staging.Apply(false, false);
                Graphics.CopyTexture(staging, 0, 0, 0, 0, Page, Page, target, 0, 0, left, top);
            }
        }
    }
}
