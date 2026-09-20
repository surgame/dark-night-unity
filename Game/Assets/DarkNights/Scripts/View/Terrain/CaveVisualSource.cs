using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>只读区块的洞穴材质适配；缓存是可重建的显示纹理，坡形仍由输入格 Flags 唯一决定，随预览释放。</summary>
    public sealed class CaveVisualSource : IMapChunkSource, IDisposable
    {
        private const int W = 320, H = 192;
        private readonly IMapChunkSource source;
        private readonly Color32[] cells = new Color32[W * H];
        private readonly Dictionary<uint, byte> materials = new Dictionary<uint, byte>();
        private readonly Texture2D map;
        private readonly Texture2D light;
        private readonly Material background;
        private readonly GameObject backdrop;
        private readonly Mesh mesh;
        private bool dirty;
        public Material Material { get; }
        public CaveVisualSource(IMapChunkSource source, CaveTerrainStyle style, TileCatalog catalog, Transform parent)
        {
            this.source = source;
            string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
            for (int i = 0; i < keys.Length; i++)
            {
                uint id = catalog.ByKey(keys[i]);
                materials[id] = (byte)(i + 1);
            }
            map = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            light = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Material = new Material(style.Shader) { name = "Cave per-map rock" };
            Material.SetTexture("_CaveMap", map); Material.SetTexture("_CaveLight", light); Material.SetTexture("_RockTex", style.Rock);
            Material.SetMatrix("_MapWorldToLocal", parent.worldToLocalMatrix);
            background = new Material(Material); background.SetFloat("_Background", 1);
            backdrop = new GameObject("Cave distant wall"); backdrop.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Cave backdrop quad" };
            mesh.vertices = new[] { new Vector3(-1,-193,1), new Vector3(321,-193,1), new Vector3(321,12,1), new Vector3(-1,12,1) };
            mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateBounds();
            backdrop.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = backdrop.AddComponent<MeshRenderer>(); renderer.sharedMaterial = background; renderer.sortingOrder = -100;
        }
        public async Task<MapChunkData> LoadAsync(WorldDescriptor descriptor, ChunkCoord coordinate, CancellationToken cancellation)
        {
            MapChunkData data = await source.LoadAsync(descriptor, coordinate, cancellation);
            int size = descriptor.ChunkSize;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int u = coordinate.U * size + x, row = -(coordinate.V * size + y);
                if (u < 0 || u >= W || row < 0 || row >= H) continue;
                var cell = data.Cells[y * size + x];
                cells[row * W + u] = new Color32(cell.IsEmpty ? (byte)0 : materials[cell.TileId],
                    (byte)((cell.Flags >> 1) & 15), 0, 255);
            }
            dirty = true; return data;
        }
        public void Flush()
        {
            if (!dirty) return;
            dirty = false;
            var lights = CaveLightField.Build(cells, W, H);
            map.SetPixels32(cells); map.Apply(false, false);
            light.SetPixels32(lights); light.Apply(false, false);
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(backdrop); UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(Material); UnityEngine.Object.Destroy(background);
            UnityEngine.Object.Destroy(map); UnityEngine.Object.Destroy(light);
        }
    }
}
