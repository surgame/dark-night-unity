using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Runner;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>单局地图生命周期与保存边界；活动格子唯一归 TerrainMapAuthority，恢复候选先构造成功再与实体一起提交。</summary>
    public sealed class SessionTerrain : IDisposable
    {
        private readonly ObjectSessionContext context;
        private readonly ServerGameplayCatalog catalog;
        private PlayableTerrain initial;
        private bool[] softRock;
        private TerrainRoom[] rooms;
        private TerrainDepositBlueprint[] deposits;
        private uint generation;
        public TerrainMapAuthority Map { get; private set; }
        public string Seed { get; private set; }
        public bool Expedition { get; private set; }
        public BackgroundBakeDescriptor Background { get; private set; }
        public IReadOnlyList<TerrainDepositBlueprint> Deposits => new List<TerrainDepositBlueprint>(deposits).AsReadOnly();
        public SessionTerrain(ObjectSessionContext context, ServerGameplayCatalog catalog, PlayableTerrain initial)
        {
            this.context = context; this.catalog = catalog; this.initial = initial ?? throw new ArgumentNullException(nameof(initial));
            Seed = initial.Seed; SetStatic(initial);
        }
        public void Activate() { Map = Prepare(initial); initial = null; }
        public TerrainMapAuthority Prepare(PlayableTerrain data)
        {
            if (data == null) throw new FormatException("随机场景存档缺少地图。");
            var candidate = new TerrainMapAuthority(context, data.Blueprint(), catalog,
                new WorldIdentity(StableGuid.Parse(data.WorldId), checked(++generation)));
            candidate.BindMutations(context.Container.Resolve<ObjectSession>().Mutations);
            return candidate;
        }
        public void Replace(TerrainMapAuthority candidate, PlayableTerrain data)
        {
            if (candidate == null || data == null) throw new ArgumentNullException(nameof(candidate));
            var previous = Map; Map = candidate; Seed = data.Seed; SetStatic(data); previous?.Dispose();
        }
        /// <summary>航程事务交换已准备地图；先复制静态数据，返回无通知回滚动作，由调用方在提交成功后退休旧地图。</summary>
        internal Action Swap(TerrainMapAuthority candidate, PlayableTerrain data)
        {
            if (candidate == null || data == null) throw new ArgumentNullException(nameof(candidate));
            var nextSoft = data.CopySoftRock();
            var nextRooms = new List<TerrainRoom>(data.Rooms).ToArray();
            var nextDeposits = new List<TerrainDepositBlueprint>(data.Deposits).ToArray();
            var previousMap = Map; var previousSeed = Seed; var previousExpedition = Expedition;
            var previousBackground = Background; var previousSoft = softRock;
            var previousRooms = rooms; var previousDeposits = deposits;
            Action rollback = () =>
            {
                Map = previousMap; Seed = previousSeed; Expedition = previousExpedition;
                Background = previousBackground; softRock = previousSoft; rooms = previousRooms; deposits = previousDeposits;
            };
            Map = candidate; Seed = data.Seed; Expedition = data.Expedition; Background = data.Background;
            softRock = nextSoft; rooms = nextRooms; deposits = nextDeposits;
            return rollback;
        }
        public PlayableTerrain Capture()
        {
            if (Map == null) return initial;
            var cells = new byte[TerrainGenerationSettings.Width * TerrainGenerationSettings.Height];
            var flags = new bool[cells.Length];
            var shapes = new byte[cells.Length];
            string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
            var palette = new System.Collections.Generic.Dictionary<uint, byte>();
            for (byte i = 0; i < keys.Length; i++) palette.Add(catalog.Tiles.ByKey(keys[i]), (byte)(i + 1));
            for (int y = 0; y < TerrainGenerationSettings.Height; y++) for (int x = 0; x < TerrainGenerationSettings.Width; x++)
            {
                var cell = Map.Read(new CellCoord(x, -y)).Cell;
                int index = y * TerrainGenerationSettings.Width + x;
                cells[index] = cell.IsEmpty ? (byte)0 : palette[cell.TileId]; flags[index] = (cell.Flags & 1) != 0;
                shapes[index] = (byte)((cell.Flags >> 1) & 15);
            }
            return new PlayableTerrain(Map.World.WorldId.ToString().Replace("-", ""), Seed, cells, flags,
                (bool[])softRock.Clone(), (TerrainRoom[])rooms.Clone(), (TerrainDepositBlueprint[])deposits.Clone(), shapes, Expedition, Background);
        }
        private void SetStatic(PlayableTerrain data)
        {
            Expedition = data.Expedition;
            Background = data.Background;
            softRock = data.CopySoftRock(); rooms = new List<TerrainRoom>(data.Rooms).ToArray();
            deposits = new List<TerrainDepositBlueprint>(data.Deposits).ToArray();
        }
        public void Dispose() { Map?.Dispose(); Map = null; initial = null; }
    }
}
