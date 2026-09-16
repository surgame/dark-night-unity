using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>绑定可信 YYGC 会话的地图逻辑所有者。只读查询供网络使用；破坏以短事务更新，禁止逐格实体和整图复制。</summary>
    public sealed class TerrainMapAuthority : IReadOnlyGrid, IGridSnapshotSource, IDisposable
    {
        private readonly ObjectSessionContext session;
        private readonly ARDMap map;
        private readonly Dictionary<int, ulong> sequences = new Dictionary<int, ulong>();
        private bool disposed;
        public WorldIdentity World => map.World;
        public WorldDescriptor Descriptor => map.Descriptor;
        public TileCatalog Tiles => map.Tiles;
        public ulong CommitId => map.CommitId;
        public event Action<GridChangeSet> Changed;

        public TerrainMapAuthority(ObjectSessionContext session, TerrainBlueprint initial,
            ServerGameplayCatalog catalog, WorldIdentity world)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            session.RequireAvailable();
            if (!session.IsActive || !session.CanWriteState) throw new InvalidOperationException("需要活跃的服务端 YYGC 会话。");
            var descriptor = new WorldDescriptor(world, 42, 0, new GridBounds(0, -initial.Height + 1, initial.Width, initial.Height));
            map = ARDMap.CreateAuthority(descriptor, catalog.Tiles, () => !disposed && session.IsActive && session.CanWriteState);
            try { TerrainMapInitialization.Load(map, initial); map.Changed += Notify; }
            catch { map.Dispose(); throw; }
        }
        public GridSample Read(CellCoord position) => map.Read(position);
        public GridSnapshot CaptureSnapshot(GridBounds bounds) => map.CaptureSnapshot(bounds);
        public GridSnapshot CapturePageSnapshot(PageCoord page) => map.CapturePageSnapshot(page);

        // Only call after the YYGC NetworkCommandContext has resolved the connection and current policy.
        // authorize must evaluate Ready, policy revision, actor lease, tool/cooldown and distance on the server.
        public GridCommitReceipt DestroyTrusted(int connectionGeneration, ulong sequence, WorldIdentity world,
            ulong expectedRevision, IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize)
        {
            if (disposed || !session.IsActive || !session.CanWriteState) throw new InvalidOperationException("地图无写权限。");
            if (!World.Equals(world) || sequence == 0 || connectionGeneration < 0 ||
                sequences.TryGetValue(connectionGeneration, out ulong previous) && sequence <= previous)
                throw new InvalidOperationException("地图身份或请求序号已过期。");
            if (targets == null || targets.Count < 1 || targets.Count > 64 || authorize == null)
                throw new ArgumentException("破坏批次必须为 1–64 个格子并提供服务端授权。");
            var unique = new HashSet<CellCoord>();
            foreach (var p in targets)
            {
                if (!unique.Add(p) || !Descriptor.Bounds.Contains(p) || !authorize(p) ||
                    !Read(p).TryGetCell(out var value) || value.IsEmpty || (value.Flags & 1) != 0)
                    throw new InvalidOperationException("破坏目标无效、未加载、受保护或无权限。");
            }
            using var edit = map.BeginEdit(expectedRevision);
            foreach (var p in targets) edit.ClearTile(p);
            var receipt = edit.Commit();
            sequences[connectionGeneration] = sequence;
            return receipt;
        }

        public void ForgetConnection(int connectionGeneration) => sequences.Remove(connectionGeneration);
        private void Notify(GridChangeSet change) => Changed?.Invoke(change);
        public void Dispose()
        {
            if (disposed) return;
            map.Changed -= Notify; map.Dispose(); disposed = true; sequences.Clear(); Changed = null;
        }
    }
}
