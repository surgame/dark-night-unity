using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AnyRules.Next;
using AnyRules.Next.Networking;

namespace DarkNights.Tools.MineralMapProbe
{
    /// <summary>只读编译原生内核和 AMP1 的前置探针；证明业务储量与区域代次合同，真实 FishNet 和 Unity 另行验收。</summary>
    internal static class Program
    {
        private static readonly List<string> Checks = new List<string>();
        private static int Main(string[] args)
        {
            try
            {
                var foreground = Catalog("wall", 1, true);
                var mineral = Catalog("iron", 2, false);
                var wallBusiness = new GridBusinessCatalog(foreground, Array.Empty<GameplayDefinitionData>());
                var oreBusiness = new GridBusinessCatalog(mineral, Array.Empty<GameplayDefinitionData>());
                var world = new WorldIdentity(Id(900), 1);
                var bounds = new GridBounds(0, 0, 96, 32);
                using var wall = ARDMap.CreateAuthority(new WorldDescriptor(world, 42, 0, bounds), foreground.Tiles, () => true, wallBusiness);
                using var ore = ARDMap.CreateAuthority(new WorldDescriptor(world, 42, 1, bounds), mineral.Tiles, () => true, oreBusiness);
                for (int c = 0; c < 3; c++)
                {
                    wall.LoadChunk(new ChunkCoord(c, 0), Enumerable.Repeat(new GridCell(1), 1024).ToArray());
                    ore.LoadChunk(new ChunkCoord(c, 0), Enumerable.Repeat(new GridCell(1), 1024).ToArray());
                }
                var cell = new CellCoord(5, 5);
                using (var edit = ore.BeginEdit(ore.CommitId))
                { edit.SetBusinessState(cell, new GridBusinessState(10, remainingReserves: 3)); edit.Commit(); }
                var visual = new string('a', 64);
                using var walls = Stream(wall, foreground, wallBusiness, visual, 1);
                using var ores = Stream(ore, mineral, oreBusiness, visual, 2);
                var wallReplica = new ChunkReplicaStateMachine(foreground.Tiles, wallBusiness.ContentDigest, visual, wallBusiness);
                var oreReplica = new ChunkReplicaStateMachine(mineral.Tiles, oreBusiness.ContentDigest, visual, oreBusiness);
                walls.Subscribe(new GridBounds(0, 0, 16, 16)); ores.Subscribe(new GridBounds(0, 0, 16, 16));
                Flush(walls, wallReplica); Flush(ores, oreReplica);
                Check("two_distinct_layer_baselines", wallReplica.Query(cell).State.Durability == 40 && oreReplica.Query(cell).State.RemainingReserves == 3);
                Check("unsubscribed_cells_unknown", oreReplica.Read(new CellCoord(80, 5)).State == GridSampleState.Unknown);
                using (var edit = ore.BeginEdit(ore.CommitId))
                { edit.SetBusinessState(cell, new GridBusinessState(40, remainingReserves: 2)); edit.Commit(); }
                ores.Publish(); Flush(ores, oreReplica);
                Check("harvest_resets_hp_and_decrements_reserves", oreReplica.Query(cell).State.Durability == 40 && oreReplica.Query(cell).State.RemainingReserves == 2);
                Check("other_layer_unchanged", wallReplica.Query(cell).State.Durability == 40);
                using (var edit = ore.BeginEdit(ore.CommitId)) { edit.ClearTile(cell); edit.Commit(); }
                ores.Publish(); Flush(ores, oreReplica);
                Check("depleted_cell_is_explicit_empty", oreReplica.Read(cell).State == GridSampleState.Empty);
                var oldGeneration = oreReplica.Generation;
                ores.Subscribe(new GridBounds(72, 0, 16, 16)); Flush(ores, oreReplica);
                Check("region_move_retires_old_cells", oreReplica.Generation > oldGeneration && oreReplica.Read(cell).State == GridSampleState.Unknown);
                Check("new_region_current", oreReplica.Read(new CellCoord(80, 5)).State == GridSampleState.Present);
                ores.Subscribe(new GridBounds(0, 0, 16, 16)); Flush(ores, oreReplica);
                Check("return_preserves_depletion", oreReplica.Read(cell).State == GridSampleState.Empty);
                Write(args, null); Console.WriteLine("MINERAL_MAP_PROTOCOL passed=" + Checks.Count); return 0;
            }
            catch (Exception error) { Write(args, error.ToString()); Console.Error.WriteLine(error); return 1; }
        }
        private static ServerGameplayCatalog Catalog(string key, int id, bool solid) =>
            new ServerGameplayCatalog(Id((uint)id), 1, new string((char)('b' + id), 64),
                new[] { new TerrainDefinitionData(Id((uint)id + 100), TerrainKey.Parse(key), key, solid: solid, maximumDurability: 40) });
        private static MapInterestService Stream(ARDMap map, ServerGameplayCatalog catalog, GridBusinessCatalog business, string visual, ulong session) =>
            new MapInterestService(map, catalog.Tiles, new MapHandshake(map.Descriptor, business.ContentDigest, visual,
                catalog.Definitions.Select(d => d.Identity.Guid).ToArray()), session, _ => true, () => map.CommitId, () => 1);
        private static void Flush(MapInterestService stream, ChunkReplicaStateMachine replica)
        {
            int limit = 1000; byte[] packet;
            while ((packet = stream.Dequeue()) != null)
            { if (--limit == 0) throw new InvalidOperationException("Unbounded stream."); replica.ReceivePacket(packet); }
            if (replica.Closed || replica.NeedsResync) throw new InvalidOperationException("Replica failed admission.");
            stream.Acknowledge(replica.CommitId);
        }
        private static void Check(string name, bool passed)
        { if (!passed) throw new InvalidOperationException(name); Checks.Add(name); }
        private static StableGuid Id(uint value) => StableGuid.Parse(value.ToString("x32"));
        private static void Write(string[] args, string error)
        {
            if (args.Length == 0) return;
            File.WriteAllText(args[0], JsonSerializer.Serialize(new { passed = error == null, checks = Checks, error,
                scope = "Native map/business/AMP1 only; not FishNet transport, independent processes or Unity rendering." },
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
