using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 关卡矿洞的单份作者输入；基线栅格保持原格坐标，新增洞室与连接显式声明。
    /// 进入后台生成前深复制并校验，既不保存玩家状态，也不改变正式地图及协议尺寸。
    /// </summary>
    [Serializable]
    public sealed class MineMapDefinition
    {
        public int SchemaVersion = 1;
        public string Id;
        public string Title;
        public string SourceHash;
        public string BaseRuns;
        public int BaseWidth = 132;
        public int BaseHeight = 78;
        public int Width = 264;
        public int Height = 156;
        public int OffsetX = 40;
        public int OffsetY = 32;
        public int[] Seams = Array.Empty<int>();
        public MineMapRoom[] Rooms = Array.Empty<MineMapRoom>();
        public MineMapPath[] Paths = Array.Empty<MineMapPath>();
        public MineMapShaft[] Shafts = Array.Empty<MineMapShaft>();
        public MineMapOre[] BaseOres = Array.Empty<MineMapOre>();

        public MineMapDefinition CopyValidated()
        {
            if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Id) || Id.Length > 48 ||
                Id.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-') ||
                string.IsNullOrWhiteSpace(Title) || Title.Length > 64 || SourceHash == null || SourceHash.Length != 64 ||
                SourceHash.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("矿洞身份或来源无效。");
            if (BaseWidth != 132 || BaseHeight != 78 || Width != 264 || Height != 156 ||
                OffsetX < 3 || OffsetY < 3 || OffsetX + Width > TerrainGenerationSettings.Width - 3 ||
                OffsetY + Height > TerrainGenerationSettings.Height - 3)
                throw new ArgumentException("矿洞必须保持 132×78 基线及 264×156 扩展，且位于正式地图安全边界内。");
            if (Rooms == null || Rooms.Length < 1 || Rooms.Length > 80 || Paths == null || Paths.Length > 128 ||
                Shafts == null || Shafts.Length > 40 || BaseOres == null || BaseOres.Length > 256 ||
                Seams == null || Seams.Length % 4 != 0 || Seams.Length > 64)
                throw new ArgumentException("矿洞声明缺失或超出预算。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in Rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.Id) || !ids.Add(room.Id) ||
                    string.IsNullOrWhiteSpace(room.Name) || room.Radius < 2 || room.Radius > 12 ||
                    room.Height < 3 || room.Height > 14 || room.X - room.Radius < 1 ||
                    room.X + room.Radius >= Width - 1 || room.Floor < 8 || room.Floor >= Height - 4)
                    throw new ArgumentException("矿洞洞室重复、越界或净空无效。");
            }
            var rooms = new HashSet<string>(ids, StringComparer.Ordinal);
            foreach (var path in Paths)
            {
                if (path == null || string.IsNullOrWhiteSpace(path.Id) || !ids.Add(path.Id) ||
                    !rooms.Contains(path.From) || !rooms.Contains(path.To) || path.From == path.To ||
                    path.Points == null || path.Points.Length < 4 || path.Points.Length % 2 != 0 ||
                    path.Points.Length > 128) throw new ArgumentException("矿洞通路身份、引用或格点无效。");
                for (int i = 0; i < path.Points.Length; i += 2)
                {
                    int x = path.Points[i], floor = path.Points[i + 1];
                    if (x < 2 || x >= Width - 2 || floor < 8 || floor >= Height - 4 || i > 0 &&
                        (x == path.Points[i - 2] || Math.Abs(floor - path.Points[i - 1]) > Math.Abs(x - path.Points[i - 2]) * 2))
                        throw new ArgumentException("步行格点越界或超过每列两格落差。");
                }
                if (path.SealX < -1 || path.SealX >= Width) throw new ArgumentException("封岩列越界。");
            }
            foreach (var shaft in Shafts)
                if (shaft == null || string.IsNullOrWhiteSpace(shaft.Id) || !ids.Add(shaft.Id) ||
                    !rooms.Contains(shaft.Lower) || !rooms.Contains(shaft.Upper) || shaft.Lower == shaft.Upper ||
                    shaft.X < 4 || shaft.X >= Width - 5 || shaft.Top < 8 || shaft.Bottom >= Height - 4 ||
                    shaft.Bottom - shaft.Top < 2 || shaft.Bottom - shaft.Top > 14 || Math.Abs(shaft.Side) != 1)
                    throw new ArgumentException("竖井引用、边界或 2–14 格长度无效。");
            foreach (var ore in BaseOres)
                if (ore == null || string.IsNullOrWhiteSpace(ore.Id) || !ids.Add(ore.Id) ||
                    ore.X < 1 || ore.X >= BaseWidth - 1 || ore.Y < 1 || ore.Y >= BaseHeight - 1 ||
                    ore.Capacity < 1 || ore.Capacity > 1000 || ore.Kind != "iron" && ore.Kind != "gold")
                    throw new ArgumentException("基线矿簇身份、位置或容量无效。");
            for (int i = 0; i < Seams.Length; i += 4)
                if (Seams[i] < 0 || Seams[i + 1] < 0 || Seams[i + 2] < 1 || Seams[i + 3] < 1 ||
                    Seams[i] + Seams[i + 2] > BaseWidth || Seams[i + 1] + Seams[i + 3] > BaseHeight)
                    throw new ArgumentException("基线接缝必须是界内半开矩形。");
            DecodeBase();
            var copy = (MineMapDefinition)MemberwiseClone();
            copy.Seams = (int[])Seams.Clone();
            copy.Rooms = Rooms.Select(r => r.Copy()).ToArray();
            copy.Paths = Paths.Select(p => p.Copy()).ToArray();
            copy.Shafts = Shafts.Select(s => s.Copy()).ToArray();
            copy.BaseOres = BaseOres.Select(o => o.Copy()).ToArray();
            return copy;
        }

        /// <summary>解码只读基线的「材料:连续格数」序列；要求完整覆盖，不容忍截断、额外格或未知材料。</summary>
        public byte[] DecodeBase()
        {
            if (string.IsNullOrEmpty(BaseRuns) || BaseRuns.Length > 40000) throw new ArgumentException("矿洞基线栅格缺失。");
            var result = new byte[BaseWidth * BaseHeight]; int offset = 0;
            foreach (string run in BaseRuns.Split(','))
            {
                string[] pair = run.Split(':');
                if (pair.Length != 2 || !byte.TryParse(pair[0], out byte value) || value > 2 ||
                    !int.TryParse(pair[1], out int count) || count < 1 || count > result.Length - offset)
                    throw new ArgumentException("矿洞基线栅格的连续段无效。");
                for (int i = 0; i < count; i++) result[offset++] = value;
            }
            if (offset != result.Length) throw new ArgumentException("矿洞基线栅格尺寸不完整。");
            return result;
        }

        public bool AllowsSeam(int x, int y)
        {
            for (int i = 0; i < Seams.Length; i += 4)
                if (x >= Seams[i] && x < Seams[i] + Seams[i + 2] && y >= Seams[i + 1] && y < Seams[i + 1] + Seams[i + 3]) return true;
            return false;
        }
    }
}
