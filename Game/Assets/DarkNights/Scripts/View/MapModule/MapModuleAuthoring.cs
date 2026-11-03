using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.MapModule;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>
    /// 原生地形模块Prefab的唯一轮廓来源，Scene实例只拥有格对齐位置与作者覆盖。
    /// 支持多块闭合实体及多个端口；固定单位缩放、正侧视、不持有权威地图或随机状态。
    /// </summary>
    public sealed class MapModuleAuthoring : MonoBehaviour
    {
        public const float CellUnit = .16f;
        public const int UnitCells = 20;
        public string ModuleId;
        public string DisplayName;
        public Vector2Int Footprint = new Vector2Int(2, 1);
        public MapModuleSolid[] Solids = Array.Empty<MapModuleSolid>();
        public MapModulePort[] Ports = Array.Empty<MapModulePort>();
        public MeshRenderer PreviewRenderer;

        public Vector2Int CellOffset
        {
            get
            {
                var p = transform.position;
                return new Vector2Int(Mathf.RoundToInt(p.x / CellUnit), Mathf.RoundToInt(p.y / CellUnit));
            }
        }
        public void Validate()
        {
            Vector3 p = transform.position;
            if (!Identifier(ModuleId) || string.IsNullOrWhiteSpace(DisplayName) ||
                Footprint.x < 1 || Footprint.x > 8 || Footprint.y < 1 || Footprint.y > 8 ||
                transform.lossyScale != Vector3.one || Quaternion.Angle(transform.rotation, Quaternion.identity) > .001f ||
                !Finite(p.x) || !Finite(p.y) || Math.Abs(p.z) > .0001f ||
                Math.Abs(p.x / CellUnit - CellOffset.x) > .0001f || Math.Abs(p.y / CellUnit - CellOffset.y) > .0001f)
                throw new InvalidOperationException("模块身份、占格或变换无效；仅允许0.16单位对齐平移。");
            if (Solids == null || Solids.Length == 0 || Solids.Length > 32 || Solids.Any(s => s == null) ||
                Solids.Select(s => s.Id).Distinct().Count() != Solids.Length)
                throw new InvalidOperationException("实体清单为空、重复或过大。");
            foreach (var solid in Solids)
                if (!Identifier(solid.Id) || solid.Vertices == null || solid.Vertices.Length < 3 ||
                    solid.Vertices.Length > 128 || solid.Material < 1 || solid.Material > 7 ||
                    solid.Vertices.Any(v => v.x < 0 || v.x > Footprint.x * UnitCells ||
                        v.y < 0 || v.y > Footprint.y * UnitCells))
                    throw new InvalidOperationException("实体轮廓超出模块占格或内容无效：" + solid.Id);
            if (Ports == null || Ports.Any(port => port == null) || Ports.Select(port => port.Id).Distinct().Count() != Ports.Length)
                throw new InvalidOperationException("端口清单缺失或身份重复。");
            foreach (var port in Ports)
            {
                bool vertical = port.From.x == port.To.x && port.From.y < port.To.y &&
                    port.Normal.y == 0 && Math.Abs(port.Normal.x) == 1 &&
                    port.From.x == (port.Normal.x < 0 ? 0 : Footprint.x * UnitCells);
                bool horizontal = port.From.y == port.To.y && port.From.x < port.To.x &&
                    port.Normal.x == 0 && Math.Abs(port.Normal.y) == 1 &&
                    port.From.y == (port.Normal.y < 0 ? 0 : Footprint.y * UnitCells);
                if (!Identifier(port.Id) || !(vertical || horizontal) ||
                    port.From.x < 0 || port.From.y < 0 || port.To.x > Footprint.x * UnitCells ||
                    port.To.y > Footprint.y * UnitCells)
                    throw new InvalidOperationException("端口需位于占格边缘且方向、端点有效：" + port.Id);
            }
        }
        public IReadOnlyList<MapModulePolygon> Freeze()
        {
            Validate(); var offset = CellOffset;
            return Solids.Select(s => new MapModulePolygon(ModuleId + "/" + s.Id, (byte)s.Material,
                s.Vertices.Select(v => new MapModuleVertex(v.x + offset.x, v.y + offset.y)).ToArray())).ToArray();
        }
        public Vector3 WorldPoint(Vector2Int vertex) => transform.position +
            new Vector3((vertex.x - .5f) * CellUnit, vertex.y * CellUnit, 0);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Identifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 40 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_');
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.75f, .85f, .55f);
            foreach (var solid in Solids ?? Array.Empty<MapModuleSolid>())
            {
                if (solid?.Vertices == null) continue;
                for (int i = 0; i < solid.Vertices.Length; i++)
                    Gizmos.DrawLine(WorldPoint(solid.Vertices[i]), WorldPoint(solid.Vertices[(i + 1) % solid.Vertices.Length]));
            }
        }
    }
}
