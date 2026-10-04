using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>YYGC 会话拥有的独立原生矿层；矿格无 ObjectInstance，储量、耐久、空格和提交统一归 ARDMap 与其业务存储。</summary>
    public sealed class MineralMapAuthority : IGridChangeSource, IGridSnapshotSource, IGridBusinessQuery, IGridCellIdentitySource, IDisposable
    {
        private readonly ARDMap map;
        private readonly TerrainMapTransactions transactions;
        private readonly Dictionary<CellCoord, ulong> versions = new Dictionary<CellCoord, ulong>();
        public FrozenMineralRules Rules { get; }
        public WorldDescriptor Descriptor => map.Descriptor;
        public WorldIdentity World => map.World;
        public TileCatalog Tiles => map.Tiles;
        public ulong CommitId => map.CommitId;
        public event Action<GridChangeSet> Changed;
        private bool disposed;
        public MineralMapAuthority(ObjectSessionContext context, PlayableTerrain data, WorldIdentity world, FrozenMineralRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            map = ARDMap.CreateAuthority(new WorldDescriptor(world, 42, 1,
                new GridBounds(0, -TerrainGenerationSettings.Height + 1, TerrainGenerationSettings.Width, TerrainGenerationSettings.Height)),
                rules.Business.Gameplay.Tiles, () => !disposed && context.IsActive && context.CanWriteState, rules.Business);
            transactions = new TerrainMapTransactions(map, change => Changed?.Invoke(change));
            try
            {
                Load(data); map.Changed += Notify;
                transactions.Bind(context.Container.Resolve<ObjectSession>().Mutations);
            }
            catch { map.Dispose(); throw; }
        }
        private void Load(PlayableTerrain data)
        {
            var snapshot = data.Minerals;
            if (snapshot != null && snapshot.RulesFingerprint != Rules.Fingerprint) throw new FormatException("矿层保存规则指纹不匹配。");
            int count = TerrainGenerationSettings.Width * TerrainGenerationSettings.Height;
            var kinds = new byte[count]; var remaining = new int[count]; var durability = new int[count];
            if (snapshot != null)
                for (int i = 0; i < count; i++) { kinds[i] = snapshot.Kind(i); remaining[i] = snapshot.Reserves(i); durability[i] = snapshot.Durability(i); }
            else foreach (var deposit in data.Deposits) foreach (var cell in deposit.Cells)
            {
                int i = -cell.V * TerrainGenerationSettings.Width + cell.U;
                kinds[i] = (byte)(Array.IndexOf(FrozenMineralRules.Keys, deposit.MineralKind) + 1);
                remaining[i] = cell.Capacity; durability[i] = Rules.MaximumDurability;
            }
            int size = Descriptor.ChunkSize;
            for (int cv = GridMath.FloorDiv(Descriptor.Bounds.MinV, size); cv <= 0; cv++)
                for (int cu = 0; cu < TerrainGenerationSettings.Width / size; cu++)
                {
                    var cells = new GridCell[size * size];
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        int u = cu * size + x, v = cv * size + y;
                        if (!Descriptor.Bounds.Contains(new CellCoord(u, v))) continue;
                        byte kind = kinds[-v * TerrainGenerationSettings.Width + u];
                        if (kind > 0) cells[y * size + x] = new GridCell(Tiles.ByKey(FrozenMineralRules.Keys[kind - 1]));
                    }
                    map.LoadChunk(new ChunkCoord(cu, cv), cells);
                }
            using var edit = map.BeginEdit(map.CommitId);
            for (int i = 0; i < count; i++) if (kinds[i] > 0)
            {
                if (durability[i] > Rules.MaximumDurability) throw new FormatException("矿格保存耐久超过规则上限。");
                edit.SetBusinessState(new CellCoord(i % TerrainGenerationSettings.Width, -i / TerrainGenerationSettings.Width),
                    new GridBusinessState(durability[i], remainingReserves: remaining[i]));
            }
            edit.Commit();
        }
        public GridSample Read(CellCoord cell) => transactions.Read(cell);
        public GridBusinessSample Query(CellCoord cell) => transactions.Query(cell);
        public ulong ContentVersion(CellCoord cell) => versions.TryGetValue(cell, out var version) ? version : 0;
        public GridSnapshot CaptureSnapshot(GridBounds bounds) => map.CaptureSnapshot(bounds);
        public GridSnapshot CapturePageSnapshot(PageCoord page) => map.CapturePageSnapshot(page);
        internal bool StageHit(CellCoord target, int damage, out int harvested)
        {
            harvested = 0;
            if (!Read(target).TryGetCell(out var cell) || cell.IsEmpty || damage < 1) return false;
            var sample = Query(target); var state = sample.State;
            if (state.RemainingReserves <= 0) return false;
            int hp = Math.Max(0, state.Durability - damage);
            if (hp > 0) return transactions.SetResult(target, cell, state.WithDurability(hp));
            harvested = Math.Min(state.RemainingReserves, Rules.UnitsPerHarvest);
            int reserves = state.RemainingReserves - harvested;
            return transactions.SetResult(target, reserves == 0 ? default : cell,
                reserves == 0 ? default : state.WithReserves(reserves).WithDurability(Rules.MaximumDurability));
        }
        public MineralMapSnapshot Capture()
        {
            int count = TerrainGenerationSettings.Width * TerrainGenerationSettings.Height;
            var kinds = new byte[count]; var hp = new int[count]; var reserves = new int[count];
            for (int i = 0; i < count; i++)
            {
                var cell = new CellCoord(i % TerrainGenerationSettings.Width, -i / TerrainGenerationSettings.Width);
                if (!Read(cell).TryGetCell(out var value) || value.IsEmpty) continue;
                kinds[i] = (byte)(Array.IndexOf(FrozenMineralRules.Keys, Rules.Kind(value.TileId)) + 1);
                var state = Query(cell).State; hp[i] = state.Durability; reserves[i] = state.RemainingReserves;
            }
            return new MineralMapSnapshot(kinds, hp, reserves, Rules.Fingerprint);
        }
        private void Notify(GridChangeSet change)
        {
            foreach (var cell in change.Changes)
                if ((cell.Impact & GridChangeImpact.Logic) != 0) versions[cell.Position] = change.Receipt.CommitId;
            transactions.Notify(change);
        }
        public void Dispose() { if (disposed) return; disposed = true; map.Changed -= Notify; map.Dispose(); Changed = null; }
    }
}
