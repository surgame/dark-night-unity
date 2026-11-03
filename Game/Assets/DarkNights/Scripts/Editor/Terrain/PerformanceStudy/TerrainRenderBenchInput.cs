using System;

namespace DarkNights.Editor.Terrain.PerformanceStudy
{
    /// <summary>独立铺设实验的冻结输入；只由显式离屏测量读取，不接入正式地图、场景或存档。</summary>
    [Serializable]
    public sealed class TerrainRenderBenchInput
    {
        /// <summary>同一固定镜头的逻辑坐标，Y向下，仅用于实验投影。</summary>
        [Serializable]
        public sealed class ViewRect
        {
            public float x, y, w, h;
        }
        /// <summary>交错的XY和UV冻结顶点；每三个顶点形成一个三角形，不拥有可变游戏形状。</summary>
        [Serializable]
        public sealed class Group
        {
            public float[] data;
        }
        /// <summary>一个32格区块的两套等占据表现；渲染策略选取其一，不反写输入。</summary>
        [Serializable]
        public sealed class Page
        {
            public int key;
            public Group[] strip, atlas;
        }
        /// <summary>初始地图或单次破坏后的有界区块替换；包含共同形状摘要和变化角点数。</summary>
        [Serializable]
        public sealed class Frame
        {
            public string kind, hash;
            public int changed;
            public int[] dirty;
            public Page[] pages;
        }
        public int version, width, height;
        public string date;
        public ViewRect view;
        public string[] textures;
        public Frame[] frames;
    }
}
