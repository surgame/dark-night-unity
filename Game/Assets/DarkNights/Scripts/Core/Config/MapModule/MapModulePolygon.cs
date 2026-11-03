using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.MapModule
{
    /// <summary>
    /// 作者实体多边形的只读副本；描述一块连续实岩，不拥有地图、随机数或表现对象。
    /// 顶点不重复首点，边界不可自交；多块实体可以接边，正面积重叠由编译器明确拒绝。
    /// </summary>
    public sealed class MapModulePolygon
    {
        public string Id { get; }
        public byte Material { get; }
        public IReadOnlyList<MapModuleVertex> Vertices { get; }
        public MapModulePolygon(string id, byte material, MapModuleVertex[] vertices)
        {
            if (string.IsNullOrWhiteSpace(id) || material < 1 || material > 7 ||
                vertices == null || vertices.Length < 3 || vertices.Length > 128)
                throw new ArgumentException("模块实体身份、材料或顶点数量无效。");
            Id = id; Material = material;
            Vertices = Array.AsReadOnly((MapModuleVertex[])vertices.Clone());
        }
    }
}
