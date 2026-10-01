using System;
using AnyRules.Next;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>工作台的独立单格试采；仅消费冻结配置及原生离线地图，不访问玩家、网络或存档，不作为正式输入验证。</summary>
    internal sealed class TerrainMiningPreview : IDisposable
    {
        private readonly ARDMap map;
        private readonly FrozenTerrainRules rules;
        private readonly int damage;
        private readonly CellCoord target = new CellCoord(1, 1);
        internal int Hits { get; private set; }
        internal int Harvested { get; private set; }
        internal string Resource { get; private set; }
        internal int Durability => map.Read(target).Cell.IsEmpty ? 0 : map.Business.Query(target).State.Durability;
        internal int Maximum { get; }
        internal bool CanMine { get; }
        internal TerrainMiningPreview(TerrainProfileConfig profile, HandheldConfig tools, string material, bool contour)
        {
            tools.Validate(); var definition = contour ? profile.ContourDefinition : profile.Definition;
            rules = profile.Freeze(definition); uint tile = rules.Business.Gameplay.Tiles.ByKey(material);
            damage = rules.PickaxeDamage(tile, tools.PickaxeDamage); Maximum = rules.Business.Get(tile).MaximumDurability;
            CanMine = rules.CanDamage(tile); Resource = rules.Drop(tile).Resource;
            var world = new WorldIdentity(StableGuid.Parse(Guid.NewGuid().ToString("N")), 1);
            map = ARDMap.CreateOffline(new WorldDescriptor(world, 42, 0, new GridBounds(0, 0, 32, 32)),
                rules.Business.Gameplay.Tiles, business: rules.Business);
            map.LoadEmptyChunk(new ChunkCoord(0, 0));
            using var edit = map.BeginEdit(); edit.SetTileType(target, tile); edit.Commit();
        }
        internal void Hit()
        {
            if (!CanMine || Durability == 0) return;
            uint tile = map.Read(target).Cell.TileId; var state = map.Business.Query(target).State;
            using var edit = map.BeginEdit();
            if (state.Durability <= damage) { Harvested += rules.Drop(tile).Amount; edit.ClearTile(target); }
            else edit.Damage(target, damage);
            edit.Commit(); Hits++;
        }
        public void Dispose() => map.Dispose();
    }
}
