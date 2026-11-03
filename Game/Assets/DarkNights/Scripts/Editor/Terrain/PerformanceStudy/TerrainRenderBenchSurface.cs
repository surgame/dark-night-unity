using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.Editor.Terrain.PerformanceStudy
{
    /// <summary>任务专属离屏地形表面；拥有本次网格和烘焙页，释放时不触碰作者场景或正式资源。</summary>
    internal sealed class TerrainRenderBenchSurface : IDisposable
    {
        /// <summary>单区块的缓存表现，生命周期严格从首次安装到表面释放。</summary>
        private sealed class Page
        {
            internal Mesh[] Meshes;
            internal RenderTexture Texture;
        }
        private readonly Dictionary<int, Page> pages = new Dictionary<int, Page>();
        private readonly Material[] materials;
        private readonly Material bakeMaterial;
        private readonly Texture2D[] textures;
        private readonly string mode;
        private readonly CommandBuffer commands = new CommandBuffer { name = "Terrain strategy study" };
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        internal long UploadedBytes { get; private set; }
        internal long VertexBytes { get; private set; }
        internal int Draws { get; private set; }
        internal int Vertices { get; private set; }
        internal long TextureBytes => mode == "baked" ? pages.Count * 512L * 512 * 4 :
            mode == "atlas" ? 288L * 288 * 4 : (64L * 64 + 64 * 4 * 2) * 4;

        internal TerrainRenderBenchSurface(string mode, Texture2D[] textures, Shader shader)
        {
            this.mode = mode; this.textures = textures;
            materials = new Material[4];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                materials[i].mainTexture = textures[i];
            }
            materials[0].SetFloat("_TexelSnap", mode == "strip" ? 64 : 0);
            bakeMaterial = new Material(materials[3]) { hideFlags = HideFlags.HideAndDontSave };
            bakeMaterial.SetInt("_SrcBlend", (int)BlendMode.One);
            bakeMaterial.SetInt("_DstBlend", (int)BlendMode.Zero);
        }
        internal void Apply(TerrainRenderBenchInput.Frame frame)
        {
            UploadedBytes = 0;
            foreach (var source in frame.pages)
            {
                if (!pages.TryGetValue(source.key, out var page))
                { page = new Page(); pages.Add(source.key, page); }
                if (mode == "baked")
                {
                    var temporary = Make(source.atlas[0].data);
                    UploadedBytes += source.atlas[0].data.Length / 4 * 24L;
                    if (page.Texture == null)
                    {
                        page.Texture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                        page.Texture.Create();
                    }
                    int x = source.key % 10 * 32, y = source.key / 10 * 32;
                    commands.Clear(); commands.SetRenderTarget(page.Texture); commands.ClearRenderTarget(false, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(x, x + 32, -y - 32, -y, -1, 1));
                    commands.DrawMesh(temporary, Matrix4x4.identity, bakeMaterial);
                    Graphics.ExecuteCommandBuffer(commands); UnityEngine.Object.DestroyImmediate(temporary);
                    if (page.Meshes == null)
                    {
                        page.Meshes = new[] { Make(new float[] {
                            x,y,0,0, x+32,y,1,0, x+32,y+32,1,1,
                            x,y,0,0, x+32,y+32,1,1, x,y+32,0,1 }) };
                    }
                    continue;
                }
                var groups = mode == "atlas" ? source.atlas : source.strip;
                if (page.Meshes != null) foreach (var mesh in page.Meshes) UnityEngine.Object.DestroyImmediate(mesh);
                page.Meshes = new Mesh[groups.Length];
                for (int i = 0; i < groups.Length; i++)
                {
                    page.Meshes[i] = Make(groups[i].data);
                    UploadedBytes += groups[i].data.Length / 4 * 24L;
                }
            }
            VertexBytes = 0;
            foreach (var page in pages.Values) foreach (var mesh in page.Meshes) VertexBytes += mesh.vertexCount * 24L;
        }
        internal void Draw(RenderTexture target, TerrainRenderBenchInput.ViewRect view)
        {
            commands.Clear(); commands.SetRenderTarget(target);
            commands.ClearRenderTarget(false, true, new Color(.075f, .105f, .13f, 1));
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(view.x, view.x + view.w, -view.y - view.h, -view.y, -1, 1));
            Draws = 0; Vertices = 0;
            foreach (var entry in pages)
            {
                int x = entry.Key % 10 * 32, y = entry.Key / 10 * 32;
                if (x >= view.x + view.w || y >= view.y + view.h || x + 32 <= view.x || y + 32 <= view.y) continue;
                var page = entry.Value;
                for (int i = 0; i < page.Meshes.Length; i++)
                {
                    var mesh = page.Meshes[i]; if (mesh.vertexCount == 0) continue;
                    properties.Clear();
                    var material = materials[mode == "atlas" ? 3 : i];
                    if (mode == "baked") properties.SetTexture("_MainTex", page.Texture);
                    commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, -1, properties);
                    Draws++; Vertices += mesh.vertexCount;
                }
            }
            Graphics.ExecuteCommandBuffer(commands);
        }
        private static Mesh Make(float[] data)
        {
            int count = data.Length / 4;
            var vertices = new Vector3[count]; var uv = new Vector2[count]; var indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data[i * 4], -data[i * 4 + 1], 0);
                uv[i] = new Vector2(data[i * 4 + 2], 1 - data[i * 4 + 3]); indices[i] = i;
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic(); mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(indices, 0); mesh.UploadMeshData(false);
            return mesh;
        }
        public void Dispose()
        {
            commands.Dispose();
            foreach (var page in pages.Values)
            {
                foreach (var mesh in page.Meshes) UnityEngine.Object.DestroyImmediate(mesh);
                if (page.Texture != null) { page.Texture.Release(); UnityEngine.Object.DestroyImmediate(page.Texture); }
            }
            foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(bakeMaterial);
        }
    }
}
