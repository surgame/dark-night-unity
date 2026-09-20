using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>绑定可信 YYGC 会话的地图逻辑所有者。只读查询供网络使用；破坏以短事务更新，禁止逐格实体和整图复制。</summary>
    public sealed class TerrainMapAuthority : IReadOnlyGrid, IGridSnapshotSource, IDisposable
    {
        private readonly ObjectSessionContext session;
        private readonly ARDMap map;
        private readonly bool[] softRock;
        private readonly Dictionary<int, ulong> sequences = new Dictionary<int, ulong>();
        private readonly Dictionary<int, Dictionary<string, (string Fingerprint, TerrainEditAction Action, GridCommitReceipt Receipt)>> results =
            new Dictionary<int, Dictionary<string, (string, TerrainEditAction, GridCommitReceipt)>>();
        private readonly Dictionary<int, Queue<string>> resultOrder = new Dictionary<int, Queue<string>>();
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
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            session.RequireAvailable();
            if (!session.IsActive || !session.CanWriteState) throw new InvalidOperationException("需要活跃的服务端 YYGC 会话。");
            softRock = initial.CopySoftRock();
            var descriptor = new WorldDescriptor(world, 42, 0, new GridBounds(0, -initial.Height + 1, initial.Width, initial.Height));
            map = ARDMap.CreateAuthority(descriptor, catalog.Tiles, () => !disposed && session.IsActive && session.CanWriteState);
            try { TerrainMapInitialization.Load(map, initial); map.Changed += Notify; }
            catch { map.Dispose(); throw; }
        }
        public GridSample Read(CellCoord position) => map.Read(position);
        public bool IsSoftRock(CellCoord position)
        {
            int y = -position.V;
            return position.U >= 0 && position.U < TerrainGenerationSettings.Width && y >= 0 &&
                y < TerrainGenerationSettings.Height && softRock[y * TerrainGenerationSettings.Width + position.U];
        }
        public string ResourceAt(CellCoord position)
        {
            if (!Read(position).TryGetCell(out var value) || value.IsEmpty) return "";
            switch (TileMaterial(value.TileId))
            {
                case 4:
                case 5: return "iron";
                case 6: return "gold";
                default: return "";
            }
        }
        public GridSnapshot CaptureSnapshot(GridBounds bounds) => map.CaptureSnapshot(bounds);
        public GridSnapshot CapturePageSnapshot(PageCoord page) => map.CapturePageSnapshot(page);

        /// <summary>仅由服务端投射物引信调用；允许空中落点，沿用有限十三格爆破范围，过滤基岩及保护格。</summary>
        internal void Detonate(float x, float height, long projectileId)
        {
            if (disposed || !session.IsActive || !session.CanWriteState)
                throw new InvalidOperationException("地图无写权限。");
            var center = new CellCoord((int)Math.Floor(x / PlayableTerrain.CellPixels),
                (int)Math.Floor((height - PlayableTerrain.OriginY) / PlayableTerrain.CellPixels));
            var targets = new List<CellCoord>();
            foreach (var offset in TerrainDestructionPolicy.Offsets(TerrainEditAction.Explosive))
            {
                var position = new CellCoord(center.U + offset.X, center.V + offset.Y);
                if (Descriptor.Bounds.Contains(position) && CanDestroy(TerrainEditAction.Explosive, position))
                    targets.Add(position);
            }
            if (targets.Count == 0) return;
            using var edit = map.BeginEdit(CommitId);
            foreach (var position in targets) edit.ClearTile(position);
            edit.Commit();
        }

        /// <summary>服务端根据可信动作生成固定范围；客户端不能提交半径或目标列表替代此结果。</summary>
        public IReadOnlyList<CellCoord> BuildTargets(TerrainEditAction action, CellCoord center)
        {
            if (!Descriptor.Bounds.Contains(center)) throw new ArgumentException("破坏中心格越界。", nameof(center));
            if (!CanDestroy(action, center)) throw new InvalidOperationException("破坏中心格不可作用。");
            var targets = new List<CellCoord>(TerrainDestructionPolicy.MaximumTargets);
            foreach (TerrainOffset offset in TerrainDestructionPolicy.Offsets(action))
            {
                var position = new CellCoord(center.U + offset.X, center.V + offset.Y);
                if (Descriptor.Bounds.Contains(position) && CanDestroy(action, position)) targets.Add(position);
            }
            if (targets.Count == 0 || targets.Count > TerrainDestructionPolicy.MaximumTargets)
                throw new InvalidOperationException("破坏范围没有合法目标。");
            return targets.AsReadOnly();
        }

        // Only call after the YYGC NetworkCommandContext has resolved the connection and current policy.
        // authorize must evaluate Ready, policy revision, actor lease, tool/cooldown and distance on the server.
        public GridCommitReceipt DestroyTrusted(int connectionGeneration, ulong sequence, WorldIdentity world,
            ulong expectedRevision, IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize)
        {
            if (sequence == 0 || targets == null || targets.Count == 0) throw new ArgumentException("旧式地图请求无效。");
            if (connectionGeneration < 0 || sequences.TryGetValue(connectionGeneration, out ulong previous) && sequence <= previous)
                throw new InvalidOperationException("地图身份或请求序号已过期。");
            var receipt = DestroyTrusted(connectionGeneration, "legacy:" + sequence, TerrainEditAction.Explosive,
                world, targets[0], targets, authorize, out _, false);
            sequences[connectionGeneration] = sequence;
            return receipt;
        }

        public GridCommitReceipt DestroyTrusted(int connectionGeneration, ulong sequence, WorldIdentity world,
            IReadOnlyList<CellCoord> targets, Func<CellCoord, bool> authorize) =>
            DestroyTrusted(connectionGeneration, sequence, world, CommitId, targets, authorize);

        /// <summary>执行正式地形动作并缓存首次回执；重复 RequestId 返回同一回执而不再次提交地图事务。</summary>
        public GridCommitReceipt DestroyTrusted(int connectionGeneration, string requestId, TerrainEditAction action,
            WorldIdentity world, CellCoord center, IReadOnlyList<CellCoord> targets,
            Func<CellCoord, bool> authorize)
        {
            return DestroyTrusted(connectionGeneration, requestId, action, world, center, targets, authorize, out _);
        }

        /// <summary>兼容既有内部测试调用；客户端不再携带该参数，权威事务始终读取当前 CommitId。</summary>
        public GridCommitReceipt DestroyTrusted(int connectionGeneration, string requestId, TerrainEditAction action,
            WorldIdentity world, ulong ignoredExpectedRevision, CellCoord center, IReadOnlyList<CellCoord> targets,
            Func<CellCoord, bool> authorize)
        {
            return DestroyTrusted(connectionGeneration, requestId, action, world, center, targets, authorize, out _);
        }

        public GridCommitReceipt DestroyTrusted(int connectionGeneration, string requestId, TerrainEditAction action,
            WorldIdentity world, CellCoord center, IReadOnlyList<CellCoord> targets,
            Func<CellCoord, bool> authorize, out bool applied)
        {
            return DestroyTrusted(connectionGeneration, requestId, action, world, center, targets, authorize, out applied, true);
        }

        private GridCommitReceipt DestroyTrusted(int connectionGeneration, string requestId, TerrainEditAction action,
            WorldIdentity world, CellCoord center, IReadOnlyList<CellCoord> targets,
            Func<CellCoord, bool> authorize, out bool applied, bool requireCanonical)
        {
            applied = false;
            if (disposed || !session.IsActive || !session.CanWriteState) throw new InvalidOperationException("地图无写权限。");
            if (!World.Equals(world) || connectionGeneration < 0) throw new InvalidOperationException("地图身份已过期。");
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 96)
                throw new ArgumentException("RequestId 必须为 1–96 个非空字符。", nameof(requestId));
            string fingerprint = Fingerprint(action, center, targets);
            if (results.TryGetValue(connectionGeneration, out var cached) && cached.TryGetValue(requestId, out var previous))
            {
                if (previous.Fingerprint != fingerprint) throw new InvalidOperationException("RequestId 与首次请求不一致。");
                return previous.Receipt;
            }
            ValidateTargets(action, center, targets, authorize);
            if (requireCanonical)
            {
                IReadOnlyList<CellCoord> canonical = BuildTargets(action, center);
                if (canonical.Count != targets.Count || canonical.Any(position => !targets.Contains(position)))
                    throw new InvalidOperationException("破坏目标必须由服务端动作规则生成。");
            }
            using var edit = map.BeginEdit(CommitId);
            foreach (CellCoord position in targets) edit.ClearTile(position);
            var receipt = edit.Commit();
            Remember(connectionGeneration, requestId, (fingerprint, action, receipt));
            applied = true;
            return receipt;
        }

        public void ForgetConnection(int connectionGeneration)
        {
            sequences.Remove(connectionGeneration); results.Remove(connectionGeneration); resultOrder.Remove(connectionGeneration);
        }

        public bool TryGetCached(int connectionGeneration, string requestId, TerrainEditAction action,
            ulong expectedRevision, CellCoord center, out GridCommitReceipt receipt)
        {
            receipt = null;
            if (!results.TryGetValue(connectionGeneration, out var cached) ||
                !cached.TryGetValue(requestId, out var previous)) return false;
            if (previous.Action != action || !CenterMatches(previous.Fingerprint, center))
                throw new InvalidOperationException("RequestId 与首次请求不一致。");
            receipt = previous.Receipt;
            return true;
        }

        public bool TryGetCached(int connectionGeneration, string requestId, CellCoord center,
            out TerrainEditAction action, out GridCommitReceipt receipt)
        {
            action = default;
            receipt = null;
            if (!results.TryGetValue(connectionGeneration, out var cached) ||
                !cached.TryGetValue(requestId, out var previous)) return false;
            if (!CenterMatches(previous.Fingerprint, center))
                throw new InvalidOperationException("RequestId 与首次请求不一致。");
            action = previous.Action;
            receipt = previous.Receipt;
            return true;
        }

        private bool CanDestroy(TerrainEditAction action, CellCoord position)
        {
            if (!Read(position).TryGetCell(out var value)) return false;
            return TerrainDestructionPolicy.CanDestroy(action, (byte)TileMaterial(value.TileId),
                (value.Flags & 1) != 0, IsSoftRock(position));
        }

        private void ValidateTargets(TerrainEditAction action, CellCoord center, IReadOnlyList<CellCoord> targets,
            Func<CellCoord, bool> authorize)
        {
            if (targets == null || targets.Count < 1 || targets.Count > TerrainDestructionPolicy.MaximumTargets || authorize == null)
                throw new ArgumentException("破坏批次必须为 1–13 个格子并提供服务端授权。");
            if (!CanDestroy(action, center)) throw new InvalidOperationException("破坏中心格不可作用。");
            var unique = new HashSet<CellCoord>();
            foreach (CellCoord position in targets)
            {
                if (!unique.Add(position) || !Descriptor.Bounds.Contains(position) || !authorize(position) ||
                    !CanDestroy(action, position))
                    throw new InvalidOperationException("破坏目标无效、未加载、受保护、基岩或无权限。");
            }
        }

        private byte TileMaterial(uint tileId)
        {
            if (!Tiles.TryGet(tileId, out var definition)) return 0;
            string key = definition.Key.ToString();
            switch (key)
            {
                case "loam": return 1; case "slate": return 2; case "basalt": return 3; case "copper": return 4;
                case "iron": return 5; case "gold": return 6; case "moss": return 7; case "bedrock": return 8;
                default: return 0;
            }
        }

        private void Remember(int connectionGeneration, string requestId,
            (string Fingerprint, TerrainEditAction Action, GridCommitReceipt Receipt) result)
        {
            if (!results.TryGetValue(connectionGeneration, out var cache))
            {
                cache = new Dictionary<string, (string, TerrainEditAction, GridCommitReceipt)>(); results.Add(connectionGeneration, cache);
                resultOrder.Add(connectionGeneration, new Queue<string>());
            }
            cache.Add(requestId, result); resultOrder[connectionGeneration].Enqueue(requestId);
            while (resultOrder[connectionGeneration].Count > 64) cache.Remove(resultOrder[connectionGeneration].Dequeue());
        }

        private static string Fingerprint(TerrainEditAction action, CellCoord center,
            IReadOnlyList<CellCoord> targets)
        {
            var text = action + ":" + center.U + ":" + center.V + ":" + (targets == null ? -1 : targets.Count);
            if (targets != null) foreach (CellCoord target in targets) text += ":" + target.U + "," + target.V;
            return text;
        }

        private static bool CenterMatches(string fingerprint, CellCoord center)
        {
            int first = fingerprint.IndexOf(':');
            int second = first < 0 ? -1 : fingerprint.IndexOf(':', first + 1);
            int third = second < 0 ? -1 : fingerprint.IndexOf(':', second + 1);
            return first >= 0 && second > first && third > second &&
                fingerprint.Substring(first, third - first) == ":" + center.U + ":" + center.V;
        }

        private void Notify(GridChangeSet change) => Changed?.Invoke(change);
        public void Dispose()
        {
            if (disposed) return;
            map.Changed -= Notify; map.Dispose(); disposed = true; sequences.Clear(); results.Clear(); resultOrder.Clear(); Changed = null;
        }
    }
}
