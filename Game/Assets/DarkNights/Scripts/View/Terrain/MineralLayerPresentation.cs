using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>冻结矿床到单矿层输入的表现装配；只重建占用变化，不结算耐久或存量，共享洞穴光场在原生宿主退出后才可释放。</summary>
    public sealed class MineralLayerPresentation
    {
        private readonly MineralLayerInputSource source;
        private readonly TileCatalog tiles;
        private readonly MineralLayerView view;
        public bool Ready => view.Ready;
        public Exception LastError => view.LastError;
        public long BuiltPages => view.BuiltPages;
        public long InputBatches => view.InputBatches;

        public MineralLayerPresentation(ARDMapDefinition definition, Transform parent)
        {
            tiles = definition.LoadGameplayCatalog().Tiles; source = new MineralLayerInputSource(tiles);
            var root = new GameObject("Embedded minerals"); root.transform.SetParent(parent, false);
            view = root.AddComponent<MineralLayerView>();
        }

        public Task OpenAsync(ARDMapDefinition definition, Camera camera, WorldIdentity world, Texture light, float ambient, int sortingOrder)
            => view.OpenAsync(definition, source, camera, world, lights: light, ambient: ambient, sortingOrder: sortingOrder);

        public void Replace(IReadOnlyList<MineralDepositViewData> minerals)
        {
            if (minerals == null) throw new ArgumentNullException(nameof(minerals));
            var cells = new List<MapInputCell>();
            foreach (var mineral in minerals)
            {
                uint tile = tiles.ByKey(mineral.MineralKind);
                foreach (var cell in mineral.Cells)
                    if (cell.Remaining > 0) cells.Add(new MapInputCell(new CellCoord(cell.U, cell.V), new GridCell(tile)));
            }
            source.Replace(cells);
        }

        public void Tick() => view.Tick();
        public Task RetireAsync() => view.RetireAsync();
    }
}
