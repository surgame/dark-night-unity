using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Expedition;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>单张地图拥有的地表遮罩；RG 为当前首个实心像素、BA 为冻结地表深度参考，仅重算变化列，不拥有权威格子。</summary>
    public sealed class CaveSurfaceSkyline : IDisposable
    {
        private readonly BackgroundBakeDescriptor reference;
        private readonly Color32[] pixels;
        private readonly Color32[] column = new Color32[8];
        private readonly HashSet<int> dirtyColumns = new HashSet<int>();
        public Texture2D Texture { get; }
        public int RebuiltColumns { get; private set; }

        public CaveSurfaceSkyline(BackgroundBakeDescriptor reference, Material material, SurfaceEnvironmentSettings settings)
        {
            this.reference = reference ?? throw new ArgumentNullException(nameof(reference));
            ushort[] heights = TerrainVisualCoordinates.SurfaceSkyline(reference);
            pixels = new Color32[heights.Length];
            for (int i = 0; i < heights.Length; i++)
                pixels[i] = new Color32((byte)(heights[i] & 255), (byte)(heights[i] >> 8), (byte)(heights[i] & 255), (byte)(heights[i] >> 8));
            Texture = new Texture2D(heights.Length, 1, TextureFormat.RGBA32, false, true)
            { name = "Surface current and reference skyline", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Texture.SetPixels32(pixels); Texture.Apply(false, false);
            material.SetTexture("_SurfaceSkyline", Texture); material.SetFloat("_SurfaceSky", 1);
            material.SetVector("_SurfaceSettings", new Vector4(settings.Enabled ? settings.WeatheredDepth : 0, settings.EntranceDepth, settings.UndergroundAmbient, 0));
        }

        public void Mark(int columnIndex) => dirtyColumns.Add(columnIndex);
        public void Flush(Color32[] cells)
        {
            if (dirtyColumns.Count == 0) return;
            foreach (int x in dirtyColumns)
            {
                for (int px = 0; px < 8; px++)
                {
                    int height = reference.Height * 8;
                    for (int row = 0; row < reference.Height; row++)
                    {
                        Color32 cell = cells[row * reference.Width + x];
                        byte material = cell.a == 0 ? reference.Material(x, row) : cell.r;
                        if (material == 0) continue;
                        var shape = (TerrainCellShape)(cell.a == 0 ? reference.Shape(x, row) : cell.g);
                        for (int py = 0; py < 8; py++)
                        {
                            if (!TerrainShapeGeometry.Contains(shape, (px + .5f) / 8, 1 - (py + .5f) / 8)) continue;
                            height = row * 8 + py; break;
                        }
                        if (height != reference.Height * 8) break;
                    }
                    int index = x * 8 + px;
                    var old = pixels[index];
                    pixels[index] = column[px] = new Color32((byte)(height & 255), (byte)(height >> 8), old.b, old.a);
                }
                Texture.SetPixels32(x * 8, 0, 8, 1, column); RebuiltColumns++;
            }
            Texture.Apply(false, false); dirtyColumns.Clear();
        }

        public void Dispose()
        {
            dirtyColumns.Clear();
            if (Application.isPlaying) UnityEngine.Object.Destroy(Texture); else UnityEngine.Object.DestroyImmediate(Texture);
        }
    }
}
