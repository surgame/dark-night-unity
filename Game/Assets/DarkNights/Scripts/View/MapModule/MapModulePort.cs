using System;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>模块边缘的显式开放端口，端点为局部整数格边，法线必须为四个轴向之一；不自动生成过渡块。</summary>
    [Serializable]
    public sealed class MapModulePort
    {
        public string Id;
        public Vector2Int From;
        public Vector2Int To;
        public Vector2Int Normal = Vector2Int.right;
    }
}
