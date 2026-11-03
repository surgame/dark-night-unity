using System;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>原生模块内的一块闭合实岩作者轮廓，顶点以0.16单位的整数格边计量，不含运行耐久或矿物状态。</summary>
    [Serializable]
    public sealed class MapModuleSolid
    {
        public string Id;
        [Range(1, 7)] public int Material = 2;
        public Vector2Int[] Vertices = Array.Empty<Vector2Int>();
    }
}
