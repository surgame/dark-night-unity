using System.Collections.Generic;
using DarkNights.Core.Config.MapModule;

namespace DarkNights.Core.Logic.MapModule
{
    /// <summary>单格内的临时凸裁剪片，生命周期限于一次模块编译，不发布为运行状态或作者资源。</summary>
    internal sealed class MapModuleFragment
    {
        internal byte Material { get; }
        internal IReadOnlyList<MapModuleVertex> Vertices { get; }
        internal MapModuleFragment(byte material, MapModuleVertex[] vertices)
        {
            Material = material; Vertices = vertices;
        }
    }
}
