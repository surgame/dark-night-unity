using System;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>功能场景原生岩格的作者来源；格坐标与材质显式序列化，运行仅从场景冻结初始地图。SpriteRenderer在编辑态展示同一矩形，不反写资产或保存运行破坏。</summary>
    public sealed class GeologyLabBlock : MonoBehaviour
    {
        public int U, V, Width = 1, Height = 1;
        public byte Material = 2;
        public bool Protected;
        public SpriteRenderer Art;

        public void Write(byte[] cells, bool[] protection)
        {
            if (Width < 1 || Height < 1 || U < 0 || U + Width > TerrainGenerationSettings.Width ||
                V > 0 || V - Height + 1 <= -TerrainGenerationSettings.Height || Material > 8 || Art == null)
                throw new InvalidOperationException("功能场景岩格配置无效：" + name);
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                int index = -(V - y) * TerrainGenerationSettings.Width + U + x;
                if (Material == 0) continue;
                if (cells[index] != 0) throw new InvalidOperationException("功能场景岩格重叠：" + name);
                cells[index] = Material; protection[index] = Protected;
            }
        }
    }
}
