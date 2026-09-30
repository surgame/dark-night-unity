using System;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>地形样式所属的本地采矿网格参数；只改变九格或扩展网格外观，不改变服务端采集范围与节奏。</summary>
    [Serializable]
    public sealed class TerrainMiningSelectorSettings
    {
        [Range(1, 2)] public int Radius = 1;
        [Range(1, 3)] public float LinePixels = 2;
        [Range(0, 1)] public float CenterOpacity = 1;
        [Range(0, 1)] public float SurroundOpacity = .4f;
        [Range(0, .2f)] public float FillOpacity = .055f;
        [Range(.5f, 4)] public float FadePower = 2;
        [Range(0, 1)] public float InvalidOpacity = .3f;
    }
}
