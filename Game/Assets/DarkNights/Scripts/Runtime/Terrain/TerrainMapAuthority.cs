using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>YYGC 会话内唯一网格所有者；原生地图拥有耐久，业务门控破坏，内容版本与只读分块投影不形成第二份状态。</summary>
    public sealed class TerrainMapAuthority : IGridChangeSource, IGridSnapshotSource, IGridBusinessQuery, IGridCellIdentitySource, IDisposable
    {
        private readonly ObjectSessionContext session;
        private readonly ARDMap map;
        private readonly bool[] softRock;
        private readonly TerrainMapTransactions transactions;
        private readonly TerrainRequestHistory history = new TerrainRequestHistory();
        private readonly Dictionary<CellCoord, ulong> versions;
        private readonly int pickaxeDamage, bombDamage;
        private bool disposed;
        public FrozenTerrainRules Rules { get; }
        public WorldIdentity World => map.World;
        public WorldDescriptor Descriptor => map.Descriptor;
        public TileCatalog Tiles => map.Tiles;
        public ulong CommitId => map.CommitId;
        public event Action<GridChangeSet> Changed;

        public TerrainMapAuthority(ObjectSessionContext session, TerrainBlueprint initial,
            ServerGameplayCatalog catalog, WorldIdentity world, FrozenTerrainRules rules = null, HandheldConfig handheld = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            session.RequireAvailable(); RequireAuthority();
            Rules = rules ?? new FrozenTerrainRules(catalog, Array.Empty<GameplayDefinitionData>(), new TerrainProfileConfig().Materials);
            var tools = handheld ?? new HandheldConfig();
            tools.Validate(); pickaxeDamage = MiningToolConfig.DefaultRules().Damage; bombDamage = tools.BombDamage;
            softRock = initial.CopySoftRock(); versions = new Dictionary<CellCoord, ulong>(initial.Width * initial.Height);
            var descriptor = new WorldDescriptor(world, 42, 0, new GridBounds(0, -initial.Height + 1, initial.Width, initial.Height));
            // 地图与业务组件共享冻结目录的实例；重复加载同一资产也会产生不同的 TileCatalog。
            map = ARDMap.CreateAuthority(descriptor, Rules.Business.Gameplay.Tiles,
                () => !disposed && session.IsActive && session.CanWriteState, Rules.Business);
            transactions = new TerrainMapTransactions(map, Publish);
            try { TerrainMapInitialization.Load(map, initial); map.Changed += Notify; }
            catch { map.Dispose(); throw; }
        }
        public GridSample Read(CellCoord position) => transactions.Read(position);
        public GridBusinessSample Query(CellCoord position) => transactions.Query(position);
        public ulong ContentVersion(CellCoord position) => versions.TryGetValue(position, out var version) ? version : 0;
        internal void BindMutations(ObjectMutationBatch owner) => transactions.Bind(owner);
        internal bool StageDamage(CellCoord target, int damage, out bool destroyed)
        {
            destroyed = false; RequireAuthority();
            return Read(target).TryGetCell(out var cell) && Rules.CanDamage(cell.TileId) && transactions.Damage(target, damage, out destroyed);
        }
        internal bool StageMine(CellCoord target) => StageDamage(target, Rules.PickaxeDamage(Read(target).Cell.TileId, pickaxeDamage), out _);
        public bool IsSoftRock(CellCoord position)
        {
            int row = -position.V;
            return position.U >= 0 && position.U < TerrainGenerationSettings.Width && row >= 0 && row < TerrainGenerationSettings.Height &&
                softRock[row * TerrainGenerationSettings.Width + position.U];
        }
        public string ResourceAt(CellCoord position) => Read(position).TryGetCell(out var cell) && !cell.IsEmpty ? Rules.Drop(cell.TileId).Resource : "";
        public GridSnapshot CaptureSnapshot(GridBounds bounds) => map.CaptureSnapshot(bounds);
        public GridSnapshot CapturePageSnapshot(PageCoord page) => map.CapturePageSnapshot(page);

        internal void ApplyWorkshopCells(IReadOnlyDictionary<CellCoord, GridCell> cells)
        {
            RequireAuthority();
            if (cells == null || cells.Count == 0) throw new ArgumentException("工作台笔触为空。");
            foreach (var pair in cells)
                if (!Descriptor.Bounds.Contains(pair.Key) || !Read(pair.Key).TryGetCell(out var old) ||
                    !old.IsEmpty && !Rules.CanDamage(old.TileId) || !pair.Value.IsEmpty && !Rules.CanDamage(pair.Value.TileId))
                    throw new InvalidOperationException("工作台目标越界、未加载或为基岩。");
            using var edit = map.BeginEdit(CommitId);
            foreach (var pair in cells) { edit.ClearTile(pair.Key); if (!pair.Value.IsEmpty) edit.SetCell(pair.Key, pair.Value); }
            edit.Commit();
        }
        internal void Detonate(float x, float height, long projectileId)
        {
            RequireAuthority();
            var center = new CellCoord((int)Math.Floor(x / PlayableTerrain.CellPixels),
                (int)Math.Floor((height - PlayableTerrain.OriginY) / PlayableTerrain.CellPixels));
            if (!transactions.CanStage) throw new InvalidOperationException("正式爆破必须处于对象事务中。");
            foreach (var target in Targets(TerrainEditAction.Explosive, center)) StageDamage(target, bombDamage, out _);
        }
        public IReadOnlyList<CellCoord> BuildTargets(TerrainEditAction action, CellCoord center)
        {
            if (!Descriptor.Bounds.Contains(center)) throw new ArgumentException("目标中心越界。");
            var targets = Targets(action, center);
            if (targets.Count == 0) throw new InvalidOperationException("范围内没有可破坏目标。");
            return targets.AsReadOnly();
        }
        private List<CellCoord> Targets(TerrainEditAction action, CellCoord center)
        {
            var result = new List<CellCoord>();
            foreach (var offset in TerrainDestructionPolicy.Offsets(action))
            {
                var cell = new CellCoord(center.U + offset.X, center.V + offset.Y);
                if (Descriptor.Bounds.Contains(cell) && Read(cell).TryGetCell(out var value) && Rules.CanDamage(value.TileId)) result.Add(cell);
            }
            return result;
        }
        public GridCommitReceipt DestroyTrusted(int connection, ulong sequence, WorldIdentity world,
            ulong expectedRevision, IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize)
        {
            history.RequireSequence(connection, sequence);
            if (targets == null || targets.Count == 0) throw new ArgumentException("目标为空。");
            var result = ApplyTrusted(connection, "legacy:" + sequence, TerrainEditAction.Explosive, world, targets[0], targets, authorize, out _, false);
            history.Advance(connection, sequence); return result;
        }
        public GridCommitReceipt DestroyTrusted(int connection, ulong sequence, WorldIdentity world,
            IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize) => DestroyTrusted(connection, sequence, world, CommitId, targets, authorize);
        public GridCommitReceipt DestroyTrusted(int connection, string request, TerrainEditAction action,
            WorldIdentity world, CellCoord center, IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize) =>
            DestroyTrusted(connection, request, action, world, center, targets, authorize, out _);
        public GridCommitReceipt DestroyTrusted(int connection, string request, TerrainEditAction action,
            WorldIdentity world, ulong ignoredRevision, CellCoord center, IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize) =>
            DestroyTrusted(connection, request, action, world, center, targets, authorize, out _);
        public GridCommitReceipt DestroyTrusted(int connection, string request, TerrainEditAction action,
            WorldIdentity world, CellCoord center, IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize, out bool applied) =>
            ApplyTrusted(connection, request, action, world, center, targets, authorize, out applied, true);

        private GridCommitReceipt ApplyTrusted(int connection, string request, TerrainEditAction action, WorldIdentity world,
            CellCoord center, IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize, out bool applied, bool canonical)
        {
            applied = false; RequireAuthority();
            if (!World.Equals(world) || connection < 0 || string.IsNullOrWhiteSpace(request) || request.Length > 96 ||
                targets == null || targets.Count < 1 || targets.Count > TerrainDestructionPolicy.MaximumTargets || authorize == null)
                throw new ArgumentException("地图请求身份或范围不合法。");
            string fingerprint = action + ":" + string.Join(";", targets.Select(value => value.U + "," + value.V));
            if (history.Find(connection, request, action, center, fingerprint, out var prior)) return prior;
            var expected = canonical ? BuildTargets(action, center) : targets;
            if (new HashSet<CellCoord>(targets).Count != targets.Count || expected.Count != targets.Count ||
                expected.Any(value => !targets.Contains(value))) throw new InvalidOperationException("目标必须来自服务端动作规则。");
            foreach (var target in targets)
                if (!Descriptor.Bounds.Contains(target) || !authorize(target) || !Read(target).TryGetCell(out var cell) || !Rules.CanDamage(cell.TileId))
                    throw new InvalidOperationException("目标无权限、未加载或为基岩。");
            if (transactions.CanStage) throw new InvalidOperationException("对象事务中的采集必须调用暂存伤害入口。");
            using var edit = map.BeginEdit(CommitId);
            foreach (var target in targets)
            {
                var state = Query(target).State;
                int amount = action == TerrainEditAction.HandMine ? Rules.PickaxeDamage(Read(target).Cell.TileId, pickaxeDamage) : bombDamage;
                if (state.Durability <= amount) edit.ClearTile(target);
                else edit.SetBusinessState(target, state.WithDurability(state.Durability - amount));
            }
            var receipt = edit.Commit(); history.Remember(connection, request, action, center, fingerprint, receipt);
            applied = !receipt.IsNoOp; return receipt;
        }
        public void ForgetConnection(int connection) => history.Forget(connection);
        public bool TryGetCached(int connection, string request, TerrainEditAction action, ulong expectedRevision,
            CellCoord center, out GridCommitReceipt receipt)
        {
            bool found = history.Find(connection, request, center, out var prior, out receipt);
            if (found && prior != action) throw new InvalidOperationException("重复动作已改变。");
            return found;
        }
        public bool TryGetCached(int connection, string request, CellCoord center, out TerrainEditAction action,
            out GridCommitReceipt receipt) => history.Find(connection, request, center, out action, out receipt);
        internal IReadOnlyList<GridBusinessRecord> CaptureBusiness() => map.Business.CaptureRecords(Descriptor.Bounds);
        internal void RestoreBusiness(IEnumerable<TerrainDamageRecord> records)
        {
            using var edit = map.BeginEdit(CommitId);
            foreach (var record in records)
            {
                var cell = new CellCoord(record.U, record.V); var sample = Query(cell);
                if (!sample.HasState || sample.Terrain.Identity.Guid.ToString().Replace("-", "") != record.MaterialGuid)
                    throw new FormatException("耐久存档与格材质不匹配。");
                var state = new GridBusinessState(record.Durability, record.Quality, record.Reserves,
                    (GridBlockingOverride)record.Blocking, string.IsNullOrEmpty(record.Occupant) ? default : StableGuid.Parse(record.Occupant));
                sample.Definition.Validate(state); edit.SetBusinessState(cell, state);
            }
            edit.Commit();
        }
        private void RequireAuthority()
        {
            if (disposed || !session.IsActive || !session.CanWriteState) throw new InvalidOperationException("地图无权威写权限。");
        }
        private void Notify(GridChangeSet change)
        {
            foreach (var cell in change.Changes)
                if ((cell.Impact & GridChangeImpact.Logic) != 0) versions[cell.Position] = change.Receipt.CommitId;
            transactions.Notify(change);
        }
        private void Publish(GridChangeSet change) => Changed?.Invoke(change);
        public void Dispose()
        {
            if (disposed) return;
            map.Changed -= Notify; map.Dispose(); disposed = true; history.Clear(); Changed = null;
        }
    }
}
